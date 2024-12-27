import styled, { css } from "styled-components"
import Piece from "./Piece";
import { useEffect, useRef, useState } from "react";
import { Game, Move, Position, move } from "../game";
import * as signalR from "@microsoft/signalr";
import { createGame } from "../api";
import { useNavigate } from "react-router";

const square_size = css`64px`;

const Square = styled.div<{ dark: boolean; highlight? : string }>`
background: ${props => props.dark ? "#b58863" : "#f0d9b5"};
${({ highlight }) => highlight && `
     box-shadow: 0px 0px 25px ${highlight} inset;
  `}
display: flex;
align-items: center;
justify-content: center;
height: ${square_size};
width: ${square_size};
`;

const BoardContainer = styled.div`    
display: grid;
grid-template-columns: repeat(8, ${square_size});
grid-template-rows: repeat(8, ${square_size});
width: calc(8 * ${square_size});
height: calc(8 * ${square_size});
`;

export interface ChessboardProps {
    seq: number,
    game?: Game
}

const Chessboard = ({ game, seq }: ChessboardProps) => {
    if (!game) return (<>Loading..</>);
    const navigate = useNavigate();

    const { initialPosition } = game;
    const [moves, setMoves] = useState(game.moves);

    const file = (x: number) => x % 8;
    const rank = (x: number) => 8 - Math.trunc(x / 8);

    const ref = useRef(window);
    const mounted = useRef(false);

    const [us, setUs] = useState<'w' | 'b'>('w');
    const [fen, setFen] = useState<string | undefined>();

    const [selectedSquare, setSelectedSquare] = useState<string>();
    const [legalMoves, setLegalMoves] = useState<string[]>([]);
    const [highlight, setHighlights] = useState<string[]>([]);


    const [moveNumber, setMoveNumber] = useState(0);
    const [position, setPosition] = useState<Position>(initialPosition);

    const [connection, setConnection] = useState((prev: any) => {
        return prev || new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7097/game/realtime")
            .build();
    });

    useEffect(() => {
        if (!mounted.current) {

            connection.on('movePlayed', (message) => {
                if (message.plyCount > moveNumber) {
                    console.log(message);

                    const [from, to] = message.move;
                    executeMove(from, to);
                }
            });
            connection.on('legalMovesReceived', setLegalMoves);
            connection.on('finished', () => alert('checkmate'));

            connection.start().catch(console.error);
            mounted.current = true;
        }
    }, [mounted]);

    const canMoveForward = moveNumber < moves.length;
    const canMoveBackwards = moveNumber > 0;

    const doMove = () => {
        if (moveNumber >= moves.length) return;

        setPosition(prev => move(prev, moves[moveNumber]));
        setMoveNumber(moveNumber + 1);
    };

    const undoMove = () => {
        if (moveNumber <= 0) return;

        const newPosition = moves
            .slice(0, moveNumber - 1)
            .reduce(move, initialPosition);

        setPosition(newPosition);
        setMoveNumber(moveNumber - 1);
    };

    const sendDebug = async () => {
        const result = await fetch(`https://localhost:7097/game/${seq}/debug`);
        if (result.status === 204) {
            console.log(result.statusText);
        }
    };

    useEffect(() => {
        const keyHandler = (e: KeyboardEvent) => {
            if (e.key === 'ArrowLeft') undoMove();
            else if (e.key === 'ArrowRight') doMove();
        };

        ref.current.addEventListener('keydown', keyHandler);

        return () => ref.current.removeEventListener('keydown', keyHandler);
    }, [moveNumber, position])

    useEffect(() => {
        connection.send('checkMove', { 'gameId': seq, 'square': selectedSquare });
    }, [selectedSquare])

    const onClick = (id: string) => {
        if (selectedSquare && legalMoves.includes(id)) {
            executeMove(selectedSquare, id);
            connection.send('move', { "gameId": seq, "move": [selectedSquare, id], "plyCount": moveNumber });
        }
        else {
            choosePiece(id);
        }
    };

    const choosePiece = (id: string) => {
        const newSelection = m.has(id) ? id : '';
        setSelectedSquare(newSelection);
    }

    const executeMove = (from: string, to: string) => {
        const m = [from, to] as Move;
        setMoves(prev => [...prev, m]);
        setPosition(prev => move(prev, m));
        setMoveNumber(prev => prev + 1);

        setSelectedSquare(undefined);
        setLegalMoves([]);
    };

    const m = new Map<string, string>(Object.entries(position));

    const onDragStart = (e: DragEvent<HTMLDivElement>, id: string) => {
        e.dataTransfer.setData("text/plain", id);
    };

    const onDragEnter = (e: DragEvent<HTMLDivElement>) => {
        e.preventDefault();
    };
    const onDrop = (e: DragEvent<HTMLDivElement>, id: string) => {
        const from = e.dataTransfer.getData("text/plain") as string;
        const to = id;
        executeMove(from, to);
        connection.send('move', { "gameId": seq, "move": [selectedSquare, id], "plyCount": moveNumber });

        e.preventDefault();
    };

    const serverUndoMove = (e : Event)=>{
        connection.send('undo', { "gameId": seq });

        e.preventDefault();
    }

    const highlightColor = (id: string) => {
        if (highlight.includes(id))
            return 'red';
        if (id === selectedSquare)
            return '#baca44';
        if (legalMoves.includes(id))
            return '#add8e6'
        return undefined;
    }

    const highlightBitboard = async (id: string) => {
        const result = await fetch(`https://localhost:7097/game/${seq}/bitboard/${id}`);
        if (result.status === 200) {
            setHighlights(await result.json());
        }
    }

    return (
        <div>
            <BoardContainer>
                {Array.from({ length: 64 }).map((_, i) => {
                    const f = file(i);
                    const r = rank(i);
                    const id = "abcdefgh"[f] + r;

                    return (<Square key={id}
                        dark={(f % 2) !== (r % 2)}
                        highlight={highlightColor(id)}
                        onClick={() => onClick(id)}
                        onDragStart={(e) => onDragStart(e, id)}
                        onDragEnter={onDragEnter}
                        onDragOver={onDragEnter}
                        onDrop={(e) => onDrop(e, id)}>
                        <Piece value={m.get(id)} />
                    </Square>)
                })
                }
            </BoardContainer>
            <button onClick={() => undoMove()} disabled={!canMoveBackwards} >Back</button>
            <button onClick={() => doMove()} disabled={!canMoveForward} >Next</button>
            <button onClick={() => sendDebug()}  >Debug</button>
            <button onClick={() => serverUndoMove()}  >ServerUndo</button>
            <button onClick={() => alert(legalMoves.join(", "))}  >PrintLegal</button>
            <hr />

            
            <button onClick={() => highlightBitboard('x')}  >checks</button>
            <button onClick={() => highlightBitboard('i')}  >pins</button>
            <button onClick={() => highlightBitboard('o')}  >occ</button>
            <button onClick={() => highlightBitboard('e')}  >empt</button>
            <button onClick={() => highlightBitboard('w')}  >white</button>
            <button onClick={() => highlightBitboard('l')}  >black</button>

            <button onClick={() => highlightBitboard('n')}  >n</button>
            <button onClick={() => highlightBitboard('N')}  >N</button>
            <button onClick={() => highlightBitboard('b')}  >b</button>
            <button onClick={() => highlightBitboard('B')}  >B</button>
            <button onClick={() => highlightBitboard('r')}  >r</button>
            <button onClick={() => highlightBitboard('R')}  >R</button>
            <button onClick={() => highlightBitboard('q')}  >q</button>
            <button onClick={() => highlightBitboard('Q')}  >Q</button>
            <button onClick={() => highlightBitboard('k')}  >k</button>
            <button onClick={() => highlightBitboard('K')}  >K</button>            
            <button onClick={() => highlightBitboard('p')}  >p</button>
            <button onClick={() => highlightBitboard('P')}  >P</button>

            <hr />

            <input value={fen} type="text" onChange={e => setFen(e.target.value)}></input>
            <button onClick={async () => {
                const seq = await createGame(fen);
                navigate(`/game/${seq}`);
            }}>New</button>


        </div>
    )
}

export default Chessboard;
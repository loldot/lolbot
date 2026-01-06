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

const SearchInfo = styled.div`
    
    padding: 8px;
    margin-left: 20px;
    border-radius: 4px;
    font-size: 14px;
    font-family: monospace;
    min-width: 150px;
`;

const BoardContainer = styled.div`    
display: grid;
grid-template-columns: repeat(8, ${square_size});
grid-template-rows: repeat(8, ${square_size});
width: calc(8 * ${square_size});
height: calc(8 * ${square_size});
position: relative;
`;

const getCoords = (sq: string) => {
    const file = sq.charCodeAt(0) - 97; // 'a' -> 0
    const rank = 8 - parseInt(sq[1]);   // '8' -> 0
    return { x: file * 64 + 32, y: rank * 64 + 32 };
}

const Arrow = ({ from, to }: { from: string, to: string }) => {
    const start = getCoords(from);
    const end = getCoords(to);
    return (
        <g>
            <defs>
                <marker id="arrowhead" markerWidth="6" markerHeight="4" refX="5" refY="2" orient="auto">
                    <polygon points="0 0, 6 2, 0 4" fill="orange" opacity="0.8" />
                </marker>
            </defs>
            <line x1={start.x} y1={start.y} x2={end.x} y2={end.y} stroke="orange" strokeWidth="6" opacity="0.8" markerEnd="url(#arrowhead)" />
        </g>
    );
}

import { ApiNnue } from "../api-nnue";

export interface ChessboardProps {
    seq: number,
    game?: Game,
    onNnueUpdate?: (data: ApiNnue) => void
}

const Chessboard = ({ game, seq, onNnueUpdate }: ChessboardProps) => {
    if (!game) return (<>Loading..</>);
    const navigate = useNavigate();

    const { initialPosition } = game;
    const [moves, setMoves] = useState(game.moves);

    const file = (x: number) => x % 8;
    const rank = (x: number) => 8 - Math.trunc(x / 8);

    const ref = useRef(window);
    const mounted = useRef(false);

    // Perspective switching placeholder (unused for now)
    const [fen, setFen] = useState<string | undefined>();

    const [selectedSquare, setSelectedSquare] = useState<string>();
    const [legalMoves, setLegalMoves] = useState<string[]>([]);
    const [highlight, setHighlights] = useState<string[]>([]);
    const [suggestedMove, setSuggestedMove] = useState<string[] | null>(null);
    const [searchInfo, setSearchInfo] = useState<{ depth: number, eval: number } | null>(null);


    const [moveNumber, setMoveNumber] = useState(0);
    const moveNumberRef = useRef(moveNumber);
    useEffect(() => { moveNumberRef.current = moveNumber; }, [moveNumber]);

    const [position, setPosition] = useState<Position>(initialPosition);

    const [connection] = useState((prev: any) => {
        return prev || new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7097/game/realtime")
            .build();
    });

    useEffect(() => {
        if (!mounted.current) {

            connection.on('movePlayed', (message: any) => {
                if (message.plyCount > moveNumberRef.current) {
                    console.log(message);

                    const [from, to] = message.move;
                    executeMove(from, to);
                }
                
                if (message.nnue && onNnueUpdate) {
                    onNnueUpdate(message.nnue);
                }
            });
            connection.on('legalMovesReceived', setLegalMoves);
            connection.on('suggestedMove', (message: any) => {
                console.log('Suggested move:', message);
                setSuggestedMove(message.move);
                setSearchInfo({ depth: message.depth, eval: message.eval });
            });
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
        let m = [from, to] as Move;

        const piece = position[from];
        if ((piece === 'K' || piece === 'k') && Math.abs(from.charCodeAt(0) - to.charCodeAt(0)) === 2) {
            const row = piece === 'K' ? '1' : '8';
            if (to === 'g' + row) {
                 m = [from, to, 'h' + row, piece === 'K' ? 'R' : 'r', 'f' + row];
            }
            else if (to === 'c' + row) {
                 m = [from, to, 'a' + row, piece === 'K' ? 'R' : 'r', 'd' + row];
            }
        }

        setMoves(prev => [...prev, m]);
        setPosition(prev => move(prev, m));
        setSuggestedMove(null);
        setSearchInfo(null);
        setMoveNumber(prev => prev + 1);
        moveNumberRef.current++;

        setSelectedSquare(undefined);
        setLegalMoves([]);
    };

    const m = new Map<string, string>(Object.entries(position));

    const onDragStart = (e: React.DragEvent<HTMLDivElement>, id: string) => {
        e.dataTransfer.setData("text/plain", id);
    };

    const onDragEnter = (e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault();
    };
    const onDrop = (e: React.DragEvent<HTMLDivElement>, id: string) => {
        const from = e.dataTransfer.getData("text/plain") as string;
        const to = id;
        executeMove(from, to);
        connection.send('move', { "gameId": seq, "move": [from, id], "plyCount": moveNumber });

        e.preventDefault();
    };

    const serverUndoMove = ()=>{
        connection.send('undo', { "gameId": seq });
    };

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
        <div style={{ display: 'flex', flexDirection: 'row' }}>
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
                <svg style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', pointerEvents: 'none', zIndex: 10 }}>
                    {suggestedMove && (
                        <Arrow from={suggestedMove[0]} to={suggestedMove[1]} />
                    )}
                </svg>
            </BoardContainer>
            <div style={{marginTop: '10px'}}>
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
            </div>

            {searchInfo && (
                <SearchInfo>
                    <h4>Engine Analysis</h4>
                    <div><strong>Depth:</strong> {searchInfo.depth}</div>
                    <div><strong>Eval:</strong> {searchInfo.eval}</div>
                </SearchInfo>
            )}
        </div>
    )
}

export default Chessboard;
import styled, { css } from "styled-components"
import Piece from "./Piece";
import { useEffect, useRef, useState } from "react";
import { Game, Move, Position, move } from "../game";

const square_size = css`64px`;

const Square = styled.div<{ dark: boolean; selected: boolean; isLegal: boolean }>`
background: ${props => props.dark ? "#b58863" : "#f0d9b5"};
${({ selected }) => selected && `
     box-shadow: 0px 0px 25px #baca44 inset;
  `}
${({ isLegal }) => isLegal && `
     box-shadow: 0px 0px 25px #add8e6 inset;
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
    if (!game) return (<>Loading..</>)
    const { initialPosition } = game;
    const [moves, setMoves] = useState(game.moves);

    const file = (x: number) => x % 8;
    const rank = (x: number) => 8 - Math.trunc(x / 8);

    const ref = useRef(window);
    const [selectedSquare, setSelectedSquare] = useState("e2");
    const [isLegal, setIsLegal] = useState(["e3", "e4"]);

    const [moveNumber, setMoveNumber] = useState(0);
    const [position, setPosition] = useState<Position>(initialPosition);

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
        const getLegalMoves = async () => {
            if (!selectedSquare) return;

            const piece = m.get(selectedSquare);
            const result = await fetch(`https://localhost:7097/game/${seq}/legal-moves/${selectedSquare}/${piece}`);
            if (result.status === 200) {
                const moves = await result.json();
                setIsLegal(moves);
            }
        };
        getLegalMoves();
    }, [selectedSquare])

    const choosePiece = (id: string) => {
        const newSelection = m.has(id) ? id : '';
        setSelectedSquare(newSelection);
    }

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

        const m = [from, to] as Move;
        setMoves(prev => [...prev, m]);
        setPosition(prev => move(prev, m));
        setMoveNumber(prev => prev + 1);

        fetch(`https://localhost:7097/game/${seq}`, { 
            method: 'POST', 
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(m) 
        });

        e.preventDefault();
    };

    return (
        <div>
            <BoardContainer>
                {Array.from({ length: 64 }).map((_, i) => {
                    const f = file(i);
                    const r = rank(i);
                    const id = "abcdefgh"[f] + r;

                    return (<Square key={id}
                        dark={(f % 2) !== (r % 2)}
                        selected={id === selectedSquare}
                        isLegal={isLegal.includes(id)}
                        onClick={() => choosePiece(id)}
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
        </div>
    )
}

export default Chessboard;
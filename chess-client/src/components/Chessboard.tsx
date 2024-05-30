import styled, { css } from "styled-components"
import Piece from "./Piece";
import { useEffect, useRef, useState } from "react";

const square_size = css`64px`;

const Square = styled.div<{ dark: boolean; }>`
background: ${props => props.dark ? "#b58863" : "#f0d9b5"};
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

const Chessboard = () => {
    const file = (x: number) => x % 8;
    const rank = (x: number) => 8 - Math.trunc(x / 8);
    
    const ref = useRef(window);
    const [moveNumber, setMoveNumber] = useState(0);
    const [position, setPosition] = useState<any>({
        "a2": "R",
        "a4": "R",
        "a7": "p",
        "b5": "P",
        "c4": "K",
        "c5": "p",
        "e8": "k",
        "f6": "q",
        "h1": "B",
        "h8": "r"
    });

    const moves = [
        ["a7", "p", "a5", "p"],
        ["b5", "P", "a6", "P", "a5", "p"],
        ["f6", "q", "e6", "q"],
        ["c4", "K", "d3", "K"],
        ["e6", "q", "a2", "q", "a2", "R"],
        ["a6", "P", "a7", "P"],
        ["a2", "q", "a4", "q", "a4", "R"],
        ["a7", "P", "a8", "Q"],
        ["a4", "q", "a8", "q", "a8", "Q"],
        ["h1", "B", "a8", "B", "a8", "q"],
        ["e8", "k", "g8", "k", "h8", "r" , "f8"],
    ];

    const canMoveForward = moveNumber < moves.length;
    const canMoveBackwards = moveNumber > 0;

    const doMove = () => {
        if (moveNumber >= moves.length) return;
        const [
            fromSquare, _, 
            toSquare, toPiece, 
            captureSquare, capturePiece,
            castleSquare
        ] = moves[moveNumber];
        const newPosition =  {
            ...position, 
            [fromSquare]: undefined, 
            [captureSquare]: undefined, 
            [castleSquare]: capturePiece,
            [toSquare]: toPiece
        };

        setPosition(newPosition);
        setMoveNumber(moveNumber + 1);
    };

    const undoMove = () => {
        if (moveNumber <= 0) return;

        const [
            fromSquare, fromPiece, 
            toSquare, _, 
            captureSquare, capturePiece,
            castleSquare
        ] = moves[moveNumber - 1];

        const newPosition = {
            ...position,
            [fromSquare]: fromPiece,
            [toSquare]: undefined,
            [castleSquare]: undefined,
            [captureSquare]: capturePiece,
        };

        setPosition(newPosition);
        setMoveNumber(moveNumber - 1);
    };

    useEffect(() => {
        const keyHandler = (e: KeyboardEvent) => {
            if (e.key === 'ArrowLeft') undoMove();
            else if (e.key === 'ArrowRight') doMove();
        };
        
        ref.current.addEventListener('keydown', keyHandler);

        return () => ref.current.removeEventListener('keydown', keyHandler);
    }, [moveNumber, position])

    const m = new Map<string, string>(Object.entries(position));

    return (
        <div>
            <BoardContainer>
                {Array.from({ length: 64 }).map((_, i) => {
                    const f = file(i);
                    const r = rank(i);
                    const id = "abcdefgh"[f] + r;

                    return (<Square key={id} dark={(f % 2) !== (r % 2)}>
                        <Piece value={m.get(id)} />
                    </Square>)
                })
                }
            </BoardContainer>
            <button onClick={() => undoMove()} disabled={!canMoveBackwards} >Back</button>
            <button onClick={() => doMove()} disabled={!canMoveForward} >Next</button>
        </div>
    )
}

export default Chessboard;
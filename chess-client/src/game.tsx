export interface Game {
    initialPosition: any,
    moves: Array<Move>;
}

export type Piece = 'p' | 'n' | 'b' | 'r' | 'r' | 'q'
    | 'P' | 'N' | 'B' | 'R' | 'R' | 'Q';

export interface Position {
    [square: string]: Piece
}

export type Move = [
    fromSquare: string, fromPiece: string,
    toSquare: string, toPiece: string,
    captureSquare?: string, capturePiece?: string,
    castleSquare?: string];

export const move = (position : Position, move : Move) : Position => {
    const [
        fromSquare,
        toSquare,
        captureSquare, capturePiece,
        castleSquare
    ] = move;
    const toPiece = position[fromSquare];
    console.log({ fromSquare, toSquare, captureSquare, capturePiece, castleSquare })
    return {
        ...position,
        [fromSquare]: undefined,
        [captureSquare]: undefined,
        [castleSquare || '_']: capturePiece,
        [toSquare]: toPiece
    } as Position;
}
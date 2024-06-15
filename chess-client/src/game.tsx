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
    fromSquare: string,
    toSquare: string,
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
    return {
        ...position,
        [fromSquare]: undefined,
        [captureSquare || '_']: undefined,
        [castleSquare || '_']: capturePiece,
        [toSquare]: toPiece
    } as Position;
}
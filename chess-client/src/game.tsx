export interface Game {
    initialPosition : any,
    moves: Array<[
        fromSquare : string, fromPiece : string,
        toSquare : string, toPiece : string,
        captureSquare? : string, capturePiece? : string,
        castleSquare? : string]>;
}
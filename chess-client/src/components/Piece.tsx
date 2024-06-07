import BlackPawn from '../assets/pd.svg';
import BlackKnight from '../assets/nd.svg';
import BlackBishop from '../assets/bd.svg';
import BlackRook from '../assets/rd.svg';
import BlackQueen from '../assets/qd.svg';
import BlackKing from '../assets/kd.svg';

import WhitePawn from '../assets/pl.svg';
import WhiteKnight from '../assets/nl.svg';
import WhiteBishop from '../assets/bl.svg';
import WhiteRook from '../assets/rl.svg';
import WhiteQueen from '../assets/ql.svg';
import WhiteKing from '../assets/kl.svg';

const Chessmen = new Map<string, string>([
    ["p", BlackPawn],
    ["n", BlackKnight],
    ["b", BlackBishop],
    ["r", BlackRook],
    ["q", BlackQueen],
    ["k", BlackKing],
    ["P", WhitePawn],
    ["N", WhiteKnight],
    ["B", WhiteBishop],
    ["R", WhiteRook],
    ["Q", WhiteQueen],
    ["K", WhiteKing]
]);

const Piece = ({ value } : { value : string | undefined}) => {
    const source = value ? Chessmen.get(value) : value; 

    const startDrag = (e : DragEvent<HTMLImageElement>)=> {
        console.log(e);
        e.dataTransfer.setData("textPlain", value);
        e.dataTransfer.dropEffect = "move";

    }

    return (source) 
        ? <img src={source} alt={value} draggable onDragStart={startDrag} />
        : <></>;
}

export default Piece;
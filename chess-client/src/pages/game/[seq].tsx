import { useEffect, useState } from "react";
import Chessboard from "../../components/Chessboard";
import { Game } from "../../game";
import { useParams } from "react-router";

const GameView = () => {
    const { seq } = useParams();
    const [game, setGame] = useState<Game>();

    useEffect(() => {
        const loadData = async () => {
            const result = await fetch(`https://localhost:7097/game/${seq}`);
            if (result.status === 200) {
                const data = await result.json();
                setGame(data);
            }
        };
        loadData();
    }, [seq])

    return (<Chessboard game={game} seq={Number.parseInt(seq || '-1')} />);
}

export default GameView;
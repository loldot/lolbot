import { useEffect, useState } from "react";
import Chessboard from "../../components/Chessboard";
import { Game } from "../../game";

const MasterGameView = () => {
    const [game, setGame] = useState<Game>();

    useEffect(() => {
        const loadData = async () => {
            // Load game sequence 0 (master game)
            const result = await fetch(`https://localhost:7097/game/0`);
            if (result.status === 200) {
                const data = await result.json();
                setGame(data);
            }
        };
        loadData();
    }, [])

    return game ? <Chessboard game={game} seq={0} /> : <div>Loading master game...</div>;
}

export default MasterGameView;
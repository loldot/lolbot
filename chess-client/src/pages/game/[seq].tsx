import { useEffect, useState } from "react";
import Chessboard from "../../components/Chessboard";
import { Game } from "../../game";
import { useParams } from "react-router";
import NnueVisualizer from "../../components/NnueVisualizer";
import { ApiNnue, getNnueData } from "../../api-nnue";

const GameView = () => {
    const { seq } = useParams();
    const [game, setGame] = useState<Game>();
    const [nnueData, setNnueData] = useState<ApiNnue | null>(null);
    const seqNum = Number.parseInt(seq || '-1');

    useEffect(() => {
        const loadData = async () => {
            const result = await fetch(`https://localhost:7097/game/${seq}`);
            if (result.status === 200) {
                const data = await result.json();
                setGame(data);
            }
            
            if (seqNum !== -1) {
                const nnue = await getNnueData(seqNum);
                setNnueData(nnue);
            }
        };
        loadData();
    }, [seq])

    return (
        <div>
            <Chessboard game={game} seq={seqNum} onNnueUpdate={setNnueData} />
            {seqNum !== -1 && <NnueVisualizer data={nnueData} />}
        </div>
    );
}

export default GameView;
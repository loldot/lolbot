import { useEffect, useState } from "react";
import Chessboard from "../../components/Chessboard";
import { Game } from "../../game";
import { useNavigate, useParams } from "react-router-dom";
import NnueVisualizer from "../../components/NnueVisualizer";
import { ApiNnue, getNnueData } from "../../api-nnue";

const GameView = () => {
    const { seq } = useParams();
    const navigate = useNavigate();
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

    const handlePgnUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) {
            return;
        }

        const formData = new FormData();
        formData.append("file", file);

        const result = await fetch(`https://localhost:7097/game/pgn`, {
            method: "POST",
            body: formData,
        });

        if (result.ok) {
            const data = await result.json();
            navigate(`/game/${data.id}`);
        }
    };

    return (
        <div>
            <div>
                <label htmlFor="pgn-upload">Upload PGN:</label>
                <input id="pgn-upload" type="file" onChange={handlePgnUpload} accept=".pgn" />
            </div>
            <Chessboard game={game} seq={seqNum} onNnueUpdate={setNnueData} />
            {seqNum !== -1 && <NnueVisualizer data={nnueData} />}
        </div>
    );
}

export default GameView;
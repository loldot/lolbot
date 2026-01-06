import { useEffect, useState } from "react";
import Chessboard from "../../components/Chessboard";
import { Game } from "../../game";
import { createGame, baseUrl } from "../../api";

const AnalysisBoard = () => {
    const [game, setGame] = useState<Game>();
    const [seq, setSeq] = useState<number>(-1);

    useEffect(() => {
        const initGame = async () => {
            const newSeq = await createGame();
            if (newSeq !== -1) {
                setSeq(newSeq);
                const result = await fetch(`${baseUrl}/game/${newSeq}`);
                if (result.status === 200) {
                    const data = await result.json();
                    setGame(data);
                }
            }
        };
        initGame();
    }, []);

    if (!game) {
        return <div>Starting new analysis session...</div>;
    }

    return (
        <div style={{ padding: '20px', maxWidth: '800px', margin: '0 auto' }}>
            <Chessboard game={game} seq={seq} />
        </div>
    );
}

export default AnalysisBoard;
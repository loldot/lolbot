import { useState } from "react";
import { useNavigate } from "react-router";
import { createGame } from "../../api";

const NewGame = () => {
    const [fen, setFen] = useState<string>("");
    const navigate = useNavigate();

    const handleCreateGame = async () => {
        const seq = await createGame(fen || undefined);
        if (seq !== -1) {
            navigate(`/game/${seq}`);
        }
    };

    return (
        <div style={{ padding: '20px', maxWidth: '600px', margin: '0 auto' }}>
            <h2>New Game</h2>
            <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', marginBottom: '5px', fontWeight: 'bold' }}>
                    Starting Position (FEN) - Optional
                </label>
                <input 
                    type="text" 
                    value={fen} 
                    onChange={(e) => setFen(e.target.value)} 
                    placeholder="Standard starting position if left empty"
                    style={{ width: '100%', padding: '8px', boxSizing: 'border-box' }}
                />
            </div>
            <button 
                onClick={handleCreateGame} 
                
            >
                Start Game
            </button>
        </div>
    );
}

export default NewGame;
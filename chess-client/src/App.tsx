import { useEffect, useState } from 'react'
import Chessboard from './components/Chessboard'
import { Game } from './game';

function App() {
  const [gameId, setGameId] = useState("asd");
  const [game, setGame] = useState<Game>();

  useEffect(() => {
    const loadData = async () => {
      const result = await fetch(`https://localhost:7097/game/${gameId}`);
      if (result.status === 200) {
        const data = await result.json();
        setGame(data);
      }
    };
    loadData();
  }, [gameId])

  return (<Chessboard game={game} />)
}

export default App

import { useEffect, useState } from "react";
import { Navigate } from "react-router";

const NewGame = () => {
    const [seq, setSeq] = useState<Number | undefined>();
    useEffect(() => {
        const loadData = async () => {
          const result = await fetch('https://localhost:7097/game/new', { method: 'POST'});
          if (result.status === 200) {
            const data = await result.json();
            setSeq(data.seq);
          }
        };
        loadData();
      });
  
      return seq ? <Navigate to={`/game/${seq}`} /> : null;
}

export default NewGame;
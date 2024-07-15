const baseUrl = 'https://localhost:7097';

export const createGame = async (fen? : string) : Promise<number> => {
    const result = await fetch(`${baseUrl}/game/new`, { 
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(fen)
    });
    if (result.status === 200) {
      const data = await result.json();
      return data.seq as number;
    }

    return -1;
}
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

export interface EngineSummary {
    commitHash: string;
    totalPositions: number;
    correctPositions: number;
    correctPercentage: number;
    averageDepth: number;
    averageNodes: number;
    averageNps: number;
    averageTimeMs: number;
    latestTestTime: string;
}

export interface PositionResult {
    positionName: string;
    fen: string;
    bestMove: string;
    actualDepth: number;
    nodes: number;
    nps: number;
    timeMs: number;
    scoreCp?: number;
    scoreMate?: number;
    principalVariation: string;
    isCorrectMove: boolean;
}

export async function fetchEngineSummaries(): Promise<EngineSummary[]> {
    const res = await fetch(`${baseUrl}/api/tests/engines`);
    if (!res.ok) throw new Error('Failed to fetch engine summaries');
    return await res.json();
}

export async function fetchPositionResults(commitHash: string): Promise<PositionResult[]> {
    const res = await fetch(`${baseUrl}/api/tests/positions/${commitHash}`);
    if (res.status === 404) return [];
    if (!res.ok) throw new Error('Failed to fetch position results');
    return await res.json();
}
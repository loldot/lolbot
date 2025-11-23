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
    enginePath: string;
    engineFolder: string;
    commitHash: string;
    committedAt?: string;
    totalPositions: number;
    correctPositions: number;
    correctPercentage: number;
    averageDepth: number;
    averageNodes: number;
    totalNodes: number;
    averageNps: number;
    averageBranchingFactor: number;
}

export interface PositionResult {
    category: string;
    fen: string;
    bestMove: string;
    worstMove: string;
    depth: number;
    averageNodes: number;
    totalNodes: number;
    averageNps: number;
    branchingFactor: number;
    isCorrectMove: boolean;
}

export interface AvailablePosition {
    category: string;
    fen: string;
    bestMove: string;
    worstMove: string;
    attempts: number;
}

export interface PositionHistoryPoint {
    enginePath: string;
    engineFolder: string;
    commitHash: string;
    committedAt?: string | null;
    category: string;
    bestMove: string;
    worstMove: string;
    depth: number;
    averageNodes: number;
    totalNodes: number;
    averageNps: number;
    branchingFactor: number;
    isCorrectMove: boolean;
}

export async function fetchEngineSummaries(limit = 250): Promise<EngineSummary[]> {
    const params = new URLSearchParams({ limit: limit.toString() });
    const res = await fetch(`${baseUrl}/api/tests/engines?${params}`);
    if (!res.ok) throw new Error('Failed to fetch engine summaries');
    return await res.json();
}

export async function fetchPositionResults(enginePath: string): Promise<PositionResult[]> {
    const encoded = encodeURIComponent(enginePath);
    const res = await fetch(`${baseUrl}/api/tests/positions/${encoded}`);
    if (!res.ok) throw new Error('Failed to fetch position results');
    return await res.json();
}

export async function fetchAvailablePositions(): Promise<AvailablePosition[]> {
    const res = await fetch(`${baseUrl}/api/tests/positions`);
    if (!res.ok) throw new Error('Failed to fetch available positions');
    return await res.json();
}

export async function fetchPositionHistory(fen: string, limit = 500): Promise<PositionHistoryPoint[]> {
    const params = new URLSearchParams({ fen, limit: limit.toString() });
    const res = await fetch(`${baseUrl}/api/tests/positions/history?${params}`);
    if (!res.ok) throw new Error('Failed to fetch position history');
    return await res.json();
}
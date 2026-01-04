import { baseUrl } from './api';

export interface ApiNnue {
    hiddenActivations: number[];
    outputWeights: number[];
    outputBias: number;
    evaluation: number;
}

export const getNnueData = async (seq: number): Promise<ApiNnue | null> => {
    const result = await fetch(`${baseUrl}/game/${seq}/nnue`);
    if (result.status === 200) {
        return await result.json();
    }
    return null;
}

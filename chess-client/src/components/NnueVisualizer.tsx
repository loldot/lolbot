import { ApiNnue } from "../api-nnue";

interface Props {
    data: ApiNnue | null;
}

const NnueVisualizer = ({ data }: Props) => {
    if (!data) return <div>Loading NNUE data...</div>;

    const { hiddenActivations, outputWeights, outputBias, evaluation } = data;

    // Calculate contributions
    const contributions = hiddenActivations.map((act, i) => act * outputWeights[i]);

    return (
        <div style={{ padding: '20px', border: '1px solid #ccc', marginTop: '20px' }}>
            <h3>NNUE Visualization</h3>
            <div>Evaluation: {evaluation}</div>
            <div>Output Bias: {outputBias}</div>
            
            <h4>Hidden Activations</h4>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '2px' }}>
                {hiddenActivations.map((act, i) => (
                    <div key={i} style={{ 
                        width: '15px', 
                        height: '60px', 
                        border: '1px solid #eee',
                        position: 'relative',
                        backgroundColor: '#f9f9f9'
                    }} title={`Neuron ${i}: ${act.toFixed(4)}`}>
                        <div style={{ 
                            height: `${Math.min(Math.max(act, 0) * 100, 100)}%`, 
                            width: '100%', 
                            backgroundColor: 'blue',
                            position: 'absolute',
                            bottom: 0
                        }} />
                    </div>
                ))}
            </div>

            <h4>Contributions (Activation * Weight)</h4>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '2px' }}>
                {contributions.map((cont, i) => (
                    <div key={i} style={{ 
                        width: '15px', 
                        height: '60px', 
                        border: '1px solid #eee',
                        position: 'relative',
                        backgroundColor: '#f9f9f9'
                    }} title={`Neuron ${i}: ${cont.toFixed(4)} (W: ${outputWeights[i].toFixed(4)})`}>
                        <div style={{ 
                            height: `${Math.min(Math.abs(cont) / 5 * 100, 100)}%`, 
                            width: '100%', 
                            backgroundColor: cont > 0 ? 'green' : 'red',
                            position: 'absolute',
                            bottom: 0
                        }} />
                    </div>
                ))}
            </div>
        </div>
    );
};

export default NnueVisualizer;

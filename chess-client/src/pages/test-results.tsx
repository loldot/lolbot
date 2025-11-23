import React, { useEffect, useState } from 'react';
import { EngineSummary, fetchEngineSummaries, fetchPositionResults, PositionResult } from '../api';

// TypeScript declarations for web components
declare global {
  namespace JSX {
    interface IntrinsicElements {
      'ui-card': any;
      'ui-tabs': any;
      'ui-tab-panel': any;
    }
  }
}

export default function TestResultsPage() {
  const [engines, setEngines] = useState<EngineSummary[]>([]);
  const [selectedEnginePath, setSelectedEnginePath] = useState<string | null>(null);
  const [positions, setPositions] = useState<PositionResult[]>([]);
  const [loadingEngines, setLoadingEngines] = useState(false);
  const [loadingPositions, setLoadingPositions] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoadingEngines(true);
    fetchEngineSummaries()
      .then(setEngines)
      .catch(e => setError(e.message))
      .finally(() => setLoadingEngines(false));
  }, []);

  useEffect(() => {
    if (!selectedEnginePath) return;
    setLoadingPositions(true);
    fetchPositionResults(selectedEnginePath)
      .then(setPositions)
      .catch(e => setError(e.message))
      .finally(() => setLoadingPositions(false));
  }, [selectedEnginePath]);

  return (
    <div style={{ display: 'flex', gap: '2rem', padding: '1rem' }}>
      <div style={{ flex: '1 1 30%' }}>
        <h2>Engine Versions</h2>
        {loadingEngines && <p>Loading engines...</p>}
        {error && <p style={{ color: 'red' }}>{error}</p>}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {engines.map(e => (
            <ui-card
              key={e.enginePath}
              checked={selectedEnginePath === e.enginePath}
              onClick={() => setSelectedEnginePath(e.enginePath)}
              style={{ cursor: 'pointer' }}
            >
              <span slot="header" title={e.enginePath}>{extractName(e.enginePath)}</span>
              <ul>
                <li><strong>Correct:</strong> {e.correctPositions}/{e.totalPositions} ({e.correctPercentage.toFixed(1)}%)</li>
                <li><strong>Avg Depth:</strong> {e.averageDepth.toFixed(1)}</li>
                <li><strong>Avg Nodes:</strong> {Math.round(e.averageNodes).toLocaleString()}</li>
                <li><strong>Total Nodes:</strong> {e.totalNodes.toLocaleString()}</li>
                <li><strong>Avg NPS:</strong> {Math.round(e.averageNps).toLocaleString()}</li>
                <li><strong>Avg Branching:</strong> {e.averageBranchingFactor.toFixed(2)}</li>
              </ul>
            </ui-card>
          ))}
        </div>
      </div>

      <div style={{ flex: '1 1 70%' }}>
        <h2>Position Results {selectedEnginePath && `(${extractName(selectedEnginePath)})`}</h2>

        {!selectedEnginePath && (
          <ui-card checked={true}>
            <span slot="icon">ℹ️</span>
            <span slot="header">No Engine Selected</span>
            <div>Please select an engine version to view position results.</div>
          </ui-card>
        )}

        {loadingPositions && <p>Loading positions...</p>}
        {!loadingPositions && positions.length === 0 && selectedEnginePath && (
          <ui-card checked={false}>
            <span slot="icon">⚠️</span>
            <span slot="header">No Results</span>
            <div>No position results found for this engine version.</div>
          </ui-card>
        )}

        {positions.length > 0 && (
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr>
                <th style={th}>Category</th>
                <th style={th}>Best</th>
                <th style={th}>Depth</th>
                <th style={th}>Worst</th>
                <th style={th}>Avg Nodes</th>
                <th style={th}>Total Nodes</th>
                <th style={th}>Avg NPS</th>
                <th style={th}>Branching</th>
                <th style={th}>FEN</th>
                <th style={th}>Correct</th>
              </tr>
            </thead>
            <tbody>
              {positions.map(p => (
                <tr key={`${p.category}-${p.fen}`} style={{ background: p.isCorrectMove ? 'var(--accent-dark)' : '' }}>
                  <td style={td}>{p.category}</td>
                  <td style={td}>{p.bestMove}</td>
                  <td style={td}>{p.depth}</td>
                  <td style={td}>{p.worstMove}</td>
                  <td style={td}>{p.averageNodes.toLocaleString()}</td>
                  <td style={td}>{p.totalNodes.toLocaleString()}</td>
                  <td style={td}>{p.averageNps.toLocaleString()}</td>
                  <td style={td}>{p.branchingFactor.toFixed(2)}</td>
                  <td style={{ ...td, maxWidth: 240, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={p.fen}>{p.fen}</td>
                  <td style={td}>{p.isCorrectMove ? '✔' : '✖'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

const th: React.CSSProperties = { padding: '4px', position: 'sticky', top: 0 };
const td: React.CSSProperties = { padding: '4px' };

function extractName(path: string): string {
  return path;
}

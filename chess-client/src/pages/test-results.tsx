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
  const [selectedCommit, setSelectedCommit] = useState<string | null>(null);
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
    if (!selectedCommit) return;
    setLoadingPositions(true);
    fetchPositionResults(selectedCommit)
      .then(setPositions)
      .catch(e => setError(e.message))
      .finally(() => setLoadingPositions(false));
  }, [selectedCommit]);

  return (
    <div style={{ display: 'flex', gap: '2rem', padding: '1rem' }}>
      <div style={{ flex: '1 1 30%' }}>
        <h2>Engine Versions</h2>
        {loadingEngines && <p>Loading engines...</p>}
        {error && <p style={{ color: 'red' }}>{error}</p>}
        
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {engines.map(e => (
            <ui-card 
              key={e.commitHash}
              checked={selectedCommit === e.commitHash}
              onClick={() => setSelectedCommit(e.commitHash)}
              style={{ cursor: 'pointer' }}
            >
              <span slot="icon">🚀</span>
              <span slot="header">{e.commitHash.slice(0, 8)}</span>
              <ul>
                <li><strong>Correct:</strong> {e.correctPositions}/{e.totalPositions} ({e.correctPercentage.toFixed(1)}%)</li>
                <li><strong>Avg Depth:</strong> {e.averageDepth.toFixed(1)}</li>
                <li><strong>Avg NPS:</strong> {e.averageNps.toLocaleString()}</li>
              </ul>
            </ui-card>
          ))}
        </div>
      </div>
      
      <div style={{ flex: '1 1 70%' }}>
        <h2>Position Results {selectedCommit && `(${selectedCommit.slice(0,8)})`}</h2>
        
        {!selectedCommit && (
          <ui-card checked={true}>
            <span slot="icon">ℹ️</span>
            <span slot="header">No Engine Selected</span>
            <div>Please select an engine version to view position results.</div>
          </ui-card>
        )}
        
        {loadingPositions && <p>Loading positions...</p>}
        {!loadingPositions && positions.length === 0 && selectedCommit && (
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
                <th style={th}>Pos</th>
                <th style={th}>Best</th>
                <th style={th}>Depth</th>
                <th style={th}>Nodes</th>
                <th style={th}>NPS</th>
                <th style={th}>Time(ms)</th>
                <th style={th}>Score</th>
                <th style={th}>Correct</th>
                <th style={th}>PV</th>
              </tr>
            </thead>
            <tbody>
              {positions.map(p => (
                <tr key={p.positionName} style={{ background: p.isCorrectMove ? 'var(--accent-dark)' : '' }}>
                  <td style={td}>{p.positionName}</td>
                  <td style={td}>{p.bestMove}</td>
                  <td style={td}>{p.actualDepth}</td>
                  <td style={td}>{p.nodes.toLocaleString()}</td>
                  <td style={td}>{p.nps.toLocaleString()}</td>
                  <td style={td}>{p.timeMs}</td>
                  <td style={td}>{p.scoreMate ? `M${p.scoreMate}` : p.scoreCp}</td>
                  <td style={td}>{p.isCorrectMove ? '✔' : '✖'}</td>
                  <td style={{...td, maxWidth: 240, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'}} title={p.principalVariation}>{p.principalVariation}</td>
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
const td: React.CSSProperties = {  padding: '4px' };

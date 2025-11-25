import React, { useEffect, useMemo, useState } from 'react';
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
  const [searchQuery, setSearchQuery] = useState('');
  
  const sortedEngines = useMemo(
    () =>
      [...engines].sort(
        (a, b) => getCommitTimestamp(b.committedAt) - getCommitTimestamp(a.committedAt)
      ),
    [engines]
  );
  
  const filteredEngines = useMemo(
    () => {
      if (!searchQuery.trim()) return sortedEngines;
      const query = searchQuery.toLowerCase();
      return sortedEngines.filter(engine => 
        engine.commitHash?.toLowerCase().includes(query) ||
        engine.engineFolder?.toLowerCase().includes(query) ||
        engine.enginePath?.toLowerCase().includes(query)
      );
    },
    [sortedEngines, searchQuery]
  );
  const selectedEngine = selectedEnginePath
    ? filteredEngines.find((engine) => engine.enginePath === selectedEnginePath)
    : undefined;

  useEffect(() => {
    setLoadingEngines(true);
    fetchEngineSummaries()
      .then(data => {
        const ordered = [...data].sort(
          (a, b) => getCommitTimestamp(b.committedAt) - getCommitTimestamp(a.committedAt)
        );
        setEngines(ordered);
        if (ordered.length > 0) {
          setSelectedEnginePath(prev => prev ?? ordered[0].enginePath);
        }
      })
      .catch(e => setError(e.message))
      .finally(() => setLoadingEngines(false));
  }, []);

  useEffect(() => {
    if (!selectedEnginePath) return;
    const engine = filteredEngines.find((item) => item.enginePath === selectedEnginePath);
    const identifier = engine?.commitHash || engine?.enginePath || selectedEnginePath;
    setLoadingPositions(true);
    fetchPositionResults(identifier)
      .then(setPositions)
      .catch(e => setError(e.message))
      .finally(() => setLoadingPositions(false));
  }, [selectedEnginePath, engines]);

  return (
    <div style={{ display: 'flex', gap: '2rem', padding: '1rem' }}>
      <div style={{ flex: '1 1 30%' }}>
        <h2>Engine Versions</h2>
        {loadingEngines && <p>Loading engines...</p>}
        {error && <p style={{ color: 'red' }}>{error}</p>}

        <input
          type="text"
          placeholder="Search by commit, folder, or path..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          style={{
            width: '100%',
            padding: '0.5rem',
            marginBottom: '1rem',
            borderRadius: '4px',
            border: '1px solid var(--border-color, #ccc)',
            fontSize: '0.9rem'
          }}
        />

        {filteredEngines.length === 0 && searchQuery && (
          <p style={{ color: 'var(--text-muted, #666)' }}>
            No engines match "{searchQuery}"
          </p>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {filteredEngines.map((engine) => (
            <ui-card
              key={engine.enginePath}
              checked={selectedEnginePath === engine.enginePath}
              onClick={() => setSelectedEnginePath(engine.enginePath)}
              style={{ cursor: 'pointer' }}
            >
              <span slot="icon">🚀</span>
              <span slot="header" title={engine.enginePath}>{getEngineLabel(engine)}</span>
              <ul>
                <li><strong>Commit:</strong> {engine.commitHash || 'Unknown'}</li>
                <li><strong>Committed:</strong> {formatCommittedAt(engine.committedAt)}</li>
                <li><strong>Folder:</strong> {engine.engineFolder || 'n/a'}</li>
                <li><strong>Correct:</strong> {engine.correctPositions}/{engine.totalPositions} ({engine.correctPercentage.toFixed(1)}%)</li>
                <li><strong>Avg Depth:</strong> {engine.averageDepth.toFixed(1)}</li>
                <li><strong>Avg Nodes:</strong> {Math.round(engine.averageNodes).toLocaleString()}</li>
                <li><strong>Total Nodes:</strong> {engine.totalNodes.toLocaleString()}</li>
                <li><strong>Avg NPS:</strong> {Math.round(engine.averageNps).toLocaleString()}</li>
                <li><strong>Avg Branching:</strong> {engine.averageBranchingFactor.toFixed(2)}</li>
              </ul>
            </ui-card>
          ))}
        </div>
      </div>

      <div style={{ flex: '1 1 70%' }}>
        <h2>Position Results {selectedEngine && `(${getEngineLabel(selectedEngine)})`}</h2>

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

        {selectedEngine && positions.length > 0 && (
          <p style={{ margin: '0 0 0.5rem 0' }}>
            <strong>Commit:</strong> {selectedEngine.commitHash || getEngineLabel(selectedEngine)} &nbsp;•&nbsp; <strong>Committed:</strong> {formatCommittedAt(selectedEngine.committedAt)}
          </p>
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
  if (!path) return '';
  const trimmed = path.replace(/[/\\]+$/, '');
  const parts = trimmed.split(/[\\/]/);
  const last = parts.pop();
  return last && last.length > 0 ? last : trimmed;
}

function getEngineLabel(engine: EngineSummary): string {
  if (engine.commitHash) return engine.commitHash;
  if (engine.engineFolder && /^[0-9a-fA-F]{7}$/.test(engine.engineFolder)) {
    return engine.engineFolder;
  }
  return extractName(engine.enginePath);
}

function formatCommittedAt(value?: string): string {
  if (!value) return 'Unknown';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function getCommitTimestamp(value?: string): number {
  if (!value) return 0;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getTime();
}

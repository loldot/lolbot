import React, { useEffect, useMemo, useState } from 'react';
import { Line } from 'react-chartjs-2';
import type { ChartOptions, TooltipItem } from 'chart.js';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Tooltip,
  Legend,
} from 'chart.js';
import {
  AvailablePosition,
  fetchAvailablePositions,
  fetchPositionHistory,
  PositionHistoryPoint,
} from '../api';

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Legend);

type MetricKey = 'averageNodes' | 'averageNps' | 'branchingFactor';

type MetricDefinition = {
  key: MetricKey;
  label: string;
  color: string;
  background: string;
  formatter: (value: number) => number;
};

const METRICS: MetricDefinition[] = [
  {
    key: 'averageNodes',
    label: 'Average Nodes',
    color: 'rgb(59, 130, 246)',
    background: 'rgba(59, 130, 246, 0.2)',
    formatter: (value) => value,
  },
  {
    key: 'averageNps',
    label: 'Average NPS',
    color: 'rgb(14, 165, 233)',
    background: 'rgba(14, 165, 233, 0.2)',
    formatter: (value) => value,
  },
  {
    key: 'branchingFactor',
    label: 'Branching Factor',
    color: 'rgb(16, 185, 129)',
    background: 'rgba(16, 185, 129, 0.2)',
    formatter: (value) => value,
  },
];

export default function PositionTrendsPage() {
  const [positions, setPositions] = useState<AvailablePosition[]>([]);
  const [selectedFen, setSelectedFen] = useState<string>('');
  const [history, setHistory] = useState<PositionHistoryPoint[]>([]);
  const [loadingPositions, setLoadingPositions] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedMetrics, setSelectedMetrics] = useState<MetricKey[]>([
    'averageNodes',
    'averageNps',
    'branchingFactor',
  ]);

  useEffect(() => {
    setLoadingPositions(true);
    setError(null);
    fetchAvailablePositions()
      .then((data) => {
        setPositions(data);
        if (data.length > 0) {
          setSelectedFen((prev) => prev || data[0].fen);
        }
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoadingPositions(false));
  }, []);

  useEffect(() => {
    if (!selectedFen) {
      setHistory([]);
      return;
    }
    setLoadingHistory(true);
    setError(null);
    fetchPositionHistory(selectedFen)
      .then(setHistory)
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoadingHistory(false));
  }, [selectedFen]);

  const groupedPositions = useMemo(() => {
    const map = new Map<string, AvailablePosition[]>();
    positions.forEach((pos) => {
      const list = map.get(pos.category) ?? [];
      list.push(pos);
      map.set(pos.category, list);
    });
    return Array.from(map.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([category, list]) => [
        category,
        list.sort((a, b) =>
          (a.bestMove || a.worstMove || '').localeCompare(b.bestMove || b.worstMove || '')
        ),
      ] as const);
  }, [positions]);

  const latestPoint = history.length > 0 ? history[history.length - 1] : undefined;

  const labels = useMemo(
    () =>
      history.map((point) => {
        const date = point.committedAt ? formatShortDate(point.committedAt) : 'Unknown';
        return `${point.commitHash || point.engineFolder} (${date})`;
      }),
    [history]
  );

  const chartConfigs = useMemo(
    () =>
      METRICS.filter((metric) => selectedMetrics.includes(metric.key)).map((metric) => ({
        metric,
        data: {
          labels,
          datasets: [
            {
              label: metric.label,
              data: history.map((point) => metric.formatter(getMetricValue(point, metric.key))),
              borderColor: metric.color,
              backgroundColor: metric.background,
              tension: 0.25,
              pointRadius: 3,
              pointHoverRadius: 6,
              borderWidth: 2,
            },
          ],
        },
      })),
    [history, labels, selectedMetrics]
  );

  const historyDescending = useMemo(
    () =>
      [...history]
        .sort((a, b) => getCommitTimestamp(b.committedAt) - getCommitTimestamp(a.committedAt))
        .map((point) => point),
    [history]
  );

  const chartOptions = useMemo<ChartOptions<'line'>>(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'bottom' as const,
        },
        tooltip: {
          callbacks: {
            label: (context: TooltipItem<'line'>) => {
              const label = context.dataset.label ?? '';
              const parsed = typeof context.parsed === 'number' ? context.parsed : context.parsed.y ?? 0;
              return `${label}: ${formatNumber(parsed)}`;
            },
          },
        },
      },
      scales: {
        y: {
          ticks: {
            callback: (value: number | string) => formatNumber(Number(value)),
          },
        },
      },
    }),
    []
  );

  const handleMetricToggle = (metric: MetricKey) => {
    setSelectedMetrics((prev) =>
      prev.includes(metric) ? prev.filter((m) => m !== metric) : [...prev, metric]
    );
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
      <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap' }}>
        <div style={{ flex: '1 1 280px', maxWidth: 340 }}>
          <ui-card>
            <span slot="icon">📌</span>
            <span slot="header">Select Position</span>
            {loadingPositions && <p>Loading positions...</p>}
            {error && <p style={{ color: 'red' }}>{error}</p>}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              <label>
                <span style={{ display: 'block', marginBottom: 4 }}>Position</span>
                <select
                  value={selectedFen}
                  onChange={(evt) => setSelectedFen(evt.target.value)}
                  disabled={loadingPositions}
                  style={{ width: '100%', padding: '0.4rem' }}
                >
                  {groupedPositions.map(([category, list]) => (
                    <optgroup label={category} key={category}>
                      {list.map((pos) => (
                        <option value={pos.fen} key={`${category}-${pos.fen}`}>
                          {pos.category}: {pos.bestMove || pos.worstMove || 'Unknown'}
                        </option>
                      ))}
                    </optgroup>
                  ))}
                </select>
              </label>

              {latestPoint && (
                <div>
                  <div><strong>Best Move:</strong> {latestPoint.bestMove || 'n/a'}</div>
                  <div><strong>Worst Move:</strong> {latestPoint.worstMove || 'n/a'}</div>
                  <div><strong>Latest Commit:</strong> {latestPoint.commitHash || latestPoint.engineFolder}</div>
                  <div><strong>Committed:</strong> {formatCommittedAt(latestPoint.committedAt)}</div>
                </div>
              )}

              <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
                <legend style={{ fontWeight: 600, marginBottom: 4 }}>Metrics</legend>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  {METRICS.map((metric) => (
                    <label key={metric.key} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <input
                        type="checkbox"
                        checked={selectedMetrics.includes(metric.key)}
                        onChange={() => handleMetricToggle(metric.key)}
                      />
                      <span>{metric.label}</span>
                    </label>
                  ))}
                </div>
              </fieldset>
            </div>
          </ui-card>
        </div>

        <div style={{ flex: '3 3 320px', minWidth: 'min(100%, 840px)', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          {loadingHistory && (
            <div>
              <span slot="icon">⏳</span>
              <span slot="header">Performance Over Time</span>
              <p>Loading history...</p>
            </div>
          )}

          {!loadingHistory && history.length === 0 && selectedFen && (
            <div>
              <span slot="icon">ℹ️</span>
              <span slot="header">Performance Over Time</span>
              <p>No history found for this position yet.</p>
            </div>
          )}

          {!loadingHistory && history.length > 0 && selectedMetrics.length === 0 && (
            <div>
              <span slot="icon">⚠️</span>
              <span slot="header">Performance Over Time</span>
              <p>Select at least one metric to view the charts.</p>
            </div>
          )}

          {!loadingHistory && history.length > 0 && selectedMetrics.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'row', gap: '1.25rem', width: '100%' }}>
              {chartConfigs.map(({ metric, data }) => (
                <div key={metric.key} style={{ width: '100%' }}>
                  <span slot="icon">📈</span>
                  <span slot="header">{metric.label}</span>
                  <div style={{ width: '100%', height: 360 }}>
                    <Line data={data} options={chartOptions} />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {history.length > 0 && (
        <div>
          <h3>Recent Samples</h3>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr>
                <th style={th}>Commit</th>
                <th style={th}>Date</th>
                <th style={th}>Depth</th>
                <th style={th}>Avg Nodes</th>
                <th style={th}>Avg NPS</th>
                <th style={th}>Branching</th>
                <th style={th}>Correct</th>
              </tr>
            </thead>
            <tbody>
              {historyDescending.slice(0, 30).map((point) => (
                <tr key={`${point.commitHash}-${point.enginePath}`}>
                  <td style={td}>{point.commitHash || point.engineFolder}</td>
                  <td style={td}>{formatCommittedAt(point.committedAt)}</td>
                  <td style={td}>{point.depth}</td>
                  <td style={td}>{formatNumber(point.averageNodes)}</td>
                  <td style={td}>{formatNumber(point.averageNps)}</td>
                  <td style={td}>{point.branchingFactor.toFixed(2)}</td>
                  <td style={td}>{point.isCorrectMove ? '✔' : '✖'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

const th: React.CSSProperties = { padding: '4px', textAlign: 'left', borderBottom: '1px solid var(--text)' };
const td: React.CSSProperties = { padding: '4px', borderBottom: '1px solid rgba(255,255,255,0.05)' };

function formatShortDate(value?: string | null): string {
  if (!value) return 'Unknown';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString();
}

function formatCommittedAt(value?: string | null): string {
  if (!value) return 'Unknown';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function formatNumber(value: number): string {
  if (!Number.isFinite(value)) return '0';
  if (Math.abs(value) >= 1_000_000)
  {
    return `${(value / 1_000_000).toFixed(1)}M`;
  }
  if (Math.abs(value) >= 1_000)
  {
    return `${(value / 1_000).toFixed(1)}k`;
  }
  return Math.round(value).toLocaleString();
}

function getMetricValue(point: PositionHistoryPoint, key: MetricKey): number {
  switch (key)
  {
    case 'averageNodes':
      return point.averageNodes;
    case 'averageNps':
      return point.averageNps;
    case 'branchingFactor':
      return point.branchingFactor;
    default:
      return 0;
  }
}

function getCommitTimestamp(value?: string | null): number {
  if (!value) return 0;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getTime();
}

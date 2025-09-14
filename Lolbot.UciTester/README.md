# Lolbot UCI Tester

A command-line tool for testing chess engines using the UCI protocol. This tool runs a set of test positions against different engine versions and stores performance metrics in an SQLite database. It also provides comprehensive reporting capabilities to analyze engine performance over time.

## Usage

### Testing Engines
```bash
Lolbot.UciTester.exe test <commit-hash> [options]
```

### Generating Reports
```bash
Lolbot.UciTester.exe report [options]
```

## Commands

### test
Run UCI tests against a specific engine version.

**Arguments:**
- `commit-hash`: Short commit hash of the engine version to test

**Options:**
- `--depth <int>`: Search depth for each position (default: 10)
- `--db <path>`: Path to SQLite database file (default: test_results.db)
- `--engine-dir <path>`: Base directory for engine versions (default: C:\dev\lolbot-versions)
- `--categories <categories...>`: Test categories to run (default: CCC)
- `--log`: Enable UCI communication logging to console

### report
Generate performance reports from test data.

**Options:**
- `--db <path>`: Path to SQLite database file (default: test_results.db)
- `--limit <int>`: Number of top engines to show (default: 10)
- `--detail <commit>`: Show detailed results for a specific commit

Available categories:
- `Tactics`: Tactical positions requiring specific moves
- `CCC`: Computer Chess Championship test positions
- `Mate`: Mate-in-one and checkmate positions
- `Defense`: Defensive positions requiring correct moves to avoid mate
- `Endgame`: Endgame positions
- `Perft`: Positions for move generation testing
- `CCC_Avoid`: Positions where certain moves should be avoided

### Examples

**Testing:**
```bash
# Test a specific commit with default settings
Lolbot.UciTester.exe test abc1234

# Test with deeper search and logging
Lolbot.UciTester.exe test abc1234 --depth 15 --log

# Test with custom categories
Lolbot.UciTester.exe test abc1234 --categories CCC CCC_Avoid
```

**Reporting:**
```bash
# Show top 10 performing engines
Lolbot.UciTester.exe report

# Show top 5 performing engines
Lolbot.UciTester.exe report --limit 5

# Show detailed results for a specific commit
Lolbot.UciTester.exe report --detail abc1234

# Use custom database
Lolbot.UciTester.exe report --db my_results.db --limit 15
```

**PowerShell Script:**
```powershell
# Test specific commit
.\test-uci.ps1 -CommitHash "abc1234" -EnableLogging

# Test all engine versions in C:\dev\lolbot\versions
.\test-uci.ps1 -TestAll

# Test all with deeper search
.\test-uci.ps1 -TestAll -Depth 12

# Generate top 10 report
.\test-uci.ps1 -Report

# Generate top 5 report
.\test-uci.ps1 -Report -ReportLimit 5

# Generate detailed report for specific commit
.\test-uci.ps1 -Report -DetailCommit "abc1234"
```
```

## Engine Directory Structure

The tool expects engines to be organized as:

```
C:\dev\lolbot\versions\
├── abc1234\
│   └── Lolbot.Engine.exe
├── def5678\
│   └── Lolbot.Engine.exe
└── ...
```

Where each subdirectory is named after the commit hash (7-8 character short hash) and contains the built engine executable.

## Database Schema

The tool creates an SQLite database with the following tables:

### test_runs
- `id`: Unique test run identifier
- `commit_hash`: Git commit hash of the engine
- `engine_path`: Full path to the engine executable
- `start_time`, `end_time`: Test run timestamps
- `total_positions`, `completed_positions`: Progress tracking

### test_results
- Individual position test results
- Includes depth searched, nodes, NPS, timing data
- Best move and principal variation
- Score (centipawns or mate distance)

## Test Positions

The tool includes test positions from:

1. **Tactical Positions**: From the existing test suite, including sacrifices and combinations
2. **CCC Positions**: Computer Chess Championship test positions with known best moves
3. **Mate Positions**: Mate-in-one and forced mate sequences
4. **Endgame Positions**: King and pawn, king and rook endgames
5. **Perft Positions**: Standard positions for move generation testing

## Output

The tool provides real-time progress output showing:
- Position name and FEN
- Best move found by the engine
- Search depth achieved
- Nodes searched and NPS (nodes per second)
- Search time
- Score evaluation (centipawns or mate distance)
- Principal variation (sequence of best moves)

All results are stored in the SQLite database for later analysis and comparison between engine versions.
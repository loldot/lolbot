# Lolbot

Lolbot is a chess engine currently in development.

- It currently only supports x86-64-v3 instruction set on Windows/Linux
- It does not yet have a complete implementation of UCI, but enough to run basic tournaments
- It does not have its own opening book, currently we rely on the tournament software to
  provide the opening moves.
- It does have very limited support for Szyzygy tables, but only for generating training data.
- In general all of the functionality should be considered experimental

## Features

- Negamax
- Iterative deepening
- Aspiration windows
- Quiescence search
- TT move, history heuristic, MVV-LVA, killer moves for move ordering
- Futility pruning
- Reverse futility pruning
- Null move pruning
- Delta pruning
- SEE pruning
- Toy NNUE for evaluation

## UCI Interface

### Supported Commands

| Command | Description |
| ------- | ----------- |
| `uci` | Initialize and report engine ID + options |
| `isready` | Initialize engine and respond with readyok |
| `position fen <fen>` | Set position from FEN string |
| `position startpos` | Set standard starting position |
| `position ... moves <moves>` | Apply a list of moves to the position |
| `go wtime <ms> btime <ms> winc <ms> binc <ms>` | Search with time control |
| `ucinewgame` | Reset engine for a new game |
| `setoption name Threads <n>` | Configure search threads |
| `eval` | Print static evaluation of current position |
| `perft <depth>` | Run perft to given depth |
| `quit` | Exit engine |

### Missing UCI Commands

- `go depth` - fixed depth search
- `go nodes` - node limit search
- `go movetime` - fixed time search
- `go mate` - search for forced mate
- `go movestogo` - control by number of moves
- `go infinite` - background searching
- `ponder` - ponder mode
- `setoption name Hash` - transposition table size
- `setoption name Ponder` - ponder toggle
- `setoption name Contempt` - draw tendency
- `setoption name ClearBook` - clear opening book
- `setoption name Debug LogFile` - debug logging
- `setoption name EvalFile` - external eval file

## Requirements

The pre-built binaries are self-contained and will run on the specified OS and architecture.

Building requires the .NET 10.0 SDK

## Building

```bash
cd Lolbot.Engine


dotnet build -c Release

# or to build the AOT compiled version, on my machine the runtime dependent 
# version is slightly more performant though.
dotnet publish -c Release
```

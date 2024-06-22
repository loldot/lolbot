using System.Text;

namespace Lolbot.Core;

public readonly record struct Position
{
    public Color CurrentPlayer { get; init; } = Color.White;
    public ulong WhitePawns { get; init; } = 0x000000000000ff00;
    public ulong WhiteRooks { get; init; } = Bitboards.Create("A1", "H1");
    public ulong WhiteBishops { get; init; } = Bitboards.Create("C1", "F1");
    public ulong WhiteKnights { get; init; } = Bitboards.Create("B1", "G1");
    public ulong WhiteQueens { get; init; } = Bitboards.Create("D1");
    public ulong WhiteKing { get; init; } = Bitboards.Create("E1");

    public ulong BlackPawns { get; init; } = 0x00ff000000000000;
    public ulong BlackRooks { get; init; } = Bitboards.Create("A8", "H8");
    public ulong BlackBishops { get; init; } = Bitboards.Create("C8", "F8");
    public ulong BlackKnights { get; init; } = Bitboards.Create("B8", "G8");
    public ulong BlackQueens { get; init; } = Bitboards.Create("D8");
    public ulong BlackKing { get; init; } = Bitboards.Create("E8");
    public byte EnPassant { get; init; } = 0;

    public ulong Checkmask => FindCheckMask(CurrentPlayer, out var _);

    public Position()
    {

    }

    public ulong this[Piece piece]
    {
        get => piece switch
        {
            Piece.WhitePawn => WhitePawns,
            Piece.WhiteKnight => WhiteKnights,
            Piece.WhiteBishop => WhiteBishops,
            Piece.WhiteRook => WhiteRooks,
            Piece.WhiteQueen => WhiteQueens,
            Piece.WhiteKing => WhiteKing,
            Piece.BlackPawn => BlackPawns,
            Piece.BlackKnight => BlackKnights,
            Piece.BlackBishop => BlackBishops,
            Piece.BlackRook => BlackRooks,
            Piece.BlackQueen => BlackQueens,
            Piece.BlackKing => BlackKing,
            _ => Empty,
        };
    }

    public readonly ulong White => Bitboards.Create(WhitePawns, WhiteRooks, WhiteKnights, WhiteBishops, WhiteQueens, WhiteKing);
    public readonly ulong Black => Bitboards.Create(BlackPawns, BlackRooks, BlackKnights, BlackBishops, BlackQueens, BlackKing);

    public readonly ulong Occupied => Bitboards.Create(White, Black);
    public readonly ulong Empty => ~Occupied;

    public override string ToString()
    {
        var sb = new StringBuilder(72);
        for (char rank = '8'; rank > '0'; rank--)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                var sq = Squares.FromCoordinates("" + file + rank);
                foreach (var p in Enum.GetValues<Piece>())
                {
                    if ((sq & this[p]) != 0) sb.Append(Utils.PieceName(p));
                }
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public Position Move(Move m)
    {
        Color next = CurrentPlayer == Color.White ? Color.Black : Color.White;
        return this with
        {
            EnPassant = SetEnPassant(m),
            WhitePawns = ApplyMove(WhitePawns, m),
            WhiteBishops = ApplyMove(WhiteBishops, m),
            WhiteKnights = ApplyMove(WhiteKnights, m),
            WhiteRooks = ApplyMove(WhiteRooks, m) | Castle(0x7e, m),
            WhiteQueens = ApplyMove(WhiteQueens, m),
            WhiteKing = ApplyMove(WhiteKing, m),

            BlackPawns = ApplyMove(BlackPawns, m),
            BlackBishops = ApplyMove(BlackBishops, m),
            BlackKnights = ApplyMove(BlackKnights, m),
            BlackRooks = ApplyMove(BlackRooks, m) | Castle(0x7e00000000000000, m),
            BlackQueens = ApplyMove(BlackQueens, m),
            BlackKing = ApplyMove(BlackKing, m),
            CurrentPlayer = next,
        };
    }

    public readonly byte SetEnPassant(Move m)
    {
        var from = 1ul << m.FromIndex;
        var to = 1ul << m.ToIndex;

        // Pawn moves leading to en passant has the en passant square
        // 1 square in front of the start and 1 square behind the target
        var fromWhite = from & WhitePawns;
        var enPassant = (fromWhite << 8) & (to >> 8);

        var fromBlack = from & BlackPawns;
        enPassant ^= (fromBlack >> 8) & (to << 8);

        return Squares.ToIndex(enPassant);
    }

    private static ulong ApplyMove(ulong bitboard, Move m)
    {
        if (m.CapturePiece != Piece.None)
        {
            bitboard &= ~Squares.FromIndex(m.CaptureIndex);
        }

        var fromSq = Squares.FromIndex(m.FromIndex);
        if ((bitboard & fromSq) != 0)
        {
            bitboard ^= fromSq;
            bitboard |= Squares.FromIndex(m.ToIndex);
        }

        return bitboard;
    }

    private static ulong Castle(ulong mask, Move m)
        => mask & Squares.FromIndex(m.CastleIndex);

    public Move[] GenerateLegalMoves(Color color, Piece? pieceType = null)
    {
        const int max_moves = 218;
        Memory<Move> moves = new Move[max_moves];
        var count = 0;

        var pinmask = ulong.MaxValue;

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 1)
            count += AddPawnMoves(color, moves.Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 2)
            count += AddKnightMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 3)
            count += AddBishopMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 4)
            count += AddRookMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 5)
            count += AddQueenMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 6)
            count += AddKingMoves(color, moves[count..].Span);

        return moves[..count].ToArray();
    }

    private ulong FindCheckMask(Color color, out int countCheckers)
    {
        ulong checkmask = 0;
        countCheckers = 0;

        int opponentColor = color == Color.White ? 0x20 : 0x10;
        byte king = Squares.ToIndex(color == Color.White ? WhiteKing : BlackKing);

        // king can never check
        for (int i = 1; i < 6; i++)
        {
            var pieceType = (Piece)(opponentColor + i);
            var pieceBitboard = this[pieceType];
            while (pieceBitboard != 0)
            {
                var piece = Bitboards.PopLsb(ref pieceBitboard);
                var checker = Squares.FromIndex(piece);

                var squares = MovePatterns.SquaresBetween[piece][king];
                // Bitboards.Debug(squares);
                var attacks = MovePatterns.GetAttack(pieceType, checker, Empty);
                // if (pieceType == Piece.WhiteBishop) Bitboards.Debug(attacks);


                var pieceCheckmask = attacks & squares;
                if((pieceCheckmask & (1ul << king)) != 0)
                {
                    checkmask |= pieceCheckmask;
                    countCheckers++;
                }
            }
        }
        return countCheckers > 0 ? checkmask : ulong.MaxValue;
    }

    private ulong GetSuperKing(Color color) => color switch
    {
        Color.White => MovePatterns.GenerateSuper(Squares.ToIndex(WhiteKing), Empty) & Black,
        Color.Black => MovePatterns.GenerateSuper(Squares.ToIndex(BlackKing), Empty) & White,
        _ => 0,
    };

    private int AddQueenMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (rooks, targets, friendlies) = (color == Color.White)
            ? (WhiteQueens, Black, White)
            : (BlackQueens, White, Black);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);
            var from = Squares.FromIndex(fromIndex);

            var valid = (
                MovePatterns.RookAttacks(from, Empty)
              | MovePatterns.BishopAttacks(from, Empty)
            ) & ~friendlies & Checkmask;

            var quiets = valid & ~targets;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddKingMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (rooks, targets) = (color == Color.White)
            ? (WhiteKing, Black)
            : (BlackKing, White);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);

            var quiets = MovePatterns.Kings[fromIndex] & ~Occupied & ~Checkmask;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Kings[fromIndex] & targets & ~Checkmask;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddRookMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (rooks, targets, friendlies) = (color == Color.White)
            ? (WhiteRooks, Black, White)
            : (BlackRooks, White, Black);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);
            var from = Squares.FromIndex(fromIndex);

            var valid = MovePatterns.RookAttacks(from, Empty) & ~friendlies & Checkmask;
            var quiets = valid & ~targets;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddBishopMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (bishops, targets, friendlies) = (color == Color.White)
            ? (WhiteBishops, Black, White)
            : (BlackBishops, White, Black);

        while (bishops != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bishops);
            var from = Squares.FromIndex(fromIndex);

            var valid = MovePatterns.BishopAttacks(from, Empty) & ~friendlies & Checkmask;
            var quiets = valid & ~Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddKnightMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (knights, targets) = (color == Color.White)
            ? (WhiteKnights, Black)
            : (BlackKnights, White);

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var quiets = MovePatterns.Knights[fromIndex] & ~Occupied & Checkmask;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Knights[fromIndex] & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    private int AddPawnMoves(Color color, Span<Move> moves)
    {
        int count = 0;

        var (pawns, targets, pushPattern, attackPattern) = (color == Color.White)
            ? (WhitePawns, Black, MovePatterns.WhitePawnPushes, MovePatterns.WhitePawnAttacks)
            : (BlackPawns, White, MovePatterns.BlackPawnPushes, MovePatterns.BlackPawnAttacks);

        while (pawns != 0)
        {
            var sq = Bitboards.PopLsb(ref pawns);

            var pushes = pushPattern[sq] & Checkmask & Empty;
            while (pushes != 0)
            {
                var push = Bitboards.PopLsb(ref pushes);
                moves[count++] = new Move(sq, push);
            }

            var attacks = attackPattern[sq] & (targets | (1ul << EnPassant)) & Checkmask;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(sq, attack, attack, GetOccupant(attack));
            }
        }
        return count;
    }

    public Piece GetOccupant(byte attack)
    {
        var square = Squares.FromIndex(attack);
        foreach (var type in Enum.GetValues<Piece>())
        {
            if (type == Piece.None) continue;
            if ((this[type] & square) != 0) return type;
        }
        return Piece.None;
    }
}

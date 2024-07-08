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
    public CastlingRights CastlingRights { get; init; } = CastlingRights.All;

    public ulong Checkmask => CreateCheckMask(CurrentPlayer).Item1;
    public ulong Pinmask => CreateCheckMask(CurrentPlayer).Item2;
    public int CheckerCount => CreateCheckMask(CurrentPlayer).Item3;

    public Position()
    {
    }

    public static Position EmptyBoard => new Position() with
    {
        WhitePawns = 0,
        WhiteKnights = 0,
        WhiteBishops = 0,
        WhiteRooks = 0,
        WhiteQueens = 0,
        WhiteKing = 0,
        BlackPawns = 0,
        BlackKnights = 0,
        BlackBishops = 0,
        BlackRooks = 0,
        BlackQueens = 0,
        BlackKing = 0
    };

    public static Position FromFen(string fen)
    {
        var fenSerializer = new FenSerializer();
        return fenSerializer.Parse(fen);
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

        var position = this with
        {
            CastlingRights = ApplyCastlingRights(m),
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

        if (m.PromotionPiece != Piece.None)
        {
            var sq = Squares.FromIndex(m.ToIndex);
            return position.Update(m.PromotionPiece, this[m.PromotionPiece] | sq) with
            {
                WhitePawns = WhitePawns ^ sq,
                BlackPawns = BlackPawns ^ sq,
            };
        }

        return position;
    }

    private CastlingRights ApplyCastlingRights(Move m)
    {
        var removedCastling = m.FromIndex switch
        {
            0 => CastlingRights.WhiteQueen,
            4 => CastlingRights.WhiteKing | CastlingRights.WhiteQueen,
            7 => CastlingRights.WhiteKing,
            56 => CastlingRights.BlackQueen,
            60 => CastlingRights.BlackKing | CastlingRights.BlackQueen,
            63 => CastlingRights.BlackKing,
            _ => CastlingRights.None
        };

        removedCastling |= m.CaptureIndex switch
        {
            0 => CastlingRights.WhiteQueen,
            7 => CastlingRights.WhiteKing,
            56 => CastlingRights.BlackQueen,
            63 => CastlingRights.BlackKing,
            _ => CastlingRights.None
        };

        return CastlingRights & ~removedCastling;
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

    public Span<Move> GenerateLegalMoves()
    {
        return GenerateLegalMoves(CurrentPlayer);
    }

    public Span<Move> GenerateLegalMoves(char pieceType)
    {
        Color color = char.IsLower(pieceType) ? Color.Black : Color.White;
        Piece piece = Utils.FromName(pieceType);

        return GenerateLegalMoves(color, piece);
    }

    public Span<Move> GenerateLegalMoves(Color color, Piece? pieceType = null)
    {
        const int max_moves = 218;
        Memory<Move> moves = new Move[max_moves];
        var count = 0;

        if (CheckerCount > 1) pieceType = Piece.WhiteKing;

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

        return moves[..count].Span;
    }

  
    private (ulong, ulong, int) CreateCheckMask(Color color)
    {
        ulong pinmask = 0;
        ulong checkmask = 0;
        int countCheckers = 0;

        int opponentColor = color == Color.White ? 0x20 : 0x10;
        byte king = Squares.ToIndex(color == Color.White ? WhiteKing : BlackKing);
        var friendly = color == Color.White ? White : Black;

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
                var attacks = MovePatterns.GetAttack(pieceType, checker, Empty);

                var pieceCheckmask = attacks & squares;

                // if only two friendlies on checkmask (including king, piece is pinned)
                if (pieceCheckmask != 0 && Bitboards.CountOccupied(squares & friendly) == 2)
                {
                    pinmask |= squares | checker;
                }

                if ((pieceCheckmask & (1ul << king)) != 0)
                {
                    checkmask |= pieceCheckmask | checker;
                    countCheckers++;
                }
            }
        }

        return (
            countCheckers > 0 ? checkmask : ulong.MaxValue, 
            pinmask == 0 ? ulong.MaxValue : pinmask,
            countCheckers);
    }

    private int AddQueenMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (rooks, targets, friendlies) = (color == Color.White)
            ? (WhiteQueens, Black, White)
            : (BlackQueens, White, Black);

        while (rooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref rooks);

            var valid = (
                MovePatterns.RookAttacks(fromIndex, Occupied)
              | MovePatterns.BishopAttacks(fromIndex, Occupied)
            ) & ~friendlies & Checkmask & PinnedPiece(fromIndex);

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

    private ulong PinnedPiece(byte fromIndex)
    {
        return ((Pinmask & Squares.FromIndex(fromIndex)) > 0)
            ? Pinmask
            : ulong.MaxValue;
    }

    private int AddKingMoves(Color color, Span<Move> moves)
    {
        var count = 0;
        var (king, targets) = (color == Color.White)
            ? (WhiteKing, Black)
            : (BlackKing, White);

        ulong enemyAttacks = CreateAttackMask(color);

        var fromIndex = Squares.ToIndex(king);
        var quiets = MovePatterns.Kings[fromIndex] & ~(Occupied | enemyAttacks);
        while (quiets != 0)
        {
            var toIndex = Bitboards.PopLsb(ref quiets);
            moves[count++] = new Move(fromIndex, toIndex);
        }

        var attacks = MovePatterns.Kings[fromIndex] & targets & ~enemyAttacks;
        while (attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(fromIndex, attack, attack, GetOccupant(attack));
        }

        if (IsCastleLegal(CastlingRights.WhiteKing, king, MovePatterns.SquaresBetween[4][6], enemyAttacks))
        {
            moves[count++] = Core.Move.Castle(color);
        }

        if (IsCastleLegal(CastlingRights.WhiteQueen, king, MovePatterns.SquaresBetween[4][2], enemyAttacks))
        {
            moves[count++] = Core.Move.QueenSideCastle(color);
        }

        if (IsCastleLegal(CastlingRights.BlackKing, king, MovePatterns.SquaresBetween[60][62], enemyAttacks))
        {
            moves[count++] = Core.Move.Castle(color);
        }

        if (IsCastleLegal(CastlingRights.BlackQueen, king, MovePatterns.SquaresBetween[60][58], enemyAttacks))
        {
            moves[count++] = Core.Move.QueenSideCastle(color);
        }


        return count;
    }

    private bool IsCastleLegal(CastlingRights requiredCastle, ulong king, ulong between, ulong enemyAttacks)
    {
        var occupiedBetween = between & Occupied;
        var attacked = (king | between) & enemyAttacks;

        return CastlingRights.HasFlag(requiredCastle)
            && occupiedBetween == 0 && attacked == 0;
    }

    private ulong CreateAttackMask(Color color)
    {
        var (king, enemyRooks, enemyQueen, enemyBishops, enemyKnights, enemyPawns) = (color == Color.White)
            ? (WhiteKing, BlackRooks, BlackQueens, BlackBishops, BlackKnights, Bitboards.FlipAlongVertical(BlackPawns))
            : (BlackKing, WhiteRooks, WhiteQueens, WhiteBishops, WhiteKnights, WhitePawns);

        var enemyAttacks = MovePatterns.GenerateRookAttacks(enemyRooks | enemyQueen, Empty ^ king)
            | MovePatterns.GenerateBishopAttacks(enemyBishops | enemyQueen, Empty ^ king);

        while (enemyKnights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref enemyKnights);
            enemyAttacks |= MovePatterns.Knights[fromIndex];
        }

        var enemyPawnAttacks = MovePatterns.CalculateAllPawnAttacks(enemyPawns);
        if (color == Color.White)
        {
            enemyAttacks |= Bitboards.FlipAlongVertical(enemyPawnAttacks);
            enemyAttacks |= MovePatterns.Kings[Squares.ToIndex(BlackKing)];
        }
        else
        {
            enemyAttacks |= enemyPawnAttacks;
            enemyAttacks |= MovePatterns.Kings[Squares.ToIndex(WhiteKing)];
        }

        return enemyAttacks;
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

            var valid = MovePatterns.RookAttacks(fromIndex, Occupied) & ~friendlies & Checkmask & PinnedPiece(fromIndex);
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

            var valid = MovePatterns.BishopAttacks(fromIndex, Occupied) & ~friendlies & Checkmask & PinnedPiece(fromIndex);
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

            var quiets = MovePatterns.Knights[fromIndex] & ~Occupied & Checkmask & PinnedPiece(fromIndex);
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Knights[fromIndex] & targets & Checkmask & PinnedPiece(fromIndex);
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

            var pushes = pushPattern[sq] & Checkmask & PinnedPiece(sq) & Empty;
            while (pushes != 0)
            {
                var push = Bitboards.PopLsb(ref pushes);
                if ((MovePatterns.SquaresBetween[sq][push] & Occupied) == 0)
                {
                    foreach (var promotionPiece in MovePatterns.PromotionPieces[push])
                    {
                        moves[count++] = new Move(sq, push) with { PromotionPiece = promotionPiece };
                    }
                }
            }

            var attacks = attackPattern[sq] & (targets | (1ul << EnPassant)) & Checkmask & PinnedPiece(sq);
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                moves[count++] = (attack != EnPassant)
                    ? new Move(sq, attack, attack, GetOccupant(attack))
                    : DoEnPassant(sq, attack);
            }
        }
        return count;
    }

    private Move DoEnPassant(byte sq, byte attack)
    {
        var captureOffset = CurrentPlayer == Color.White ? MovePatterns.S : MovePatterns.N;
        var epCapture = (byte)(EnPassant + captureOffset);

        return new Move(sq, attack, epCapture, GetOccupant(epCapture));
    }

    public Piece GetOccupant(byte attack)
    {
        var square = Squares.FromIndex(attack);
        foreach (var type in Enum.GetValues<Piece>().Except([Piece.None]))
        {
            if ((this[type] & square) != 0) return type;
        }
        return Piece.None;
    }

    public Position Update(Piece piece, ulong bitboard) => piece switch
    {
        Piece.WhitePawn => this with { WhitePawns = bitboard },
        Piece.WhiteKnight => this with { WhiteKnights = bitboard },
        Piece.WhiteBishop => this with { WhiteBishops = bitboard },
        Piece.WhiteRook => this with { WhiteRooks = bitboard },
        Piece.WhiteQueen => this with { WhiteQueens = bitboard },
        Piece.WhiteKing => this with { WhiteKing = bitboard },
        Piece.BlackPawn => this with { BlackPawns = bitboard },
        Piece.BlackKnight => this with { BlackKnights = bitboard },
        Piece.BlackBishop => this with { BlackBishops = bitboard },
        Piece.BlackRook => this with { BlackRooks = bitboard },
        Piece.BlackQueen => this with { BlackQueens = bitboard },
        Piece.BlackKing => this with { BlackKing = bitboard },
        _ => this,
    };
}

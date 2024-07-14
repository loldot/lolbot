using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Lolbot.Core;

public readonly record struct Position
{
    public readonly Color CurrentPlayer { get; init; } = Color.White;
    public readonly ulong WhitePawns { get; init; } = 0x000000000000ff00;
    public readonly ulong WhiteRooks { get; init; } = Bitboards.Create("A1", "H1");
    public readonly ulong WhiteBishops { get; init; } = Bitboards.Create("C1", "F1");
    public readonly ulong WhiteKnights { get; init; } = Bitboards.Create("B1", "G1");
    public readonly ulong WhiteQueens { get; init; } = Bitboards.Create("D1");
    public readonly ulong WhiteKing { get; init; } = Bitboards.Create("E1");

    public readonly ulong BlackPawns { get; init; } = 0x00ff000000000000;
    public readonly ulong BlackRooks { get; init; } = Bitboards.Create("A8", "H8");
    public readonly ulong BlackBishops { get; init; } = Bitboards.Create("C8", "F8");
    public readonly ulong BlackKnights { get; init; } = Bitboards.Create("B8", "G8");
    public readonly ulong BlackQueens { get; init; } = Bitboards.Create("D8");
    public readonly ulong BlackKing { get; init; } = Bitboards.Create("E8");
    public readonly byte EnPassant { get; init; } = 0;
    public readonly Castle CastlingRights { get; init; } = Core.Castle.All;

    public readonly ulong Checkmask { get; init; } = ulong.MaxValue;
    public readonly ulong[] Pinmasks { get; init; } = [0, 0, 0, 0];

    public readonly byte CheckerCount { get; init; } = 0;

    public Position()
    {
        White = Bitboards.Create(WhitePawns, WhiteRooks, WhiteKnights, WhiteBishops, WhiteQueens, WhiteKing);
        Black = Bitboards.Create(BlackPawns, BlackRooks, BlackKnights, BlackBishops, BlackQueens, BlackKing);
        Occupied = Bitboards.Create(White, Black);
        Empty = ~Occupied;
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


    public readonly ulong White { get; init; }
    public readonly ulong Black { get; init; }

    public readonly ulong Occupied { get; init; }
    public readonly ulong Empty { get; init; }

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
        var (colorOffset, next) = CurrentPlayer == Color.White ? (0x10, Color.Black) : (0x20, Color.White);

        Piece piece = Piece.None;
        ulong bitboard = 0;

        for (var i = 0; i <= 6; i++)
        {
            piece = (Piece)(colorOffset + i);
            bitboard = this[piece];
            if ((bitboard & m.FromSquare) != 0) break;
        }

        var update = m.FromSquare | m.ToSquare;
        var capture = m.CapturePiece != Piece.None ? m.CaptureSquare : 0;

        var position = Update(piece, bitboard ^ update)
            .Update(m.CapturePiece, this[m.CapturePiece] ^ capture) with
        {
            CastlingRights = ApplyCastlingRights(m),
            EnPassant = SetEnPassant(m),

            Occupied = Occupied ^ update ^ capture,
            Empty = ~(Occupied ^ update ^ capture),
            White = (CurrentPlayer == Color.White) ? White ^ update : White ^ capture,
            Black = (CurrentPlayer == Color.Black) ? Black ^ update : Black ^ capture,

            CurrentPlayer = next
        };

        if (m.PromotionPiece != Piece.None)
        {
            position = position.Update(m.PromotionPiece, this[m.PromotionPiece] | m.ToSquare);
            position = position.Update(piece, position[piece] & ~m.ToSquare);
        }
        if (m.CastleIndex != 0)
        {
            var w_castle = Castle(Bitboards.Masks.Rank_1 ^ Bitboards.Create("a1", "h1"), m);
            var b_castle = Castle(Bitboards.Masks.Rank_8 ^ Bitboards.Create("a8", "h8"), m);
            position = position with
            {
                WhiteRooks = position.WhiteRooks ^ w_castle,
                BlackRooks = position.BlackRooks ^ b_castle,
                White = position.White ^ (w_castle | capture),
                Black = position.Black ^ (b_castle | capture),
                Occupied = position.Occupied ^ (w_castle | b_castle)
            };
        }

        var (checkmask, checkers) = position.CreateCheckMask(next);
        var (isPinned, pinmasks) = position.CreatePinmasks(next);

        return position with
        {
            Checkmask = checkmask,
            Pinmasks = pinmasks,
            CheckerCount = checkers,
        };
    }

    private Castle ApplyCastlingRights(Move m)
    {
        var removedCastling = m.FromIndex switch
        {
            Squares.A1 => Core.Castle.WhiteQueen,
            Squares.E1 => Core.Castle.WhiteKing | Core.Castle.WhiteQueen,
            Squares.H1 => Core.Castle.WhiteKing,
            Squares.A8 => Core.Castle.BlackQueen,
            Squares.E8 => Core.Castle.BlackKing | Core.Castle.BlackQueen,
            Squares.H8 => Core.Castle.BlackKing,
            _ => Core.Castle.None
        };
        if (m.CapturePiece != Piece.None)
            removedCastling |= m.CaptureIndex switch
            {
                Squares.A1 => Core.Castle.WhiteQueen,
                Squares.H1 => Core.Castle.WhiteKing,
                Squares.A8 => Core.Castle.BlackQueen,
                Squares.H8 => Core.Castle.BlackKing,
                _ => Core.Castle.None
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

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 6)
            count += AddKingMoves(color, moves.Span);

        if (CheckerCount > 1) return moves[..count].Span;

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 1)
            count += AddPawnMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 2)
            count += AddKnightMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 3)
            count += AddBishopMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 4)
            count += AddRookMoves(color, moves[count..].Span);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 5)
            count += AddQueenMoves(color, moves[count..].Span);


        return moves[..count].Span;
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

        if (IsCastleLegal(Core.Castle.WhiteKing | Core.Castle.BlackKing, Core.Move.Castle(color), enemyAttacks))
        {
            moves[count++] = Core.Move.Castle(color);
        }

        if (IsCastleLegal(Core.Castle.WhiteQueen | Core.Castle.BlackQueen, Core.Move.QueenSideCastle(color), enemyAttacks))
        {
            moves[count++] = Core.Move.QueenSideCastle(color);
        }


        return count;
    }

    private int AddQueenMoves(Color color, Span<Move> moves)
    {
        var queens = (color == Color.White) ? WhiteQueens : BlackQueens;
        var count = 0;
        count += AddSlider(color, moves, queens, MovePatterns.RookAttacks);
        count += AddSlider(color, moves[count..], queens, MovePatterns.BishopAttacks);
        return count;
    }

    private int AddRookMoves(Color color, Span<Move> moves)
    {
        var rooks = (color == Color.White) ? WhiteRooks : BlackRooks;
        return AddSlider(color, moves, rooks, MovePatterns.RookAttacks);
    }

    private int AddBishopMoves(Color color, Span<Move> moves)
    {
        var bishops = color == Color.White ? WhiteBishops : BlackBishops;
        return AddSlider(color, moves, bishops, MovePatterns.BishopAttacks);
    }

    private int AddSlider(Color color, Span<Move> moves, ulong bitboard, Func<byte, ulong, ulong> attackFunc)
    {
        var count = 0;
        var (targets, friendlies) = (color == Color.White)
            ? (Black, White)
            : (White, Black);

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var valid = attackFunc(fromIndex, Occupied) & ~friendlies & Checkmask & PinnedPiece(fromIndex);
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
                    // For all ranks except 2 and 7 promotion pieces = [Piece.None]
                    foreach (var promotionPiece in MovePatterns.PromotionPieces[push])
                    {
                        moves[count++] = new Move(sq, push) with { PromotionPiece = promotionPiece };
                    }
                }
            }

            var attacks = attackPattern[sq] & targets & Checkmask & PinnedPiece(sq);

            if (((1ul << EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(moves, count, ref sq, EnPassant);

            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                foreach (var promotionPiece in MovePatterns.PromotionPieces[attack])
                {
                    moves[count++] = new Move(sq, attack, attack, GetOccupant(attack))
                        with
                    { PromotionPiece = promotionPiece };
                }
            }
        }
        return count;
    }

    internal (bool, ulong[]) CreatePinmasks(Color color)
    {
        bool isPinned = false;
        byte king = Squares.ToIndex(color == Color.White ? WhiteKing : BlackKing);
        var (enemyHV, enemyAD, friendly, enemy) = color == Color.White
            ? (BlackRooks | BlackQueens, BlackBishops | BlackQueens, White, Black)
            : (WhiteRooks | WhiteQueens, WhiteBishops | WhiteQueens, Black, White);

        ulong rookAttack = MovePatterns.RookAttacks(king, enemy);
        ulong bishopAttack = MovePatterns.BishopAttacks(king, enemy);

        ulong[] pinmasks = new ulong[4];
        ulong[] tempPins = [
            rookAttack & Bitboards.Masks.GetRank(king),
            rookAttack & Bitboards.Masks.GetFile(king),
            bishopAttack & Bitboards.Masks.GetAntiadiagonal(king),
            bishopAttack & Bitboards.Masks.GetDiagonal(king)
        ];
        ulong[] enemies = [enemyHV, enemyHV, enemyAD, enemyAD];

        for (int i = 0; i < pinmasks.Length; i++)
        {
            var temp = tempPins[i] & enemies[i];
            while (temp != 0)
            {
                int sq = Bitboards.PopLsb(ref temp);
                var test = tempPins[i] & MovePatterns.SquaresBetween[king][sq];

                var friendliesOnPin = Bitboards.CountOccupied(test & friendly);

                pinmasks[i] |= friendliesOnPin == 1 ? test : 0;
                isPinned |= friendliesOnPin == 1;
            }
        }

        return (isPinned, pinmasks);
    }

    internal (ulong, byte) CreateCheckMask(Color color)
    {
        ulong checkmask = 0;
        byte countCheckers = 0;

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
                var attacks = MovePatterns.GetAttack(pieceType, checker, Empty);

                var pieceCheckmask = attacks & squares;

                if ((pieceCheckmask & (1ul << king)) != 0)
                {
                    checkmask |= pieceCheckmask | checker;
                    countCheckers++;
                }
            }
        }

        return (countCheckers > 0 ? checkmask : ulong.MaxValue, countCheckers);
    }


    private ulong PinnedPiece(byte fromIndex)
    {
        for (var i = 0; i < 4; i++)
        {
            if ((Pinmasks[i] & Squares.FromIndex(fromIndex)) != 0) return Pinmasks[i];
        }

        return ulong.MaxValue;
    }



    private bool IsCastleLegal(Castle requiredCastle, Move castle, ulong enemyAttacks)
    {
        var clearingRequired = MovePatterns.SquaresBetween[castle.FromIndex][castle.CaptureIndex]
            & ~castle.CaptureSquare;

        var occupiedBetween = clearingRequired & Occupied;
        var attacked = (
            castle.FromSquare |
            MovePatterns.SquaresBetween[castle.FromIndex][castle.ToIndex]
        ) & enemyAttacks;

        return ((CastlingRights & requiredCastle) != 0)
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


    private int DoEnPassant(Span<Move> moves, int count, ref byte sq, byte attack)
    {
        var (king, opponentRook, opponentQueen) = CurrentPlayer == Color.White
            ? (WhiteKing, BlackRooks, BlackQueens)
            : (BlackKing, WhiteRooks, WhiteQueens);

        var captureOffset = CurrentPlayer == Color.White ? MovePatterns.S : MovePatterns.N;
        var epCapture = (byte)(EnPassant + captureOffset);
        var ep = new Move(sq, attack, epCapture, GetOccupant(epCapture));

        var occupiedAfter = Occupied ^ (Squares.FromIndex(epCapture) | Squares.FromIndex(sq));
        var kingRook = MovePatterns.RookAttacks(Squares.ToIndex(king), occupiedAfter);

        var epWouldCheck = 0 != (kingRook &
            (opponentQueen | opponentRook));

        var epSavesCheck = Checkmask < ulong.MaxValue;
        // Double pushed pawn is checking, capture en passant.
        if (!epWouldCheck || epSavesCheck)
        {
            moves[count++] = ep;
        }
        return count;
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

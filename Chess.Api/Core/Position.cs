using System.Runtime.Intrinsics;
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
    public readonly Vector256<ulong> Pinmasks { get; init; } = Vector256<ulong>.Zero;
    public readonly bool IsPinned { get; init; } = false;

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

    public readonly ulong this[byte index]
    {
        get => index switch 
        {
            1 => White,
            2 => Black,
            0x11 => WhitePawns,
            0x12 => WhiteKnights,
            0x13 => WhiteBishops,
            0x14 => WhiteRooks,
            0x15 => WhiteQueens,
            0x16 => WhiteKing,
            0x21 => BlackPawns,
            0x22 => BlackKnights,
            0x23 => BlackBishops,
            0x24 => BlackRooks,
            0x25 => BlackQueens,
            0x26 => BlackKing,
            0xfe => Black,
            0xfd => White,
            _ => Empty
        };
    }

    public readonly ulong this[Piece piece] => this[(byte)piece];
    public readonly ulong this[Color color]
    {
        get => this [(byte)color];
    }
    public readonly ulong this[Color color, PieceType pieceType]
    {
        get => this [(Piece)((byte)color << 4 | (byte)pieceType)];
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
        var next = CurrentPlayer == Color.White ? Color.Black : Color.White;
        ulong bitboard = this[m.FromPiece];

        var update = m.FromSquare | m.ToSquare;
        var capture = m.CapturePiece != Piece.None ? m.CaptureSquare : 0;

        var position = Update(m.FromPiece, bitboard ^ update)
            .Update(m.CapturePiece, this[m.CapturePiece] ^ capture) with
        {
            CastlingRights = ApplyCastlingRights(ref m),
            EnPassant = SetEnPassant(ref m),

            Occupied = Occupied ^ update ^ capture,
            Empty = ~(Occupied ^ update ^ capture),
            White = (CurrentPlayer == Color.White) ? White ^ update : White ^ capture,
            Black = (CurrentPlayer == Color.Black) ? Black ^ update : Black ^ capture,

            CurrentPlayer = next
        };

        if (m.PromotionPiece != Piece.None)
        {
            position = position.Update(m.PromotionPiece, this[m.PromotionPiece] | m.ToSquare);
            position = position.Update(m.FromPiece, position[m.FromPiece] & ~m.ToSquare);
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
            IsPinned = isPinned,
            CheckerCount = checkers,
        };
    }

    private Castle ApplyCastlingRights(ref Move m)
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

    public readonly byte SetEnPassant(ref Move m)
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
        => mask & m.CastleSquare;

    public Span<Move> GenerateLegalMoves(Piece pieceType)
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator.Legal(in this, ref moves, pieceType);
        return moves[..count].ToArray();
    }

    public Span<Move> GenerateLegalMoves()
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator.Legal(in this, ref moves);
        return moves[..count].ToArray();
    }

    public Span<Move> GenerateLegalMoves(char pieceType)
    {
        Piece piece = Utils.FromName(pieceType);

        return GenerateLegalMoves(piece);
    }

    internal (bool, Vector256<ulong>) CreatePinmasks(Color color)
    {
        bool isPinned = false;
        byte king = Squares.ToIndex(color == Color.White ? WhiteKing : BlackKing);
        var (enemyHV, enemyAD, friendly, enemy) = color == Color.White
            ? (BlackRooks | BlackQueens, BlackBishops | BlackQueens, White, Black)
            : (WhiteRooks | WhiteQueens, WhiteBishops | WhiteQueens, Black, White);

        ulong rookAttack = MovePatterns.RookAttacks(king, ref enemy);
        ulong bishopAttack = MovePatterns.BishopAttacks(king, ref enemy);

        Span<ulong> pinmasks = stackalloc ulong[4];
        var attacks = Vector256.Create(
            rookAttack,
            rookAttack,
            bishopAttack,
            bishopAttack
        );
        var tempPins = Vector256.Create(
            Bitboards.Masks.GetRank(king),
            Bitboards.Masks.GetFile(king),
            Bitboards.Masks.GetAntiadiagonal(king),
            Bitboards.Masks.GetDiagonal(king)
        );
        tempPins &= attacks;

        var enemies = Vector256.Create(enemyHV, enemyHV, enemyAD, enemyAD);

        for (int i = 0; i < 4; i++)
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

        return (isPinned, Vector256.Create(pinmasks[0], pinmasks[1], pinmasks[2], pinmasks[3]));
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
                var checker = Squares.FromIndex(in piece);

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


    public readonly ulong PinnedPiece(ref readonly byte fromIndex)
    {
        if (!IsPinned) return ulong.MaxValue;
        // var sq = Squares.FromIndex(in fromIndex);
        // var mask = Pinmasks & 

        // var v = Vector128.ConditionalSelect(mask, Pinmasks.GetLower(), Pinmasks.GetUpper());
        // var pin = Vector64.ConditionalSelect(Vector64.CreateScalar(sq), v.GetLower(), v.GetUpper()).ToScalar();

        // return pin == 0 ? ulong.MaxValue : pin;

        var sq = Squares.FromIndex(in fromIndex);
        for (var i = 0; i < 4; i++)
        {
            if ((Pinmasks[i] & sq) != 0) return Pinmasks[i];
        }

        return ulong.MaxValue;
    }

    public ulong CreateAttackMask(Color color)
    {
        var (king, enemyRooks, enemyBishops, enemyKnights, enemyPawns) = (color == Color.White)
            ? (WhiteKing, BlackRooks | BlackQueens, BlackBishops | BlackQueens, BlackKnights, Bitboards.FlipAlongVertical(BlackPawns))
            : (BlackKing, WhiteRooks | WhiteQueens, WhiteBishops | WhiteQueens, WhiteKnights, WhitePawns);

        var enemyAttacks = 0ul;
        var occupiedExceptKing = Occupied ^ king;
        while (enemyRooks != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref enemyRooks);
            enemyAttacks |= MovePatterns.RookAttacks(fromIndex, ref occupiedExceptKing);
        }

        while (enemyBishops != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref enemyBishops);
            enemyAttacks |= MovePatterns.BishopAttacks(fromIndex, ref occupiedExceptKing);
        }

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

    public Piece GetOccupant(ref byte attack)
    {
        Piece piece = Piece.None;
        byte colorOffset;

        var square = Squares.FromIndex(in attack);

        if ((White & square) != 0) colorOffset = 0x10;
        else if ((Black & square) != 0) colorOffset = 0x20;
        else return piece;

        for (var i = 0; i <= 6; i++)
        {
            piece = (Piece)(colorOffset + i);
            if ((this[piece] & square) != 0) break;
        }

        return piece;
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

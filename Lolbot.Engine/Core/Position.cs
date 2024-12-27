using System.Runtime.Intrinsics;
using System.Text;
using static Lolbot.Core.Utils;

namespace Lolbot.Core;

public readonly struct Position
{
    public readonly Colors CurrentPlayer { get; init; } = Colors.White;

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
    public readonly CastlingRights CastlingRights { get; init; } = CastlingRights.All;

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
        Black = 0,
        White = 0,
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

    public readonly ulong this[Piece piece] => piece switch
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

    public readonly ulong this[Colors color]
    {
        get => color == Colors.White ? White : Black;
    }
    public readonly ulong this[Colors color, PieceType pieceType]
    {
        get => this[(Piece)((byte)color << 4 | (byte)pieceType)];
    }

    public readonly ulong this[PieceType pieceType]
    {
        get => this[(Piece)((byte)CurrentPlayer << 4 | (byte)pieceType)];
    }

    public readonly ulong White { get; init; }
    public readonly ulong Black { get; init; }

    public readonly ulong Occupied { get; init; }
    public readonly ulong Empty { get; init; }
    public readonly ulong Hash { get; init; } = Hashes.Default;
    public bool IsCheck => Checkmask != ulong.MaxValue;

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
        var next = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;
        ulong bitboard = this[m.FromPiece];

        var moveMask = m.FromSquare | m.ToSquare;
        var captureMask = m.CapturePiece != Piece.None ? m.CaptureSquare : 0;

        var position = Update(m.FromPiece, bitboard ^ moveMask)
            .Update(m.CapturePiece, this[m.CapturePiece] ^ captureMask) with
        {
            CastlingRights = ApplyCastlingRights(ref m),
            EnPassant = SetEnPassant(ref m),

            Occupied = Occupied ^ moveMask ^ captureMask,
            Empty = ~(Occupied ^ moveMask ^ captureMask),
            White = (CurrentPlayer == Colors.White) ? White ^ moveMask : White ^ captureMask,
            Black = (CurrentPlayer == Colors.Black) ? Black ^ moveMask : Black ^ captureMask,

            CurrentPlayer = next
        };

        if (m.PromotionPiece != Piece.None)
        {
            position = position.Update(m.PromotionPiece, this[m.PromotionPiece] | m.ToSquare);
            position = position.Update(m.FromPiece, position[m.FromPiece] & ~m.ToSquare);
        }
        if (m.CastleIndex != 0)
        {
            var w_castle = Castle(0x7e, m);
            var b_castle = Castle(0x7e00000000000000, m);
            position = position with
            {
                WhiteRooks = position.WhiteRooks ^ w_castle,
                BlackRooks = position.BlackRooks ^ b_castle,
                White = position.White ^ (w_castle | captureMask),
                Black = position.Black ^ (b_castle | captureMask),
                Occupied = position.Occupied ^ (w_castle | b_castle)
            };
        }

        var (checkmask, checkers) = position.CreateCheckMask(next);
        var (isPinned, pinmasks) = position.CreatePinmasks(next);

        var hash = position.Hash;
        hash ^= Hashes.GetValue(m.FromPiece, m.FromIndex);
        hash ^= Hashes.GetValue(m.FromPiece, m.ToIndex);
        hash ^= Hashes.GetValue(m.CapturePiece, m.CaptureIndex);
        hash ^= Hashes.GetValue(m.PromotionPiece, m.ToIndex);
        hash ^= Hashes.GetValue(CastlingRights);
        hash ^= Hashes.GetValue(position.CastlingRights);
        hash ^= Hashes.GetValue(EnPassant);
        hash ^= Hashes.GetValue(position.EnPassant);
        hash ^= Hashes.GetValue(Colors.White);

        return position with
        {
            Checkmask = checkmask,
            Pinmasks = pinmasks,
            IsPinned = isPinned,
            CheckerCount = checkers,
            Hash = hash
        };
    }

    private CastlingRights ApplyCastlingRights(ref Move m)
    {
        var removedCastling = m.FromIndex switch
        {
            Squares.A1 => CastlingRights.WhiteQueen,
            Squares.E1 => CastlingRights.WhiteKing | CastlingRights.WhiteQueen,
            Squares.H1 => CastlingRights.WhiteKing,
            Squares.A8 => CastlingRights.BlackQueen,
            Squares.E8 => CastlingRights.BlackKing | CastlingRights.BlackQueen,
            Squares.H8 => CastlingRights.BlackKing,
            _ => CastlingRights.None
        };
        if (m.CapturePiece != Piece.None)
            removedCastling |= m.CaptureIndex switch
            {
                Squares.A1 => CastlingRights.WhiteQueen,
                Squares.H1 => CastlingRights.WhiteKing,
                Squares.A8 => CastlingRights.BlackQueen,
                Squares.H8 => CastlingRights.BlackKing,
                _ => CastlingRights.None
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

        var nextTo = (to << 1 | to >> 1) & this[Enemy(CurrentPlayer)] 
            & (
                (WhitePawns & Bitboards.Masks.Rank_5) | (Bitboards.Masks.Rank_4 & BlackPawns)
            );

        return (nextTo != 0) ? Squares.ToIndex(enPassant) : (byte)0;
    }

    private static ulong Castle(ulong mask, Move m)
        => mask & m.CastleSquare;

    public Span<Move> GenerateLegalMoves(Piece pieceType)
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator.Legal(in this, ref moves);


        return moves[..count].ToArray()
            .Where(x => x.FromPiece == pieceType)
            .ToArray();
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

    internal (bool, Vector256<ulong>) CreatePinmasks(Colors color)
    {
        bool isPinned = false;
        byte king = Squares.ToIndex(color == Colors.White ? WhiteKing : BlackKing);
        var (enemyHV, enemyAD, friendly, enemy) = color == Colors.White
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

    internal (ulong, byte) CreateCheckMask(Colors color)
    {
        ulong checkmask = 0;
        byte countCheckers = 0;

        Colors opponentColor = Enemy(color);
        byte king = Squares.ToIndex(color == Colors.White ? WhiteKing : BlackKing);

        // king can never check
        for (int i = 1; i < 6; i++)
        {
            var pieceType = (Piece)((int)opponentColor << 4 | i);
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


    public readonly ulong PinnedPiece(ref readonly byte fromIndex)
    {
        if (!IsPinned) return ulong.MaxValue;

        var sq = Squares.FromIndex(fromIndex);
        for (var i = 0; i < 4; i++)
        {
            if ((Pinmasks[i] & sq) != 0) return Pinmasks[i];
        }

        return ulong.MaxValue;
    }

    public ulong CreateAttackMask(Colors color)
    {
        var (king, enemyRooks, enemyBishops, enemyKnights, enemyPawns) = (color == Colors.White)
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

        var enemyPawnAttacks = MovePatterns.CalculateAllPawnAttacksWhite(enemyPawns);
        if (color == Colors.White)
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

    public Piece GetOccupant(ref readonly byte attack)
    {
        Piece piece = Piece.None;
        byte colorOffset;

        var square = Squares.FromIndex(attack);

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

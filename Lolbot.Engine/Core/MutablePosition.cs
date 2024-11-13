using System.Runtime.Intrinsics;
using System.Text;

namespace Lolbot.Core;

public sealed class MutablePosition
{

    public Colors CurrentPlayer { get; private set; } = Colors.White;
    public const int MaxDepth = 256;
    public const int Pawns = 1;
    public const int Knights = 2;
    public const int Bishops = 3;
    public const int Rooks = 4;
    public const int Queens = 5;
    public const int Kings = 6;
    public const int WhiteIndex = 7;
    public const int BlackIndex = 8;
    private readonly DiffData[] Diffs = new DiffData[MaxDepth];
    private int plyfromRoot = 0;

    private readonly ulong[] bb = [
        /*Empty*/ 0x00_00_ff_ff_ff_ff_ff_00_00,
        /*Pawns*/ 0x000000000000ff00 | 0x00ff000000000000,
        /*Knights*/ Bitboards.Create("B1", "G1", "B8", "G8"),
        /*Bishops */ Bitboards.Create("C1", "F1", "C8", "F8"),
        /*Rooks*/ Bitboards.Create("A1", "H1", "A8", "H8"),
        /*Queens*/ Bitboards.Create("D1", "D8"),
        /*Kings*/ Bitboards.Create("E1", "E8"),
        /*White*/ 0x000000000000ffff,
        /*Black*/ 0xffff000000000000
    ];

    public ulong WhitePawns => bb[Pawns] & White;
    public ulong WhiteRooks => bb[Rooks] & White;
    public ulong WhiteBishops => bb[Bishops] & White;
    public ulong WhiteKnights => bb[Knights] & White;
    public ulong WhiteQueens => bb[Queens] & White;
    public ulong WhiteKing => bb[Kings] & White;


    public ulong BlackPawns => bb[Pawns] & Black;
    public ulong BlackRooks => bb[Rooks] & Black;
    public ulong BlackBishops => bb[Bishops] & Black;
    public ulong BlackKnights => bb[Knights] & Black;
    public ulong BlackQueens => bb[Queens] & Black;
    public ulong BlackKing => bb[Kings] & Black;

    public byte EnPassant { get; private set; } = 0;
    public CastlingRights CastlingRights { get; private set; } = CastlingRights.All;

    public ulong Checkmask { get; private set; } = ulong.MaxValue;
    public Vector256<ulong> Pinmasks { get; private set; } = Vector256<ulong>.Zero;
    public bool IsPinned { get; private set; } = false;

    public byte CheckerCount { get; private set; } = 0;

    public ulong this[byte index]
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

    public ulong this[Piece piece] => this[(byte)piece];
    public ulong this[Colors color]
    {
        get => this[(byte)color];
    }
    public ulong this[Colors color, PieceType pieceType]
    {
        get => bb[(byte)pieceType] & bb[6 + (int)color];
    }

    public ulong this[PieceType pieceType]
    {
        get => bb[(byte)pieceType];
    }

    public ulong White => bb[WhiteIndex];
    public ulong Black => bb[BlackIndex];

    public ulong Occupied => White | Black;
    public ulong Empty => ~(White | Black);
    public ulong Hash = Hashes.Default;
    public bool IsCheck => Checkmask != ulong.MaxValue;

    public void Move(ref readonly Move m)
    {
        var (us, oponent, next) = CurrentPlayer == Colors.White
            ? (WhiteIndex, BlackIndex, Colors.Black)
            : (BlackIndex, WhiteIndex, Colors.White);

        Diffs[plyfromRoot] = new DiffData(
           Hash, CastlingRights, EnPassant
        );


        var pieceIndex = (int)m.FromPieceType;
        bb[pieceIndex] ^= m.FromSquare | m.ToSquare;
        bb[us] ^= m.FromSquare | m.ToSquare;

        var captureIndex = (int)m.CapturePieceType;

        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            bb[captureIndex] ^= rookmask;
            bb[us] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            bb[captureIndex] ^= m.CaptureSquare;
            bb[oponent] ^= m.CaptureSquare;
        }

        if (m.PromotionPieceType != PieceType.None)
        {
            var promoteIndex = (int)m.PromotionPieceType;
            bb[promoteIndex] ^= m.ToSquare;
            bb[Pawns] ^= m.ToSquare;
        }

        var newCastling = ApplyCastlingRights(in m);
        var newEnPassant = SetEnPassant(in m);

        Hashes.Update(ref Hash, in m,
            CastlingRights, newCastling,
            EnPassant, newEnPassant);

        CastlingRights = newCastling;
        EnPassant = newEnPassant;

        (Checkmask, CheckerCount) = CreateCheckMask(next);
        (IsPinned, Pinmasks) = CreatePinmasks(next);
        CurrentPlayer = next;

        plyfromRoot++;
    }


    public void Undo(ref readonly Move m)
    {
        plyfromRoot--;
        var (us, oponent, next) = CurrentPlayer == Colors.White
                    ? (BlackIndex, WhiteIndex, Colors.Black)
                    : (WhiteIndex, BlackIndex, Colors.White);

        var pieceIndex = (int)m.FromPieceType;
        bb[pieceIndex] ^= m.FromSquare | m.ToSquare;
        bb[us] ^= m.FromSquare | m.ToSquare;

        var captureIndex = (int)m.CapturePieceType;
        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            bb[captureIndex] ^= rookmask;
            bb[us] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            bb[captureIndex] ^= m.CaptureSquare;
            bb[oponent] ^= m.CaptureSquare;
        }

        if (m.PromotionPieceType != PieceType.None)
        {
            var promoteIndex = (int)m.PromotionPieceType;
            bb[promoteIndex] ^= m.ToSquare;
            bb[Pawns] ^= m.ToSquare;
        }

        CastlingRights = Diffs[plyfromRoot].Castling;
        EnPassant = Diffs[plyfromRoot].EnPassant;
        Hash = Diffs[plyfromRoot].Hash;

        (Checkmask, CheckerCount) = CreateCheckMask(next);
        (IsPinned, Pinmasks) = CreatePinmasks(next);
        CurrentPlayer = next;
    }

    public void SkipTurn()
    {
        CurrentPlayer = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;
    }

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



    private CastlingRights ApplyCastlingRights(ref readonly Move m)
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

    public byte SetEnPassant(ref readonly Move m)
    {
        var from = 1ul << m.FromIndex;
        var to = 1ul << m.ToIndex;

        if (m.FromPieceType != PieceType.Pawn) return 0;

        // Pawn moves leading to en passant has the en passant square
        // 1 square in front of the start and 1 square behind the target
        var (rank2, rank4, ep, op) = m.Color == (byte)Colors.White
            ? (Bitboards.Masks.Rank_2, Bitboards.Masks.Rank_4, (from << 8) & (to >> 8), Black)
            : (Bitboards.Masks.Rank_7, Bitboards.Masks.Rank_5, (from >> 8) & (to << 8), White);

        if ((from & rank2) == 0 || (to & rank4) == 0) return 0;

        var adjacent = (to << 1 | to >> 1) & bb[Pawns] & op;
        return adjacent == 0 ? (byte)0 : Squares.ToIndex(ep);
    }

    public static MutablePosition FromFen(string fen)
    {
        var pos = Position.FromFen(fen);
        var pos2 = new MutablePosition();

        pos2.bb[Pawns] = pos.WhitePawns | pos.BlackPawns;
        pos2.bb[Knights] = pos.WhiteKnights | pos.BlackKnights;
        pos2.bb[Bishops] = pos.WhiteBishops | pos.BlackBishops;
        pos2.bb[Rooks] = pos.WhiteRooks | pos.BlackRooks;
        pos2.bb[Queens] = pos.WhiteQueens | pos.BlackQueens;
        pos2.bb[Kings] = pos.WhiteKing | pos.BlackKing;
        pos2.bb[WhiteIndex] = pos.White;
        pos2.bb[BlackIndex] = pos.Black;
        pos2.CastlingRights = pos.CastlingRights;
        pos2.EnPassant = pos.EnPassant;
        pos2.CurrentPlayer = pos.CurrentPlayer;
        pos2.CheckerCount = pos.CheckerCount;
        pos2.Checkmask = pos.Checkmask;
        pos2.Pinmasks = pos.Pinmasks;
        pos2.IsPinned = pos.IsPinned;
        return pos2;

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

        int opponentColor = color == Colors.White ? 0x20 : 0x10;
        byte king = Squares.ToIndex(color == Colors.White ? WhiteKing : BlackKing);

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


    public ulong PinnedPiece(ref readonly byte fromIndex)
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

    public Span<Move> GenerateLegalMoves(Piece pieceType)
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator2.Legal(this, ref moves);


        return moves[..count].ToArray()
            .Where(x => x.FromPiece == pieceType)
            .ToArray();
    }

    public Span<Move> GenerateLegalMoves()
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator2.Legal(this, ref moves);
        return moves[..count].ToArray();
    }

    public Span<Move> GenerateLegalMoves(char pieceType)
    {
        Piece piece = Utils.FromName(pieceType);

        return GenerateLegalMoves(piece);
    }

    private readonly struct DiffData(ulong hash, CastlingRights castle, byte ep)
    {
        public readonly ulong Hash = hash;
        public readonly CastlingRights Castling = castle;
        public readonly byte EnPassant = ep;
    }
}

using System.Runtime.Intrinsics;
using System.Text;

namespace Lolbot.Core;

public sealed class MutablePosition
{
    public Colors CurrentPlayer { get; private set; } = Colors.White;
    public const int MaxDepth = 1024;

    private readonly DiffData[] Diffs = new DiffData[MaxDepth];
    private int plyfromRoot = 0;

    public ulong WhitePawns = Bitboards.Masks.Rank_2;
    public ulong WhiteKnights = Bitboards.Create("B1", "G1");
    public ulong WhiteBishops = Bitboards.Create("C1", "F1");
    public ulong WhiteRooks = Bitboards.Create("A1", "H1");
    public ulong WhiteQueens = Bitboards.Create("D1");
    public ulong WhiteKing = Bitboards.Create("E1");
    public ulong White = 0x000000000000ffff;

    public ulong BlackPawns = Bitboards.Masks.Rank_7;
    public ulong BlackKnights = Bitboards.Create("B8", "G8");
    public ulong BlackBishops = Bitboards.Create("C8", "F8");
    public ulong BlackRooks = Bitboards.Create("A8", "H8");
    public ulong BlackQueens = Bitboards.Create("D8");
    public ulong BlackKing = Bitboards.Create("E8");
    public ulong Black = 0xffff000000000000;

    public byte EnPassant { get; private set; } = 0;
    public CastlingRights CastlingRights { get; private set; } = CastlingRights.All;

    public ulong Checkmask { get; private set; } = ulong.MaxValue;
    public Vector256<ulong> Pinmasks { get; private set; } = Vector256<ulong>.Zero;
    public bool IsPinned { get; private set; } = false;

    public byte CheckerCount { get; private set; } = 0;

    public ulong AttackMask = Bitboards.Masks.Rank_3;
    public ulong BlackAttacks = Bitboards.Masks.Rank_6;

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
        set
        {
            switch (index)
            {
                case 1: White = value; break;
                case 2: Black = value; break;
                case 0x11: WhitePawns = value; break;
                case 0x12: WhiteKnights = value; break;
                case 0x13: WhiteBishops = value; break;
                case 0x14: WhiteRooks = value; break;
                case 0x15: WhiteQueens = value; break;
                case 0x16: WhiteKing = value; break;
                case 0x21: BlackPawns = value; break;
                case 0x22: BlackKnights = value; break;
                case 0x23: BlackBishops = value; break;
                case 0x24: BlackRooks = value; break;
                case 0x25: BlackQueens = value; break;
                case 0x26: BlackKing = value; break;
                case 0xfe: Black = value; break;
                case 0xfd: White = value; break;
                default: throw new NotImplementedException();
            }
        }
    }

    public ulong this[Piece piece]
    {
        get => this[(byte)piece];
        set => this[(byte)piece] = value;
    }
    public ulong this[Colors color]
    {
        get => this[(byte)color];
        set => this[(byte)color] = value;
    }
    public ulong this[Colors color, PieceType pieceType]
    {
        get => this[(byte)((byte)color << 4 | (byte)pieceType)];
        set => this[(byte)((byte)color << 4 | (byte)pieceType)] = value;
    }

    public ulong this[PieceType pieceType]
    {
        get => this[(byte)((byte)CurrentPlayer << 4 | (byte)pieceType)];
        set => this[(byte)((byte)CurrentPlayer << 4 | (byte)pieceType)] = value;
    }

    public ulong Occupied => White | Black;
    public ulong Empty => ~(White | Black);
    public ulong Hash = Hashes.Default;
    public bool IsCheck => Checkmask != ulong.MaxValue;

    public void Move(ref readonly Move m)
    {
        var oponent = CurrentPlayer == Colors.White
            ? Colors.Black
            : Colors.White;

        Diffs[plyfromRoot] = new DiffData(
           Hash, CastlingRights, EnPassant
        );

        this[m.FromPiece] ^= m.FromSquare | m.ToSquare;
        this[CurrentPlayer] ^= m.FromSquare | m.ToSquare;

        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            this[CurrentPlayer, m.CapturePieceType] ^= rookmask;
            this[CurrentPlayer] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            this[m.CapturePiece] ^= m.CaptureSquare;
            this[oponent] ^= m.CaptureSquare;
        }

        if (m.PromotionPieceType != PieceType.None)
        {
            var promoteIndex = m.PromotionPiece;
            this[promoteIndex] ^= m.ToSquare;
            this[m.FromPiece] ^= m.ToSquare;
        }

        var newCastling = ApplyCastlingRights(in m);
        var newEnPassant = SetEnPassant(in m);

        Hash ^= Hashes.GetValue(m.FromPiece, m.FromIndex);
        Hash ^= Hashes.GetValue(m.FromPiece, m.ToIndex);
        Hash ^= Hashes.GetValue(m.CapturePiece, m.CaptureIndex);
        Hash ^= Hashes.GetValue(m.PromotionPiece, m.ToIndex);
        Hash ^= Hashes.GetValue(CastlingRights);
        Hash ^= Hashes.GetValue(newCastling);
        Hash ^= Hashes.GetValue(EnPassant);
        Hash ^= Hashes.GetValue(newEnPassant);
        Hash ^= Hashes.GetValue(Colors.White);

        CastlingRights = newCastling;
        EnPassant = newEnPassant;

        AttackMask = CreateEnemyAttackMask(oponent);
        (Checkmask, CheckerCount) = CreateCheckMask(oponent);
        (IsPinned, Pinmasks) = CreatePinmasks(oponent);

        CurrentPlayer = oponent;
        plyfromRoot++;
    }


    public void Undo(ref readonly Move m)
    {
        plyfromRoot--;

        var (us, oponent) = CurrentPlayer == Colors.White
                    ? (Colors.Black, Colors.White)
                    : (Colors.White, Colors.Black);


        this[m.FromPiece] ^= m.FromSquare | m.ToSquare;
        this[us] ^= m.FromSquare | m.ToSquare;

        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            this[us, m.CapturePieceType] ^= rookmask;
            this[us] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            this[m.CapturePiece] ^= m.CaptureSquare;
            this[oponent] ^= m.CaptureSquare;
        }

        if (m.PromotionPieceType != PieceType.None)
        {
            this[m.PromotionPiece] ^= m.ToSquare;
            this[m.FromPiece] ^= m.ToSquare;
        }

        CastlingRights = Diffs[plyfromRoot].Castling;
        EnPassant = Diffs[plyfromRoot].EnPassant;
        Hash = Diffs[plyfromRoot].Hash;

        AttackMask = CreateEnemyAttackMask(us);
        (Checkmask, CheckerCount) = CreateCheckMask(us);
        (IsPinned, Pinmasks) = CreatePinmasks(us);

        CurrentPlayer = us;
    }

    public void SkipTurn()
    {
        CurrentPlayer = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;
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
        if (m.FromPieceType != PieceType.Pawn) return 0;

        var from = 1ul << m.FromIndex;
        var to = 1ul << m.ToIndex;

        // Pawn moves leading to en passant has the en passant square
        // 1 square in front of the start and 1 square behind the target
        var (rank2, rank4, ep, op) = m.Color == (byte)Colors.White
            ? (Bitboards.Masks.Rank_2, Bitboards.Masks.Rank_4, (from << 8) & (to >> 8), BlackPawns)
            : (Bitboards.Masks.Rank_7, Bitboards.Masks.Rank_5, (from >> 8) & (to << 8), WhitePawns);

        if ((from & rank2) == 0 || (to & rank4) == 0) return 0;

        var adjacent = (to << 1 | to >> 1) & op;
        return adjacent == 0 ? (byte)0 : Squares.ToIndex(ep);
    }

    public static MutablePosition FromFen(string fen)
    {
        return FromReadOnly(Position.FromFen(fen));
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
        ulong bbKing = color == Colors.White ? WhiteKing : BlackKing;
        if ((AttackMask & bbKing) == 0) return (ulong.MaxValue, 0);

        byte king = Squares.ToIndex(bbKing);

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

    private ulong CreateEnemyAttackMask(Colors color)
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

    public Position AsReadOnly()
    {
        return new Position() with
        {
            WhitePawns = WhitePawns,
            WhiteKnights = WhiteKnights,
            WhiteBishops = WhiteBishops,
            WhiteRooks = WhiteRooks,
            WhiteQueens = WhiteQueens,
            WhiteKing = WhiteKing,
            White = White,

            BlackPawns = BlackPawns,
            BlackKnights = BlackKnights,
            BlackBishops = BlackBishops,
            BlackRooks = BlackRooks,
            BlackQueens = BlackQueens,
            BlackKing = BlackKing,
            Black = Black,

            CastlingRights = CastlingRights,
            EnPassant = EnPassant,

            Empty = Empty,
            Occupied = Occupied,

            CheckerCount = CheckerCount,
            Checkmask = Checkmask,
            Pinmasks = Pinmasks,
            IsPinned = IsPinned,
            CurrentPlayer = CurrentPlayer,
        };
    }

    public static MutablePosition FromReadOnly(Position pos)
    {
        var mutable = new MutablePosition
        {
            WhitePawns = pos.WhitePawns,
            WhiteKnights = pos.WhiteKnights,
            WhiteBishops = pos.WhiteBishops,
            WhiteRooks = pos.WhiteRooks,
            WhiteQueens = pos.WhiteQueens,
            WhiteKing = pos.WhiteKing,
            White = pos.White,

            BlackPawns = pos.BlackPawns,
            BlackKnights = pos.BlackKnights,
            BlackBishops = pos.BlackBishops,
            BlackRooks = pos.BlackRooks,
            BlackQueens = pos.BlackQueens,
            BlackKing = pos.BlackKing,
            Black = pos.Black,

            CastlingRights = pos.CastlingRights,
            EnPassant = pos.EnPassant,
            CurrentPlayer = pos.CurrentPlayer,
            CheckerCount = pos.CheckerCount,
            Checkmask = pos.Checkmask,
            Pinmasks = pos.Pinmasks,
            IsPinned = pos.IsPinned,
            Hash = pos.Hash
        };
        mutable.AttackMask = mutable.CreateEnemyAttackMask(mutable.CurrentPlayer);
        return mutable;
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

    private readonly struct DiffData(ulong hash, CastlingRights castle, byte ep)
    {
        public readonly ulong Hash = hash;
        public readonly CastlingRights Castling = castle;
        public readonly byte EnPassant = ep;
    }
}

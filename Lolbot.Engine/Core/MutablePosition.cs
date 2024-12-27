using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using static Lolbot.Core.Utils;

namespace Lolbot.Core;

public sealed class MutablePosition
{
    public Colors CurrentPlayer { get; private set; } = Colors.White;
    public const int MaxDepth = 1024;

    private readonly DiffData[] Diffs = new DiffData[MaxDepth];
    private int plyfromRoot = 0;

    private const int BlackIndex = 0;
    private const int PawnIndex = 1;
    private const int KnightsIndex = 2;
    private const int BishopsIndex = 3;
    private const int RooksIndex = 4;
    private const int QueensIndex = 5;
    private const int KingsIndex = 6;
    private const int WhiteIndex = 7;


    private DenseBitboards bb = new()
    {
        [BlackIndex] = Bitboards.Masks.Rank_8 | Bitboards.Masks.Rank_7,
        [PawnIndex] = Bitboards.Masks.Rank_2 | Bitboards.Masks.Rank_7,
        [KnightsIndex] = Bitboards.Create("B1", "G1", "B8", "G8"),
        [BishopsIndex] = Bitboards.Create("C1", "F1", "C8", "F8"),
        [RooksIndex] = Bitboards.Create("A1", "H1", "A8", "H8"),
        [QueensIndex] = Bitboards.Create("D1", "D8"),
        [KingsIndex] = Bitboards.Create("E1", "E8"),
        [WhiteIndex] = Bitboards.Masks.Rank_1 | Bitboards.Masks.Rank_2
    };

    public ulong WhitePawns => bb[WhiteIndex] & bb[PawnIndex];
    public ulong WhiteKnights => bb[WhiteIndex] & bb[KnightsIndex];
    public ulong WhiteBishops => bb[WhiteIndex] & bb[BishopsIndex];
    public ulong WhiteRooks => bb[WhiteIndex] & bb[RooksIndex];
    public ulong WhiteQueens => bb[WhiteIndex] & bb[QueensIndex];
    public ulong WhiteKing => bb[WhiteIndex] & bb[KingsIndex];
    public ulong White => bb[WhiteIndex];

    public ulong BlackPawns => bb[BlackIndex] & bb[PawnIndex];
    public ulong BlackKnights => bb[BlackIndex] & bb[KnightsIndex];
    public ulong BlackBishops => bb[BlackIndex] & bb[BishopsIndex];
    public ulong BlackRooks => bb[BlackIndex] & bb[RooksIndex];
    public ulong BlackQueens => bb[BlackIndex] & bb[QueensIndex];
    public ulong BlackKing => bb[BlackIndex] & bb[KingsIndex];
    public ulong Black => bb[BlackIndex];

    public byte EnPassant { get; private set; } = 0;
    public CastlingRights CastlingRights { get; private set; } = CastlingRights.All;

    public ulong Checkmask { get; private set; } = ulong.MaxValue;
    public PinData Pinmasks = new PinData();
    public bool IsPinned { get; private set; } = false;

    public byte CheckerCount { get; private set; } = 0;

    public ulong AttackMask = Bitboards.Masks.Rank_3;
    public ulong BlackAttacks = Bitboards.Masks.Rank_6;
    public ulong this[Piece piece]
    {
        get => bb[(byte)((byte)piece >> 4), (byte)piece & 0xf];
        set => bb[(byte)((byte)piece >> 4), (byte)piece & 0xf] = value;
    }
    public ulong this[Colors color]
    {
        get => bb[(byte)color];
        set => bb[(byte)color] = value;
    }
    public ulong this[Colors color, PieceType pieceType]
    {
        get => bb[(byte)color, (byte)pieceType];
        set => bb[(byte)color, (byte)pieceType] = value;
    }

    public ulong this[PieceType pieceType]
    {
        get => bb[(byte)CurrentPlayer, (byte)pieceType];
        set => bb[(byte)CurrentPlayer, (byte)pieceType] = value;
    }

    public ulong Occupied => White | Black;
    public ulong Empty => ~Occupied;
    public ulong Hash = Hashes.Default;
    public bool IsCheck => Checkmask != ulong.MaxValue;

    public bool IsEndgame => Occupied == (WhiteKing | WhitePawns | BlackKing | BlackPawns);

    public void Move(ref readonly Move m)
    {
        var oponent = Enemy(CurrentPlayer);

        Diffs[plyfromRoot] = new DiffData(
           Hash, CastlingRights, EnPassant
        );

        bb[(int)Colors.White * m.Color, (int)m.FromPieceType] ^= m.FromSquare | m.ToSquare;

        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            bb[(int)m.CapturePieceType] ^= rookmask;
            bb[(int)CurrentPlayer] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            bb[(int)m.CapturePieceType] ^= m.CaptureSquare;
            bb[(int)oponent] ^= m.CaptureSquare;
        }

        if (m.PromotionPieceType != PieceType.None)
        {
            var promoteIndex = m.PromotionPieceType;
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
        IsPinned = CreatePinmasks(oponent);

        CurrentPlayer = oponent;
        plyfromRoot++;
    }


    public void Undo(ref readonly Move m)
    {
        plyfromRoot--;

        var (us, oponent) = (Enemy(CurrentPlayer), CurrentPlayer);

        bb[(int)us, (int)m.FromPieceType] ^= m.FromSquare | m.ToSquare;

        if (m.CastleFlag != CastlingRights.None)
        {
            var rookmask = m.CaptureSquare | m.CastleSquare;
            bb[(int)m.CapturePieceType] ^= rookmask;
            bb[(int)us] ^= rookmask;
        }
        else if (m.CapturePieceType != PieceType.None)
        {
            bb[(int)m.CapturePieceType] ^= m.CaptureSquare;
            bb[(int)oponent] ^= m.CaptureSquare;
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
        IsPinned = CreatePinmasks(us);

        CurrentPlayer = us;
    }

    public void SkipTurn()
    {
        Diffs[plyfromRoot] = new DiffData(
            Hash, CastlingRights, EnPassant
        );
        var us = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;

        CurrentPlayer = us;
        EnPassant = 0;

        AttackMask = CreateEnemyAttackMask(us);
        (Checkmask, CheckerCount) = CreateCheckMask(us);
        IsPinned = CreatePinmasks(us);

        Hash ^= Hashes.GetValue(Colors.White);
        Hash ^= Hashes.GetValue(EnPassant);
        plyfromRoot++;
    }

    public void UndoSkipTurn()
    {
        plyfromRoot--;
        var us = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;

        CurrentPlayer = us;
        AttackMask = CreateEnemyAttackMask(us);
        (Checkmask, CheckerCount) = CreateCheckMask(us);
        IsPinned = CreatePinmasks(us);

        CastlingRights = Diffs[plyfromRoot].Castling;
        EnPassant = Diffs[plyfromRoot].EnPassant;
        Hash = Diffs[plyfromRoot].Hash;
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

        var from = m.FromSquare;
        var to = m.ToSquare;

        // Pawn moves leading to en passant has the en passant square
        // 1 square in front of the start and 1 square behind the target
        var (rank2, rank4, ep, op) = m.Color == ((byte)Colors.White & 1)
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

    public bool CreatePinmasks(Colors color)
    {
        bool isPinned = false;
        byte king = Squares.ToIndex(color == Colors.White ? WhiteKing : BlackKing);
        var enemy = Enemy(color);

        var enemyRooks = bb[(int)enemy, RooksIndex] | bb[(int)enemy, QueensIndex];

        var rank = Bitboards.Masks.GetRank(king);
        var file = Bitboards.Masks.GetFile(king);

        var epTargetSquare = (color == Colors.White)
            ? EnPassant - 8
            : EnPassant + 8;

        Pinmasks = new PinData();

        while (enemyRooks != 0)
        {
            var sq = Bitboards.PopLsb(ref enemyRooks);
            var attack = MovePatterns.SquaresBetween[king][sq];

            if (EnPassant != 0)
            {
                attack ^= Squares.FromIndex(epTargetSquare);
            }

            if (Bitboards.CountOccupied(attack & this[color]) == 1
                && Bitboards.CountOccupied(attack & this[enemy]) == 1)
            {
                if ((attack & rank) != 0)
                {
                    Pinmasks[0] |= attack;
                    isPinned = true;
                }
                else if ((attack & file) != 0)
                {
                    Pinmasks[1] |= attack;
                    isPinned = true;
                }
            }
        }

        var enemyBishops = bb[(int)enemy, BishopsIndex] | bb[(int)enemy, QueensIndex];

        var diagonal = Bitboards.Masks.GetDiagonal(king);
        var antiDiagonal = Bitboards.Masks.GetAntiadiagonal(king);

        while (enemyBishops != 0)
        {
            var sq = Bitboards.PopLsb(ref enemyBishops);
            var attack = MovePatterns.SquaresBetween[king][sq];

            if (Bitboards.CountOccupied(attack & this[color]) == 1
                && Bitboards.CountOccupied(attack & this[enemy]) == 1)
            {
                if ((attack & diagonal) != 0)
                {
                    Pinmasks[3] |= attack;
                    isPinned = true;
                }
                else if ((attack & antiDiagonal) != 0)
                {
                    Pinmasks[2] |= attack;
                    isPinned = true;
                }
            }
        }


        return isPinned;
    }

    internal (ulong, byte) CreateCheckMask(Colors color)
    {
        ulong checkmask = 0;
        byte countCheckers = 0;

        Colors opponentColor = Utils.Enemy(color);
        ulong bbKing = color == Colors.White ? WhiteKing : BlackKing;
        if ((AttackMask & bbKing) == 0) return (ulong.MaxValue, 0);

        byte king = Squares.ToIndex(bbKing);

        // king can never check
        for (int i = 1; i < 6; i++)
        {
            var pieceBitboard = this[opponentColor, (PieceType)i];
            while (pieceBitboard != 0)
            {
                var piece = Bitboards.PopLsb(ref pieceBitboard);
                var checker = Squares.FromIndex(piece);

                var squares = MovePatterns.SquaresBetween[piece][king];
                var attacks = MovePatterns.GetAttack((Piece)((int)opponentColor << 4 | i), checker, Empty);

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

    public (bool, Vector256<ulong>) CreatePinmasksOld(Colors color)
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

        return (isPinned, Vector256.LoadUnsafe(ref pinmasks[0]));
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

    public string ToDebugString()
    {
        return
            $"W: {White:X}\n" +
            $"P: {WhitePawns:X}\n" +
            $"N: {WhiteKnights:X}\n" +
            $"B: {WhiteBishops:X}\n" +
            $"R: {WhiteRooks:X}\n" +
            $"Q: {WhiteQueens:X}\n" +
            $"K: {WhiteKing:X}\n" +
            $"B: {Black:X}\n" +
            $"p: {BlackPawns:X}\n" +
            $"n: {BlackKnights:X}\n" +
            $"b: {BlackBishops:X}\n" +
            $"r: {BlackRooks:X}\n" +
            $"q: {BlackQueens:X}\n" +
            $"k: {BlackKing:X}\n" +
            $"Player: {CurrentPlayer}\n" +
            $"Castle: {CastlingRights}\n" +
            $"EP: {EnPassant}\n" +
            $"Checkmask: {Checkmask:X}";
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
        Colors color;

        var square = Squares.FromIndex(attack);

        if ((White & square) != 0) color = Colors.White;
        else if ((Black & square) != 0) color = Colors.Black;
        else return Piece.None;

        // Find the piece type
        for (var pieceType = PieceType.Pawn; pieceType <= PieceType.King; pieceType++)
        {
            if ((bb[(int)pieceType] & square) != 0) return GetPiece(color, pieceType);
        }
        throw new InvalidOperationException($"{Squares.CoordinateFromIndex(attack)} is {color}, but missing in piece bitboards");
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
            Pinmasks = (Vector256<ulong>)Pinmasks,
            IsPinned = IsPinned,
            CurrentPlayer = CurrentPlayer,
        };
    }

    public static MutablePosition FromReadOnly(Position pos)
    {
        var mutable = new MutablePosition
        {
            CastlingRights = pos.CastlingRights,
            EnPassant = pos.EnPassant,
            CurrentPlayer = pos.CurrentPlayer,
            CheckerCount = pos.CheckerCount,
            Checkmask = pos.Checkmask,
            Pinmasks = new PinData(pos.Pinmasks),
            IsPinned = pos.IsPinned,
            Hash = pos.Hash
        };

        mutable.bb[BlackIndex] = pos.Black;
        mutable.bb[PawnIndex] = pos.WhitePawns | pos.BlackPawns;
        mutable.bb[KnightsIndex] = pos.WhiteKnights | pos.BlackKnights;
        mutable.bb[BishopsIndex] = pos.WhiteBishops | pos.BlackBishops;
        mutable.bb[RooksIndex] = pos.WhiteRooks | pos.BlackRooks;
        mutable.bb[QueensIndex] = pos.WhiteQueens | pos.BlackQueens;
        mutable.bb[KingsIndex] = pos.WhiteKing | pos.BlackKing;
        mutable.bb[WhiteIndex] = pos.White;

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

    [InlineArray(8)]
    private struct DenseBitboards
    {
        private ulong _element0;

        public ulong this[int i]
        {
            readonly get => this[i];
            set => this[i] = value;
        }

        public ulong this[int color, int piece]
        {
            readonly get => this[color] & this[piece];
            set
            {
                var mask = (this[color] & this[piece]) ^ value;
                this[color] ^= mask;
                this[piece] ^= mask;
            }
        }
    }

    [InlineArray(4)]
    public struct PinData
    {
        private ulong _element0;

        public ulong this[int i]
        {
            readonly get => this[i];
            set => this[i] = value;
        }

        public PinData()
        {
        }
        internal PinData(Vector256<ulong> v)
        {
            v.StoreUnsafe(ref _element0);
        }

        public static explicit operator Vector256<ulong>(PinData p)
        {
            return Vector256.LoadUnsafe(ref p[0]);
        }
    }
}
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using static Lolbot.Core.NNUE;
using static Lolbot.Core.Utils;

namespace Lolbot.Core;

public sealed class MutablePosition
{
    public Colors CurrentPlayer { get; internal set; } = Colors.White;
    public const int MaxDepth = 1024;

    private readonly DiffData[] Diffs = new DiffData[MaxDepth];
    #if NNUE
    private readonly Accumulator Accumulator;
    #endif

    public int plyfromRoot = 0;

    private const int BlackIndex = 0;
    private const int PawnIndex = 1;
    private const int KnightsIndex = 2;
    private const int BishopsIndex = 3;
    private const int RooksIndex = 4;
    private const int QueensIndex = 5;
    private const int KingsIndex = 6;
    private const int WhiteIndex = 7;


    private DenseBitboards bb = new();

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

    public byte EnPassant { get; set; } = 0;
    public CastlingRights CastlingRights { get; set; } = CastlingRights.All;

    public ulong Checkmask { get; set; } = ulong.MaxValue;
    public PinData Pinmasks = new PinData();
    public bool IsPinned { get; set; } = false;

    public byte CheckerCount { get; set; } = 0;

    public ulong AttackMask = Bitboards.Masks.Rank_3;

    public ulong this[Piece piece]
    {
        get => bb[(byte)((byte)piece >> 4)] & bb[(byte)piece & 0xf];
        set => bb.SetMask((byte)((byte)piece >> 4), (byte)piece & 0xf, value);
    }
    public ulong this[Colors color]
    {
        get => bb[(byte)color];
        set => bb[(byte)color] = value;
    }
    public ulong this[Colors color, PieceType pieceType]
    {
        get => bb[(byte)color] & bb[(byte)pieceType];
        set => bb.SetMask((byte)color, (byte)pieceType, value);
    }

    public ulong this[PieceType pieceType]
    {
        get => bb[(byte)CurrentPlayer] & bb[(byte)pieceType];
        set => bb.SetMask((byte)CurrentPlayer, (byte)pieceType, value);
    }

    public ulong Occupied => White | Black;
    public ulong Empty => ~Occupied;
    public ulong Hash = Hashes.Default;
    public bool IsCheck => Checkmask != ulong.MaxValue;

    public bool IsEndgame => Occupied == (WhiteKing | WhitePawns | BlackKing | BlackPawns);

    public MutablePosition()
    {
        bb[BlackIndex] = Bitboards.Masks.Rank_8 | Bitboards.Masks.Rank_7;
        bb[PawnIndex] = Bitboards.Masks.Rank_2 | Bitboards.Masks.Rank_7;
        bb[KnightsIndex] = Bitboards.Create("B1", "G1", "B8", "G8");
        bb[BishopsIndex] = Bitboards.Create("C1", "F1", "C8", "F8");
        bb[RooksIndex] = Bitboards.Create("A1", "H1", "A8", "H8");
        bb[QueensIndex] = Bitboards.Create("D1", "D8");
        bb[KingsIndex] = Bitboards.Create("E1", "E8");
        bb[WhiteIndex] = Bitboards.Masks.Rank_1 | Bitboards.Masks.Rank_2;
#if NNUE
        Accumulator = NNUE.Accumulator.Create(this);
#endif
    }

    public static MutablePosition EmptyBoard => new()
    {
        bb = new DenseBitboards(),
        CastlingRights = CastlingRights.All,
        EnPassant = 0,
        CurrentPlayer = Colors.White,
    };

#if NNUE
    public int Eval => Accumulator.Read(CurrentPlayer);
#endif

    public void Move(ref readonly Move m)
    {
#if NNUE
        Accumulator.Move(in m);
#endif
        var oponent = Enemy(CurrentPlayer);

        Diffs[plyfromRoot] = new DiffData(
           Hash, CastlingRights, EnPassant
        );

        bb[(int)Colors.White * m.Color] ^= m.FromSquare | m.ToSquare;
        bb[(int)m.FromPieceType] ^= m.FromSquare | m.ToSquare;

        Hash ^= Hashes.GetValue(m.FromPiece, m.FromIndex);
        Hash ^= Hashes.GetValue(m.FromPiece, m.ToIndex);

        if (m.IsSpecial)
        {

            if (m.CastleFlag != CastlingRights.None)
            {
                var rookmask = m.CaptureSquare | m.CastleSquare;
                bb[(int)m.CapturePieceType] ^= rookmask;
                bb[(int)CurrentPlayer] ^= rookmask;

                Hash ^= Hashes.GetValue(m.CapturePiece, m.CaptureIndex);
                Hash ^= Hashes.GetValue(m.CapturePiece, m.CastleIndex);
            }
            else if (m.CapturePieceType != PieceType.None)
            {
                bb[(int)m.CapturePieceType] ^= m.CaptureSquare;
                bb[(int)oponent] ^= m.CaptureSquare;

                Hash ^= Hashes.GetValue(m.CapturePiece, m.CaptureIndex);
            }

            if (m.PromotionPieceType != PieceType.None)
            {
                var promoteIndex = m.PromotionPieceType;
                this[promoteIndex] ^= m.ToSquare;
                this[m.FromPiece] ^= m.ToSquare;

                Hash ^= Hashes.GetValue(m.FromPiece, m.ToIndex); // remove pawn from hash again
                Hash ^= Hashes.GetValue(m.PromotionPiece, m.ToIndex);
            }
        }

        if (((m.FromSquare | m.CaptureSquare) & Squares.MayDropCastlingRightsMask) != 0)
            ApplyCastlingRights(in m);

        Hash ^= Hashes.GetValue(EnPassant);
        EnPassant = GetEnPassantSquare(in m);
        Hash ^= Hashes.GetValue(EnPassant);
        Hash ^= Hashes.GetValue(Colors.White);

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

        bb[(int)us] ^= m.FromSquare | m.ToSquare;
        bb[(int)m.FromPieceType] ^= m.FromSquare | m.ToSquare;

        if (m.IsSpecial)
        {
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
        }

        CastlingRights = Diffs[plyfromRoot].Castling;
        EnPassant = Diffs[plyfromRoot].EnPassant;
        Hash = Diffs[plyfromRoot].Hash;

        AttackMask = CreateEnemyAttackMask(us);
        (Checkmask, CheckerCount) = CreateCheckMask(us);
        IsPinned = CreatePinmasks(us);

        CurrentPlayer = us;
        #if NNUE
        Accumulator.Undo(in m);
        #endif
    }

    public void SkipTurn()
    {
        Diffs[plyfromRoot] = new DiffData(
            Hash, CastlingRights, EnPassant
        );
        var us = CurrentPlayer == Colors.White ? Colors.Black : Colors.White;

        // Remove EP hash if it was active and capturable by the side to move
        if (EnPassant != 0)
        {
            var prePawnAttacks = CurrentPlayer == Colors.White
                ? MovePatterns.CalculateAllPawnAttacksWhite(WhitePawns)
                : MovePatterns.CalculateAllPawnAttacksBlack(BlackPawns);
            Hash ^= Hashes.GetValue(prePawnAttacks, EnPassant);
        }

        CurrentPlayer = us;
        EnPassant = 0;

        AttackMask = CreateEnemyAttackMask(us);
        (Checkmask, CheckerCount) = CreateCheckMask(us);
        IsPinned = CreatePinmasks(us);

        Hash ^= Hashes.GetValue(Colors.White);
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

    /// SEE score from the perspective of the side to move *before* 'm' is played.
    /// Returns the net material swing in centipawns; >=0 ⇒ non-losing capture (by this evaluation).
    public int SEE(Move m, bool treatPromotion = true)
    {
        if (m.IsQuiet) return 0;

        int d = 0;
        Span<int> gain = stackalloc int[32];

        Piece aPiece = m.FromPiece;

        ulong mayXray = bb[PawnIndex] | bb[BishopsIndex] | bb[RooksIndex] | bb[QueensIndex];
        ulong fromSet = m.FromSquare;
        ulong occ = Occupied;
        ulong attadef = AttackersTo(m.ToIndex, ref occ);

        gain[d] = Heuristics.GetPieceValue(m.CapturePiece);

        var sideToMove = (Colors)(7 * m.Color);
        do
        {
            d++; // next depth and side
            gain[d] = Heuristics.GetPieceValue(aPiece) - gain[d - 1]; // speculative store, if defended
            attadef ^= fromSet; // reset bit in set to traverse
            occ ^= fromSet; // reset bit in temporary occupancy (for x-Rays)

            if ((fromSet & mayXray) != 0)
                attadef = AttackersTo(m.ToIndex, ref occ);

            sideToMove = Enemy(sideToMove);
            fromSet = GetLeastValuablePiece(attadef, sideToMove, ref aPiece);
        } while (fromSet != 0);

        while (--d >= 1)
        {
            gain[d - 1] = -Math.Max(-gain[d - 1], gain[d]);
        }

        return gain[0];
    }


    ulong GetLeastValuablePiece(ulong attackers, Colors c, ref Piece piece)
    {
        PieceType[] types = [PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King];
        for (var i = 0; i < types.Length; i++)
        {
            var pt = types[i];
            piece = GetPiece(c, pt);
            ulong subset = attackers & bb[(int)c] & bb[(int)pt];

            if (subset != 0)
            {
                return Bitboards.PopSquare(ref subset);
            }
        }
        return 0; // empty set
    }

    private ulong AttackersTo(byte sq, ref ulong occ)
    {
        var attackers =
            (MovePatterns.Knights[sq] & bb[KnightsIndex]) |
            (MovePatterns.Kings[sq] & bb[KingsIndex]) |
            (MovePatterns.BishopAttacks(sq, ref occ) & (bb[BishopsIndex] | bb[QueensIndex])) |
            (MovePatterns.RookAttacks(sq, ref occ) & (bb[RooksIndex] | bb[QueensIndex])) |
            (MovePatterns.WhitePawnAttacks[sq] & BlackPawns) |
            (MovePatterns.BlackPawnAttacks[sq] & WhitePawns);

        return occ & attackers;
    }

    private void ApplyCastlingRights(ref readonly Move m)
    {
        CastlingRights removed = CastlingRights.None;

        ulong x = Squares.FromIndex(m.FromIndex);

        if (m.CapturePiece != Piece.None)
            x |= Squares.FromIndex(m.CaptureIndex);

        if ((x & Squares.FromIndex(Squares.A1)) != 0)
            removed = CastlingRights.WhiteQueen;
        else if ((x & Squares.FromIndex(Squares.H1)) != 0)
            removed = CastlingRights.WhiteKing;
        else if ((x & Squares.FromIndex(Squares.A8)) != 0)
            removed = CastlingRights.BlackQueen;
        else if ((x & Squares.FromIndex(Squares.H8)) != 0)
            removed = CastlingRights.BlackKing;

        if (m.FromIndex == Squares.E1)
            removed = CastlingRights.WhiteKing | CastlingRights.WhiteQueen;
        else if (m.FromIndex == Squares.E8)
            removed = CastlingRights.BlackKing | CastlingRights.BlackQueen;

        if (removed != 0)
        {
            var newCastling = CastlingRights & ~removed;
            Hash ^= Hashes.GetValue(CastlingRights);
            Hash ^= Hashes.GetValue(newCastling);
            CastlingRights = newCastling;
        }
    }

    public byte GetEnPassantSquare(ref readonly Move m)
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

        var adjacent = rank4 & (to << 1 | to >> 1) & op;
        return adjacent == 0 ? (byte)0 : Squares.ToIndex(ep);
    }

    internal void FillInput(ref short[] input)
    {
        Array.Clear(input, 0, input.Length);
        for (int color = 0; color < 2; color++)
        {
            ulong mask = bb[color * 7]; // BlackIndex = 0, WhiteIndex = 7
            for (int piece = 0; piece < 6; piece++)
            {
                ulong bitboard = bb[piece + 1] & mask;
                while (bitboard != 0)
                {
                    int square = Bitboards.PopLsb(ref bitboard);
                    input[(color * 6 + piece) * 64 + square] = 1;
                }
            }
        }
    }

    // Check and pin masks
    public void Reevaluate()
    {
        AttackMask = CreateEnemyAttackMask(CurrentPlayer);
        (Checkmask, CheckerCount) = CreateCheckMask(CurrentPlayer);
        IsPinned = CreatePinmasks(CurrentPlayer);
        #if NNUE
        Accumulator.Reevaluate(this);
        #endif
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


    public bool CreatePinmasks(Colors color)
    {
        bool isPinned = false;
        byte king = Squares.ToIndex(color == Colors.White ? WhiteKing : BlackKing);
        var enemy = Enemy(color);

        var enemyRooks = bb[(int)enemy] & (bb[RooksIndex] | bb[QueensIndex]);

        var rank = Bitboards.Masks.GetRank(king);
        var file = Bitboards.Masks.GetFile(king);

        Pinmasks = new PinData();

        while (enemyRooks != 0)
        {
            var sq = Bitboards.PopLsb(ref enemyRooks);
            var attack = MovePatterns.SquaresBetween[king][sq];

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

        var enemyBishops = bb[(int)enemy] & (bb[BishopsIndex] | bb[QueensIndex]);

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
        return Piece.None;
    }

    // Move generation
    public Span<Move> GenerateLegalMoves(Piece pieceType)
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator.Legal(this, ref moves);


        return moves[..count].ToArray()
            .Where(x => x.FromPiece == pieceType)
            .ToArray();
    }

    public Span<Move> GenerateLegalMoves()
    {
        const int max_moves = 218;

        Span<Move> moves = stackalloc Move[max_moves];
        var count = MoveGenerator.Legal(this, ref moves);
        return moves[..count].ToArray();
    }

    // Move generation
    public Span<Move> GenerateLegalMoves(char pieceType)
    {
        return GenerateLegalMoves(FromName(pieceType));
    }

    // String functions
    public static MutablePosition FromFen(string fen)
    {
        return FenSerializer.Parse(fen);
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

    public const int BinarySize = 67;
    public int CopyTo(Span<byte> buffer)
    {
        Debug.Assert(Unsafe.SizeOf<DenseBitboards>() + 3 == BinarySize);
        if (buffer.Length < BinarySize) throw new ArgumentException($"Buffer too small, need at least {BinarySize} bytes");

        MemoryMarshal.Write(buffer, in bb);
        buffer[Unsafe.SizeOf<DenseBitboards>()] = (byte)CurrentPlayer;
        buffer[Unsafe.SizeOf<DenseBitboards>() + 1] = (byte)CastlingRights;
        buffer[Unsafe.SizeOf<DenseBitboards>() + 2] = EnPassant;

        return BinarySize;
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

    internal MutablePosition Clone()
    {
        return new MutablePosition
        {
            bb = this.bb,
            CurrentPlayer = this.CurrentPlayer,
            CastlingRights = this.CastlingRights,
            EnPassant = this.EnPassant,
            Hash = this.Hash,
            Checkmask = this.Checkmask,
            Pinmasks = this.Pinmasks,
            IsPinned = this.IsPinned,
            CheckerCount = this.CheckerCount,
            AttackMask = this.AttackMask,
            plyfromRoot = this.plyfromRoot,
        };
    }

    internal void DropRandomPiece()
    {
        var bitboard = CurrentPlayer == Colors.White ? White : Black;
        bitboard &= ~bb[KingsIndex]; // cannot drop king
        var pieceCount = Bitboards.CountOccupied(bitboard);

        if (pieceCount == 0) return;

        var targetIndex = Random.Shared.Next(pieceCount);
        var currentIndex = 0;

        while (bitboard != 0)
        {
            var dropIndex = Bitboards.PopLsb(ref bitboard);

            if (currentIndex == targetIndex)
            {
                Debug.WriteLine(FenSerializer.ToFenString(this));
                var piece = GetOccupant(ref dropIndex);
                var pieceType = (PieceType)((byte)piece & 0xf);

                if (pieceType == PieceType.Rook && CastlingRights != CastlingRights.None)
                {
                    Hash ^= Hashes.GetValue(CastlingRights);
                    CastlingRights = CastlingRights.None;
                }

                bb[(int)CurrentPlayer] ^= Squares.FromIndex(dropIndex);
                bb[(int)pieceType] ^= Squares.FromIndex(dropIndex);

                Hash ^= Hashes.GetValue(piece, dropIndex);
                Reevaluate();
                Debug.WriteLine(FenSerializer.ToFenString(this));

                if (Occupied !=
                    (BlackPawns | BlackKnights | BlackBishops | BlackRooks |
                    BlackQueens | BlackKing |
                    WhitePawns | WhiteKnights | WhiteBishops | WhiteRooks |
                    WhiteQueens | WhiteKing)
                    )
                {
                    Debugger.Break();
                }

                return;
            }

            currentIndex++;
        }
    }
    private readonly struct DiffData(ulong hash, CastlingRights castle, byte ep)
    {
        public readonly ulong Hash = hash;
        public readonly CastlingRights Castling = castle;
        public readonly byte EnPassant = ep;
    }

    [InlineArray(8)]
    internal struct DenseBitboards
    {
        private ulong _element0;
  
        internal void SetMask(int color, int piece, ulong value)
        {
            var mask = (this[color] & this[piece]) ^ value;
            this[color] ^= mask;
            this[piece] ^= mask;
        }
    }

    [InlineArray(4)]
    public struct PinData
    {
        private ulong _element0;

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
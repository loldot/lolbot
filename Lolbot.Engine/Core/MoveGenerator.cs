using static Lolbot.Core.Utils;
namespace Lolbot.Core;

public class MoveGenerator
{
    public static int Legal(MutablePosition position, ref Span<Move> moves)
    {
        var count = 0;
        AddKingMoves<Legal>(position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        AddPawnMoves<Legal>(position, ref moves, ref count);
        AddKnightMoves<Legal>(position, ref moves, ref count);
        AddBishopMoves<Legal>(position, ref moves, ref count);
        AddRookMoves<Legal>(position, ref moves, ref count);
        AddQueenMoves<Legal>(position, ref moves, ref count);

        return count;
    }

    public static int Captures(MutablePosition position, ref Span<Move> moves)
    {
        var count = 0;
        AddKingMoves<Captures>(position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        AddPawnMoves<Captures>(position, ref moves, ref count);
        AddKnightMoves<Captures>(position, ref moves, ref count);
        AddBishopMoves<Captures>(position, ref moves, ref count);
        AddRookMoves<Captures>(position, ref moves, ref count);
        AddQueenMoves<Captures>(position, ref moves, ref count);

        return count;
    }

    private static void AddKingMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.King);
        var king = position[piece];
        var targets = position[Enemy(position.CurrentPlayer)];

        ulong enemyAttacks = position.AttackMask;
        var fromIndex = Squares.ToIndex(king);

        var pseudoAttacks = MovePatterns.Kings[fromIndex];
        var attacks = pseudoAttacks & targets & ~enemyAttacks;
        while (TMove.HasCaptures && attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
        }

        var quiets = MovePatterns.Kings[fromIndex] & ~(position.Occupied | enemyAttacks);
        while (TMove.HasQuiets && quiets != 0)
        {
            var toIndex = Bitboards.PopLsb(ref quiets);
            moves[count++] = new Move(piece, fromIndex, toIndex);
        }

        if (IsCastleLegal(position, CastlingRights.WhiteKing | CastlingRights.BlackKing, Move.Castle(position.CurrentPlayer), enemyAttacks))
        {
            if (TMove.HasQuiets) moves[count++] = Move.Castle(position.CurrentPlayer);
        }

        if (IsCastleLegal(position, CastlingRights.WhiteQueen | CastlingRights.BlackQueen, Move.QueenSideCastle(position.CurrentPlayer), enemyAttacks))
        {
            if (TMove.HasQuiets) moves[count++] = Move.QueenSideCastle(position.CurrentPlayer);
        }
    }

    private static void AddQueenMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Queen);
        var queens = position[piece];

        AddSlider<TMove>(ref piece, position, ref moves, queens, MovePatterns.RookAttacks, ref count);
        AddSlider<TMove>(ref piece, position, ref moves, queens, MovePatterns.BishopAttacks, ref count);
    }

    private static void AddRookMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Rook);
        var rooks = position[piece];

        AddSlider<TMove>(ref piece, position, ref moves, rooks, MovePatterns.RookAttacks, ref count);
    }

    private static void AddBishopMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Bishop);
        var bishops = position[piece];

        AddSlider<TMove>(ref piece, position, ref moves, bishops, MovePatterns.BishopAttacks, ref count);
    }

    private delegate ulong SligerGen(byte sq, ref readonly ulong bitboard);

    private static void AddSlider<TMove>(
        ref Piece piece,
        MutablePosition position,
        ref Span<Move> moves,
        ulong bitboard,
        SligerGen attackFunc,
        ref int count) where TMove : MoveType
    {
        var friendlies = position[position.CurrentPlayer];
        var targets = position[Enemy(position.CurrentPlayer)];

        var occ = position.Occupied;

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var pseudoAttacks = attackFunc(fromIndex, ref occ);

            var valid = pseudoAttacks & ~friendlies & position.Checkmask & position.PinnedPiece(in fromIndex);

            var attacks = valid & targets;
            while (TMove.HasCaptures && attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
            }

            var quiets = valid & ~position.Occupied;
            while (TMove.HasQuiets && quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(piece, fromIndex, toIndex);
            }
        }
    }

    private static void AddKnightMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = GetPiece(position.CurrentPlayer, PieceType.Knight);
        var knights = position[piece];
        var targets = position[Enemy(position.CurrentPlayer)];

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var pseudoAttacks = MovePatterns.Knights[fromIndex];
            if (TMove.HasCaptures)
            {
                var attacks = MovePatterns.Knights[fromIndex] & targets & position.Checkmask & position.PinnedPiece(in fromIndex);
                while (attacks != 0)
                {
                    var attack = Bitboards.PopLsb(ref attacks);
                    var occupant = position.GetOccupant(ref attack);

                    moves[count++] = new Move(piece, fromIndex, attack, occupant);
                }
            }

            if (TMove.HasQuiets)
            {
                var quiets = pseudoAttacks & ~position.Occupied & position.Checkmask & position.PinnedPiece(in fromIndex);
                while (quiets != 0)
                {
                    var toIndex = Bitboards.PopLsb(ref quiets);
                    if (TMove.HasQuiets)
                        moves[count++] = new Move(piece, fromIndex, toIndex);
                }
            }
        }
    }

    private static void AddPawnMoves<TMove>(MutablePosition position, ref Span<Move> moves, ref int count)
        where TMove : MoveType
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var pawns = position[piece];
        var targets = position[Enemy(position.CurrentPlayer)];

        var (pushPattern, attackPattern) = (position.CurrentPlayer == Colors.White)
            ? (MovePatterns.WhitePawnPushes, MovePatterns.WhitePawnAttacks)
            : (MovePatterns.BlackPawnPushes, MovePatterns.BlackPawnAttacks);

        while (pawns != 0)
        {
            var sq = Bitboards.PopLsb(ref pawns);
            var pseudoAttacks = attackPattern[sq];
            var attacks = pseudoAttacks & targets & position.Checkmask & position.PinnedPiece(ref sq);

            while (TMove.HasCaptures && attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                foreach (var promotionPiece in MovePatterns.PromotionPieces[attack])
                {
                    moves[count++] = new Move(piece, sq, attack, position.GetOccupant(ref attack), promotionPiece);
                }
            }
            if (TMove.HasCaptures && position.EnPassant != 0 && ((1ul << position.EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(position, ref moves, count, ref sq, position.EnPassant);
                
            var pushes = pushPattern[sq] & position.Checkmask & position.PinnedPiece(in sq) & position.Empty;
            while (TMove.HasQuiets && pushes != 0)
            {
                var push = Bitboards.PopLsb(ref pushes);
                if ((MovePatterns.SquaresBetween[sq][push] & position.Occupied) == 0)
                {
                    // For all ranks except 2 and 7 promotion pieces = [Piece.None]
                    foreach (var promotionPiece in MovePatterns.PromotionPieces[push])
                    {
                        moves[count++] = new Move(piece, sq, push, Piece.None, promotionPiece);
                    }
                }
            }
        }
    }

    private static bool IsCastleLegal(MutablePosition position, CastlingRights requiredCastle, Move castle, ulong enemyAttacks)
    {
        requiredCastle &= (position.CurrentPlayer == Colors.White)
            ? CastlingRights.WhiteQueen | CastlingRights.WhiteKing
            : CastlingRights.BlackQueen | CastlingRights.BlackKing;

        var clearingRequired = MovePatterns.SquaresBetween[castle.FromIndex][castle.CaptureIndex]
            & ~castle.CaptureSquare;

        var occupiedBetween = clearingRequired & position.Occupied;
        var attacked = (
            castle.FromSquare |
            MovePatterns.SquaresBetween[castle.FromIndex][castle.ToIndex]
        ) & enemyAttacks;

        return ((position.CastlingRights & requiredCastle) != 0)
            && occupiedBetween == 0 && attacked == 0;
    }

    private static int DoEnPassant(
        MutablePosition position,
        ref Span<Move> moves,
        int count, ref byte sq, byte attack)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var king = position[position.CurrentPlayer, PieceType.King];
        var oppositeColor = position.CurrentPlayer == Colors.White ? Colors.Black : Colors.White;

        var opponentBishop = position[oppositeColor, PieceType.Bishop];
        var opponentRook = position[oppositeColor, PieceType.Rook];
        var opponentQueen = position[oppositeColor, PieceType.Queen];

        var captureOffset = position.CurrentPlayer == Colors.White ? MovePatterns.S : MovePatterns.N;
        var epCapture = (byte)(position.EnPassant + captureOffset);
        var ep = new Move(piece, sq, attack, position.GetOccupant(ref epCapture), epCapture);

        var occupiedAfter = position.Occupied ^ (Squares.FromIndex(epCapture) | Squares.FromIndex(sq) | (1ul << attack));

        var kingindex = Squares.ToIndex(king);
        var kingRook = MovePatterns.RookAttacks(kingindex, ref occupiedAfter);
        var kingBishop = MovePatterns.BishopAttacks(kingindex, ref occupiedAfter);

        var epWouldCheck = 0 != (kingRook & (opponentQueen | opponentRook));
        epWouldCheck |= 0 != (kingBishop & (opponentQueen | opponentBishop));

        var epSavesCheck = position.Checkmask < ulong.MaxValue;
        // Double pushed pawn is checking, capture en passant.
        if (!epWouldCheck || epSavesCheck)
        {
            moves[count++] = ep;
        }
        return count;
    }
}

internal interface MoveType
{
    static abstract bool HasQuiets { get; }
    static abstract bool HasCaptures { get; }
}

internal struct Legal : MoveType
{
    public static bool HasQuiets => true;
    public static bool HasCaptures => true;
}


internal struct Captures : MoveType
{
    public static bool HasQuiets => false;
    public static bool HasCaptures => true;
}
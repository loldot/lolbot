namespace Lolbot.Core;

public class MoveGenerator2
{
    public static int Legal(MutablePosition position, ref Span<Move> moves)
    {
        var count = 0;
        var attackmask = 0ul;
        AddKingMoves(position, ref moves, ref count, ref attackmask);

        if (position.CheckerCount > 1) return count;

        AddPawnMoves(position, ref moves, ref count, ref attackmask);
        AddKnightMoves(position, ref moves, ref count, ref attackmask);
        AddBishopMoves(position, ref moves, ref count, ref attackmask);
        AddRookMoves(position, ref moves, ref count, ref attackmask);
        AddQueenMoves(position, ref moves, ref count, ref attackmask);

        return count;
    }

    public static int Captures(MutablePosition position, ref Span<Move> moves)
    {
        var count = 0;
        AddKingCaptures(position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        AddPawnCaptures(position, ref moves, ref count);
        AddKnightCaptures(position, ref moves, ref count);
        AddBishopCaptures(position, ref moves, ref count);
        AddRookCaptures(position, ref moves, ref count);
        AddQueenCaptures(position, ref moves, ref count);

        return count;
    }

    private static void AddKingMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.King);
        var king = position[piece];
        var targets = position[~position.CurrentPlayer];

        ulong enemyAttacks = position.AttackMask;

        var fromIndex = Squares.ToIndex(king);
        var quiets = MovePatterns.Kings[fromIndex] & ~(position.Occupied | enemyAttacks);
        while (quiets != 0)
        {
            var toIndex = Bitboards.PopLsb(ref quiets);
            moves[count++] = new Move(piece, fromIndex, toIndex);
        }

        var pseudoAttacks = MovePatterns.Kings[fromIndex];
        attackmask |= pseudoAttacks;
        var attacks = pseudoAttacks & targets & ~enemyAttacks;
        while (attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
        }

        if (IsCastleLegal(position, CastlingRights.WhiteKing | CastlingRights.BlackKing, Move.Castle(position.CurrentPlayer), enemyAttacks))
        {
            moves[count++] = Move.Castle(position.CurrentPlayer);
        }

        if (IsCastleLegal(position, CastlingRights.WhiteQueen | CastlingRights.BlackQueen, Move.QueenSideCastle(position.CurrentPlayer), enemyAttacks))
        {
            moves[count++] = Move.QueenSideCastle(position.CurrentPlayer);
        }
    }

    private static void AddKingCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.King);
        var king = position[piece];
        var targets = position[~position.CurrentPlayer];

        ulong enemyAttacks = position.AttackMask;

        var fromIndex = Squares.ToIndex(king);

        var attacks = MovePatterns.Kings[fromIndex] & targets & ~enemyAttacks;
        while (attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
        }
    }

    private static void AddQueenMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Queen);
        var queens = position[piece];

        AddSlider(ref piece, position, ref moves, queens, MovePatterns.RookAttacks, ref count, ref attackmask);
        AddSlider(ref piece, position, ref moves, queens, MovePatterns.BishopAttacks, ref count, ref attackmask);
    }

    private static void AddQueenCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Queen);
        var queens = position[piece];

        AddSliderCaptures(ref piece, position, ref moves, queens, MovePatterns.RookAttacks, ref count);
        AddSliderCaptures(ref piece, position, ref moves, queens, MovePatterns.BishopAttacks, ref count);
    }

    private static void AddRookMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Rook);
        var rooks = position[piece];

        AddSlider(ref piece, position, ref moves, rooks, MovePatterns.RookAttacks, ref count, ref attackmask);
    }
    private static void AddRookCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Rook);
        var rooks = position[piece];

        AddSliderCaptures(ref piece, position, ref moves, rooks, MovePatterns.RookAttacks, ref count);
    }

    private static void AddBishopMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Bishop);
        var bishops = position[piece];

        AddSlider(ref piece, position, ref moves, bishops, MovePatterns.BishopAttacks, ref count, ref attackmask);
    }
    private static void AddBishopCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Bishop);
        var bishops = position[piece];

        AddSliderCaptures(ref piece, position, ref moves, bishops, MovePatterns.BishopAttacks, ref count);
    }


    private delegate ulong SligerGen(byte sq, ref ulong bitboard);

    private static void AddSlider(
        ref Piece piece,
        MutablePosition position,
        ref Span<Move> moves,
        ulong bitboard,
        SligerGen attackFunc,
        ref int count,
        ref ulong attackmask)
    {
        var friendlies = position[position.CurrentPlayer];
        var targets = position[~position.CurrentPlayer];

        var occ = position.Occupied;

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var pseudoAttacks = attackFunc(fromIndex, ref occ);
            attackmask |= pseudoAttacks;
            attackmask |= 1ul << fromIndex;

            var valid = pseudoAttacks & ~friendlies & position.Checkmask & position.PinnedPiece(in fromIndex);
            var quiets = valid & ~position.Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(piece, fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
            }
        }
    }

    private static void AddSliderCaptures(
    ref Piece piece,
    MutablePosition position,
    ref Span<Move> moves,
    ulong bitboard,
    SligerGen attackFunc,
    ref int count)
    {
        var friendlies = position[position.CurrentPlayer];
        var targets = position[~position.CurrentPlayer];

        var occ = position.Occupied;

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var pseudoAttacks = attackFunc(fromIndex, ref occ);
            var valid = pseudoAttacks & position.Checkmask & position.PinnedPiece(in fromIndex);

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
            }
        }
    }

    private static void AddKnightMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Knight);
        var knights = position[piece];
        var targets = position[~position.CurrentPlayer];

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var pseudoAttacks = MovePatterns.Knights[fromIndex];
            attackmask |= pseudoAttacks;

            var quiets = pseudoAttacks & ~position.Occupied & position.Checkmask & position.PinnedPiece(in fromIndex);
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(piece, fromIndex, toIndex);
            }

            var attacks = MovePatterns.Knights[fromIndex] & targets & position.Checkmask & position.PinnedPiece(in fromIndex);
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
            }
        }
    }

    private static void AddKnightCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Knight);
        var knights = position[piece];
        var targets = position[~position.CurrentPlayer];

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var attacks = MovePatterns.Knights[fromIndex] & targets & position.Checkmask & position.PinnedPiece(in fromIndex);
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
            }
        }
    }

    private static void AddPawnMoves(MutablePosition position, ref Span<Move> moves, ref int count, ref ulong attackmask)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var pawns = position[piece];
        var targets = position[~position.CurrentPlayer];

        var (pushPattern, attackPattern) = (position.CurrentPlayer == Colors.White)
            ? (MovePatterns.WhitePawnPushes, MovePatterns.WhitePawnAttacks)
            : (MovePatterns.BlackPawnPushes, MovePatterns.BlackPawnAttacks);

        while (pawns != 0)
        {
            var sq = Bitboards.PopLsb(ref pawns);

            var pushes = pushPattern[sq] & position.Checkmask & position.PinnedPiece(in sq) & position.Empty;
            while (pushes != 0)
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

            var pseudoAttacks = attackPattern[sq];
            attackmask |= pseudoAttacks;
            var attacks = pseudoAttacks & targets & position.Checkmask & position.PinnedPiece(ref sq);

            if (position.EnPassant != 0 && ((1ul << position.EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(position, ref moves, count, ref sq, position.EnPassant);

            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                foreach (var promotionPiece in MovePatterns.PromotionPieces[attack])
                {
                    moves[count++] = new Move(piece, sq, attack, position.GetOccupant(ref attack), promotionPiece);
                }
            }
        }
    }

    private static void AddPawnCaptures(MutablePosition position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var pawns = position[piece];
        var targets = position[~position.CurrentPlayer];

        var attackPattern = (position.CurrentPlayer == Colors.White)
            ? MovePatterns.WhitePawnAttacks
            : MovePatterns.BlackPawnAttacks;

        while (pawns != 0)
        {
            var sq = Bitboards.PopLsb(ref pawns);

            var pseudoAttacks = attackPattern[sq];
            var attacks = pseudoAttacks & targets & position.Checkmask & position.PinnedPiece(ref sq);

            if (position.EnPassant != 0 && ((1ul << position.EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(position, ref moves, count, ref sq, position.EnPassant);

            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                foreach (var promotionPiece in MovePatterns.PromotionPieces[attack])
                {
                    moves[count++] = new Move(piece, sq, attack, position.GetOccupant(ref attack), promotionPiece);
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
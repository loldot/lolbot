namespace Lolbot.Core;

public class MoveGenerator
{
    public static int Legal(ref readonly Position position, ref Span<Move> moves)
    {
        var count = 0;
        AddKingMoves(in position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        AddPawnMoves(in position, ref moves, ref count);
        AddKnightMoves(in position, ref moves, ref count);
        AddBishopMoves(in position, ref moves, ref count);
        AddRookMoves(in position, ref moves, ref count);
        AddQueenMoves(in position, ref moves, ref count);

        return count;
    }

    public static int Legal(ref readonly Position position, ref Span<Move> moves, Piece pieceType)
    {
        var count = 0;
        if (((int)pieceType & 0xf) == 6)
            AddKingMoves(in position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        if (((int)pieceType & 0xf) == 1)
            AddPawnMoves(in position, ref moves, ref count);
        if (((int)pieceType & 0xf) == 2)
            AddKnightMoves(in position, ref moves, ref count);
        if (((int)pieceType & 0xf) == 3)
            AddBishopMoves(in position, ref moves, ref count);
        if (((int)pieceType & 0xf) == 4)
            AddRookMoves(in position, ref moves, ref count);
        if (((int)pieceType & 0xf) == 5)
            AddQueenMoves(in position, ref moves, ref count);

        return count;
    }

    private static void AddKingMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.King);
        var king = position[piece];
        var targets = position[~position.CurrentPlayer];

        ulong enemyAttacks = position.CreateAttackMask(position.CurrentPlayer);

        var fromIndex = Squares.ToIndex(king);
        var quiets = MovePatterns.Kings[fromIndex] & ~(position.Occupied | enemyAttacks);
        while (quiets != 0)
        {
            var toIndex = Bitboards.PopLsb(ref quiets);
            moves[count++] = new Move(piece, fromIndex, toIndex);
        }

        var attacks = MovePatterns.Kings[fromIndex] & targets & ~enemyAttacks;
        while (attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(piece, fromIndex, attack, position.GetOccupant(ref attack));
        }

        if (IsCastleLegal(in position, Castle.WhiteKing | Castle.BlackKing, Move.Castle(position.CurrentPlayer), enemyAttacks))
        {
            moves[count++] = Move.Castle(position.CurrentPlayer);
        }

        if (IsCastleLegal(in position, Castle.WhiteQueen | Castle.BlackQueen, Move.QueenSideCastle(position.CurrentPlayer), enemyAttacks))
        {
            moves[count++] = Move.QueenSideCastle(position.CurrentPlayer);
        }
    }

    private static void AddQueenMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Queen);
        var queens = position[piece];

        AddSlider(ref piece, in position, ref moves, queens, MovePatterns.RookAttacks, ref count);
        AddSlider(ref piece, in position, ref moves, queens, MovePatterns.BishopAttacks, ref count);
    }

    private static void AddRookMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Rook);
        var rooks = position[piece];

        AddSlider(ref piece, in position, ref moves, rooks, MovePatterns.RookAttacks, ref count);
    }

    private static void AddBishopMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Bishop);
        var bishops = position[piece];

        AddSlider(ref piece, in position, ref moves, bishops, MovePatterns.BishopAttacks, ref count);
    }

    private delegate ulong SligerGen(byte sq, ref ulong bitboard);

    private static void AddSlider(ref Piece piece, ref readonly Position position, ref Span<Move> moves, ulong bitboard, SligerGen attackFunc, ref int count)
    {
        var friendlies = position[position.CurrentPlayer];
        var targets = position[~position.CurrentPlayer];

        var occ = position.Occupied;

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var valid = attackFunc(fromIndex, ref occ) & ~friendlies & position.Checkmask & position.PinnedPiece(in fromIndex);
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

    private static void AddKnightMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Knight);
        var knights = position[piece];
        var targets = position[~position.CurrentPlayer];

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var quiets = MovePatterns.Knights[fromIndex] & ~position.Occupied & position.Checkmask & position.PinnedPiece(in fromIndex);
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

    private static void AddPawnMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var pawns = position[piece];
        var targets = position[~position.CurrentPlayer];

        var (pushPattern, attackPattern) = (position.CurrentPlayer == Color.White)
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

            var attacks = attackPattern[sq] & targets & position.Checkmask & position.PinnedPiece(ref sq);

            if (position.EnPassant != 0 && ((1ul << position.EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(in position, ref moves, count, ref sq, position.EnPassant);

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

    private static bool IsCastleLegal(ref readonly Position position, Castle requiredCastle, Move castle, ulong enemyAttacks)
    {
        requiredCastle &= (position.CurrentPlayer == Color.White)
            ? Castle.WhiteQueen | Castle.WhiteKing
            : Castle.BlackQueen | Castle.BlackKing;

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
        ref readonly Position position,
        ref Span<Move> moves,
        int count, ref byte sq, byte attack)
    {
        var piece = Utils.GetPiece(position.CurrentPlayer, PieceType.Pawn);
        var king = position[position.CurrentPlayer, PieceType.King];
        var oppositeColor = position.CurrentPlayer == Color.White ? Color.Black : Color.White;

        var opponentBishop = position[oppositeColor, PieceType.Bishop];
        var opponentRook = position[oppositeColor, PieceType.Rook];
        var opponentQueen = position[oppositeColor, PieceType.Queen];

        var captureOffset = position.CurrentPlayer == Color.White ? MovePatterns.S : MovePatterns.N;
        var epCapture = (byte)(position.EnPassant + captureOffset);
        var ep = new Move(piece, sq, attack, position.GetOccupant(ref epCapture), epCapture);

        var occupiedAfter = position.Occupied ^ (Squares.FromIndex(in epCapture) | Squares.FromIndex(in sq) | (1ul << attack));

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
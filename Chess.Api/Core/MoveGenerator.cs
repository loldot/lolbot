using Lolbot.Core;

public class MoveGenerator
{
    public static int Legal(ref readonly Position position, ref Span<Move> moves, Piece? pieceType = null)
    {
        var count = 0;

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 6)
            AddKingMoves(in position, ref moves, ref count);

        if (position.CheckerCount > 1) return count;

        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 1)
            AddPawnMoves(in position, ref moves, ref count);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 2)
            AddKnightMoves(in position, ref moves, ref count);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 3)
            AddBishopMoves(in position, ref moves, ref count);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 4)
            AddRookMoves(in position, ref moves, ref count);
        if (!pieceType.HasValue || ((int)pieceType.Value & 0xf) == 5)
            AddQueenMoves(in position, ref moves, ref count);

        return count;
    }

    private static void AddKingMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var (king, targets) = (position.CurrentPlayer == Color.White)
            ? (position.WhiteKing, position.Black)
            : (position.BlackKing, position.White);

        ulong enemyAttacks = position.CreateAttackMask(position.CurrentPlayer);

        var fromIndex = Squares.ToIndex(king);
        var quiets = MovePatterns.Kings[fromIndex] & ~(position.Occupied | enemyAttacks);
        while (quiets != 0)
        {
            var toIndex = Bitboards.PopLsb(ref quiets);
            moves[count++] = new Move(fromIndex, toIndex);
        }

        var attacks = MovePatterns.Kings[fromIndex] & targets & ~enemyAttacks;
        while (attacks != 0)
        {
            var attack = Bitboards.PopLsb(ref attacks);
            moves[count++] = new Move(fromIndex, attack, attack, position.GetOccupant(attack));
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
        var queens = (position.CurrentPlayer == Color.White) ? position.WhiteQueens : position.BlackQueens;
        AddSlider(in position, ref moves, queens, MovePatterns.RookAttacks, ref count);
        AddSlider(in position, ref moves, queens, MovePatterns.BishopAttacks, ref count);
    }

    private static void AddRookMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var rooks = (position.CurrentPlayer == Color.White) ? position.WhiteRooks : position.BlackRooks;
        AddSlider(in position, ref moves, rooks, MovePatterns.RookAttacks, ref count);
    }

    private static void AddBishopMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var bishops = position.CurrentPlayer == Color.White ? position.WhiteBishops : position.BlackBishops;
        AddSlider(in position, ref moves, bishops, MovePatterns.BishopAttacks, ref count);
    }

    private static void AddSlider(ref readonly Position position, ref Span<Move> moves, ulong bitboard, Func<byte, ulong, ulong> attackFunc, ref int count)
    {
        var (targets, friendlies) = (position.CurrentPlayer == Color.White)
            ? (position.Black, position.White)
            : (position.White, position.Black);

        while (bitboard != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref bitboard);

            var valid = attackFunc(fromIndex, position.Occupied) & ~friendlies & position.Checkmask & position.PinnedPiece(in fromIndex);
            var quiets = valid & ~position.Occupied;
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = valid & targets;
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, position.GetOccupant(attack));
            }
        }
    }

    private static void AddKnightMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var (knights, targets) = (position.CurrentPlayer == Color.White)
            ? (position.WhiteKnights, position.Black)
            : (position.BlackKnights, position.White);

        while (knights != 0)
        {
            var fromIndex = Bitboards.PopLsb(ref knights);

            var quiets = MovePatterns.Knights[fromIndex] & ~position.Occupied & position.Checkmask & position.PinnedPiece(in fromIndex);
            while (quiets != 0)
            {
                var toIndex = Bitboards.PopLsb(ref quiets);
                moves[count++] = new Move(fromIndex, toIndex);
            }

            var attacks = MovePatterns.Knights[fromIndex] & targets & position.Checkmask & position.PinnedPiece(in fromIndex);
            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);
                moves[count++] = new Move(fromIndex, attack, attack, position.GetOccupant(attack));
            }
        }
    }

    private static void AddPawnMoves(ref readonly Position position, ref Span<Move> moves, ref int count)
    {
        var (pawns, targets, pushPattern, attackPattern) = (position.CurrentPlayer == Color.White)
            ? (position.WhitePawns, position.Black, MovePatterns.WhitePawnPushes, MovePatterns.WhitePawnAttacks)
            : (position.BlackPawns, position.White, MovePatterns.BlackPawnPushes, MovePatterns.BlackPawnAttacks);

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
                        moves[count++] = new Move(sq, push) with { PromotionPiece = promotionPiece };
                    }
                }
            }

            var attacks = attackPattern[sq] & targets & position.Checkmask & position.PinnedPiece(ref sq);

            if (((1ul << position.EnPassant) & attackPattern[sq]) != 0)
                count = DoEnPassant(in position, ref moves, count, ref sq, position.EnPassant);

            while (attacks != 0)
            {
                var attack = Bitboards.PopLsb(ref attacks);

                foreach (var promotionPiece in MovePatterns.PromotionPieces[attack])
                {
                    moves[count++] = new Move(sq, attack, attack, position.GetOccupant(attack))
                        with { PromotionPiece = promotionPiece };
                }
            }
        }
    }

    private static bool IsCastleLegal(ref readonly Position position, Castle requiredCastle, Move castle, ulong enemyAttacks)
    {
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

    private static int DoEnPassant(ref readonly Position position, ref Span<Move> moves, int count, ref byte sq, byte attack)
    {
        var (king, opponentRook, opponentQueen) = position.CurrentPlayer == Color.White
            ? (position.WhiteKing, position.BlackRooks, position.BlackQueens)
            : (position.BlackKing, position.WhiteRooks, position.WhiteQueens);

        var captureOffset = position.CurrentPlayer == Color.White ? MovePatterns.S : MovePatterns.N;
        var epCapture = (byte)(position.EnPassant + captureOffset);
        var ep = new Move(sq, attack, epCapture, position.GetOccupant(epCapture));

        var occupiedAfter = position.Occupied ^ (Squares.FromIndex(epCapture) | Squares.FromIndex(sq));
        var kingRook = MovePatterns.RookAttacks(Squares.ToIndex(king), occupiedAfter);

        var epWouldCheck = 0 != (kingRook &
            (opponentQueen | opponentRook));

        var epSavesCheck = position.Checkmask < ulong.MaxValue;
        // Double pushed pawn is checking, capture en passant.
        if (!epWouldCheck || epSavesCheck)
        {
            moves[count++] = ep;
        }
        return count;
    }

}
#if NNUE
using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class AccumulatorTests
{
    static bool initialized = false;
    [OneTimeSetUp]
    public void Init()
    {
        if (initialized) return;
        initialized = true;
        
        NNUE.Initialize("nnue_weights_16.bin");
    }

    [Test]
    public void TestAccumulatorInitialPosition()
    {
        var pos = new MutablePosition();
        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        var acc = NNUE.Accumulator.Create(pos);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output");
    }

    [Test]
    public void TestAccumulatorAfterE4()
    {
        var pos = new MutablePosition();
        var move = new Move('P', "e2", "e4");

        var acc = NNUE.Accumulator.Create(pos);

        pos.Move(in move);

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        acc.Move(ref move);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after e4");
    }

    [Test]
    public void TestAccumulatorWithCustomPosition()
    {
        var fen = "r1k5/1ppnnp2/3p4/8/3P4/2N1PN2/2P2P2/3K2R1 w - - 0 1";
        var pos = MutablePosition.FromFen(fen);

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        var acc = NNUE.Accumulator.Create(pos);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output for custom position");
    }

    [Test]
    public void TestAccumulatorAfterCapture()
    {
        var pos = new MutablePosition();
        var move1 = new Move('P', "e2", "e4");
        var move2 = new Move('p', "d7", "d5");
        var captureMove = new Move('P', "e4", "d5", 'p');

        pos.Move(in move1);
        pos.Move(in move2);

        var acc = NNUE.Accumulator.Create(pos);

        pos.Move(in captureMove);
        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        acc.Move(ref captureMove);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after capture");
    }

    [Test]
    public void TestAccumulatorAfterKnightMove()
    {
        var pos = new MutablePosition();
        var move = new Move('N', "g1", "f3");

        var acc = NNUE.Accumulator.Create(pos);

        pos.Move(in move);
        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        acc.Move(ref move);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after knight move");
    }

    [Test]
    public void TestAccumulatorAfterCastling()
    {
        // Set up a position where castling is possible
        var fen = "r3k2r/pppppppp/8/8/8/8/PPPPPPPP/R3K2R w KQkq - 0 1";
        var pos = MutablePosition.FromFen(fen);
        var castleMove = Move.Castle(Colors.White);

        var acc = NNUE.Accumulator.Create(pos);

        pos.Move(in castleMove);
        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        acc.Move(ref castleMove);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after castling");
    }

    [Test]
    public void TestAccumulatorAfterPawnPromotion()
    {
        // Set up a position where promotion is possible
        var fen = "8/P7/8/8/8/8/8/K6k w - - 0 1";
        var pos = MutablePosition.FromFen(fen);
        var promotionMove = Move.Promote('P', "a7", "a8", 'Q'); // This should be a promotion move

        var acc = NNUE.Accumulator.Create(pos);

        pos.Move(in promotionMove);
        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        acc.Move(ref promotionMove);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after pawn promotion");
    }

    [TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")]
    [TestCase("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1")]
    [TestCase("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1")]
    [TestCase("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1")]
    public void TestAccumulatorForVariousPositions(string fen)
    {
        var pos = MutablePosition.FromFen(fen);

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        var acc = NNUE.Accumulator.Create(pos);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, $"Accumulator output should match NNUE output for FEN: {fen}");
    }

    [Test]
    public void TestAccumulatorAfterMultipleMoves()
    {
        var pos = new MutablePosition();
        var moves = new[]
        {
            new Move('P', "e2", "e4"),
            new Move('p', "e7", "e5"),
            new Move('N', "g1", "f3"),
            new Move('n', "b8", "c6"),
            new Move('B', "f1", "c4"),
            new Move('b', "f8", "c5")
        };

        var acc = NNUE.Accumulator.Create(pos);

        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos.Move(in move);
            acc.Move(ref move);
        }

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator output should match NNUE output after multiple moves");
    }

    [Test]
    public void TestAccumulatorIncrementalVsFullRecalculation()
    {
        // Start with initial position
        var pos1 = new MutablePosition();
        var pos2 = new MutablePosition();

        var moves = new[]
        {
            new Move('P', "d2", "d4"),
            new Move('p', "d7", "d5"),
            new Move('N', "g1", "f3")
        };

        // Method 1: Incremental updates using accumulator
        var acc = NNUE.Accumulator.Create(pos1);
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos1.Move(in move);
            acc.Move(ref move);
        }
        var incrementalOutput = acc.Read(pos1.CurrentPlayer);

        // Method 2: Full recalculation after all moves
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos2.Move(in move);
        }
        var fullCalcAcc = NNUE.Accumulator.Create(pos2);
        var fullRecalcOutput = fullCalcAcc.Read(pos2.CurrentPlayer);

        incrementalOutput.Should().Be(fullRecalcOutput, "Incremental accumulator updates should match full recalculation");
    }

    [Test]
    public void TestAccumulatorConsistencyWithFeedForward()
    {
        var positions = new[]
        {
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
            "rnbqkb1r/pppppppp/5n2/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 1 2",
            "rnbqkb1r/pppppppp/5n2/8/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 2 2"
        };

        foreach (var fen in positions)
        {
            var pos = MutablePosition.FromFen(fen);

            // Traditional approach
            pos.FillInput(ref NNUE.input);
            var traditional = NNUE.FeedForward();

            // Accumulator approach
            var acc = NNUE.Accumulator.Create(pos);
            var accumulator = acc.Read(pos.CurrentPlayer);

            accumulator.Should().Be(traditional,
                $"Accumulator should match FeedForward for position: {fen}");
        }
    }

    [Test]
    public void TestAccumulatorWithEnPassant()
    {
        // Set up en passant situation
        var pos = new MutablePosition();
        var move1 = new Move('P', "e2", "e4");
        var move2 = new Move('p', "d7", "d5");
        var move3 = new Move('P', "e4", "e5");
        var move4 = new Move('p', "f7", "f5"); // This creates en passant opportunity

        pos.Move(in move1);
        pos.Move(in move2);
        pos.Move(in move3);
        pos.Move(in move4);

        var acc = NNUE.Accumulator.Create(pos);

        // En passant capture
        var enPassantMove = new Move('P', "e5", "f6"); // Capture en passant
        pos.Move(in enPassantMove);
        acc.Move(ref enPassantMove);

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator should handle en passant captures correctly");
    }

    [Test]
    public void TestAccumulatorPerformance()
    {
        var pos = new MutablePosition();
        var moves = new[]
        {
            new Move('P', "e2", "e4"),
            new Move('p', "e7", "e5"),
            new Move('N', "g1", "f3"),
            new Move('n', "b8", "c6"),
            new Move('B', "f1", "c4")
        };

        // Time accumulator approach
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var acc = NNUE.Accumulator.Create(pos);
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos.Move(in move);
            acc.Move(ref move);
        }
        var accResult = acc.Read(pos.CurrentPlayer);
        sw1.Stop();

        // Reset position for traditional approach
        var pos2 = new MutablePosition();
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos2.Move(in move);
        }

        // Time traditional approach
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        pos2.FillInput(ref NNUE.input);
        var traditionalResult = NNUE.FeedForward();
        sw2.Stop();

        accResult.Should().Be(traditionalResult, "Results should be identical");

        // Performance assertion (accumulator should be faster or at least comparable)
        // This is more of a documentation of performance characteristics
        Console.WriteLine($"Accumulator time: {sw1.ElapsedTicks} ticks");
        Console.WriteLine($"Traditional time: {sw2.ElapsedTicks} ticks");
    }

    [Test]
    public void TestAccumulatorEdgeCases()
    {
        // Test with minimal material
        var minimalFen = "8/8/8/8/8/8/8/K6k w - - 0 1";
        var pos = MutablePosition.FromFen(minimalFen);

        pos.FillInput(ref NNUE.input);
        var output = NNUE.FeedForward();

        var acc = NNUE.Accumulator.Create(pos);
        var accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator should work with minimal material");

        // Test with maximum material (promotion scenario)
        var maxFen = "r1bqk1nr/pppp1ppp/2n5/2b1p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4";
        pos = MutablePosition.FromFen(maxFen);

        pos.FillInput(ref NNUE.input);
        output = NNUE.FeedForward();

        acc = NNUE.Accumulator.Create(pos);
        accOutput = acc.Read(pos.CurrentPlayer);

        accOutput.Should().Be(output, "Accumulator should work with complex positions");
    }

    [Test]
    public void TestAccumulatorUndoSimpleMove()
    {
        var pos = new MutablePosition();
        var move = new Move('P', "e2", "e4");

        // Create accumulator from initial position
        var acc = NNUE.Accumulator.Create(pos);
        var initialOutput = acc.Read(pos.CurrentPlayer);

        // Make move and update accumulator
        pos.Move(in move);
        acc.Move(ref move);
        var afterMoveOutput = acc.Read(pos.CurrentPlayer);

        // Undo move and update accumulator
        pos.Undo(ref move);
        acc.Undo(ref move);
        var undoOutput = acc.Read(pos.CurrentPlayer);

        undoOutput.Should().Be(initialOutput,
            "Accumulator should return to initial state after undo");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        undoOutput.Should().Be(freshOutput, "Undo result should match fresh calculation");
    }

    [Test]
    public void TestAccumulatorUndoCapture()
    {
        var pos = new MutablePosition();
        var move1 = new Move('P', "e2", "e4");
        var move2 = new Move('p', "d7", "d5");
        var captureMove = new Move('P', "e4", "d5", 'p');

        pos.Move(in move1);
        pos.Move(in move2);

        // Create accumulator after setup moves
        var acc = NNUE.Accumulator.Create(pos);
        var beforeCaptureOutput = acc.Read(pos.CurrentPlayer);

        // Make capture and update accumulator
        pos.Move(in captureMove);
        acc.Move(ref captureMove);

        // Undo capture and update accumulator
        pos.Undo(ref captureMove);
        acc.Undo(ref captureMove);
        var undoOutput = acc.Read(pos.CurrentPlayer);

        undoOutput.Should().Be(beforeCaptureOutput, "Accumulator should return to pre-capture state after undo");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        undoOutput.Should().Be(freshOutput,
            "Undo capture result should match fresh calculation");
    }

    [Test]
    public void TestAccumulatorUndoCastling()
    {
        var fen = "r3k2r/pppppppp/8/8/8/8/PPPPPPPP/R3K2R w KQkq - 0 1";
        var pos = MutablePosition.FromFen(fen);
        var castleMove = Move.Castle(Colors.White);

        // Create accumulator before castling
        var acc = NNUE.Accumulator.Create(pos);
        var beforeCastleOutput = acc.Read(pos.CurrentPlayer);

        // Make castling move
        pos.Move(in castleMove);
        acc.Move(ref castleMove);

        // Undo castling
        pos.Undo(ref castleMove);
        acc.Undo(ref castleMove);
        var undoOutput = acc.Read(pos.CurrentPlayer);

        undoOutput.Should().Be(beforeCastleOutput, "Accumulator should return to pre-castle state after undo");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        undoOutput.Should().Be(freshOutput,
            "Undo castling result should match fresh calculation");
    }

    [Test]
    public void TestAccumulatorUndoPromotion()
    {
        var fen = "8/P7/8/8/8/8/8/K6k w - - 0 1";
        var pos = MutablePosition.FromFen(fen);
        var promotionMove = Move.Promote('P', "a7", "a8", 'Q');

        // Create accumulator before promotion
        var acc = NNUE.Accumulator.Create(pos);
        var beforePromotionOutput = acc.Read(pos.CurrentPlayer);

        // Make promotion move
        pos.Move(in promotionMove);
        acc.Move(ref promotionMove);

        // Undo promotion
        pos.Undo(ref promotionMove);
        acc.Undo(ref promotionMove);
        var undoOutput = acc.Read(pos.CurrentPlayer);

        undoOutput.Should().Be(beforePromotionOutput, "Accumulator should return to pre-promotion state after undo");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        undoOutput.Should().Be(freshOutput,
            "Undo promotion result should match fresh calculation");
    }

    [Test]
    public void TestAccumulatorUndoEnPassant()
    {
        var pos = new MutablePosition();
        var move1 = new Move('P', "e2", "e4");
        var move2 = new Move('p', "d7", "d5");
        var move3 = new Move('P', "e4", "e5");
        var move4 = new Move('p', "f7", "f5");

        pos.Move(in move1);
        pos.Move(in move2);
        pos.Move(in move3);
        pos.Move(in move4);

        // Create accumulator before en passant
        var acc = NNUE.Accumulator.Create(pos);
        var beforeEnPassantOutput = acc.Read(pos.CurrentPlayer);

        // Make en passant capture
        var enPassantMove = new Move('P', "e5", "f6");
        pos.Move(in enPassantMove);
        acc.Move(ref enPassantMove);

        // Undo en passant
        pos.Undo(ref enPassantMove);
        acc.Undo(ref enPassantMove);
        var undoOutput = acc.Read(pos.CurrentPlayer);

        undoOutput.Should().Be(beforeEnPassantOutput, "Accumulator should return to pre-en-passant state after undo");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        undoOutput.Should().Be(freshOutput, "Undo en passant result should match fresh calculation");
    }

    [Test]
    public void TestAccumulatorUndoMultipleMoves()
    {
        var pos = new MutablePosition();
        var moves = new[]
        {
            new Move('P', "e2", "e4"),
            new Move('p', "e7", "e5"),
            new Move('N', "g1", "f3"),
            new Move('n', "b8", "c6"),
            new Move('B', "f1", "c4")
        };

        // Create initial accumulator
        var acc = NNUE.Accumulator.Create(pos);
        var initialOutput = acc.Read(pos.CurrentPlayer);

        // Make all moves
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos.Move(in move);
            acc.Move(ref move);
        }

        // Undo all moves in reverse order
        for (int i = moves.Length - 1; i >= 0; i--)
        {
            var move = moves[i];
            pos.Undo(ref move);
            acc.Undo(ref move);
        }

        var finalOutput = acc.Read(pos.CurrentPlayer);

        finalOutput.Should().Be(initialOutput, "Accumulator should return to initial state after undoing all moves");

        // Verify against fresh calculation
        pos.FillInput(ref NNUE.input);
        var freshOutput = NNUE.FeedForward();
        finalOutput.Should().Be(freshOutput,
            "Final undo result should match fresh calculation of initial position");
    }

    [Test]
    public void TestAccumulatorMakeUndoSequence()
    {
        var pos = new MutablePosition();
        var moves = new[]
        {
            new Move('P', "d2", "d4"),
            new Move('p', "d7", "d5"),
            new Move('N', "g1", "f3"),
            new Move('n', "g8", "f6"),
            new Move('B', "c1", "f4")
        };

        var acc = NNUE.Accumulator.Create(pos);
        var outputs = new short[moves.Length + 1];
        outputs[0] = acc.Read(pos.CurrentPlayer);

        // Make moves and record outputs
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos.Move(in move);
            acc.Move(ref move);
            outputs[i + 1] = acc.Read(pos.CurrentPlayer);
        }

        // Undo moves and verify each state
        for (int i = moves.Length - 1; i >= 0; i--)
        {
            var move = moves[i];
            pos.Undo(ref move);
            acc.Undo(ref move);
            var undoOutput = acc.Read(pos.CurrentPlayer);

            undoOutput.Should().Be(outputs[i],
                $"Undo should restore state exactly to step {i}");

            // Also verify against fresh calculation
            pos.FillInput(ref NNUE.input);
            var freshOutput = NNUE.FeedForward();
            undoOutput.Should().Be(freshOutput,
                $"Undo result at step {i} should match fresh calculation");
        }
    }

    [Test]
    public void TestAccumulatorUndoWithDifferentPieceTypes()
    {
        var pos = new MutablePosition();
        var moves = new[]
        {
            new Move('P', "e2", "e4"),    // Pawn move
            new Move('N', "g1", "f3"),    // Knight move
            new Move('B', "f1", "c4"),    // Bishop move
            new Move('Q', "d1", "h5"),    // Queen move
            new Move('K', "e1", "f1")     // King move
        };

        var acc = NNUE.Accumulator.Create(pos);
        var checkpoints = new short[moves.Length + 1];
        checkpoints[0] = acc.Read(pos.CurrentPlayer);

        // Make moves and checkpoint each state
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            pos.Move(in move);
            acc.Move(ref move);
            checkpoints[i + 1] = acc.Read(pos.CurrentPlayer);
        }

        // Test undoing each individual move type
        for (int i = moves.Length - 1; i >= 0; i--)
        {
            var move = moves[i];
            pos.Undo(ref move);
            acc.Undo(ref move);
            var undoOutput = acc.Read(pos.CurrentPlayer);

            undoOutput.Should().Be(checkpoints[i],
                $"Undo of {move.FromPieceType} move should restore previous state");
        }
    }

    [Test]
    public void TestAccumulatorComplexUndoScenario()
    {
        // This test simulates a complex game scenario with multiple move types
        var pos = new MutablePosition();
        var acc = NNUE.Accumulator.Create(pos);

        var scenario = new[]
        {
            ("opening", new Move('P', "e2", "e4")),
            ("response", new Move('p', "e7", "e5")),
            ("development", new Move('N', "g1", "f3")),
            ("attack", new Move('p', "d7", "d6")),
            ("development2", new Move('B', "f1", "c4")),
            ("defense", new Move('n', "g8", "f6")),
            ("castle_prep", new Move('K', "e1", "f1")),  // Manual king move instead of castle for this test
            ("counter", new Move('b', "c8", "g4"))
        };

        var stateHistory = new List<(string name, short output)>();
        stateHistory.Add(("initial", acc.Read(pos.CurrentPlayer)));

        // Execute moves and record states
        for (int i = 0; i < scenario.Length; i++)
        {
            var (name, move) = scenario[i];
            pos.Move(in move);
            acc.Move(ref move);
            var output = acc.Read(pos.CurrentPlayer);
            stateHistory.Add((name, output));

            // Verify accumulator matches traditional calculation at each step
            pos.FillInput(ref NNUE.input);
            var traditional = NNUE.FeedForward();
            output.Should().Be(traditional,
                $"Accumulator should match traditional at step: {name}");
        }

        // Now undo everything and verify we get back to previous states
        for (int i = scenario.Length - 1; i >= 0; i--)
        {
            var (name, move) = scenario[i];
            pos.Undo(ref move);
            acc.Undo(ref move);
            var undoOutput = acc.Read(pos.CurrentPlayer);

            var expectedOutput = stateHistory[i].output;
            undoOutput.Should().Be(expectedOutput,
                $"Undo should restore state before '{name}' step");

            // Also verify against fresh calculation
            pos.FillInput(ref NNUE.input);
            var freshOutput = NNUE.FeedForward();
            undoOutput.Should().Be(freshOutput,
                $"Undo result should match fresh calculation at step before '{name}'");
        }
    }

    [Test]
    public void TestAccumulatorUndoStressTest()
    {
        // Stress test with many moves and undos to check for accumulation errors
        var pos = new MutablePosition();
        var acc = NNUE.Accumulator.Create(pos);
        var initialOutput = acc.Read(pos.CurrentPlayer);

        var moves = new List<Move>();
        var random = new Random(12345); // Fixed seed for reproducibility

        // Generate a series of legal moves (simplified for this test)
        var testMoves = new[]
        {
            new Move('P', "a2", "a3"), new Move('P', "a3", "a4"),
            new Move('P', "b2", "b3"), new Move('P', "b3", "b4"),
            new Move('P', "c2", "c3"), new Move('P', "c3", "c4"),
            new Move('P', "d2", "d3"), new Move('P', "d3", "d4"),
            new Move('P', "e2", "e3"), new Move('P', "e3", "e4"),
        };

        // Randomly select and apply moves
        for (int i = 0; i < 5; i++)
        {
            var move = testMoves[random.Next(testMoves.Length)];
            try
            {
                pos.Move(in move);
                acc.Move(ref move);
                moves.Add(move);
            }
            catch
            {
                // Skip illegal moves for this simplified test
            }
        }

        // Undo all moves
        for (int i = moves.Count - 1; i >= 0; i--)
        {
            var move = moves[i];
            pos.Undo(ref move);
            acc.Undo(ref move);
        }

        var finalOutput = acc.Read(pos.CurrentPlayer);
        finalOutput.Should().Be(initialOutput,
            "After stress test make/undo sequence, should return to initial state");
    }
}
#endif
using Lolbot.Core;

namespace Lolbot.Tests;

public class TrainingDataTests
{
    private static readonly TranspositionTable tt = new TranspositionTable();

    [TestCase("2kr4/3q4/8/1n3n2/8/3B4/8/3K4 w - - 0 1", false)]
    [TestCase("8/p7/5k2/3p2p1/3r2P1/1P1NRP2/P4K1P/8 w - - 0 1", false)]
    [TestCase("8/p1p3pp/4Q3/5q2/6kP/P1P3B1/6PK/3r1r2 w - - 1 32", false)]
    [TestCase("2k5/8/2p5/1p3p2/8/n1P1N2b/3P3P/4B2K w - - 0 1", true)]
    [TestCase("3rq3/2nrkp2/ppp1p3/4P3/5P2/1P3NP1/PBP1Q2P/1K2R3 w - - 0 1", true)]
    [TestCase("8/1p6/5k2/8/1K6/8/1P6/8 w - - 0 1", true)]
    [TestCase("8/8/5k2/8/1K6/3N4/8/8 w - - 0 1", true)]
    [TestCase("8/8/5k2/8/1K6/3b4/8/8 w - - 0 1", true)]
    [TestCase("4r1k1/5p2/5Ppp/8/1KQ5/8/8/8 b - - 0 1", true)]
    public void Should_Test_Position_Correctly(string fen, bool expected)
    {
        var position = MutablePosition.FromFen(fen);
        var game = new Game(position, []);
        var search = new Search(game, tt, [new int[4096], new int[4096]]);

        var (stat, _, score, result) = GenTrainingData.TestPosition(search, position);

        result.Should().Be(expected);
    }

    [Test]
    public void Should_Generate_Training_Data()
    {
        var inputPath = "./Testdata/lichess-3.pgn";
        var outputPath = "./Testdata/training-data-test-output.bin";

        GenTrainingData.Generate(inputPath, outputPath).Wait();

        File.Exists(outputPath).Should().BeTrue();
        File.Delete(outputPath);
    }

    
    [Test]
    public async Task Should_Generate_Training_Data2()
    {
        var inputPath = "./Testdata/lichess-multi.pgn";
        var outputPath = "./Testdata/training-data-test-multi.bin";

        await GenTrainingData.Generate(inputPath, outputPath);

        File.Exists(outputPath).Should().BeTrue();
        File.Delete(outputPath);
    }
}
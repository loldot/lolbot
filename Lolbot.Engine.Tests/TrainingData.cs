using Lolbot.Core;

namespace Lolbot.Tests;

public class TrainingDataTests
{
    private static readonly TranspositionTable tt = new TranspositionTable();

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
    [Explicit]
    public async Task Should_Generate_Training_Data2()
    {
        var inputPath = "./Testdata/lichess-multi.pgn";
        var outputPath = "./Testdata/training-data-test-multi.bin";

        await GenTrainingData.Generate(inputPath, outputPath);

        File.Exists(outputPath).Should().BeTrue();
        File.Delete(outputPath);
    }
}
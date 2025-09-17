using Lolbot.Core;

namespace Lolbot.Tests;

public class Lmr
{
    static readonly float[] LogTable = new float[256];

    static Lmr()
    {
        for (int i = 1; i < 256; i++)
        {
            LogTable[i] = (int)(1024 * Math.Log(i));
        }
    }
    [Test]
    public void Should_Calculate_Lmr_Correctly()
    {
        Console.WriteLine(string.Join(", ", LogTable));
        static int Lmr(int depth, int move) => 1 + (int)(LogTable[depth] * LogTable[move + 1] / 2);

        for (byte depth = 1; depth <= 36; depth++)
        {
            for (byte move = 0; move < 128; move++)
            {
                var result = Lmr(depth, move);
                var resultLn = LmrLn(depth, move);

                result.Should().Be(resultLn, $"Lmr and LmrLn for depth {depth} and move {move} should be the same");
            }
        }
    }

    public static int LmrLn(int depth, int move) => 1 + (int)(Math.Log(depth) * Math.Log(1 + move) / 2);
}
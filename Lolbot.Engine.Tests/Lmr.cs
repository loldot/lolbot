using Lolbot.Core;

namespace Lolbot.Tests;

public class Lmr
{
    static readonly int[] LogTable = new int[256];

    static Lmr()
    {
        for (int i = 1; i < 256; i++)
        {
            LogTable[i] = (int)MathF.Round(128f * MathF.Log(i));
        }
    }
    [Test]
    public void Should_Calculate_Lmr_Correctly()
    {
        static int Lmr(byte depth, byte move) => 1 + ((LogTable[depth] * LogTable[move + 1]) >> 15);

        for (byte depth = 1; depth <= 36; depth++)
        {
            for (byte move = 0; move < 128; move++)
            {
                var result = Lmr(depth, move);
                var resultLn = LmrLn(depth, move);

                // Console.WriteLine($"Depth: {depth}, Move: {move}, {LogTable[depth]}, {LogTable[move + 1]}, Result: {result}, ResultLn: {resultLn}");

                result.Should().BeCloseTo(resultLn, 1, $"Lmr and LmrLn for depth {depth} and move {move} should be the same");
            }
        }
    }

    public static int LmrLn(int depth, int move) => 1 + (int)(Math.Log(depth) * Math.Log(1 + move) / 2);
}
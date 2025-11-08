
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lolbot.Core;

public static class NNUE
{
    const int Scale = 410;
    const short QA = 255;
    const short QB = 64;
    const short HiddenSize = 16;
    const int InputSize = 768; // 768 piece features + 1 side-to-move feature

    public readonly struct Accumulator
    {
        const int white = 1, black = 0;
        private readonly short[] v = new short[HiddenSize];
        public Accumulator()
        {
        }

        public static Accumulator Create(MutablePosition pos)
        {
            var acc = new Accumulator();
            acc.Reevaluate(pos);
            return acc;
        }

        public void Reevaluate(MutablePosition pos)
        {
            hiddenBias.CopyTo(v, 0);

            for (int piece = 0; piece < 6; piece++)
            {
                var p = (PieceType)(piece + 1);
                var pw = pos[Colors.White, p];
                var pb = pos[Colors.Black, p];

                while (pb != 0)
                {
                    var sq = Bitboards.PopLsb(ref pb);
                    for (int h = 0; h < HiddenSize; h++)
                    {
                        v[h] += hiddenWeights[h * InputSize + FeatureIndex(black, p, sq)];
                    }
                }
                while (pw != 0)
                {
                    // (color * 6 + piece) * 64 + square
                    var sq = Bitboards.PopLsb(ref pw);
                    for (int h = 0; h < HiddenSize; h++)
                    {
                        v[h] += hiddenWeights[h * InputSize + FeatureIndex(white, p, sq)];
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FeatureIndex(byte color, PieceType piece, byte square)
        {
            return ((color * 6) + ((int)piece - 1)) * 64 + square;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FeatureIndexBlack(byte color, PieceType piece, byte square)
        {
            return ((color ^ 1) * 6 + ((int)piece - 1)) * 64 + (square ^ 63);
        }

        public void Move(ref readonly Move m)
        {
            var toPiece = m.PromotionPieceType == PieceType.None ? m.FromPieceType : m.PromotionPieceType;

            var w_From = FeatureIndex(m.Color, m.FromPieceType, m.FromIndex);
            // var b_From = FeatureIndexBlack(m.Color, m.FromPieceType, m.FromIndex);

            var w_To = FeatureIndex(m.Color, toPiece, m.ToIndex);
            // var b_To = FeatureIndexBlack(m.Color, toPiece, m.ToIndex);

            var w_capture = m.CastleFlag != 0
                ? FeatureIndex(m.Color, m.CapturePieceType, m.CaptureIndex)
                : FeatureIndex((byte)(m.Color ^ 1), m.CapturePieceType, m.CaptureIndex);

            // var b_castle_from = FeatureIndexBlack(m.Color, m.CapturePieceType, m.CaptureIndex);

            var w_castle = FeatureIndex(m.Color, m.CapturePieceType, m.CastleIndex);
            // var b_castle_to = FeatureIndexBlack(m.Color, m.CapturePieceType, m.CastleIndex);

            for (int h = 0; h < HiddenSize; h++)
            {
                int ix = h * InputSize;
                v[h] -= hiddenWeights[ix + w_From];
                v[h] += hiddenWeights[ix + w_To];

                if (m.CapturePieceType != PieceType.None)
                {
                    v[h] -= hiddenWeights[ix + w_capture];
                }
                if (m.CastleFlag != 0)
                {
                    v[h] += hiddenWeights[ix + w_castle];
                }
            }
        }

        public void Undo(ref readonly Move m)
        {
            var toPiece = m.PromotionPieceType == PieceType.None ? m.FromPieceType : m.PromotionPieceType;

            var w_From = FeatureIndex(m.Color, m.FromPieceType, m.FromIndex);
            var w_to = FeatureIndex(m.Color, toPiece, m.ToIndex);

            var w_capture = m.CastleFlag != 0
                ? FeatureIndex(m.Color, m.CapturePieceType, m.CaptureIndex)
                : FeatureIndex((byte)(m.Color ^ 1), m.CapturePieceType, m.CaptureIndex);

            var w_castle = FeatureIndex(m.Color, m.CapturePieceType, m.CastleIndex);

            for (int h = 0; h < HiddenSize; h++)
            {
                var ix = h * InputSize;

                v[h] += hiddenWeights[ix + w_From];
                v[h] -= hiddenWeights[ix + w_to];

                if (m.CapturePieceType != PieceType.None)
                {
                    v[h] += hiddenWeights[ix + w_capture];
                }

                if (m.CastleFlag != 0)
                {
                    v[h] -= hiddenWeights[ix + w_castle];
                }
            }
        }

        public short Read(Colors sideToMove)
        {
            short[] hiddenValues = new short[HiddenSize];
            v.CopyTo(hiddenValues, 0);


            // Output calculation
            int output = outputBias;
            for (int i = 0; i < HiddenSize; i++)
            {
                output += ClipRelu(hiddenValues[i]) * outputWeights[i];
            }
            var eval = Scale * output / (QA * QB);

            return (short)(sideToMove == Colors.White ? eval : -eval);
        }
        
        public void CopyTo(Accumulator target)
        {
            v.CopyTo(target.v, 0);
        }
    }


    internal static short[] input = new short[InputSize];
    static short[] hidden = new short[HiddenSize];

    static short[] hiddenWeights = new short[HiddenSize * InputSize];
    static float[] hiddenWeightsf = new float[HiddenSize * InputSize];
    static short[] hiddenBias = new short[HiddenSize];
    static float[] hiddenBiasf = new float[HiddenSize];
    static int[] outputWeights = new int[HiddenSize];
    static float[] outputWeightsf = new float[HiddenSize];
    static int outputBias = 0;
    static float outputBiasf = 0;

    public static void Initialize(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));

        // We need to determine the hidden layer size from the file
        // The file structure is: hidden_weights, hidden_bias, output_weights, output_bias
        long fileLength = reader.BaseStream.Length;
        int floatsInFile = (int)(fileLength / sizeof(float));

        // Read hidden weights
        for (int i = 0; i < hiddenWeights.Length; i++)
        {
            hiddenWeightsf[i] = reader.ReadSingle();
            hiddenWeights[i] = (short)Math.Clamp(QA * hiddenWeightsf[i], -127, 127);
        }

        // Read hidden bias
        for (int i = 0; i < hiddenBias.Length; i++)
        {
            hiddenBiasf[i] = reader.ReadSingle();
            hiddenBias[i] = (short)Math.Clamp(QA * hiddenBiasf[i], -127, 127);
        }

        // Read output weights
        for (int i = 0; i < outputWeights.Length; i++)
        {
            outputWeightsf[i] = reader.ReadSingle();
            outputWeights[i] = (int)(QB * outputWeightsf[i]);
        }

        // Read output bias
        outputBiasf = reader.ReadSingle();
        outputBias = (int)(QA * QB * outputBiasf);
    }


    internal static short FeedForward()
    {
        // Input to hidden layer (single layer)
        for (int i = 0; i < HiddenSize; i++)
        {
            hidden[i] = hiddenBias[i];
            for (int j = 0; j < InputSize; j++)
            {
                hidden[i] += (short)(input[j] * hiddenWeights[i * InputSize + j]);
            }
            hidden[i] = ClipRelu(hidden[i]);
        }

        // Output calculation with sigmoid activation
        float output = outputBias;
        for (int i = 0; i < HiddenSize; i++)
        {
            output += hidden[i] * outputWeights[i];
        }

        return (short)((Scale * output) / (QA * QB));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static short ClipRelu(short x) => Math.Clamp(x, (short)0, QA);
}
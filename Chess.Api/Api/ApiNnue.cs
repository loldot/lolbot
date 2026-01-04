namespace Lolbot.Api;

public class ApiNnue
{
    public required float[] HiddenActivations { get; set; }
    public required float[] OutputWeights { get; set; }
    public float OutputBias { get; set; }
    public short Evaluation { get; set; }
}

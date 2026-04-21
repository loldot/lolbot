using static System.Math;

namespace Lolbot.Core;

public class Options
{
    public int MaxThreads { get; } = Environment.ProcessorCount;
    public int MinThreads { get; } = 1;

    private int threads;

    public Options()
    {
        threads = Max(MinThreads, MaxThreads / 2);
    }

    [UciOption("Threads", "spin", nameof(MaxThreads), nameof(MinThreads))]
    public int Threads
    {
        get => threads;
        set => threads = Clamp(value, MinThreads, MaxThreads);
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class UciOption : Attribute
{
    public string Name { get; }
    public string Type { get; }
    public string? MaxValuePropName { get; }
    public string? MinValuePropName { get; }

    public UciOption(string name, string type, string? maxValuePropName = null, string? minValuePropName = null)
    {
        Name = name;
        Type = type;
        MaxValuePropName = maxValuePropName;
        MinValuePropName = minValuePropName;

    }
}
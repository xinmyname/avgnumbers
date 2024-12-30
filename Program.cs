using System.Diagnostics;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<SumAndAverage>();

var sumAndAverage = new SumAndAverage();

Console.WriteLine();
Console.WriteLine("File read time :     {0} ms", SumAndAverage.ElapsedMillis);
Console.WriteLine("Sum() :              {0}", sumAndAverage.Sum());
Console.WriteLine("ParallelSum() :      {0}", sumAndAverage.ParallelSum());
Console.WriteLine("TaskParallelSum() :  {0}", sumAndAverage.TaskParallelSum());
Console.WriteLine("RestrainedTPLSum() : {0}", sumAndAverage.RestrainedTPLSum(16));
Console.WriteLine("SIMDDoubleSum() :    {0}", sumAndAverage.SIMDDoubleSum());
Console.WriteLine("SIMDFloatSum() :     {0}", sumAndAverage.SIMDFloatSum());
Console.WriteLine();

[MemoryDiagnoser]
public class SumAndAverage
{
    static readonly List<double[]> Numbers = new();
    static readonly List<float[]> Floats = new();
    static readonly double[] Sums;
    public static readonly long ElapsedMillis;

    static SumAndAverage()
    {
        Stopwatch sw = Stopwatch.StartNew();
        string numbersFilePath = Path.Combine(Path.GetTempPath(), "numbers.txt");
        
        foreach (string line in File.ReadAllLines(numbersFilePath))
        {
            Numbers.Add(line.Split(',').Select(double.Parse).ToArray());
            Floats.Add(line.Split(',').Select(float.Parse).ToArray());
        }
        sw.Stop();
        ElapsedMillis = sw.ElapsedMilliseconds;

        Sums = new double[Numbers.Count];
    }

    [Benchmark]
    public double Sum()
    {
        Array.Clear(Sums);

        for (int i = 0; i < Numbers.Count; i++)
            Sums[i] = Numbers[i].Sum();

        return Sums.Average();
    }

    [Benchmark]
    public double ParallelSum()
    {
        Array.Clear(Sums);

        Parallel.For(0, Numbers.Count, i =>
        {
            Sums[i] = Numbers[i].Sum();
        });

        return Sums.Average();
    }

    [Benchmark]
    public double TaskParallelSum()
    {
        Array.Clear(Sums);

        Task[] tasks = new Task[Numbers.Count];
        for (int i = 0; i < Numbers.Count; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() => Sums[index] = Numbers[index].Sum());
        }

        Task.WaitAll(tasks);
        return Sums.Average();
    }

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(16)]
    [Arguments(32)]
    public double RestrainedTPLSum(int numTasks)
    {
        Array.Clear(Sums);

        var tasks = new Task[numTasks];

        for (int t = 0; t < numTasks; t++)
        {
            int start = t * Numbers.Count / numTasks;
            int end = (t + 1) * Numbers.Count / numTasks;

            tasks[t] = Task.Run(() =>
            {
                for (int i = start; i < end; i++)
                    Sums[i] = Numbers[i].Sum();
            });
        }

        Task.WaitAll(tasks);
        return Sums.Average();
    }

    [Benchmark]
    public double SIMDDoubleSum()
    {
        if (!Vector.IsHardwareAccelerated)
            throw new NotSupportedException("SIMD is not supported on this hardware");

        Array.Clear(Sums);

        int vcount = Vector<double>.Count;
  
        for (int i = 0; i < Numbers.Count; i++)
        {
            for (int n = 0; n < Numbers[i].Length; n += vcount)
            {
                var vsum = new Vector<double>(Numbers[i], n);
                for (int o = 0; o < vcount; o++)
                    Sums[i] += vsum[o];
            }
        }

        return Sums.Average();
    }

    [Benchmark]
    public double SIMDFloatSum()
    {
        if (!Vector.IsHardwareAccelerated)
            throw new NotSupportedException("SIMD is not supported on this hardware");

        int vcount = Vector<float>.Count;  
        var vsum = Vector<float>.Zero;

        for (int i = 0; i < Numbers.Count; i++)
        {
            for (int n = 0; n < Floats[i].Length; n += vcount)
                vsum += new Vector<float>(Floats[i], n);
        }

        double sum = 0.0;
        for (int o = 0; o < vcount; o++)
            sum += vsum[o];

        return sum / Numbers.Count;
    }
}

namespace ShopSupplyPetri.Core;

// середнє, СКВ, min/max з вагою Δt, бо інтервали між подіями різні
public sealed class TimeWeightedStat
{
    private double _sum;
    private double _sumSq;

    public double TotalTime { get; private set; }
    public double Min { get; private set; } = double.PositiveInfinity;
    public double Max { get; private set; } = double.NegativeInfinity;

    public void Add(double value, double dt)
    {
        if (dt <= 0) return;
        _sum += value * dt;
        _sumSq += value * value * dt;
        TotalTime += dt;
        if (value < Min) Min = value;
        if (value > Max) Max = value;
    }

    public double Mean => TotalTime > 0 ? _sum / TotalTime : 0.0;

    public double Variance
    {
        get
        {
            if (TotalTime <= 0) return 0.0;
            double m = Mean;
            double v = _sumSq / TotalTime - m * m;
            return v < 0 ? 0.0 : v;
        }
    }

    public double StdDev => Math.Sqrt(Variance);

    public void Reset()
    {
        _sum = 0; _sumSq = 0; TotalTime = 0;
        Min = double.PositiveInfinity; Max = double.NegativeInfinity;
    }
}

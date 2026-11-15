namespace ShopSupplyPetri.Core;

public interface IDelay
{
    double Next(Random rnd);
    string Notation { get; }
    double Mean { get; }
}

public sealed class DeterministicDelay : IDelay
{
    private readonly double _value;
    public DeterministicDelay(double value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        _value = value;
    }
    public double Next(Random rnd) => _value;
    public string Notation => _value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    public double Mean => _value;
}

public sealed class ExponentialDelay : IDelay
{
    private readonly double _mean;
    public ExponentialDelay(double mean)
    {
        if (mean <= 0) throw new ArgumentOutOfRangeException(nameof(mean));
        _mean = mean;
    }
    // метод оберненої функції
    public double Next(Random rnd) => -_mean * Math.Log(1.0 - rnd.NextDouble());
    public string Notation => $"exp({_mean.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    public double Mean => _mean;
}

public sealed class UniformDelay : IDelay
{
    private readonly double _mean;
    private readonly double _halfWidth;
    public UniformDelay(double mean, double halfWidth)
    {
        if (halfWidth < 0 || mean - halfWidth < 0) throw new ArgumentOutOfRangeException(nameof(halfWidth));
        _mean = mean;
        _halfWidth = halfWidth;
    }
    // рівномірно на (d - s; d + s)
    public double Next(Random rnd) => _mean - _halfWidth + 2.0 * _halfWidth * rnd.NextDouble();
    public string Notation =>
        $"unif({_mean.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {_halfWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    public double Mean => _mean;
}

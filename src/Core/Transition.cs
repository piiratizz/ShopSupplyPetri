namespace ShopSupplyPetri.Core;

public sealed class Transition
{
    private readonly List<Arc> _inputs = new();
    private readonly List<Arc> _outputs = new();
    // моменти виходу маркерів, по одному на кожен зайнятий канал
    private readonly List<double> _exitTimes = new();

    public int Id { get; }
    public string Name { get; }
    public IDelay Delay { get; }
    public int Priority { get; }
    public double Probability { get; }

    public long EntryCount { get; private set; }
    public long ExitCount { get; private set; }

    public IReadOnlyList<Arc> Inputs => _inputs;
    public IReadOnlyList<Arc> Outputs => _outputs;
    public IReadOnlyList<double> ExitTimes => _exitTimes;

    public int Buffer => _exitTimes.Count;

    public Transition(int id, string name, IDelay delay, int priority = 0, double probability = 1.0)
    {
        Id = id;
        Name = name;
        Delay = delay;
        Priority = priority;
        Probability = probability;
    }

    public void AddInput(Place place, int multiplicity) => _inputs.Add(new Arc(place, multiplicity));
    public void AddOutput(Place place, int multiplicity) => _outputs.Add(new Arc(place, multiplicity));

    // перехід можна запустити, якщо в усіх вхідних позиціях вистачає маркерів
    public bool IsEnabled()
    {
        foreach (var arc in _inputs)
            if (arc.Place.Marking < arc.Multiplicity) return false;
        return true;
    }

    public double NextExitTime => _exitTimes.Count == 0 ? double.PositiveInfinity : _exitTimes.Min();

    public double Enter(double currentTime, Random rnd)
    {
        foreach (var arc in _inputs)
            arc.Place.Marking -= arc.Multiplicity;
        double exitTime = currentTime + Delay.Next(rnd);
        _exitTimes.Add(exitTime);
        EntryCount++;
        return exitTime;
    }

    // виходять одразу всі канали, у яких час виходу збігся з t
    public int Exit(double currentTime)
    {
        int n = _exitTimes.RemoveAll(x => x == currentTime);
        if (n == 0) return 0;
        foreach (var arc in _outputs)
            arc.Place.Marking += arc.Multiplicity * n;
        ExitCount += n;
        return n;
    }

    public void Reset()
    {
        _exitTimes.Clear();
        EntryCount = 0;
        ExitCount = 0;
    }

    public override string ToString() => $"{Name}[{Buffer}]";
}

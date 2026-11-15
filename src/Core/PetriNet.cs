namespace ShopSupplyPetri.Core;

public sealed class PetriNet
{
    private readonly List<Place> _places = new();
    private readonly List<Transition> _transitions = new();

    public string Name { get; }
    public IReadOnlyList<Place> Places => _places;
    public IReadOnlyList<Transition> Transitions => _transitions;

    public PetriNet(string name) => Name = name;

    public Place AddPlace(string name, int initialMarking)
    {
        var p = new Place(_places.Count, name, initialMarking);
        _places.Add(p);
        return p;
    }

    public Transition AddTransition(string name, IDelay delay, int priority = 0, double probability = 1.0)
    {
        var t = new Transition(_transitions.Count, name, delay, priority, probability);
        _transitions.Add(t);
        return t;
    }

    public Place GetPlace(string name) => _places.First(p => p.Name == name);
    public Transition GetTransition(string name) => _transitions.First(t => t.Name == name);

    public void Reset()
    {
        foreach (var p in _places) p.Reset();
        foreach (var t in _transitions) t.Reset();
    }

    public void Validate()
    {
        foreach (var t in _transitions)
        {
            if (t.Inputs.Count == 0)
                throw new InvalidOperationException($"Перехід «{t.Name}» не має вхідних позицій.");
            if (t.Probability <= 0 || t.Probability > 1)
                throw new InvalidOperationException($"Перехід «{t.Name}»: ймовірність запуску має бути в (0;1].");
        }
    }

    public string MarkingToString() => string.Join("; ", _places.Select(p => $"{p.Name}={p.Marking}"));
}

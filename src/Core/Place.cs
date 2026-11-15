namespace ShopSupplyPetri.Core;

public sealed class Place
{
    public int Id { get; }
    public string Name { get; }
    public int InitialMarking { get; }
    public int Marking { get; set; }

    public Place(int id, string name, int initialMarking)
    {
        if (initialMarking < 0) throw new ArgumentOutOfRangeException(nameof(initialMarking));
        Id = id;
        Name = name;
        InitialMarking = initialMarking;
        Marking = initialMarking;
    }

    public void Reset() => Marking = InitialMarking;

    public override string ToString() => $"{Name}={Marking}";
}

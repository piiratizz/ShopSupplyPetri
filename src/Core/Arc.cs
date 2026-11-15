namespace ShopSupplyPetri.Core;

public sealed class Arc
{
    public Place Place { get; }
    public int Multiplicity { get; }

    public Arc(Place place, int multiplicity)
    {
        if (multiplicity <= 0) throw new ArgumentOutOfRangeException(nameof(multiplicity));
        Place = place;
        Multiplicity = multiplicity;
    }
}

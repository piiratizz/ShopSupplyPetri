using ShopSupplyPetri.Core;

namespace ShopSupplyPetri.Model;

public enum ReplenishmentStrategy
{
    UpToFull,
    FixedQuantity
}

public sealed record ModelParameters
{
    public int Capacity { get; init; } = 20;
    public int InitialStock { get; init; } = 20;
    public int ReorderPoint { get; init; } = 3;
    public ReplenishmentStrategy Strategy { get; init; } = ReplenishmentStrategy.UpToFull;
    public int FixedQuantity { get; init; } = 3;

    public IDelay Demand { get; init; } = new ExponentialDelay(60.0);
    public IDelay RequestFormation { get; init; } = new DeterministicDelay(60.0);
    public IDelay Kitting { get; init; } = new UniformDelay(60.0, 20.0);
    public IDelay Delivery { get; init; } = new UniformDelay(60.0, 5.0);

    // «до 20»: Q = S - s, «на 3»: Q = 3
    public int OrderQuantity => Strategy == ReplenishmentStrategy.UpToFull
        ? Capacity - ReorderPoint
        : FixedQuantity;

    // нестача S - s означає, що запас (разом із замовленим) дійшов до s
    public int TriggerDeficit => Capacity - ReorderPoint;

    public void Validate()
    {
        if (Capacity <= 0) throw new ArgumentException("Місткість складу має бути додатною.");
        if (ReorderPoint < 0 || ReorderPoint >= Capacity) throw new ArgumentException("Точка замовлення має бути в [0; S).");
        if (InitialStock < 0 || InitialStock > Capacity) throw new ArgumentException("Початковий запас має бути в [0; S].");
        if (OrderQuantity <= 0 || OrderQuantity > TriggerDeficit)
            throw new ArgumentException("Розмір замовлення має бути в [1; S - s].");
    }

    public string StrategyName => Strategy == ReplenishmentStrategy.UpToFull ? $"до {Capacity}" : $"на {FixedQuantity}";
}

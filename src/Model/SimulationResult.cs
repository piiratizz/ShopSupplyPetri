namespace ShopSupplyPetri.Model;

public sealed record SimulationResult
{
    public double TimeModeling { get; init; }
    public double WarmUp { get; init; }
    public double StatPeriod => TimeModeling - WarmUp;

    public double IdleProbability { get; init; }
    public double MeanStock { get; init; }
    public double StdStock { get; init; }
    public double MinStock { get; init; }
    public double MaxStock { get; init; }
    public double Load { get; init; }
    public double MeanOnOrder { get; init; }

    public long Issued { get; init; }
    public long Orders { get; init; }
    public long Deliveries { get; init; }
    public double ThroughputPerHour { get; init; }
    public double OrdersPerDay { get; init; }

    public int IdleEpisodes { get; init; }
    public double MeanIdleDuration { get; init; }
    public double MaxIdleDuration { get; init; }
    public double ShareOfNeedsWaiting { get; init; }
    public double ShareOfCyclesWithIdle { get; init; }
    public double MeanCycle { get; init; }

    public long InvariantViolations { get; init; }
    public long InvariantChecks { get; init; }
    public long Events { get; init; }
}

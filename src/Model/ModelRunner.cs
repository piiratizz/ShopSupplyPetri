using ShopSupplyPetri.Core;

namespace ShopSupplyPetri.Model;

public static class ModelRunner
{
    public static SimulationResult Run(ModelParameters p, double timeModeling, double warmUp, int seed,
        Action<ShopStatistics>? configure = null, TextWriter? protocol = null, int protocolEvents = 60)
    {
        var model = new ShopSupplyModel(p);
        var stats = new ShopStatistics(model);
        configure?.Invoke(stats);

        var sim = new Simulator(model.Net, seed) { Protocol = protocol, MaxProtocolEvents = protocolEvents };
        sim.AddObserver(stats);
        sim.Run(timeModeling, warmUp);
        stats.Finish();

        return BuildResult(p, stats, timeModeling, warmUp, sim.EventCount);
    }

    public static (SimulationResult result, ShopStatistics stats) RunDetailed(ModelParameters p, double timeModeling,
        double warmUp, int seed, double traceUntil = 0)
    {
        ShopStatistics? captured = null;
        var r = Run(p, timeModeling, warmUp, seed, s => { s.TraceUntil = traceUntil; captured = s; });
        return (r, captured!);
    }

    private static SimulationResult BuildResult(ModelParameters p, ShopStatistics s, double tMod, double warm, long events)
    {
        double period = tMod - warm;
        var idle = s.IdleEpisodes;
        var deliveries = s.DeliveryTimes;
        double meanCycle = deliveries.Count > 1
            ? (deliveries[^1] - deliveries[0]) / (deliveries.Count - 1)
            : double.NaN;

        return new SimulationResult
        {
            TimeModeling = tMod,
            WarmUp = warm,
            IdleProbability = s.IdleStat.Mean,
            MeanStock = s.StockStat.Mean,
            StdStock = s.StockStat.StdDev,
            MinStock = s.StockStat.Min,
            MaxStock = s.StockStat.Max,
            Load = s.StockStat.Mean / p.Capacity,
            MeanOnOrder = s.OnOrderStat.Mean,
            Issued = s.IssuedInPeriod,
            Orders = s.OrdersInPeriod,
            Deliveries = s.DeliveriesInPeriod,
            ThroughputPerHour = s.IssuedInPeriod / period * 60.0,
            OrdersPerDay = s.OrdersInPeriod / period * 1440.0,
            IdleEpisodes = idle.Count,
            MeanIdleDuration = idle.Count > 0 ? idle.Average() : 0.0,
            MaxIdleDuration = idle.Count > 0 ? idle.Max() : 0.0,
            ShareOfNeedsWaiting = s.IssuedInPeriod > 0 ? (double)idle.Count / s.IssuedInPeriod : 0.0,
            ShareOfCyclesWithIdle = s.DeliveriesInPeriod > 0 ? (double)idle.Count / s.DeliveriesInPeriod : 0.0,
            MeanCycle = meanCycle,
            InvariantViolations = s.InvariantViolations,
            InvariantChecks = s.InvariantChecks,
            Events = events
        };
    }
}

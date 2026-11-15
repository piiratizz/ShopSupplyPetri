using ShopSupplyPetri.Core;

namespace ShopSupplyPetri.Model;

public sealed class ShopStatistics : ISimulationObserver
{
    private readonly ShopSupplyModel _model;
    private readonly List<double> _idleEpisodes = new();
    private readonly List<double> _deliveryTimes = new();
    private double _currentIdle;
    private bool _inIdle;
    private double _statStart = double.NaN;

    public TimeWeightedStat StockStat { get; } = new();
    public TimeWeightedStat IdleStat { get; } = new();
    public TimeWeightedStat OnOrderStat { get; } = new();

    public long IssuedInPeriod { get; private set; }
    public long OrdersInPeriod { get; private set; }
    public long DeliveriesInPeriod { get; private set; }
    public long InvariantViolations { get; private set; }
    public long InvariantChecks { get; private set; }

    public IReadOnlyList<double> IdleEpisodes => _idleEpisodes;
    public IReadOnlyList<double> DeliveryTimes => _deliveryTimes;

    public List<(double t, int stock, int onOrder, int idle)> Trace { get; } = new();
    public double TraceUntil { get; set; } = 0;

    public ShopStatistics(ShopSupplyModel model) => _model = model;

    public void OnInterval(PetriNet net, double from, double to)
    {
        if (double.IsNaN(_statStart)) _statStart = from;
        double dt = to - from;
        int stock = _model.Stock.Marking;
        // маркер у P2 на ненульовому інтервалі = цех стоїть
        bool idle = _model.ShopNeed.Marking > 0;

        StockStat.Add(stock, dt);
        IdleStat.Add(idle ? 1 : 0, dt);
        OnOrderStat.Add(_model.OnOrder(), dt);

        if (dt > 0)
        {
            if (idle) { _inIdle = true; _currentIdle += dt; }
            else if (_inIdle) CloseIdle();
        }

        if (from < TraceUntil)
            Trace.Add((from, stock, _model.OnOrder(), idle ? 1 : 0));
    }

    private void CloseIdle()
    {
        _idleEpisodes.Add(_currentIdle);
        _currentIdle = 0;
        _inIdle = false;
    }

    public void OnEnter(Transition t, double time)
    {
        if (double.IsNaN(_statStart)) return;
        if (t == _model.Issue) IssuedInPeriod++;
        if (t == _model.Decide) OrdersInPeriod++;
    }

    public void OnExit(Transition t, double time, int channels)
    {
        if (double.IsNaN(_statStart)) return;
        if (t == _model.Deliver)
        {
            DeliveriesInPeriod += channels;
            _deliveryTimes.Add(time);
        }
    }

    // інваріанти перевіряємо після кожної події, навіть миттєвої
    public void OnEvent(PetriNet net, double time)
    {
        InvariantChecks++;
        if (_model.SupplyInvariant() != _model.Parameters.Capacity || _model.ShopInvariant() != 1)
            InvariantViolations++;
    }

    public void Finish()
    {
        if (_inIdle) CloseIdle();
    }
}

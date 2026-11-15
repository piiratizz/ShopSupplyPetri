namespace ShopSupplyPetri.Core;

public interface ISimulationObserver
{
    void OnInterval(PetriNet net, double from, double to);
    void OnEnter(Transition t, double time);
    void OnExit(Transition t, double time, int channels);
    void OnEvent(PetriNet net, double time);
}

public sealed class Simulator
{
    private readonly PetriNet _net;
    private readonly Random _rnd;
    private readonly List<ISimulationObserver> _observers = new();

    public double CurrentTime { get; private set; }
    public long EventCount { get; private set; }

    public TextWriter? Protocol { get; set; }
    public int MaxProtocolEvents { get; set; } = 60;

    public Simulator(PetriNet net, int seed)
    {
        _net = net;
        _net.Validate();
        _rnd = new Random(seed);
    }

    public void AddObserver(ISimulationObserver observer) => _observers.Add(observer);

    public void Run(double timeModeling, double warmUp = 0.0)
    {
        _net.Reset();
        CurrentTime = 0.0;
        EventCount = 0;

        FireInputs();
        foreach (var o in _observers) o.OnEvent(_net, CurrentTime);
        Log("старт");

        // основний цикл: стрибаємо одразу до найближчої події
        while (CurrentTime < timeModeling)
        {
            double tNext = double.PositiveInfinity;
            foreach (var tr in _net.Transitions)
            {
                double te = tr.NextExitTime;
                if (te < tNext) tNext = te;
            }
            double tStatEnd = Math.Min(tNext, timeModeling);

            // до кінця перехідного періоду статистику не рахуємо
            double from = Math.Max(CurrentTime, warmUp);
            if (tStatEnd > from)
                foreach (var o in _observers) o.OnInterval(_net, from, tStatEnd);

            if (tNext > timeModeling)
            {
                CurrentTime = timeModeling;
                break;
            }

            CurrentTime = tNext;

            FireOutputs();
            FireInputs();
            foreach (var o in _observers) o.OnEvent(_net, CurrentTime);

            EventCount++;
            Log(null);
        }
    }

    private void FireOutputs()
    {
        var due = _net.Transitions.Where(tr => tr.NextExitTime == CurrentTime).ToList();
        // одночасні виходи обробляємо у випадковому порядку
        Shuffle(due);
        foreach (var tr in due)
        {
            int n = tr.Exit(CurrentTime);
            if (n > 0)
            {
                foreach (var o in _observers) o.OnExit(tr, CurrentTime, n);
                LogTransition("вихід з переходу", tr, n);
            }
        }
    }

    // заходимо в переходи, поки хоч один дозволений -
    // так багатоканальний перехід захоплює всі маркери, які може
    private void FireInputs()
    {
        var enabled = new List<Transition>();
        while (true)
        {
            enabled.Clear();
            foreach (var tr in _net.Transitions)
                if (tr.IsEnabled()) enabled.Add(tr);
            if (enabled.Count == 0) break;

            Transition chosen = ResolveConflict(enabled);
            chosen.Enter(CurrentTime, _rnd);
            foreach (var o in _observers) o.OnEnter(chosen, CurrentTime);
            LogTransition("вхід у перехід", chosen, 1);
        }
    }

    // конфлікт: спершу дивимось на пріоритет, потім на ймовірність запуску
    private Transition ResolveConflict(List<Transition> enabled)
    {
        if (enabled.Count == 1) return enabled[0];
        int maxPriority = enabled.Max(t => t.Priority);
        var candidates = enabled.Where(t => t.Priority == maxPriority).ToList();
        if (candidates.Count == 1) return candidates[0];

        double total = candidates.Sum(t => t.Probability);
        double r = _rnd.NextDouble() * total;
        double acc = 0;
        foreach (var t in candidates)
        {
            acc += t.Probability;
            if (r < acc) return t;
        }
        return candidates[^1];
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private int _logged;
    private void LogTransition(string action, Transition tr, int n)
    {
        if (Protocol == null || _logged >= MaxProtocolEvents) return;
        Protocol.WriteLine($"   t = {CurrentTime,10:F3}  {action} «{tr.Name}»" + (n > 1 ? $" (каналів: {n})" : ""));
    }

    private void Log(string? header)
    {
        if (Protocol == null || _logged >= MaxProtocolEvents) return;
        _logged++;
        if (header != null) Protocol.WriteLine($"[{header}]");
        Protocol.WriteLine($"   M = ({string.Join(", ", _net.Places.Select(p => p.Marking))})");
        var busy = _net.Transitions.Where(t => t.Buffer > 0)
            .Select(t => $"{t.Name.Split(' ')[0]} = {{{string.Join(", ", t.ExitTimes.Select(x => x.ToString("F3")))}}}");
        Protocol.WriteLine($"   E: {string.Join("; ", busy)}");
        Protocol.WriteLine();
    }
}

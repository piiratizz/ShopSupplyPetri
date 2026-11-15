using System.Globalization;
using System.Text;
using ShopSupplyPetri.Core;
using ShopSupplyPetri.Model;

namespace ShopSupplyPetri.Experiments;

public sealed class ExperimentRunner
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string OutDir { get; }
    public double TimeModeling { get; set; } = 650_000;
    public double WarmUp { get; set; } = 150_000;
    public int Replications { get; set; } = 80;
    public double TargetEpsP { get; set; } = 0.001;

    public ExperimentRunner(string outDir)
    {
        OutDir = outDir;
        Directory.CreateDirectory(outDir);
    }

    private static string F(double v, string fmt = "0.######") => v.ToString(fmt, Inv);

    private void Save(string file, StringBuilder sb)
    {
        File.WriteAllText(Path.Combine(OutDir, file), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"  -> {file}");
    }

    // p прогонів паралельно, у кожного свій seed
    public List<SimulationResult> Replicate(ModelParameters p, int runs, int seedBase = 1000)
    {
        var res = new SimulationResult[runs];
        Parallel.For(0, runs, i => res[i] = ModelRunner.Run(p, TimeModeling, WarmUp, seedBase + i));
        return res.ToList();
    }

    public int ProtocolSeed { get; set; } = 12;

    public void Protocol()
    {
        Console.WriteLine("== Протокол подій ==");
        var p = new ModelParameters { InitialStock = 5 };
        using var w = new StreamWriter(Path.Combine(OutDir, "protocol.txt"), false, new UTF8Encoding(false));
        w.WriteLine("Протокол подій. Початковий запас 5 комплектів (для скорочення протоколу), s = 3, Q = 17.");
        w.WriteLine("M = (M(P1), ..., M(P9)) – маркірування позицій; E – моменти виходу маркерів із переходів.");
        w.WriteLine();
        ModelRunner.Run(p, 1500, 0, ProtocolSeed, protocol: w, protocolEvents: 40);
        Console.WriteLine("  -> protocol.txt");
    }

    public void Verification()
    {
        Console.WriteLine("== Верифікація ==");
        var b = new ModelParameters();
        var cases = new List<(string name, ModelParameters p)>
        {
            ("1 Базовий варіант", b),
            ("2 Потреба exp(30)", b with { Demand = new ExponentialDelay(30) }),
            ("3 Потреба exp(120)", b with { Demand = new ExponentialDelay(120) }),
            ("4 s = 0", b with { ReorderPoint = 0 }),
            ("5 s = 10", b with { ReorderPoint = 10 }),
            ("6 Формування запиту 0", b with { RequestFormation = new DeterministicDelay(0) }),
            ("7 Без затримок постачання", b with { RequestFormation = new DeterministicDelay(0), Kitting = new DeterministicDelay(0), Delivery = new DeterministicDelay(0) }),
            ("8 Комплектування unif(120,20)", b with { Kitting = new UniformDelay(120, 20) }),
            ("9 Стратегія «на 3»", b with { Strategy = ReplenishmentStrategy.FixedQuantity, FixedQuantity = 3 }),
            ("10 S = 40", b with { Capacity = 40, InitialStock = 40 }),
            ("11 Детерм., комплектування 70", b with { Demand = new DeterministicDelay(60), Kitting = new DeterministicDelay(70), Delivery = new DeterministicDelay(60) }),
            ("12 Детерм., комплектування 140", b with { Demand = new DeterministicDelay(60), Kitting = new DeterministicDelay(140), Delivery = new DeterministicDelay(60) }),
        };
        var sb = new StringBuilder("case;demand;s;Q;S;formation;kitting;delivery;P_idle;mean_stock;load;throughput_h;expected_throughput_h;orders_day;expected_orders_day;mean_cycle;mean_on_order;expected_on_order;share_cycles_idle;invariant_violations;invariant_checks\n");
        double tSaved = TimeModeling;
        // для звірки з розрахунком беремо довші прогони
        TimeModeling = 2_000_000;
        foreach (var (name, p) in cases)
        {
            var runs = Replicate(p, 20, 500);
            double P = runs.Average(r => r.IdleProbability);
            double L = runs.Average(r => r.MeanStock);
            double thr = runs.Average(r => r.ThroughputPerHour);
            double expThr = (1 - P) * 60.0 / p.Demand.Mean;
            double ord = runs.Average(r => r.OrdersPerDay);
            double expOrd = thr * 24.0 / p.OrderQuantity;
            double cyc = runs.Average(r => r.MeanCycle);
            double onOrd = runs.Average(r => r.MeanOnOrder);
            double lead = p.RequestFormation.Mean + p.Kitting.Mean + p.Delivery.Mean;
            // формула Літтла
            double expOnOrd = ord / 1440.0 * p.OrderQuantity * lead;
            long viol = runs.Sum(r => r.InvariantViolations);
            sb.AppendLine(string.Join(";", name, p.Demand.Notation, p.ReorderPoint, p.OrderQuantity, p.Capacity,
                p.RequestFormation.Notation, p.Kitting.Notation, p.Delivery.Notation,
                F(P), F(L), F(L / p.Capacity), F(thr), F(expThr), F(ord), F(expOrd), F(cyc), F(onOrd), F(expOnOrd),
                F(runs.Average(r => r.ShareOfCyclesWithIdle)), viol, runs.Sum(r => r.InvariantChecks)));
            Console.WriteLine($"  {name,-32} P={P:F5} L={L:F3} thr={thr:F4}/{expThr:F4} ord={ord:F3}/{expOrd:F3} cyc={cyc:F1} onOrd={onOrd:F3}/{expOnOrd:F3} viol={viol}");
        }
        TimeModeling = tSaved;
        Save("verification.csv", sb);
    }

    public void Transient(int runs = 5, double tMax = 800_000, double step = 5_000)
    {
        Console.WriteLine("== Перехідний період ==");
        int points = (int)(tMax / step);
        var idle = new double[runs, points];
        var stock = new double[runs, points];
        var p = new ModelParameters();
        Parallel.For(0, runs, i =>
        {
            var model = new ShopSupplyModel(p);
            var obs = new CumulativeObserver(model, step, points);
            var sim = new Simulator(model.Net, 3000 + i);
            sim.AddObserver(obs);
            sim.Run(tMax, 0);
            for (int k = 0; k < points; k++) { idle[i, k] = obs.Idle[k]; stock[i, k] = obs.Stock[k]; }
        });
        var sb = new StringBuilder("t");
        for (int i = 0; i < runs; i++) sb.Append($";idle_{i + 1}");
        for (int i = 0; i < runs; i++) sb.Append($";stock_{i + 1}");
        sb.Append(";idle_range;stock_range\n");
        for (int k = 0; k < points; k++)
        {
            sb.Append(F((k + 1) * step));
            double imin = double.MaxValue, imax = double.MinValue, smin = double.MaxValue, smax = double.MinValue;
            for (int i = 0; i < runs; i++) { sb.Append(';').Append(F(idle[i, k])); imin = Math.Min(imin, idle[i, k]); imax = Math.Max(imax, idle[i, k]); }
            for (int i = 0; i < runs; i++) { sb.Append(';').Append(F(stock[i, k])); smin = Math.Min(smin, stock[i, k]); smax = Math.Max(smax, stock[i, k]); }
            sb.Append(';').Append(F(imax - imin)).Append(';').Append(F(smax - smin)).Append('\n');
        }
        Save("transient.csv", sb);
    }

    // накопичені середні в контрольних точках - для перехідного періоду
    private sealed class CumulativeObserver : ISimulationObserver
    {
        private readonly ShopSupplyModel _m;
        private readonly double _step;
        private double _idle, _stock, _time;
        private int _k;
        public double[] Idle { get; }
        public double[] Stock { get; }
        public CumulativeObserver(ShopSupplyModel m, double step, int points)
        { _m = m; _step = step; Idle = new double[points]; Stock = new double[points]; }

        public void OnInterval(PetriNet net, double from, double to)
        {
            double t = from;
            int idle = _m.ShopNeed.Marking > 0 ? 1 : 0, st = _m.Stock.Marking;
            while (_k < Idle.Length && to >= (_k + 1) * _step)
            {
                double cp = (_k + 1) * _step;
                _idle += idle * (cp - t); _stock += st * (cp - t); _time += cp - t; t = cp;
                Idle[_k] = _idle / _time; Stock[_k] = _stock / _time; _k++;
            }
            _idle += idle * (to - t); _stock += st * (to - t); _time += to - t;
        }
        public void OnEnter(Transition t, double time) { }
        public void OnExit(Transition t, double time, int channels) { }
        public void OnEvent(PetriNet net, double time) { }
    }

    public void Replications100(int runs = 100)
    {
        Console.WriteLine("== Повторні прогони базової моделі ==");
        var res = Replicate(new ModelParameters(), runs, 7000);
        Console.WriteLine($"  Tmod = {TimeModeling}, Twarm = {WarmUp}");
        var sb = new StringBuilder("run;P_idle;mean_stock;std_stock;load;throughput_h;orders_day;idle_episodes;mean_idle;max_idle;share_needs_waiting;share_cycles_idle;mean_cycle;mean_on_order\n");
        for (int i = 0; i < res.Count; i++)
        {
            var r = res[i];
            sb.AppendLine(string.Join(";", i + 1, F(r.IdleProbability), F(r.MeanStock), F(r.StdStock), F(r.Load),
                F(r.ThroughputPerHour), F(r.OrdersPerDay), r.IdleEpisodes, F(r.MeanIdleDuration), F(r.MaxIdleDuration),
                F(r.ShareOfNeedsWaiting), F(r.ShareOfCyclesWithIdle), F(r.MeanCycle), F(r.MeanOnOrder)));
        }
        Save("replications.csv", sb);

        var pid = res.Select(r => r.IdleProbability).ToList();
        var st = res.Select(r => r.MeanStock).ToList();
        double sP = Stat.StdDev(pid), sL = Stat.StdDev(st);
        var summary = new StringBuilder();
        summary.AppendLine("quantity;mean;std;min;max");
        void Row(string n, IReadOnlyList<double> v) =>
            summary.AppendLine($"{n};{F(v.Average())};{F(Stat.StdDev(v))};{F(v.Min())};{F(v.Max())}");
        Row("P_idle", pid);
        Row("mean_stock", st);
        Row("load", res.Select(r => r.Load).ToList());
        Row("std_stock", res.Select(r => r.StdStock).ToList());
        Row("throughput_h", res.Select(r => r.ThroughputPerHour).ToList());
        Row("orders_day", res.Select(r => r.OrdersPerDay).ToList());
        Row("mean_idle", res.Select(r => r.MeanIdleDuration).ToList());
        Row("max_idle", res.Select(r => r.MaxIdleDuration).ToList());
        Row("share_needs_waiting", res.Select(r => r.ShareOfNeedsWaiting).ToList());
        Row("share_cycles_idle", res.Select(r => r.ShareOfCyclesWithIdle).ToList());
        Row("mean_cycle", res.Select(r => r.MeanCycle).ToList());
        Row("min_stock", res.Select(r => r.MinStock).ToList());
        Row("max_stock", res.Select(r => r.MaxStock).ToList());
        summary.AppendLine();
        foreach (var eps in new[] { 0.002, 0.001, 0.0005 })
            summary.AppendLine($"runs_for_P_idle_eps_{F(eps)};{Stat.RequiredRuns(sP, eps)}");
        foreach (var eps in new[] { 0.2, 0.1, 0.05 })
            summary.AppendLine($"runs_for_stock_eps_{F(eps)};{Stat.RequiredRuns(sL, eps)}");
        summary.AppendLine($"halfwidth_P_idle_p{Replications};{F(Stat.ChebyshevHalfWidth(sP, Replications))}");
        summary.AppendLine($"halfwidth_stock_p{Replications};{F(Stat.ChebyshevHalfWidth(sL, Replications))}");

        var (chi, table) = Stat.ChiSquareNormal(pid, 10);
        summary.AppendLine();
        summary.AppendLine($"chi2_normal_P_idle;chi2={F(chi.ChiSquare)};crit={F(chi.Critical)};df={chi.Df};accepted={chi.Accepted}");
        summary.AppendLine("bin_lo;bin_hi;observed;expected");
        foreach (var r in table) summary.AppendLine($"{F(r.lo)};{F(r.hi)};{r.obs};{F(r.exp)}");
        Save("replications_summary.csv", summary);
        Console.Write(summary.ToString());

        var (_, stats) = ModelRunner.RunDetailed(new ModelParameters(), 5_000_000, WarmUp, 99);
        var sbi = new StringBuilder("idle_duration\n");
        foreach (var d in stats.IdleEpisodes) sbi.AppendLine(F(d));
        Save("idle_episodes.csv", sbi);
    }

    public void ReorderPointExperiment(int sMax = 14)
    {
        Console.WriteLine("== Вплив точки замовлення s та стратегії ==");
        var sb = new StringBuilder("strategy;s;Q;runs;P_idle_mean;P_idle_std;P_idle_min;P_idle_max;P_idle_halfwidth;stock_mean;stock_std;load;orders_day;share_cycles_idle;idle_episodes_total;observed_minutes;analytic_cycle_stockout\n");
        foreach (var strat in new[] { ReplenishmentStrategy.UpToFull, ReplenishmentStrategy.FixedQuantity })
            for (int s = 0; s <= sMax; s++)
            {
                var p = new ModelParameters { ReorderPoint = s, Strategy = strat, FixedQuantity = 3 };
                if (p.OrderQuantity > p.TriggerDeficit) continue;
                var res = Replicate(p, Replications, 10000 + 100 * s);
                // добираємо прогони, поки точність по P не стане <= 0,001
                while (true)
                {
                    var cur = res.Select(r => r.IdleProbability).ToList();
                    if (Stat.ChebyshevHalfWidth(Stat.StdDev(cur), res.Count) <= TargetEpsP) break;
                    int need = Math.Max(Stat.RequiredRuns(Stat.StdDev(cur), TargetEpsP), res.Count + 10);
                    res.AddRange(Replicate(p, need - res.Count, 10000 + 100 * s + 50_000 + res.Count));
                }
                var pid = res.Select(r => r.IdleProbability).ToList();
                var st = res.Select(r => r.MeanStock).ToList();
                double sP = Stat.StdDev(pid);
                long episodes = res.Sum(r => (long)r.IdleEpisodes);
                double analytic = PoissonTail(s + 1, 3.0);
                sb.AppendLine(string.Join(";", strat == ReplenishmentStrategy.UpToFull ? "to20" : "by3", s, p.OrderQuantity, res.Count,
                    F(pid.Average(), "0.########"), F(sP, "0.########"), F(pid.Min(), "0.########"), F(pid.Max(), "0.########"),
                    F(Stat.ChebyshevHalfWidth(sP, res.Count), "0.########"),
                    F(st.Average()), F(Stat.StdDev(st)), F(st.Average() / p.Capacity),
                    F(res.Average(r => r.OrdersPerDay)), F(res.Average(r => r.ShareOfCyclesWithIdle)),
                    episodes, F(res.Sum(r => r.StatPeriod)), F(analytic, "0.########")));
                Console.WriteLine($"  {p.StrategyName,-6} s={s,2} Q={p.OrderQuantity,2} p={res.Count} P={pid.Average():F6}±{Stat.ChebyshevHalfWidth(sP, res.Count):F6} L={st.Average():F3} ord/day={res.Average(r => r.OrdersPerDay):F3} episodes={episodes}");
            }
        Save("exp_reorder_point.csv", sb);
    }

    // P(N >= k) для Пуассона - щоб звірити частку циклів із простоєм
    private static double PoissonTail(int k, double a)
    {
        double term = Math.Exp(-a), cdf = 0;
        for (int i = 0; i < k; i++) { cdf += term; term *= a / (i + 1); }
        return Math.Max(0, 1 - cdf);
    }

    public void Anova()
    {
        Console.WriteLine("== Дисперсійний аналіз ==");
        var sb = new StringBuilder("analysis;response;levels;p;D_F;D_R;F;F_crit;df1;df2;p_value;significant;level_means\n");

        void Do(string name, string response, List<ModelParameters> levels, Func<SimulationResult, double> sel, int seedBase)
        {
            var groups = levels.Select((p, j) => (IReadOnlyList<double>)Replicate(p, Replications, seedBase + 1000 * j).Select(sel).ToList()).ToList();
            var a = Stat.OneWayAnova(groups);
            string means = string.Join(" | ", groups.Select(g => F(g.Average())));
            sb.AppendLine(string.Join(";", name, response, a.Levels, a.Replications, F(a.FactorVariance, "0.##########"),
                F(a.ResidualVariance, "0.##########"), F(a.F, "0.####"), F(a.FCritical, "0.####"), a.Df1, a.Df2,
                F(a.PValue, "0.######"), a.Significant, means));
            Console.WriteLine($"  {name} [{response}]: F={a.F:F3} Fcrit={a.FCritical:F3} p={a.PValue:G4} significant={a.Significant}; means: {means}");
        }

        var to20 = new ModelParameters();
        var by3 = new ModelParameters { Strategy = ReplenishmentStrategy.FixedQuantity, FixedQuantity = 3 };
        Do("Стратегія (до 20 / на 3), s=3", "P_idle", new() { to20, by3 }, r => r.IdleProbability, 50000);
        Do("Стратегія (до 20 / на 3), s=3", "mean_stock", new() { to20, by3 }, r => r.MeanStock, 50000);
        Do("Стратегія (до 20 / на 3), s=3", "orders_day", new() { to20, by3 }, r => r.OrdersPerDay, 50000);
        // контрольний фактор: середнє те саме, впливу бути не повинно
        Do("Розкид доставки (unif(60,5) / 60)", "P_idle",
            new() { to20, to20 with { Delivery = new DeterministicDelay(60) } }, r => r.IdleProbability, 60000);
        Do("Час формування запиту (60/30/0)", "P_idle",
            new() { to20, to20 with { RequestFormation = new DeterministicDelay(30) }, to20 with { RequestFormation = new DeterministicDelay(0) } },
            r => r.IdleProbability, 70000);
        Save("anova.csv", sb);
    }

    public void Regression()
    {
        Console.WriteLine("== Регресійний аналіз ==");
        var sb = new StringBuilder("model;b0;b1;b2;R2;R\n");
        var data = File.ReadAllLines(Path.Combine(OutDir, "exp_reorder_point.csv")).Skip(1)
            .Select(l => l.Split(';'))
            .Where(c => c[0] == "to20" && double.Parse(c[4], Inv) > 0)
            .Select(c => (s: double.Parse(c[1], Inv), p: double.Parse(c[4], Inv)))
            // далі простоїв майже немає, логарифм рахувати нема з чого
            .Where(x => x.s <= 9)
            .ToList();
        var lin = Stat.LeastSquares(data.Select(d => new[] { 1.0, d.s }).ToList(), data.Select(d => Math.Log(d.p)).ToList());
        var quad = Stat.LeastSquares(data.Select(d => new[] { 1.0, d.s, d.s * d.s }).ToList(), data.Select(d => Math.Log(d.p)).ToList());
        sb.AppendLine($"lnP=b0+b1*s;{F(lin.Coefficients[0])};{F(lin.Coefficients[1])};;{F(lin.R2)};{F(lin.CorrelationIndex)}");
        sb.AppendLine($"lnP=b0+b1*s+b2*s^2;{F(quad.Coefficients[0])};{F(quad.Coefficients[1])};{F(quad.Coefficients[2])};{F(quad.R2)};{F(quad.CorrelationIndex)}");
        Save("regression.csv", sb);
        Console.Write(sb.ToString());
    }

    public void FormationExperiment()
    {
        Console.WriteLine("== Вплив тривалості формування запиту ==");
        var sb = new StringBuilder("formation;s;P_idle_mean;P_idle_halfwidth;stock_mean;orders_day\n");
        foreach (int s in new[] { 3, 5 })
            foreach (double f in new[] { 0.0, 15, 30, 45, 60 })
            {
                var p = new ModelParameters { ReorderPoint = s, RequestFormation = new DeterministicDelay(f) };
                var res = Replicate(p, Replications, 80000 + (int)f + 1000 * s);
                var pid = res.Select(r => r.IdleProbability).ToList();
                sb.AppendLine(string.Join(";", F(f), s, F(pid.Average(), "0.########"),
                    F(Stat.ChebyshevHalfWidth(Stat.StdDev(pid), Replications), "0.########"),
                    F(res.Average(r => r.MeanStock)), F(res.Average(r => r.OrdersPerDay))));
                Console.WriteLine($"  s={s} formation={f,3}: P={pid.Average():F6} L={res.Average(r => r.MeanStock):F3}");
            }
        Save("exp_formation.csv", sb);
    }

    public void Traces(double until = 8000)
    {
        Console.WriteLine("== Траєкторії запасу ==");
        var cases = new (string name, ModelParameters p)[]
        {
            ("to20_s3", new ModelParameters()),
            ("by3_s3", new ModelParameters { Strategy = ReplenishmentStrategy.FixedQuantity, FixedQuantity = 3 }),
            ("to20_s9", new ModelParameters { ReorderPoint = 9 }),
        };
        foreach (var (name, p) in cases)
        {
            var (_, stats) = ModelRunner.RunDetailed(p, until, 0, 21, until);
            var sb = new StringBuilder("t;stock;on_order;idle\n");
            foreach (var (t, s, o, i) in stats.Trace) sb.AppendLine($"{F(t)};{s};{o};{i}");
            Save($"trace_{name}.csv", sb);
        }
    }
}

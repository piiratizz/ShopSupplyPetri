namespace ShopSupplyPetri.Experiments;

public static class Stat
{
    public static double Mean(IReadOnlyList<double> x) => x.Average();

    public static double StdDev(IReadOnlyList<double> x)
    {
        if (x.Count < 2) return 0;
        double m = x.Average();
        return Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / (x.Count - 1));
    }

    // p = σ² / (ε²(1 - β)) - з нерівності Чебишева
    public static int RequiredRuns(double sigma, double eps, double beta = 0.95)
        => (int)Math.Ceiling(sigma * sigma / (eps * eps * (1 - beta)));

    public static double ChebyshevHalfWidth(double sigma, int p, double beta = 0.95)
        => sigma / Math.Sqrt(p * (1 - beta));

    public sealed record AnovaResult(int Levels, int Replications, double FactorVariance, double ResidualVariance,
        double F, double FCritical, int Df1, int Df2, double PValue)
    {
        public bool Significant => F > FCritical;
    }

    // однофакторний дисперсійний аналіз: q рівнів по p прогонів
    public static AnovaResult OneWayAnova(IReadOnlyList<IReadOnlyList<double>> groups, double alpha = 0.05)
    {
        int q = groups.Count;
        int p = groups[0].Count;
        double grand = groups.SelectMany(g => g).Average();
        double ssf = groups.Sum(g => p * Math.Pow(g.Average() - grand, 2));
        double ssr = groups.Sum(g => { double m = g.Average(); return g.Sum(v => (v - m) * (v - m)); });
        int df1 = q - 1, df2 = q * (p - 1);
        double df = ssf / df1, dr = ssr / df2;
        double f = df / dr;
        double fcrit = FQuantile(1 - alpha, df1, df2);
        double pval = 1 - FCdf(f, df1, df2);
        return new AnovaResult(q, p, df, dr, f, fcrit, df1, df2, pval);
    }

    public sealed record RegressionResult(double[] Coefficients, double R2, double CorrelationIndex);

    // МНК: b = (XᵀX)⁻¹XᵀY, далі звичайний Гаус
    public static RegressionResult LeastSquares(IReadOnlyList<double[]> rows, IReadOnlyList<double> y)
    {
        int n = rows.Count, m = rows[0].Length;
        var xtx = new double[m, m];
        var xty = new double[m];
        for (int i = 0; i < n; i++)
            for (int a = 0; a < m; a++)
            {
                xty[a] += rows[i][a] * y[i];
                for (int b = 0; b < m; b++) xtx[a, b] += rows[i][a] * rows[i][b];
            }
        var coef = SolveGauss(xtx, xty);
        double ym = y.Average();
        double sse = 0, sst = 0;
        for (int i = 0; i < n; i++)
        {
            double yh = 0;
            for (int a = 0; a < m; a++) yh += coef[a] * rows[i][a];
            sse += (y[i] - yh) * (y[i] - yh);
            sst += (y[i] - ym) * (y[i] - ym);
        }
        double r2 = 1 - sse / sst;
        return new RegressionResult(coef, r2, Math.Sqrt(Math.Max(0, r2)));
    }

    private static double[] SolveGauss(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var v = (double[])b.Clone();
        for (int c = 0; c < n; c++)
        {
            int piv = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[piv, c])) piv = r;
            for (int k = 0; k < n; k++) (m[c, k], m[piv, k]) = (m[piv, k], m[c, k]);
            (v[c], v[piv]) = (v[piv], v[c]);
            for (int r = c + 1; r < n; r++)
            {
                double f = m[r, c] / m[c, c];
                for (int k = c; k < n; k++) m[r, k] -= f * m[c, k];
                v[r] -= f * v[c];
            }
        }
        var x = new double[n];
        for (int r = n - 1; r >= 0; r--)
        {
            double s = v[r];
            for (int k = r + 1; k < n; k++) s -= m[r, k] * x[k];
            x[r] = s / m[r, r];
        }
        return x;
    }

    public sealed record ChiSquareResult(int Bins, double ChiSquare, double Critical, int Df)
    {
        public bool Accepted => ChiSquare < Critical;
    }

    public static (ChiSquareResult result, List<(double lo, double hi, int obs, double exp)> table)
        // χ² на нормальний закон; інтервали з малою частотою зливаємо
        ChiSquareNormal(IReadOnlyList<double> data, int bins, double alpha = 0.05)
    {
        double m = Mean(data), s = StdDev(data);
        double lo = data.Min(), hi = data.Max();
        double w = (hi - lo) / bins;
        var edges = Enumerable.Range(0, bins + 1).Select(i => lo + i * w).ToArray();
        var raw = new List<(double lo, double hi, int obs, double exp)>();
        for (int i = 0; i < bins; i++)
        {
            double a = edges[i], b = edges[i + 1];
            int obs = data.Count(v => v >= a && (i == bins - 1 ? v <= b : v < b));
            double pa = i == 0 ? 0 : NormalCdf((a - m) / s);
            double pb = i == bins - 1 ? 1 : NormalCdf((b - m) / s);
            raw.Add((a, b, obs, data.Count * (pb - pa)));
        }
        var merged = new List<(double lo, double hi, int obs, double exp)>();
        foreach (var r in raw)
        {
            if (merged.Count > 0 && merged[^1].exp < 5)
            {
                var last = merged[^1];
                merged[^1] = (last.lo, r.hi, last.obs + r.obs, last.exp + r.exp);
            }
            else merged.Add(r);
        }
        while (merged.Count > 1 && merged[^1].exp < 5)
        {
            var last = merged[^1]; merged.RemoveAt(merged.Count - 1);
            var prev = merged[^1];
            merged[^1] = (prev.lo, last.hi, prev.obs + last.obs, prev.exp + last.exp);
        }
        double chi = merged.Sum(r => (r.obs - r.exp) * (r.obs - r.exp) / r.exp);
        int df = merged.Count - 1 - 2;
        double crit = ChiSquareQuantile(1 - alpha, df);
        return (new ChiSquareResult(merged.Count, chi, crit, df), merged);
    }

    public static double NormalCdf(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    // наближення Абрамовіца-Стіган, точності вистачає
    private static double Erf(double x)
    {
        double t = 1 / (1 + 0.3275911 * Math.Abs(x));
        double y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return x >= 0 ? y : -y;
    }

    private static double LogGamma(double x)
    {
        double[] c = { 76.18009172947146, -86.50532032941677, 24.01409824083091,
                       -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;
        foreach (var ci in c) ser += ci / ++y;
        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    // неповна гамма- і бета-функції - для критичних значень χ² і F
    private static double GammaP(double a, double x)
    {
        if (x <= 0) return 0;
        if (x < a + 1)
        {
            double sum = 1 / a, del = sum, ap = a;
            for (int n = 0; n < 500; n++) { ap++; del *= x / ap; sum += del; if (Math.Abs(del) < Math.Abs(sum) * 1e-14) break; }
            return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }
        double b = x + 1 - a, cc = 1 / 1e-300, d = 1 / b, h = d;
        for (int i = 1; i < 500; i++)
        {
            double an = -i * (i - a); b += 2;
            d = an * d + b; if (Math.Abs(d) < 1e-300) d = 1e-300;
            cc = b + an / cc; if (Math.Abs(cc) < 1e-300) cc = 1e-300;
            d = 1 / d; double del = d * cc; h *= del;
            if (Math.Abs(del - 1) < 1e-14) break;
        }
        return 1 - Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    public static double ChiSquareCdf(double x, int df) => GammaP(df / 2.0, x / 2.0);

    public static double ChiSquareQuantile(double prob, int df) => Bisect(x => ChiSquareCdf(x, df), prob, 0, 1000);

    private static double BetaI(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        double bt = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x));
        return x < (a + 1) / (a + b + 2) ? bt * BetaCf(a, b, x) / a : 1 - bt * BetaCf(b, a, 1 - x) / b;
    }

    private static double BetaCf(double a, double b, double x)
    {
        double qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
        if (Math.Abs(d) < 1e-300) d = 1e-300;
        d = 1 / d; double h = d;
        for (int m = 1; m <= 500; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + aa * d; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = 1 + aa / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d; h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + aa * d; if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = 1 + aa / c; if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d; double del = d * c; h *= del;
            if (Math.Abs(del - 1) < 1e-14) break;
        }
        return h;
    }

    public static double FCdf(double f, int d1, int d2) => f <= 0 ? 0 : BetaI(d1 / 2.0, d2 / 2.0, d1 * f / (d1 * f + d2));

    public static double FQuantile(double prob, int d1, int d2) => Bisect(x => FCdf(x, d1, d2), prob, 0, 1000);

    private static double Bisect(Func<double, double> cdf, double prob, double lo, double hi)
    {
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (cdf(mid) < prob) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }
}

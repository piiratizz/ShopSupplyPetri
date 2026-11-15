using System.Text;
using ShopSupplyPetri.Experiments;
using ShopSupplyPetri.Model;

Console.OutputEncoding = Encoding.UTF8;
string mode = args.Length > 0 ? args[0] : "base";
string outDir = args.Length > 1 ? args[1] : "results";
var runner = new ExperimentRunner(outDir);

switch (mode)
{
    case "base": RunBase(); break;
    case "protocol": runner.Protocol(); break;
    case "verify": runner.Verification(); break;
    case "transient": runner.Transient(); break;
    case "reps": runner.Replications100(); break;
    case "reorder": runner.ReorderPointExperiment(); break;
    case "anova": runner.Anova(); break;
    case "regression": runner.Regression(); break;
    case "formation": runner.FormationExperiment(); break;
    case "trace": runner.Traces(); break;
    case "all":
        RunBase();
        runner.Protocol();
        runner.Verification();
        runner.Transient();
        runner.Replications100();
        runner.ReorderPointExperiment();
        runner.Anova();
        runner.Regression();
        runner.FormationExperiment();
        runner.Traces();
        break;
    default:
        Console.WriteLine("Невідомий режим.");
        break;
}

void RunBase()
{
    var p = new ModelParameters();
    Console.WriteLine("== Базовий прогін моделі D8 ==");
    Console.WriteLine($"S = {p.Capacity}, s = {p.ReorderPoint}, Q = {p.OrderQuantity}, потреба {p.Demand.Notation}, " +
                      $"формування {p.RequestFormation.Notation}, комплектування {p.Kitting.Notation}, доставка {p.Delivery.Notation}");
    var r = ModelRunner.Run(p, runner.TimeModeling, runner.WarmUp, 1);
    Console.WriteLine($"Час моделювання {r.TimeModeling} хв, перехідний період {r.WarmUp} хв");
    Console.WriteLine($"Ймовірність простою цеху          P = {r.IdleProbability:F5}");
    Console.WriteLine($"Середній запас цехового складу    L = {r.MeanStock:F3} компл. (σ = {r.StdStock:F3}, min = {r.MinStock}, max = {r.MaxStock})");
    Console.WriteLine($"Середнє завантаження складу       K = {r.Load:F4}");
    Console.WriteLine($"Продуктивність цеху               {r.ThroughputPerHour:F4} компл./год");
    Console.WriteLine($"Запитів на поповнення             {r.Orders} ({r.OrdersPerDay:F3} за добу)");
    Console.WriteLine($"Епізодів простою                  {r.IdleEpisodes}, середня тривалість {r.MeanIdleDuration:F2} хв, макс. {r.MaxIdleDuration:F2} хв");
    Console.WriteLine($"Частка циклів з простоєм          {r.ShareOfCyclesWithIdle:F4}");
    Console.WriteLine($"Середній цикл поповнення          {r.MeanCycle:F2} хв");
    Console.WriteLine($"Порушень P-інваріантів            {r.InvariantViolations}");
    Console.WriteLine($"Подій                             {r.Events}");
}

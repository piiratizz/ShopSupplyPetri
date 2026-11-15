using ShopSupplyPetri.Core;

namespace ShopSupplyPetri.Model;

public sealed class ShopSupplyModel
{
    public const string PStock = "P1 Запас цехового складу";
    public const string PShopNeed = "P2 Цех потребує комплект";
    public const string PShopKit = "P3 Комплект у цеху";
    public const string PDeficit = "P4 Нестача до повного обсягу";
    public const string PRequest = "P5 Запит ініційовано";
    public const string PAtCentral = "P6 Запит на центральному складі";
    public const string PKitted = "P7 Деталі укомплектовано";
    public const string PIssued = "P8 Видано комплектів";
    public const string POrders = "P9 Кількість запитів";

    public const string TIssue = "T1 Видача комплекту";
    public const string TUse = "T2 Використання комплекту";
    public const string TDecide = "T3 Рішення про запит";
    public const string TForm = "T4 Формування запиту";
    public const string TKit = "T5 Комплектування";
    public const string TDeliver = "T6 Доставка";

    public ModelParameters Parameters { get; }
    public PetriNet Net { get; }

    public Place Stock { get; }
    public Place ShopNeed { get; }
    public Place ShopKit { get; }
    public Place Deficit { get; }
    public Place Request { get; }
    public Place AtCentral { get; }
    public Place Kitted { get; }
    public Place Issued { get; }
    public Place Orders { get; }

    public Transition Issue { get; }
    public Transition Use { get; }
    public Transition Decide { get; }
    public Transition Form { get; }
    public Transition Kit { get; }
    public Transition Deliver { get; }

    public ShopSupplyModel(ModelParameters p)
    {
        p.Validate();
        Parameters = p;
        int S = p.Capacity, Q = p.OrderQuantity, K = p.TriggerDeficit;

        Net = new PetriNet("Забезпечення цеху деталями (D8)");

        Stock = Net.AddPlace(PStock, p.InitialStock);
        ShopNeed = Net.AddPlace(PShopNeed, 1);
        ShopKit = Net.AddPlace(PShopKit, 0);
        // P4 - це «вільні місця» на складі, які ще не замовлені
        Deficit = Net.AddPlace(PDeficit, S - p.InitialStock);
        Request = Net.AddPlace(PRequest, 0);
        AtCentral = Net.AddPlace(PAtCentral, 0);
        Kitted = Net.AddPlace(PKitted, 0);
        Issued = Net.AddPlace(PIssued, 0);
        Orders = Net.AddPlace(POrders, 0);

        Issue = Net.AddTransition(TIssue, new DeterministicDelay(0));
        Issue.AddInput(Stock, 1);
        Issue.AddInput(ShopNeed, 1);
        Issue.AddOutput(ShopKit, 1);
        Issue.AddOutput(Deficit, 1);
        Issue.AddOutput(Issued, 1);

        Use = Net.AddTransition(TUse, p.Demand);
        Use.AddInput(ShopKit, 1);
        Use.AddOutput(ShopNeed, 1);

        // T3: дуга S-s з P4 перевіряє поріг, а дуга S-s-Q назад повертає зайве,
        // тож фактично забираємо рівно Q маркерів
        Decide = Net.AddTransition(TDecide, new DeterministicDelay(0));
        Decide.AddInput(Deficit, K);
        if (K - Q > 0) Decide.AddOutput(Deficit, K - Q);
        Decide.AddOutput(Request, Q);
        Decide.AddOutput(Orders, 1);

        Form = Net.AddTransition(TForm, p.RequestFormation);
        Form.AddInput(Request, Q);
        Form.AddOutput(AtCentral, Q);

        Kit = Net.AddTransition(TKit, p.Kitting);
        Kit.AddInput(AtCentral, Q);
        Kit.AddOutput(Kitted, Q);

        Deliver = Net.AddTransition(TDeliver, p.Delivery);
        Deliver.AddInput(Kitted, Q);
        Deliver.AddOutput(Stock, Q);
    }

    // інваріант (2.1): комплекти нікуди не зникають, сума завжди = S
    public int SupplyInvariant()
    {
        int Q = Parameters.OrderQuantity, K = Parameters.TriggerDeficit;
        return Stock.Marking + Deficit.Marking + Request.Marking + AtCentral.Marking + Kitted.Marking
               + Issue.Buffer + K * Decide.Buffer + Q * (Form.Buffer + Kit.Buffer + Deliver.Buffer);
    }

    // цех або чекає комплект, або працює з ним
    public int ShopInvariant() => ShopNeed.Marking + ShopKit.Marking + Issue.Buffer + Use.Buffer;

    public int OnOrder() => Request.Marking + AtCentral.Marking + Kitted.Marking
        + Parameters.OrderQuantity * (Decide.Buffer + Form.Buffer + Kit.Buffer + Deliver.Buffer);
}

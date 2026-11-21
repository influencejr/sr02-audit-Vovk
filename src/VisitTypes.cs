namespace ClinicReception;

// Довідник типів візиту: тривалість, назва для SMS і рахунку, базова ціна.
// Невідомий код отримує значення консультації, як і раніше в гілках default.
public static class VisitTypes
{
    private static readonly VisitTypeInfo Unknown = new VisitTypeInfo(20, "visit", 450m);

    private static readonly Dictionary<string, VisitTypeInfo> Table = new Dictionary<string, VisitTypeInfo>
    {
        ["CONS"] = new VisitTypeInfo(20, "consultation", 450m),
        ["FUP"] = new VisitTypeInfo(15, "follow-up", 300m),
        ["PROC"] = new VisitTypeInfo(40, "procedure", 900m),
        ["VACC"] = new VisitTypeInfo(10, "vaccination", 250m),
    };

    public static VisitTypeInfo Of(string code)
    {
        if (code != null && Table.TryGetValue(code, out VisitTypeInfo info))
            return info;
        return Unknown;
    }
}

public record VisitTypeInfo(int Minutes, string Name, decimal BasePrice);

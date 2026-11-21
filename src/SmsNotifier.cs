namespace ClinicReception;

// SMS пацієнтам: текст повідомлення, номер у міжнародному форматі, черга
// надісланих. Лічильник статичний, як і раніше: нумерація наскрізна для програми.
public class SmsNotifier
{
    public static bool SmsEnabled = true;
    private static int smsCounter = 0;
    private string clinicName = "City Clinic #3";
    private List<string> outbox = new List<string>();

    // назовні черга лише для читання: додати SMS можна тільки через SendSms
    public IReadOnlyList<string> GetOutbox() { return outbox.AsReadOnly(); }
    public int GetSmsCount() { return smsCounter; }
    public void ClearOutbox() { outbox.Clear(); }

    public string FormatSms(Appointment a, Patient p, Doctor d)
    {
        string vt = VisitTypes.Of(a.VisitType).Name;
        return clinicName + ": " + p.FullName + ", " + vt + " with Dr. " + d.Name + " at "
            + a.Start.ToString("HH:mm") + ", room " + d.Room + (a.Urgent ? " (URGENT)" : "");
    }

    public bool SendSms(string phone, string text)
    {
        string to = NormalizePhone(phone);
        if (!SmsEnabled || to == "")
            return false;
        smsCounter++;
        outbox.Add(to + ": " + text);
        // TODO: підключити шлюз, поки що в консоль
        Console.WriteLine("  [SMS " + smsCounter + " -> " + to + "] " + text);
        return true;
    }

    // 067 123 45 67 / (067)123-45-67 / 380671234567 -> +380671234567
    public string NormalizePhone(string phone)
    {
        if (phone == null)
            return "";
        string s = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (s.StartsWith("+"))
            return s;
        if (s.StartsWith("380"))
            return "+" + s;
        if (s.StartsWith("0") && s.Length == 10)
            return "+38" + s;
        return "";
    }
}

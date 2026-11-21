namespace ClinicReception;

// Розклад: пошук вільного часу, відпустки лікарів, звіт і статистика
public class SchedulePlanner
{
    private const int SlotStepMinutes = 5;
    private const int LunchHour = 13;
    private const int MinutesPerHour = 60;
    private const int UrgentReserveMinutes = 30;
    private const int PatientGapMinutes = 30;

    private ClinicRepository repo = ClinicRepository.Current;
    private FeeCalculator fees = new FeeCalculator();
    private string lastError = "";
    private List<Holiday> holidays = new List<Holiday>();
    private int bookedCount = 0;
    private int urgentCount = 0;
    private int failedCount = 0;
    private Dictionary<string, int> perType = new Dictionary<string, int>();

    // повідомлення пацієнтам про запис
    public SmsNotifier Sms { get; } = new SmsNotifier();

    // Повертає null, причину див. у GetLastError()
    public Appointment Book(int patientId, int doctorId, string visitType, DateTime day,
        bool urgent)
    {
        lastError = "";
        Patient p = repo.FindPatient(patientId);
        Doctor d = repo.FindDoctor(doctorId);
        if (p == null || d == null)
        {
            lastError = "Patient or doctor not found";
            return null;
        }
        if (!IsBookableDay(doctorId, day))
        {
            lastError = "Date is in the past or doctor is on holiday";
            return null;
        }
        int len = VisitTypes.Of(visitType).Minutes;
        DateTime? start = FindFreeSlot(d, patientId, day, len, urgent);
        if (start == null)
        {
            failedCount++;
            lastError = "No free slot for " + visitType;
            return null;
        }
        Appointment ap = new Appointment { Id = ClinicRepository.NextId(), PatientId = patientId,
            DoctorId = doctorId, VisitType = visitType, Start = start.Value, Minutes = len };
        ap.Urgent = urgent;
        ap.Status = AppointmentStatus.Booked;
        ap.Fee = fees.CalcFee(ap, p);
        repo.Appointments.Add(ap);
        CountStats(ap);
        Sms.SendSms(p.Phone, Sms.FormatSms(ap, p, d));
        AuditLog.Write("BOOK #" + ap.Id + " patient " + patientId + " doctor " + doctorId);
        return ap;
    }

    // день не в минулому і лікар не у відпустці
    private bool IsBookableDay(int doctorId, DateTime day)
    {
        return day.Date >= Now().Date && !IsOnHoliday(doctorId, day);
    }

    // перший слот, вільний і для лікаря, і для пацієнта; null - вільного немає
    private DateTime? FindFreeSlot(Doctor d, int patientId, DateTime day, int len, bool urgent)
    {
        List<Appointment> dayList = GetDoctorDay(d.Id, day);
        // записи пацієнта на цей день (до всіх лікарів)
        List<Appointment> mine = repo.FindForPatient(patientId, day);
        DateTime from = day.Date.AddHours(d.StartHour);
        DateTime to = day.Date.AddHours(d.EndHour);
        if (!urgent)
            to = to.AddMinutes(-UrgentReserveMinutes); // останні пів години - для термінових
        if (day.Date == Now().Date && from < Now())
            from = Now().Date.AddHours(Now().Hour + 1);
        for (DateTime t = from; t.AddMinutes(len) <= to; t = t.AddMinutes(SlotStepMinutes))
        {
            if (t.Hour != LunchHour && IsDoctorFree(dayList, t, len) && IsPatientFree(mine, t))
                return t;
        }
        return null;
    }

    private static bool IsDoctorFree(List<Appointment> dayList, DateTime t, int len)
    {
        foreach (Appointment a in dayList)
        {
            bool overlapsDoctorVisit = a.Start < t.AddMinutes(len) && t < a.Start.AddMinutes(a.Minutes);
            if (overlapsDoctorVisit)
                return false;
        }
        return true;
    }

    private static bool IsPatientFree(List<Appointment> mine, DateTime t)
    {
        foreach (Appointment a in mine)
        {
            if (Math.Abs((a.Start - t).TotalMinutes) < PatientGapMinutes)
                return false;
        }
        return true;
    }

    // шов для тестів: поточний час можна підмінити в нащадку
    protected virtual DateTime Now() { return DateTime.Now; }

    public string GetLastError() { return lastError; }
    public int GetBookedCount() { return bookedCount; }

    public List<Appointment> GetDoctorDay(int doctorId, DateTime day)
    {
        List<Appointment> dayList = new List<Appointment>();
        foreach (Appointment x in repo.Appointments)
        {
            if (x.DoctorId != doctorId || x.Start.Date != day.Date)
                continue;
            if (x.Status == AppointmentStatus.Cancelled)
                continue;
            dayList.Add(x);
        }
        return dayList;
    }

    public Appointment Reschedule(int appointmentId, DateTime newDay)
    {
        Appointment old = repo.FindAppointment(appointmentId);
        if (old == null)
            return null;
        AppointmentStatus was = old.Status;
        old.Status = AppointmentStatus.Cancelled;
        Appointment n = Book(old.PatientId, old.DoctorId, old.VisitType, newDay, old.Urgent);
        if (n == null)
            old.Status = was;
        return n;
    }

    public bool AddHoliday(int doctorId, DateTime from, DateTime to)
    {
        if (to < from)
            return false;
        holidays.Add(new Holiday(doctorId, from.Date, to.Date));
        AuditLog.Write("HOLIDAY doctor " + doctorId);
        return true;
    }

    public bool IsOnHoliday(int doctorId, DateTime day)
    {
        return holidays.Any(h => h.Covers(doctorId, day));
    }

    public int CancelHolidays(int doctorId)
    {
        return holidays.RemoveAll(h => h.DoctorId == doctorId);
    }

    private void CountStats(Appointment ap)
    {
        bookedCount++;
        if (ap.Urgent)
            urgentCount++;
        perType[ap.VisitType] = perType.ContainsKey(ap.VisitType) ? perType[ap.VisitType] + 1 : 1;
    }

    // звіт для старшої медсестри в кінці зміни
    public string BuildDayReport(int doctorId, DateTime day)
    {
        Doctor d = repo.FindDoctor(doctorId);
        if (d == null)
            return "";
        List<Appointment> dayList = GetDoctorDay(doctorId, day);
        string r = "Day report: " + d.Describe() + "\n";
        int mins = 0;
        foreach (Appointment a in dayList)
        {
            string vt = VisitTypes.Of(a.VisitType).Name;
            r += "  " + a.Start.ToString("HH:mm") + " " + vt + ", "
                + repo.FindPatient(a.PatientId).FullName + " [" + a.Status + "]\n";
            mins += a.Minutes;
        }
        return r + "  total: " + dayList.Count + " visits, " + mins + " min";
    }

    public int GetLoadPercent(int doctorId, DateTime day)
    {
        Doctor d = repo.FindDoctor(doctorId);
        if (d == null)
            return -1;
        int total = (d.EndHour - d.StartHour) * MinutesPerHour;
        if (d.StartHour < LunchHour && d.EndHour > LunchHour)
            total = total - MinutesPerHour;
        int busy = 0;
        foreach (Appointment a in GetDoctorDay(doctorId, day))
            busy += a.Minutes;
        return busy * 100 / total;
    }

    public void PrintStats()
    {
        Console.WriteLine("Booked: " + bookedCount + ", urgent: " + urgentCount
            + ", failed: " + failedCount);
        foreach (KeyValuePair<string, int> kv in perType)
            Console.WriteLine("  " + kv.Key + " = " + kv.Value);
        Console.WriteLine("SMS sent: " + Sms.GetSmsCount());
    }
}

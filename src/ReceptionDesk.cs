namespace ClinicReception;

public class ReceptionDesk
{
    // дозволені переходи: (дія, поточний стан) -> новий стан; для решти пар стан не змінюється
    private static readonly Dictionary<(string Action, AppointmentStatus From), AppointmentStatus> Transitions =
        new Dictionary<(string Action, AppointmentStatus From), AppointmentStatus>
        {
            [("confirm", AppointmentStatus.Booked)] = AppointmentStatus.Confirmed,
            [("complete", AppointmentStatus.Confirmed)] = AppointmentStatus.Completed,
            [("cancel", AppointmentStatus.Booked)] = AppointmentStatus.Cancelled,
            [("cancel", AppointmentStatus.Confirmed)] = AppointmentStatus.Cancelled,
            [("noshow", AppointmentStatus.Confirmed)] = AppointmentStatus.NoShow,
        };

    private ClinicRepository repo = ClinicRepository.Current;
    public string LastMessage = "";

    // повертає id пацієнта або -1, якщо дані некоректні
    public int RegisterPatient(string fullName, string phone, int birthYear, string insuranceNo,
        string email)
    {
        LastMessage = "";
        if (fullName == null || fullName.Trim().Length < 3)
        {
            LastMessage = "Name is too short";
            return -1;
        }
        if (phone == null || phone.Length < 10)
        {
            LastMessage = "Phone number is too short";
            return -1;
        }
        if (birthYear < 1900 || birthYear > DateTime.Now.Year)
        {
            LastMessage = "Wrong birth year";
            return -1;
        }
        Patient p = new Patient { Id = repo.Patients.Count + 1, FullName = fullName.Trim(),
            Phone = phone, BirthYear = birthYear, InsuranceNo = insuranceNo, Email = email };
        repo.Patients.Add(p);
        AuditLog.Write("NEW PATIENT " + p.Id);
        return p.Id;
    }

    // action приходить з кнопок форми: confirm, complete, cancel, noshow
    public bool ChangeStatus(int appointmentId, string action)
    {
        Appointment a = repo.FindAppointment(appointmentId);
        if (a == null)
        {
            LastMessage = "Appointment #" + appointmentId + " not found";
            return false;
        }
        AppointmentStatus old = a.Status;
        if (action == "complete" && old == AppointmentStatus.Cancelled)
            throw new InvalidOperationException("Cannot complete a cancelled visit");
        a.Status = NextStatus(action, old, a.Start);
        // статус не змінився = перехід заборонений
        if (a.Status == old)
        {
            LastMessage = "Cannot " + action + " appointment #" + a.Id + " in status " + old;
            return false;
        }
        LastMessage = "Appointment #" + a.Id + ": " + old + " -> " + a.Status;
        AuditLog.Write(LastMessage);
        return true;
    }

    private static AppointmentStatus NextStatus(string action, AppointmentStatus current, DateTime start)
    {
        if (!Transitions.TryGetValue((action, current), out AppointmentStatus next))
            return current;
        // неявку можна позначити лише після початку прийому
        // TODO TD-04: системний годинник без шва, як і в FeeCalculator; строк - тиждень 14
        if (next == AppointmentStatus.NoShow && start >= DateTime.Now)
            return current;
        return next;
    }
}

using ClinicReception;

namespace Legacy.Tests;

// Розрахунковий вузол варіанта 4: добір вільного часу прийому (SchedulePlanner.Book).
// Пацієнти: 1 - пенсіонер зі страховкою, 2 - без страховки, 3 - зі страховкою.
// Лікарі: 1 - 8:00-14:00, 2 - 8:00-13:00, 3 - 15:00-16:00, 4 - 12:00-15:00.
public class SchedulePlannerBookTests
{
    private readonly SchedulePlanner _planner;

    public SchedulePlannerBookTests()
    {
        TestClinic.Fresh();
        _planner = new SchedulePlanner();
    }

    private static DateTime At(int hour, int minute = 0) =>
        TestClinic.Tomorrow.AddHours(hour).AddMinutes(minute);

    [Fact]
    public void Book_EmptyDay_StartsAtDoctorStartHour()
    {
        Appointment a = _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Assert.Equal(At(8), a.Start);
        Assert.Equal(20, a.Minutes);
        Assert.Equal(AppointmentStatus.Booked, a.Status);
    }

    [Fact]
    public void Book_DoctorBusy_TakesFirstSlotAfterVisit()
    {
        _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Appointment a = _planner.Book(new BookingRequest(2, 1, "PROC", TestClinic.Tomorrow, false));

        Assert.Equal(At(8, 20), a.Start);
    }

    [Theory]
    [InlineData("CONS", 20)]
    [InlineData("FUP", 15)]
    [InlineData("PROC", 40)]
    [InlineData("VACC", 10)]
    [InlineData("XRAY", 20)]
    public void Book_VisitType_SetsDuration(string visitType, int minutes)
    {
        Appointment a = _planner.Book(new BookingRequest(2, 1, visitType, TestClinic.Tomorrow, false));

        Assert.Equal(minutes, a.Minutes);
    }

    [Fact]
    public void Book_SlotsStartingAtOne_SkipsLunchHour()
    {
        _planner.Book(new BookingRequest(1, 4, "PROC", TestClinic.Tomorrow, false));
        _planner.Book(new BookingRequest(2, 4, "CONS", TestClinic.Tomorrow, false));

        Appointment a = _planner.Book(new BookingRequest(3, 4, "FUP", TestClinic.Tomorrow, false));

        Assert.Equal(At(14), a.Start);
    }

    [Fact]
    public void Book_ProcedureAt1240_RunsIntoLunch()
    {
        // Характеризаційний тест. Обід перевіряється лише за годиною початку слота,
        // тож процедура 12:40-13:20 заходить на обідню перерву. Зафіксовано як
        // наявну поведінку, див. TD-01 у реєстрі боргу.
        _planner.Book(new BookingRequest(1, 4, "PROC", TestClinic.Tomorrow, false));

        Appointment a = _planner.Book(new BookingRequest(2, 4, "PROC", TestClinic.Tomorrow, false));

        Assert.Equal(At(12, 40), a.Start);
    }

    [Fact]
    public void Book_NotUrgentProcedureInShortShift_NoFreeSlot()
    {
        Appointment a = _planner.Book(new BookingRequest(2, 3, "PROC", TestClinic.Tomorrow, false));

        Assert.Null(a);
        Assert.Equal("No free slot for PROC", _planner.GetLastError());
    }

    [Fact]
    public void Book_UrgentProcedureInShortShift_UsesReservedHalfHour()
    {
        Appointment a = _planner.Book(new BookingRequest(2, 3, "PROC", TestClinic.Tomorrow, true));

        Assert.Equal(At(15), a.Start);
        Assert.True(a.Urgent);
    }

    [Fact]
    public void Book_PatientVisitWithin30Minutes_ShiftsStart()
    {
        _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Appointment a = _planner.Book(new BookingRequest(1, 2, "FUP", TestClinic.Tomorrow, false));

        Assert.Equal(At(8, 30), a.Start);
    }

    [Fact]
    public void Book_PatientHasVaccination_VaccinationDoesNotBlock()
    {
        _planner.Book(new BookingRequest(1, 1, "VACC", TestClinic.Tomorrow, false));

        Appointment a = _planner.Book(new BookingRequest(1, 2, "FUP", TestClinic.Tomorrow, false));

        Assert.Equal(At(8), a.Start);
    }

    [Fact]
    public void Book_UnknownPatient_ReturnsNullWithReason()
    {
        Appointment a = _planner.Book(new BookingRequest(42, 1, "CONS", TestClinic.Tomorrow, false));

        Assert.Null(a);
        Assert.Equal("Patient or doctor not found", _planner.GetLastError());
    }

    [Fact]
    public void Book_PastDate_ReturnsNullWithReason()
    {
        Appointment a = _planner.Book(new BookingRequest(1, 1, "CONS", DateTime.Today.AddDays(-1), false));

        Assert.Null(a);
        Assert.Equal("Date is in the past or doctor is on holiday", _planner.GetLastError());
    }

    [Fact]
    public void Book_DoctorOnHoliday_ReturnsNull()
    {
        _planner.AddHoliday(1, TestClinic.Tomorrow, TestClinic.Tomorrow.AddDays(3));

        Assert.Null(_planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow.AddDays(2), false)));
    }

    [Fact]
    public void Book_LastHolidayDay_IsWorkingDay()
    {
        // кінець відпустки не входить у неї: інтервал [from; to)
        _planner.AddHoliday(1, TestClinic.Tomorrow, TestClinic.Tomorrow.AddDays(3));

        Assert.NotNull(_planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow.AddDays(3), false)));
    }

    [Fact]
    public void Book_InsuredSenior_FeeWithBothDiscounts()
    {
        Appointment a = _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Assert.Equal(283.50m, a.Fee);
    }

    [Fact]
    public void Reschedule_FreeDay_CancelsOldAndBooksNew()
    {
        Appointment old = _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Appointment moved = _planner.Reschedule(old.Id, TestClinic.Tomorrow.AddDays(1));

        Assert.Equal(AppointmentStatus.Cancelled, old.Status);
        Assert.Equal(TestClinic.Tomorrow.AddDays(1).AddHours(8), moved.Start);
    }

    [Fact]
    public void Reschedule_PastDay_KeepsOldStatus()
    {
        Appointment old = _planner.Book(new BookingRequest(2, 3, "PROC", TestClinic.Tomorrow, true));

        Appointment moved = _planner.Reschedule(old.Id, DateTime.Today.AddDays(-1));

        Assert.Null(moved);
        Assert.Equal(AppointmentStatus.Booked, old.Status);
    }
}

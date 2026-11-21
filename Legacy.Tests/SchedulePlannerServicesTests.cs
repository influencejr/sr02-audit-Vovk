using ClinicReception;

namespace Legacy.Tests;

// Решта обов'язків SchedulePlanner: звіт, завантаження, SMS, відпустки, статистика.
public class SchedulePlannerServicesTests
{
    private readonly SchedulePlanner _planner;

    public SchedulePlannerServicesTests()
    {
        TestClinic.Fresh();
        _planner = new SchedulePlanner();
    }

    [Fact]
    public void BuildDayReport_TwoVisitsAndCancelled_ListsActiveVisits()
    {
        _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));
        _planner.Book(new BookingRequest(3, 1, "XRAY", TestClinic.Tomorrow, false));
        Appointment cancelled = _planner.Book(new BookingRequest(2, 1, "PROC", TestClinic.Tomorrow, false));
        cancelled.Status = AppointmentStatus.Cancelled;

        string report = _planner.BuildDayReport(1, TestClinic.Tomorrow);

        Assert.Equal(
            "Day report: Dr. Olena Shevchenko (therapist), room 12, 8:00-14:00\n"
            + "  08:00 consultation, Petro Ivanenko [Booked]\n"
            + "  08:20 visit, Andrii Tkachenko [Booked]\n"
            + "  total: 2 visits, 40 min",
            report);
    }

    [Fact]
    public void BuildDayReport_UnknownDoctor_ReturnsEmpty()
    {
        Assert.Equal("", _planner.BuildDayReport(9, TestClinic.Tomorrow));
    }

    [Fact]
    public void GetLoadPercent_DoctorWithLunch_ExcludesLunchHour()
    {
        _planner.Book(new BookingRequest(1, 1, "PROC", TestClinic.Tomorrow, false));

        // 40 хв зайнято з 300 робочих (6 годин мінус обід), ціле ділення
        Assert.Equal(13, _planner.GetLoadPercent(1, TestClinic.Tomorrow));
    }

    [Fact]
    public void GetLoadPercent_UnknownDoctor_ReturnsMinusOne()
    {
        Assert.Equal(-1, _planner.GetLoadPercent(9, TestClinic.Tomorrow));
    }

    [Theory]
    [InlineData("067 123 45 67", "+380671234567")]
    [InlineData("(050) 765-43-21", "+380507654321")]
    [InlineData("380931112233", "+380931112233")]
    [InlineData("+380931112233", "+380931112233")]
    [InlineData("12345", "")]
    [InlineData(null, "")]
    public void NormalizePhone_Formats_ReturnsInternationalOrEmpty(string? phone, string expected)
    {
        Assert.Equal(expected, _planner.Sms.NormalizePhone(phone));
    }

    [Fact]
    public void Book_Success_PutsSmsToOutbox()
    {
        _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));

        Assert.Equal(
            "+380671234567: City Clinic #3: Petro Ivanenko, consultation with Dr. Olena Shevchenko"
            + " at 08:00, room 12",
            Assert.Single(_planner.Sms.GetOutbox()));
    }

    [Fact]
    public void SendSms_Disabled_ReturnsFalse()
    {
        SmsNotifier.SmsEnabled = false;
        try
        {
            Assert.False(_planner.Sms.SendSms("0671234567", "test"));
            Assert.Empty(_planner.Sms.GetOutbox());
        }
        finally
        {
            SmsNotifier.SmsEnabled = true;
        }
    }

    [Fact]
    public void CancelHolidays_TwoHolidays_RemovesBoth()
    {
        _planner.AddHoliday(1, TestClinic.Tomorrow, TestClinic.Tomorrow.AddDays(1));
        _planner.AddHoliday(1, TestClinic.Tomorrow.AddDays(5), TestClinic.Tomorrow.AddDays(6));

        Assert.Equal(2, _planner.CancelHolidays(1));
        Assert.NotNull(_planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false)));
    }

    [Fact]
    public void AddHoliday_EndBeforeStart_ReturnsFalse()
    {
        Assert.False(_planner.AddHoliday(1, TestClinic.Tomorrow, DateTime.Today));
    }

    [Fact]
    public void PrintStats_AfterBookings_PrintsCounters()
    {
        _planner.Book(new BookingRequest(1, 1, "CONS", TestClinic.Tomorrow, false));
        _planner.Book(new BookingRequest(2, 3, "PROC", TestClinic.Tomorrow, true));
        _planner.Book(new BookingRequest(3, 3, "PROC", TestClinic.Tomorrow, false));
        StringWriter output = new StringWriter();
        TextWriter console = Console.Out;
        Console.SetOut(output);
        try
        {
            _planner.PrintStats();
        }
        finally
        {
            Console.SetOut(console);
        }

        string[] lines = output.ToString().Split(Environment.NewLine);
        Assert.Equal("Booked: 2, urgent: 1, failed: 1", lines[0]);
        Assert.Equal("  CONS = 1", lines[1]);
        Assert.Equal("  PROC = 1", lines[2]);
        Assert.Equal(2, _planner.GetBookedCount());
    }
}

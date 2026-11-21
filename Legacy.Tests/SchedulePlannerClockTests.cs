using ClinicReception;

namespace Legacy.Tests;

// Запис «на сьогодні» залежить від поточного часу. Шов - віртуальний метод
// SchedulePlanner.Now(), тест підставляє фіксований момент через нащадка.
public class SchedulePlannerClockTests
{
    private static readonly DateTime Monday1020 = new DateTime(2031, 3, 3, 10, 20, 0);

    public SchedulePlannerClockTests()
    {
        TestClinic.Fresh();
    }

    [Fact]
    public void Book_TodayAt1020_StartsFromNextFullHour()
    {
        SchedulePlanner planner = new FixedClockPlanner(Monday1020);

        Appointment a = planner.Book(new BookingRequest(2, 1, "CONS", Monday1020.Date, false));

        Assert.Equal(Monday1020.Date.AddHours(11), a.Start);
    }

    [Fact]
    public void Book_TodayAfterShiftEnds_NoFreeSlot()
    {
        SchedulePlanner planner = new FixedClockPlanner(Monday1020.Date.AddHours(13).AddMinutes(10));

        Assert.Null(planner.Book(new BookingRequest(2, 1, "CONS", Monday1020.Date, false)));
        Assert.Equal("No free slot for CONS", planner.GetLastError());
    }

    [Fact]
    public void Book_DayBeforeFixedToday_IsPast()
    {
        SchedulePlanner planner = new FixedClockPlanner(Monday1020);

        Assert.Null(planner.Book(new BookingRequest(2, 1, "CONS", Monday1020.Date.AddDays(-1), false)));
    }

    private sealed class FixedClockPlanner : SchedulePlanner
    {
        private readonly DateTime _now;

        public FixedClockPlanner(DateTime now)
        {
            _now = now;
        }

        protected override DateTime Now()
        {
            return _now;
        }
    }
}

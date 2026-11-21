using ClinicReception;

namespace Legacy.Tests;

public class ReceptionDeskStatusTests
{
    private readonly ReceptionDesk _desk;
    private readonly Appointment _appointment;

    public ReceptionDeskStatusTests()
    {
        TestClinic.Fresh();
        _desk = new ReceptionDesk();
        _appointment = new SchedulePlanner().Book(2, 1, "CONS", TestClinic.Tomorrow, false);
    }

    [Fact]
    public void ChangeStatus_ConfirmBooked_BecomesConfirmed()
    {
        bool ok = _desk.ChangeStatus(_appointment.Id, "confirm");

        Assert.True(ok);
        Assert.Equal(AppointmentStatus.Confirmed, _appointment.Status);
        Assert.Equal($"Appointment #{_appointment.Id}: Booked -> Confirmed", _desk.LastMessage);
    }

    [Fact]
    public void ChangeStatus_CompleteBooked_Refused()
    {
        bool ok = _desk.ChangeStatus(_appointment.Id, "complete");

        Assert.False(ok);
        Assert.Equal(AppointmentStatus.Booked, _appointment.Status);
        Assert.Equal($"Cannot complete appointment #{_appointment.Id} in status Booked", _desk.LastMessage);
    }

    [Fact]
    public void ChangeStatus_CompleteCancelled_Throws()
    {
        _desk.ChangeStatus(_appointment.Id, "cancel");

        Assert.Throws<InvalidOperationException>(() => _desk.ChangeStatus(_appointment.Id, "complete"));
    }

    [Fact]
    public void ChangeStatus_UnknownAppointment_ReturnsFalse()
    {
        bool ok = _desk.ChangeStatus(777, "confirm");

        Assert.False(ok);
        Assert.Equal("Appointment #777 not found", _desk.LastMessage);
    }
}

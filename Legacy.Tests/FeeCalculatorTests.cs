using ClinicReception;

namespace Legacy.Tests;

public class FeeCalculatorTests
{
    // вік рахується від поточного року, тож рік народження задається відносно нього
    [Theory]
    [InlineData("CONS", false, "", 40, 450.00)]
    [InlineData("PROC", true, "", 40, 1125.00)]
    [InlineData("FUP", false, "UA-1", 40, 210.00)]
    [InlineData("VACC", false, "", 65, 225.00)]
    [InlineData("XRAY", true, "UA-1", 70, 354.38)]
    public void CalcFee_TypeUrgencyInsuranceAge_ReturnsFee(
        string visitType, bool urgent, string insuranceNo, int age, double expected)
    {
        Appointment a = new Appointment { VisitType = visitType, Urgent = urgent };
        Patient p = new Patient { InsuranceNo = insuranceNo, BirthYear = DateTime.Now.Year - age };

        decimal fee = new FeeCalculator().CalcFee(a, p);

        Assert.Equal((decimal)expected, fee);
    }

    [Fact]
    public void FormatInvoice_UrgentProcedure_MentionsUrgency()
    {
        Appointment a = new Appointment { Id = 1005, VisitType = "PROC", Urgent = true, Fee = 1125m };
        Patient p = new Patient { FullName = "Maria Koval" };

        Assert.Equal(
            "Invoice #1005: Maria Koval, procedure (urgent) - 1125.00 UAH",
            new FeeCalculator().FormatInvoice(a, p));
    }
}

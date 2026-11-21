namespace ClinicReception;

// Відпустка лікаря - напіввідкритий інтервал днів [From; To): день To уже робочий.
public record Holiday(int DoctorId, DateTime From, DateTime To)
{
    public bool Covers(int doctorId, DateTime day)
    {
        return DoctorId == doctorId && day.Date >= From && day.Date < To;
    }
}

namespace ClinicReception;

// Запит на запис до лікаря: хто, до кого, який візит, на який день і чи терміновий.
public record BookingRequest(int PatientId, int DoctorId, string VisitType, DateTime Day, bool Urgent);

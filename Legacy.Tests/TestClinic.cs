using ClinicReception;

// Статичний стан успадкованого коду (ClinicRepository.Current, лічильник SMS,
// прапорець SmsEnabled) спільний для всіх тестів, тому паралельний запуск вимкнено.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Legacy.Tests;

// Кожен тест отримує свіжий репозиторій. ClinicRepository.Current - публічне
// статичне поле, тож підмінити його можна без жодної зміни в коді.
internal static class TestClinic
{
    public static DateTime Tomorrow => DateTime.Today.AddDays(1);

    public static ClinicRepository Fresh()
    {
        ClinicRepository repo = new ClinicRepository();
        ClinicRepository.Current = repo;
        repo.AddDoctor(1, "Olena Shevchenko", "therapist", 12, 8, 14);
        repo.AddDoctor(2, "Taras Bondar", "surgeon", 21, 8, 13);
        repo.AddDoctor(3, "Iryna Melnyk", "cardiologist", 7, 15, 16);
        repo.AddDoctor(4, "Pavlo Lysenko", "dentist", 3, 12, 15);
        ReceptionDesk desk = new ReceptionDesk();
        desk.RegisterPatient("Petro Ivanenko", "067 123 45 67", 1948, "UA-55120", "");
        desk.RegisterPatient("Maria Koval", "(050) 765-43-21", 1985, "", "mk@ukr.net");
        desk.RegisterPatient("Andrii Tkachenko", "0931112233", 1992, "UA-90014", "");
        return repo;
    }
}

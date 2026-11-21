# Реєстр дефектів проєктування коду (стан baseline)

Код пройдено за каталогом теми 7, пункт за пунктом. Номери рядків - за
комітом `baseline`. Серйозність - за шкалою 1–5, частота - кількість місць
(понад чотири рахується як 4), пріоритет = серйозність × частота.

| ID | Місце (файл:рядки) | Дефект | Серйозність | Частота | Пріоритет |
|---|---|---|---|---|---|
| D-01 | SchedulePlanner.cs:4-288 | Великий клас (профільний) | 5 | 1 | 5 |
| D-02 | SchedulePlanner.cs:21-103 | Довгий метод | 5 | 1 | 5 |
| D-03 | SchedulePlanner.cs:171-179, 238-246; FeeCalculator.cs:34-41 | Дублювання коду | 4 | 3 | 12 |
| D-04 | SchedulePlanner.cs:111-123, 225-233 | Дублювання коду | 3 | 2 | 6 |
| D-05 | SchedulePlanner.cs:38-45, 172-179, 239-246; FeeCalculator.cs:11-18, 34-41 | Дробовик | 4 | 4 | 16 |
| D-06 | SchedulePlanner.cs:54, 59, 61, 78; FeeCalculator.cs:20-27 | Магічне число | 3 | 4 | 12 |
| D-07 | SchedulePlanner.cs:10, 142-143, 150-160 | Одержимість елементарними типами | 3 | 3 | 9 |
| D-08 | SchedulePlanner.cs:9, 57, 83, 87, 94 | Тимчасове поле | 2 | 1 | 2 |
| D-09 | SchedulePlanner.cs:159; AuditLog.cs:15-17 | Проковтнутий виняток | 4 | 2 | 8 |
| D-10 | SchedulePlanner.cs:46-47, 283-287 | Мертвий код | 2 | 2 | 4 |
| D-11 | SchedulePlanner.cs:268-272 | Заздрість до чужих даних | 2 | 1 | 2 |
| D-12 | SchedulePlanner.cs:3, 20, 54 | Коментар-виправдання | 3 | 3 | 9 |
| D-13 | SchedulePlanner.cs:21; ReceptionDesk.cs:9; ClinicRepository.cs:51 | Довгий список параметрів | 2 | 3 | 6 |

Типів дефектів - 12 різних, записів - 13.

## D-01. Великий клас `SchedulePlanner` (профільний дефект варіанта)

- Місце: SchedulePlanner.cs, рядки 4–288, увесь клас.
- Дефект: 13 полів і 20 методів, 195 логічних рядків - половина коду пакета.
  У класі чотири незалежні групи полів: пошук часу (`repo`, `fees`, `lastError`,
  `foundAt`), відпустки (`holidays`), SMS (`clinicName`, `SmsEnabled`,
  `smsCounter`, `outbox`) і статистика (`bookedCount`, `urgentCount`,
  `failedCount`, `perType`).
- Доказ:

```csharp
// Розклад. Сюди ж смс, відпустки лікарів, статистика
public class SchedulePlanner
{
    private ClinicRepository repo = ClinicRepository.Current;
    private FeeCalculator fees = new FeeCalculator();
    private string lastError = "";
    private DateTime foundAt;
    private List<string> holidays = new List<string>(); // "doctorId|from|to"
    private string clinicName = "City Clinic #3";
    public static bool SmsEnabled = true;
    private static int smsCounter = 0;
    private List<string> outbox = new List<string>();
```

- Чим шкодить: зміна формату SMS, правил відпусток чи статистики відкриває
  той самий файл, що й пошук вільного часу; тест SMS мусить створювати весь
  планувальник із репозиторієм.
- Категорія впливу: змінюваність, тестованість, читаність.
- Серйозність 5, частота 1, пріоритет 5.
- Гіпотеза: Extract Class для SMS і статистики, Move Method для опису лікаря.

## D-02. Довгий метод `Book`

- Місце: SchedulePlanner.cs, рядки 21–103, метод `Book`.
- Дефект: 54 логічні рядки, CC = 21, вкладеність 5, п'ять параметрів.
- Доказ (найглибший фрагмент):

```csharp
for (DateTime t = from; t.AddMinutes(len) <= to; t = t.AddMinutes(5))
{
    if (t.Hour == 13)
        continue; // обід
    bool ok = true;
    foreach (Appointment a in dayList)
    {
        if (a.Start < t.AddMinutes(len))
        {
            if (t < a.Start.AddMinutes(a.Minutes))
            {
                ok = false;
```

- Чим шкодить: для покриття всіх незалежних шляхів потрібен 21 тест; правило
  перетину слотів, обід, резерв для термінових і інтервал між візитами
  пацієнта змішані в одному циклі.
- Категорія впливу: читаність, тестованість, змінюваність.
- Серйозність 5, частота 1, пріоритет 5.
- Гіпотеза: Extract Method (`DurationOf`, `FindFreeSlot`, `IsDoctorFree`,
  `IsPatientFree`), Replace Nested Conditional with Guard Clauses.

## D-03. Дублювання: назва типу візиту

- Місце: SchedulePlanner.cs:171–179 (`formatSms`), 238–246 (`BuildDayReport`),
  FeeCalculator.cs:34–41 (`FormatInvoice`).
- Дефект: клон на 9 рядків, 3 входження.
- Доказ:

```csharp
string vt;
switch (a.VisitType)
{
    case "CONS": vt = "consultation"; break;
    case "FUP": vt = "follow-up"; break;
    case "PROC": vt = "procedure"; break;
    case "VACC": vt = "vaccination"; break;
    default: vt = "visit"; break;
}
```

- Чим шкодить: нова назва чи виправлення друкарської помилки вимагає трьох
  однакових правок у двох файлах; пропущена копія дасть різні назви в SMS і
  в рахунку.
- Категорія впливу: змінюваність, надійність.
- Серйозність 4, частота 3, пріоритет 12.
- Гіпотеза: Replace Conditional with Table - один довідник типів візиту.

## D-04. Дублювання: записи лікаря за день

- Місце: SchedulePlanner.cs:111–123 (`GetDoctorDay`), 225–233 (`BuildDayReport`).
- Дефект: клон на 9 рядків, 2 входження.
- Доказ:

```csharp
List<Appointment> dayList = new List<Appointment>();
foreach (Appointment x in repo.Appointments)
{
    if (x.DoctorId != doctorId || x.Start.Date != day.Date)
        continue;
    if (x.Status == AppointmentStatus.Cancelled)
        continue;
    dayList.Add(x);
}
```

- Чим шкодить: якщо правило «скасовані не займають часу» зміниться, звіт і
  пошук слотів розійдуться.
- Категорія впливу: змінюваність, надійність.
- Серйозність 3, частота 2, пріоритет 6.
- Гіпотеза: Extract Method - звіт викликає наявний `GetDoctorDay`.

## D-05. Дробовик: тип візиту

- Місце: SchedulePlanner.cs:38–45, 172–179, 239–246; FeeCalculator.cs:11–18, 34–41.
- Дефект: один і той самий `switch` за рядковим кодом типу візиту в п'яти
  місцях і двох файлах: тривалість, назва (тричі) і ціна.
- Доказ:

```csharp
switch (visitType)
{
    case "CONS": len = 20; break;
    case "FUP": len = 15; break;
    case "PROC": len = 40; break;
    case "VACC": len = 10; break;
    default: len = 20; break;
}
```

- Чим шкодить: новий тип візиту (наприклад, УЗД) вимагає п'яти правок, і
  пропущена правка не дає помилки компіляції - тип мовчки отримає значення
  `default`.
- Категорія впливу: змінюваність, надійність.
- Серйозність 4, частота 4 (п'ять місць), пріоритет 16.
- Гіпотеза: Replace Conditional with Table - довідник `VisitTypes` з
  тривалістю й назвою; ціни лишаються у `FeeCalculator` (борг).

## D-06. Магічні числа

- Місце: SchedulePlanner.cs:54 (30), 59 (5), 61 (13), 78 (30); FeeCalculator.cs:20–27
  (0.8, 1.25, 0.7, 65, 0.1).
- Дефект: числа без імен у правилах розкладу й ціни.
- Доказ:

```csharp
if (!urgent)
    to = to.AddMinutes(-30); // остання година - для термінових
...
for (DateTime t = from; t.AddMinutes(len) <= to; t = t.AddMinutes(5))
{
    if (t.Hour == 13)
        continue; // обід
...
    if (Math.Abs((a.Start - t).TotalMinutes) < 30)
```

- Чим шкодить: два різні 30 означають різне (резерв для термінових і
  інтервал між візитами пацієнта), і пошук «30» по коду не скаже, яке з них
  змінювати.
- Категорія впливу: читаність, змінюваність.
- Серйозність 3, частота 4, пріоритет 12.
- Гіпотеза: Replace Magic Number with Constant.

## D-07. Одержимість елементарними типами: відпустки рядками

- Місце: SchedulePlanner.cs:10, 142–143, 150–160.
- Дефект: відпустка зберігається як рядок `"doctorId|from|to"` і щоразу
  розбирається назад.
- Доказ:

```csharp
holidays.Add(doctorId + "|" + from.ToString("yyyy-MM-dd") + "|"
    + to.ToString("yyyy-MM-dd"));
...
string[] parts = h.Split('|');
try
{
    if (int.Parse(parts[0]) == doctorId && day.Date >= DateTime.Parse(parts[1])
        && day.Date < DateTime.Parse(parts[2]))
        return true;
}
catch { }
```

- Чим шкодить: дата форматується без культури, а розбирається з культурою
  системи; помилку розбору ковтає порожній `catch`, і відпустка мовчки
  перестає діяти.
- Категорія впливу: надійність, читаність.
- Серйозність 3, частота 3, пріоритет 9.
- Гіпотеза: Replace Primitive with Value Object - запис `Holiday`.

## D-08. Тимчасове поле `foundAt`

- Місце: SchedulePlanner.cs:9, 57, 83, 87, 94.
- Дефект: поле класу заповнюється й читається лише всередині одного виклику
  `Book`.
- Доказ:

```csharp
private DateTime foundAt;
...
foundAt = DateTime.MinValue;
...
        foundAt = t;
...
if (foundAt == DateTime.MinValue)
```

- Чим шкодить: читач шукає, хто ще використовує поле, і не знаходить; два
  виклики `Book` з різних потоків зіпсували б одне одному результат.
- Категорія впливу: читаність, надійність.
- Серйозність 2, частота 1, пріоритет 2.
- Гіпотеза: Extract Method `FindFreeSlot`, що повертає знайдений час.

## D-09. Проковтнуті винятки

- Місце: SchedulePlanner.cs:153–159 (`IsOnHoliday`), AuditLog.cs:9–17 (`Write`).
- Дефект: два порожні `catch`.
- Доказ:

```csharp
try
{
    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    File.AppendAllText(FileName, stamp + " " + message + "\n");
}
catch
{
}
```

- Чим шкодить: якщо файл журналу недоступний, дії рецепції не записуються, і
  ніхто про це не дізнається; у `IsOnHoliday` зламаний рядок відпустки
  мовчки вважається відсутньою відпусткою.
- Категорія впливу: надійність.
- Серйозність 4, частота 2, пріоритет 8.
- Гіпотеза: `IsOnHoliday` - зникне разом з D-07; `AuditLog` - борг (зміна
  поведінки).

## D-10. Мертвий код

- Місце: SchedulePlanner.cs:46–47 (закоментована перевірка неділі),
  283–287 (`IsLunchBreak` ніде не викликається).
- Дефект: закоментований блок і невикористаний приватний метод.
- Доказ:

```csharp
//if (day.DayOfWeek == DayOfWeek.Sunday)
//    { lastError = "Clinic is closed on Sunday"; return null; }
...
private bool IsLunchBreak(DateTime t, int minutes)
{
    DateTime lunch = t.Date.AddHours(13);
    return t < lunch.AddHours(1) && t.AddMinutes(minutes) > lunch;
}
```

- Чим шкодить: невідомо, чи клініка працює в неділю. `IsLunchBreak` рахує обід
  правильно (з урахуванням тривалості), а `Book` - ні, тож читач не розуміє,
  яка перевірка діє.
- Категорія впливу: читаність.
- Серйозність 2, частота 2, пріоритет 4.
- Гіпотеза: Remove Dead Code; невідповідність обіду - у борг (TD-01).

## D-11. Заздрість до чужих даних: `DescribeDoctor`

- Місце: SchedulePlanner.cs:268–272.
- Дефект: метод планувальника працює лише з полями `Doctor`.
- Доказ:

```csharp
public string DescribeDoctor(Doctor d)
{
    return "Dr. " + d.Name + " (" + d.Specialty + "), room " + d.Room + ", "
        + d.StartHour + ":00-" + d.EndHour + ":00";
}
```

- Чим шкодить: опис лікаря шукають у класі лікаря, а знаходять у розкладі;
  планувальник залежить від усіх полів `Doctor`.
- Категорія впливу: читаність, змінюваність.
- Серйозність 2, частота 1, пріоритет 2.
- Гіпотеза: Move Method у `Doctor`.

## D-12. Коментарі-виправдання

- Місце: SchedulePlanner.cs:3, 20, 54.
- Дефект: коментарі пояснюють те, що мав би пояснювати код, а один ще й бреше.
- Доказ:

```csharp
// Розклад. Сюди ж смс, відпустки лікарів, статистика
public class SchedulePlanner
...
// Повертає null, причину див. у GetLastError()
public Appointment Book(...)
...
    to = to.AddMinutes(-30); // остання година - для термінових
```

- Чим шкодить: перший коментар виправдовує великий клас, другий - код помилки
  замість винятку, третій суперечить коду: резерв для термінових - 30 хвилин,
  а не година.
- Категорія впливу: читаність.
- Серйозність 3, частота 3, пріоритет 9.
- Гіпотеза: Rename і константа `UrgentReserveMinutes` замість коментаря.

## D-13. Довгий список параметрів

- Місце: SchedulePlanner.cs:21 (`Book`, 5), ReceptionDesk.cs:9 (`RegisterPatient`, 5),
  ClinicRepository.cs:51 (`AddDoctor`, 6).
- Дефект: п'ять і більше параметрів, частина з них однотипні.
- Доказ:

```csharp
public Appointment Book(int patientId, int doctorId, string visitType, DateTime day,
    bool urgent)
...
public void AddDoctor(int id, string name, string specialty, int room, int startHour,
    int endHour)
```

- Чим шкодить: `Book(2, 1, ...)` і `Book(1, 2, ...)` компілюються однаково, і
  переплутаного пацієнта з лікарем видно лише у звіті.
- Категорія впливу: надійність, читаність.
- Серйозність 2, частота 3, пріоритет 6.
- Гіпотеза: Introduce Parameter Object для `Book`.

# sr02-audit-Vovk

Самостійна робота № 2 з дисципліни «Конструювання програмного забезпечення»:
аудит якості та рефакторинг успадкованого коду. Виконав Вовк Давід, група 471,
варіант № 4 (ClinicReception).

## Вихідні дані

- Джерело коду А: пакет `V04-ClinicReception` з навчального репозиторію
  https://github.com/andreichenko256/legacy-code, перенесений без змін
  (коміт і тег `baseline`).
- Модуль-донор: `SchedulePlanner.Book` - добір вільного часу прийому.
- Профільний дефект: великий клас `SchedulePlanner` (13 полів, 20 методів),
  обов'язкові рефакторинги Extract Class і Move Method.

## Структура

| Шлях | Вміст |
|---|---|
| `src/` | код, що аудитується (консольний застосунок .NET 8) |
| `Legacy.Tests/` | характеризаційні тести (xUnit) |
| `tools/metrics.py`, `tools/chart.py` | підрахунок метрик і діаграми |
| `docs/inventory.md` | інвентарний опис |
| `docs/metrics-before.csv`, `docs/metrics-after.csv` | метрики до і після |
| `docs/defects.md` | реєстр дефектів проєктування коду |
| `docs/plan.md` | план рефакторингу й правила безпеки |
| `docs/decisions.md` | журнал рішень |
| `docs/debt.md` | реєстр технічного боргу й план погашення |
| `docs/out-before.txt`, `docs/out-after.txt` | вивід демонстраційного сценарію до і після |

Теги: `baseline` - вихідний код, `safety-net` - сітка тестів готова,
`after-refactoring` - виконано всі рефакторинги.

## Як запустити

```bash
dotnet build Legacy.sln
dotnet test Legacy.sln
dotnet test Legacy.sln --collect:"XPlat Code Coverage"
dotnet run --project src
python tools/metrics.py src
```

Перевірка незмінності поведінки:

```bash
dotnet run --project src > out-now.txt
git diff --no-index docs/out-before.txt out-now.txt
```

Демо записує пацієнтів на завтрашній день, тож обидва виводи порівнюються в
межах одного дня.

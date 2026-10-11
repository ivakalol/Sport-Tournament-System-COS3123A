# Реално изпълнена проверка

Дата: 10 октомври 2026 г. (Europe/Kiev). Последният пълен REST run: `2026-10-10T13:45:32.1587925+00:00` UTC.

## Среда

- Windows host, стабилен .NET SDK 10.0.202; projects target `net10.0`.
- Docker Desktop Linux engine, Docker 28.4.0, Compose 2.39.4.
- MySQL `mysql:8.0`, три отделни DB containers и named volumes.
- API images се изграждат от .NET 10 SDK и ASP.NET 10 runtime images.
- EF Core/Relational/Design 9.0.14, Pomelo 9.0.0; Swashbuckle 10.3.0.

## Потвърдени резултати

| Проверка | Реален резултат |
|---|---|
| `dotnet build SportTournaments.sln -c Release` | Успешен build на всички 10 projects, 0 warnings, 0 errors |
| `dotnet test SportTournaments.sln -c Release` | **34 passed, 0 failed, 0 skipped** |
| `dotnet ef migrations has-pending-model-changes` за трите услуги | Няма разлика между models и включените migrations |
| `docker compose config --quiet` | Валиден Compose |
| `docker compose up -d --build` | Трите API images са реално построени; 6 containers работят |
| `scripts/demo.ps1 -VerifyFailure -VerifyPersistence` | **79 passed checks, 0 failed**, отчетът е записан |
| Health и Swagger JSON на 5001/5002/5003 | HTTP 200; MySQL достъпен |
| Две конкурентни заявки за capacity 1 | Един HTTP 201 и един HTTP 409, без надвишен капацитет |
| Временно спрян Participant API | Signup HTTP 503; няма нов registration; след start отново HTTP 201 |
| Compose down/up, без `-v` | Турнирът, winners, participant и registrations остават след пресъздаване на containers |
| Локален `dotnet run` за трите APIs с launchSettings | Health/Swagger HTTP 200 на правилните ports; real localhost typed clients + MySQL statistics работят |
| NuGet vulnerable package check с transitive packages | Не са отчетени vulnerable dependencies от текущите NuGet sources |
| Swagger screenshots | Три истински снимки с HTTP 200 и live persisted responses; визуално прегледани |

Изолираният test suite използва relational SQLite и network stubs. Това не се представя като MySQL test. REST сценарият отделно проверява production MySQL provider/containers, връзките между services, capacity/overlap, prizes, winners, status rules, delete rules, data validation, error handling и persistence.

В процеса на реалния REST run е открит и поправен дефект при сравняване на .NET ticks с MySQL microseconds. Добавен е regression test за редакция на occupied tournament с непроменени dates.

## Demo IDs от последния успешен run

| Обект | ID |
|---|---|
| Finished basketball tournament | 10 |
| Upcoming football tournament | 11 |
| Participant с един Finished participation | 11 |

Тези IDs са валидни за проверената локална DB. При нова машина изпълни demo script, който генерира собствен набор и актуализира report.

Подробните request statuses и assertions са в [verification-results.json](verification-results.json). Три Swagger GET заявки допълнително показват реалните persisted winners, participant counts и registration snapshots. Виж [Project screenshots](PROJECT.md#screenshots-на-работещите-услуги).

## Неизпълнени проверки / ограничения

- Не е управляван/тестван самият **Rider GUI**: проверени са solution build, project references, launch profiles и еквивалентният локален `dotnet run`. Отварянето, breakpoints и Compound configuration са описани като стъпки за потребителя.
- `demo.http` е предоставен за Rider HTTP Client; неговите IDE response handlers не са изпълнявани в Rider. Еквивалентните REST заявки са изпълнени от demo script.
- Няма публичен deployment, GitHub push или Canvas upload. Репото и чистият ZIP са подготвени за предаване.
- Няма authentication/authorization, както в workshop-а. Не са правени load/performance или production security tests.
- Parallel **signups** са проверени и сериализирани. Parallel organizer update/delete/cancel/finish срещу signup в отделната услуга няма distributed atomicity; последователни organizer workflows са учебното ограничение. Виж PROJECT.md.

**Блокери за build/start/учебното demo в тази среда: няма.** Известните граници по-горе не трябва да се представят като production guarantees.

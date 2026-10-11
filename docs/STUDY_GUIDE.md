# Учебен план за 2-3 часа - файл по файл

Не е нужно да помниш целия код. Нужно е да можеш да проследиш request от URL до MySQL, да посочиш къде е всяко бизнес правило и да обясниш границите. Чети кода в Rider и изпълнявай заявките паралелно с четенето.

## План за 150 минути

| Време | Работа | Проверка, че си разбрал |
|---|---|---|
| 0-15 min | README, старт на Compose, Swagger, solution tree | Сам стартираш и намираш трите APIs |
| 15-35 min | Domain models и AppDbContexts | Разграничаваш entity, DbSet, table, FK и remote ID |
| 35-65 min | TournamentService | Знаеш CRUD/status/winners/delete validations |
| 65-90 min | RegistrationService и typed clients | Проследяваш capacity/overlap и HTTP calls |
| 90-105 min | Controllers, DTOs, Program.cs | Разбираш routes, DI, response codes |
| 105-120 min | Dockerfiles, Compose, migrations | Обясняваш host/container port, DB ownership, persistence |
| 120-135 min | Tests + demo.http | Предсказваш кои заявки дават 400/404/409/503 |
| 135-150 min | Презентацията на глас с таймер | Минаваш през всички 4 изисквани части за 10 min |

За 2 часа намали първите обяснения и направи един rehearsal. За 3 часа повтори demo/debug flow и отдели 20 min за въпросите в края.

## Root files

| Файл | Прочети / разбери |
|---|---|
| `SportTournaments.sln` | Solution групира projects; отваряш този файл в Rider. Не е runtime код |
| `global.json` | Избира стабилен .NET 10 SDK, а не preview |
| `dotnet-tools.json` | Локален dotnet-ef инструмент; `dotnet tool restore` |
| `compose.yaml` | 6 services: 3 MySQL + 3 API; ports, connection strings, DNS, healthchecks, volumes |
| `.dockerignore` | Изключва Git/IDE/build/test/docs от Docker build context |
| `.gitignore` | Не качва bin/obj/IDE local files в GitHub |
| `.editorconfig` | UTF-8 и последователно форматиране в Rider |
| `demo.http` | Ръчни JSON заявки; response handlers запомнят създадените IDs |
| `scripts/demo.ps1` | Повторяем REST сценарий и assertions, генерира бъдещи дати и собствен demo набор |

## Tournament - `src/Tournament/`

### `SportTournaments.Tournament.Domain`

| Файл | За какво служи |
|---|---|
| `Tournament.cs` | Entity + два enums. Name/type/description/dates/location/capacity/prizes/status са requirement полетата |
| `Winner.cs` | Един ред за едно място. TournamentId е local FK; ParticipantId е remote ID. ParticipantName е snapshot |
| `AppDbContext.cs` | `DbSet<Tournament>` и `DbSet<Winner>`; EF mapping към SQL. Prize precision(12,2); enums като strings; UTC date conversion; unique constraints |
| `Migrations/*_InitialCreate.cs` | `Up` създава таблици и indexes; `Down` отменя схемата. Генерира се от EF, не се учи наизуст |
| `Migrations/*_InitialCreate.Designer.cs` | Генериран target model за конкретната migration |
| `Migrations/AppDbContextModelSnapshot.cs` | Последният model, с който EF сравнява следваща промяна |
| `*.csproj` | net10.0, EF Core 9.0.14, Pomelo 9.0.0 и migration tooling |

### `SportTournaments.Tournament`

**`Dtos/TournamentDtos.cs`:** `TournamentRequest` допуска само полета за create/update. Няма Id/Status, защото тези стойности се управляват от service. `WinnerRequest` има participantId/place/points, а `SubmitWinnersRequest` изисква три реда. Response records описват публичния JSON, не EF navigation objects. `JsonRequired` пази задължителните value fields от липсващ JSON input.

**`TournamentService.cs`:** прочети в този ред:

1. `CreateAsync` → `Validate` → `Apply` → Add → SaveChanges. Status идва от entity default Upcoming.
2. `GetAllAsync` → optional filters, AsNoTracking, order по StartDate.
3. `UpdateAsync` → RequireUpcoming, валидира dates, пита Registration, пази capacity/schedule. `NormalizeUtc` изравнява .NET date ticks с MySQL microseconds.
4. `DeleteAsync` → първо Finished guard; после Upcoming registrations guard; Remove/Save.
5. `SubmitWinnersAsync` → точно три различни participants/places, remote registration check, локална transaction за replacement.
6. `FinishAsync` → има ли 3 winners, след това Status=Finished. `CancelAsync` → Canceled и премахва provisional results.
7. `GetWinnersAsync` → winner + prize за мястото. `GetParticipantsAsync` → registrations + statistics от HTTP client.

**`RegistrationApiClient.cs`:** GET от чуждия API. `EnsureSuccessStatusCode` отхвърля неуспех; 404 е специално липсващ ресурс. DTO records съдържат само нужните remote полета. HTTP details не са разпръснати в controllers.

**`*.csproj`:** service library реферира само своя Domain project. Не реферира Registration project: границата се минава с HTTP/JSON.

### `SportTournaments.Tournament.API`

| Файл | За какво служи |
|---|---|
| `Controllers/TournamentsController.cs` | Routes/verbs, параметри, извиква service, `Ok`, `CreatedAtAction`, `NoContent` |
| `Program.cs` | Composition root: DI, DbContext/MySQL, typed client, JSON enum strings, Swagger, middleware, migrations, controllers |
| `ErrorHandlingMiddleware.cs` | Exception → ProblemDetails: 400/404/409/503/500; logger запазва diagnostic details |
| `appsettings.json` | Local DB connection и localhost адреси на services |
| `Properties/launchSettings.json` | Rider/dotnet run profile: Development, port 5001, Swagger launch URL |
| `Dockerfile` | SDK image компилира/publish-ва; runtime image стартира API DLL на 8080 |
| `*.csproj` | Web SDK, references към service/Domain, Swagger/resilience/EF design packages |

## Participant - `src/Participant/`

| Файл (спрямо съответния проект) | Какво прави |
|---|---|
| `.Domain/Participant.cs` | Само Id и Name; човекът не е registration |
| `.Domain/AppDbContext.cs` | Собствен Participants DbSet и Name max length |
| `.Domain/Migrations/` - трите EF files | Същата роля като Tournament migrations, но само за Participant DB |
| `ParticipantService.cs` | Create/list/basic details; statistics endpoint обединява профила с Registration counts |
| `Dtos/ParticipantDtos.cs` | Request name, response profile, enriched statistics |
| `RegistrationApiClient.cs` | Само HTTP statistics lookup; няма SQL към Registration DB |
| `.API/Controllers/ParticipantsController.cs` | Четири endpoints, еднакъв controller pattern |
| `.API/Program.cs` | DI за ParticipantService + client, собствен DbContext |
| `.API/ErrorHandlingMiddleware.cs` | Еднакви HTTP error conventions, независим API |
| `.API/appsettings.json` | Participant DB на host 3310; Registration API на 5003 |
| `.API/Properties/launchSettings.json` | Local API port 5002 |
| `.API/Dockerfile` | Publish/execute Participant API |
| Трите `.csproj` | Същите references и roles като Tournament |

Важно: Registration signup извиква **basic** GET profile, не statistics. Иначе би могъл да се получи рекурсивен request между услугите.

## Registration - `src/Registration/`

| Файл | Какво прави |
|---|---|
| `.Domain/Registration.cs` | Relation между remote IDs + readable snapshots и RegisteredAt |
| `.Domain/AppDbContext.cs` | Един DbSet, unique `(TournamentId, ParticipantId)`, ParticipantId index, UTC mapping |
| `.Domain/Migrations/` - трите EF files | Таблицата Registrations и indexes в собствената DB |
| `Dtos/RegistrationDtos.cs` | Входът е само двата IDs; responses включват snapshots/history/counts |
| `ParticipantApiClient.cs` | Проверка на реалния човек с HTTP GET |
| `TournamentApiClient.cs` | Взема live status, capacity и dates с HTTP GET |
| `RegistrationWriteLock.cs` | MySQL named lock; защитава checks+insert от parallel signups; освобождава се чрез await using |
| `RegistrationService.cs` | Бизнес алгоритъм за signup + история/statistics |
| `.API/Controllers/RegistrationsController.cs` | HTTP POST и GET routes; няма SQL или overlap logic |
| `.API/Program.cs` | Регистрира двата typed clients, service, own DbContext и общите API функции |
| `.API/ErrorHandlingMiddleware.cs` | Същият HTTP failure contract |
| `.API/appsettings.json` | Registration DB host 3311; Tournament/Participant localhost ports |
| `.API/Properties/launchSettings.json` | Local API port 5003 |
| `.API/Dockerfile` | Publish/execute Registration API |
| Трите `.csproj` | Същият three-layer pattern; няма references към чужди Domain projects |

### Проследи signup на глас

`POST /api/registrations` → controller приема RegistrationRequest → service взема write lock → търси participant през HTTP → търси tournament през HTTP → проверява Upcoming/future → проверява duplicate → проверява capacity → проверява previous time ranges/current status → създава snapshot entity → SaveChangesAsync → lock release → `201 Created`.

При failure преди Save няма нов registration. Съществуващите canceled events не блокират часове. Съседни интервали са позволени. History с deleted canceled event запазва името от snapshot.

## Tests - `tests/SportTournaments.Tests/`

| Файл | Роля |
|---|---|
| `TestSupport.cs` | Изолирана relational SQLite DB, fixed clock и HTTP handler stubs; не са production dependencies |
| `TournamentTests.cs` | Invalid dates/prizes/type/capacity, update/delete, winners replacement/finalization/cancel |
| `RegistrationTests.cs` | Missing IDs/status/capacity/duplicate/overlap/canceled/deleted/history/outage |
| `SportTournaments.Tests.csproj` | xUnit/test SDK + references към трите services, test-only SQLite |

Изолираният тест може да докаже service rule при контролирани отговори, но не може да докаже истински Docker DNS или MySQL precision/locks. Затова има втори слой: `scripts/demo.ps1` прави REST заявки към реалния system.

## Термини на лесен български

| Термин | Значение в този проект |
|---|---|
| Entity / domain object | Обект, който EF записва като ред/данни в DB |
| DTO | Формата на входния/изходния JSON, отделена от entity |
| Controller | HTTP вход; URL/verb/body → service call |
| Service | Къде са бизнес правилата и orchestration |
| DbContext | EF unit of work: заявява/проследява/запазва entities |
| ORM / EF Core | Превежда C# операции към SQL |
| Migration | Версионирана промяна на DB schema |
| Dependency Injection (DI) | Program регистрира зависимости; framework ги дава през constructor |
| AddScoped | Един service/DbContext instance за една HTTP request scope |
| Typed HttpClient | Клас с инжектиран HttpClient и конкретни remote operations |
| IHttpClientFactory | Управлява/reuses handlers; AddHttpClient го използва |
| async / await | Не блокира thread, докато чака DB или HTTP |
| CancellationToken | Позволява отменена request да спре чакането |
| Snapshot | Запазено копие на името/часовете за history |
| AsNoTracking | Read-only EF query без излишно проследяване на промени |
| Transaction | Няколко local DB writes са all-or-nothing |
| Named lock | Споделен MySQL lock за критичната секция на signup |
| Circuit breaker | Временно спира calls към failing dependency |
| Named volume | Docker storage, което оцелява след изтриване на container |
| Primary constructor | `class X(Dependency dep)` е кратък C# constructor; DI подава dep |
| Record | Кратък C# тип за response DTO с именувани полета |

## Самопроверка без да гледаш документацията

1. Коя услуга притежава Winners и защо?
2. В кой метод се забранява delete на Finished? Какво е HTTP status?
3. Защо не пращаме Tournament entity директно като create request?
4. Каква е разликата между port 5001 и 8080? Защо localhost е грешен remote адрес вътре в контейнер?
5. Как unique index и named lock решават различни проблеми?
6. Какво връща signup, ако Participant API е спрян? Записва ли нещо?
7. Защо `EndDate > StartDate` е документирана интерпретация?
8. Как смятаме participationCount и защо Canceled не се брои?
9. Защо SQLite service tests не доказват MySQL persistence? Коя проверка го доказва?
10. Коя concurrency граница остава между отделните услуги?

Практическо упражнение: сложи breakpoint на `CreateAsync`; обясни преди всяка стъпка какви данни вече имаш и защо следващата проверка е нужна. После представи `docs/PRESENTATION.md` с таймер.

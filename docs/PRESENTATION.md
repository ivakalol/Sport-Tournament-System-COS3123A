# Представяне за 10 минути

## Подготовка преди часа

1. Отвори `SportTournaments.sln` в Rider; направи Build Solution.
2. Стартирай `docker compose up -d --build`; отвори трите Swagger адреса.
3. Изпълни `.\scripts\demo.ps1`. Запиши FinishedTournament, UpcomingTournament и Participant IDs от края или `docs/verification-results.json`.
4. За жив signup демонстрацията създай нов бъдещ турнир и човек, без да зависиш от стари IDs. `demo.http` ти дава готовите JSON тела; изпълняваш заявките отгоре надолу.
5. Остави отворени PROJECT диаграмата, `Tournament.cs`, `AppDbContext.cs`, `RegistrationService.cs`, `TournamentApiClient.cs`, Tournament `Program.cs`, controllers и `compose.yaml`.

## 0:00-1:00 - Какъв проблем решавам

Кажи със свои думи:

> Системата управлява спортните турнири на AUBG. Организаторът задава спорт, време, място, капацитет и награди. Хората се записват, без да надвишават капацитета и без да имат два турнира по едно и също време. След турнира организаторът записва трима победители с точки и го завършва.

Покажи `GET /api/tournaments?status=Upcoming`: имена, start/end dates и locations. Посочи типовете Basketball, Football и Swimming.

## 1:00-2:15 - Design и граници

Покажи диаграмата в `docs/PROJECT.md` и solution tree в Rider.

> Имам три независими услуги. Tournament пази програмата, наградите и официалните резултати. Participant пази имената на хората. Registration пази кой в кой турнир е записан и правилата за записване. Всяка услуга има собствена MySQL база. Не чета чужди таблици директно.

> Следвам архитектурата от BookShop workshop: Domain е entities и AppDbContext, средният проект съдържа business service и typed clients, API съдържа controllers и конфигурация. References сочат надолу: API към service, service към Domain.

## 2:15-3:30 - Microservices communication

Отвори `Registration/TournamentApiClient.cs`, после `Registration.API/Program.cs`.

> При signup Registration пита Participant API дали човекът съществува и Tournament API за статуса, часовете и капацитета. Това са HTTP GET заявки през typed HttpClient. AddHttpClient регистрира клиента в DI; BaseAddress идва от configuration. В Docker ползвам tournamentservice:8080, при локален debug - localhost:5001.

> Resilience дава ограничени retries, timeout и circuit breaker. Ако нужна услуга е недостъпна, signup връща 503 и не записва непълни данни. Domain няма HTTP код, controller не прави тези проверки.

## 3:30-5:00 - High-level code overview

Покажи четири файла, без да четеш всяка линия:

| Файл | Какво казваш |
|---|---|
| `Tournament.Domain/Tournament.cs` | Entity с полетата от заданието; default Upcoming, отделен Winner |
| `Tournament.Domain/AppDbContext.cs` | DbSets, prize decimal precision, enum conversion, Winner FK и unique indexes |
| `Registration/RegistrationService.cs` | Remote IDs, status, duplicate, capacity, overlap, SaveChangesAsync |
| `Tournament.API/Controllers/TournamentsController.cs` | HTTP verbs/routes, извиква service, DTO responses, 201/204 |

При overlap посочи точно:

```csharp
start < otherEnd && otherStart < end
```

Кажи: „Това хваща и частично, и пълно припокриване. Ако единият свърши точно когато другият започва, няма overlap.“

## 5:00-8:30 - Жива демонстрация

Ползвай `demo.http` или Swagger с предварително отворени operation panels.

1. POST participant → `201` и нов ID. POST бъдещ basketball tournament → `201`, status Upcoming.
2. POST registration с тези IDs → `201`. Повтори същото → `409`, already registered.
3. Създай overlapping football tournament и запиши същия човек → `409`, time conflict.
4. Опитай DELETE на occupied Upcoming → `409`; кажи, че вместо delete organizer може да cancel.
5. Покажи предварително подготвения Finished tournament: GET winners показва трима души, places, points и USD prizes.
6. GET participant statistics за човек от Finished demo → participationCount е 1. Опитай DELETE Finished → `409`.

Ако времето позволява, покажи последните три requests в `demo.http`: provisional winners, finish, winner view. За finish трябват трима регистрирани участници; затова ползвай готовия набор от HTTP файла. Не чакай дати да минат - статусът е ръчна organizer операция по заданието.

## 8:30-9:30 - Persistence и проверки

Покажи `compose.yaml`: три MySQL services, healthchecks, три API services и named volumes. Покажи Migrations папката.

> Миграциите са включени и се прилагат при старт. Volumes пазят данните след пресъздаване на контейнерите. Направени са изолирани service tests и отделно REST тестове върху действителните APIs/MySQL. REST сценарият проверява и две едновременни заявки за последното място, outage и persistence.

Отвори `docs/VERIFICATION.md` и JSON report за точните резултати, вместо да обещаваш непоказани тестове.

## 9:30-10:00 - Интерпретация и граница

> Бележката за EndDate в PDF е противоречива. Използвам нормален интервал: EndDate е след StartDate; при създаване StartDate е в бъдещето. ParticipationCount брои Finished турнири. Проектът запазва учебния scope: без authentication и distributed transaction. Parallel signups са защитени с MySQL lock, а organizer промени и signup се изпълняват последователно.

## Въпроси, които може да получиш

| Въпрос | Кратък отговор |
|---|---|
| Защо 3 services? | Три business capabilities със собствен lifecycle и данни; SRP/CCP от урока |
| Защо отделни DB? | Data ownership, service isolation, няма директна зависимост от чужда схема |
| Защо typed HttpClient? | Скрива HTTP/JSON, получава BaseAddress от DI, ползва pooled handlers |
| Защо DTO вместо entity? | Входът не може да задава Id/Status/Winners произволно; API contract е отделен от EF model |
| Защо EF Core 9 при .NET 10? | Framework target е net10.0; стабилният Pomelo от урока е съвместим с EF Core 9 |
| Как пазиш капацитета при две заявки? | Named MySQL lock около checks и insert, плюс unique index за duplicate |
| Какво ако услуга е offline? | Bounded retry, после 503; signup не се записва |
| Как може да се изтрие Canceled със registrations? | PDF го разрешава; snapshots пазят история, remote FK не съществува |
| Защо няма automatic Finished? | Заданието дава organizer finish и изисква winners предварително |
| Защо няма UI приложение? | Workshop/assignment изискват APIs; Swagger е достатъчен за demo |

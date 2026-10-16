# Tasks: Encurtador de Links (ShortURL)

**Input**: Design documents from `/specs/001-url-shortener/` (`spec.md`, `plan.md`, `data-model.md`, `contracts/http-api.md`, `research.md`, `quickstart.md`) and project constitution (`constitution.md` v1.1.0).

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/http-api.md`.

**Tests**: Test tasks are included per **Princípio III da Constituição** ("Toda regra de negócio DEVE possuir testes unitários automatizados correspondentes. Todo endpoint HTTP exposto DEVE contar com ao menos um teste de integração validando o seu caminho feliz").

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., `[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in descriptions

## Path Conventions

- Solution and Source: `src/Domain/`, `src/Application/`, `src/Infrastructure/`, `src/Api/`
- Automated Tests: `tests/Domain.UnitTests/`, `tests/Application.UnitTests/`, `tests/Api.IntegrationTests/`
- Documentation and Specs: `specs/001-url-shortener/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, directory structure, solution setup, and fundamental framework dependencies.

- [X] T001 Create .NET 10 solution `ShortUrl.sln` and Clean Architecture project structure with projects `src/Domain`, `src/Application`, `src/Infrastructure`, `src/Api`, `tests/Domain.UnitTests`, `tests/Application.UnitTests`, and `tests/Api.IntegrationTests` per `plan.md`
- [X] T002 [P] Configure project dependencies, C# 13 nullable references, and NuGet packages (`MediatR`, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Serilog.AspNetCore`, `OpenTelemetry.Extensions.Hosting`) in `Directory.Build.props` and project `.csproj` files
- [X] T003 [P] Configure Serilog structured logging and OpenTelemetry tracing/metrics foundation in `src/Api/Program.cs`
- [X] T004 [P] Configure options binding for `ShortUrlOptions` (including `HashSalt` and connection string `ConnectionStrings:DefaultConnection`) supporting user-secrets and environment variables in `src/Infrastructure/Configuration/ShortUrlOptions.cs` and `src/Api/appsettings.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure and pipeline components that MUST be complete before ANY user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Define `IApplicationDbContext` interface with `DbSet<ShortLink> ShortLinks` and `Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)` in `src/Application/Common/Interfaces/IApplicationDbContext.cs`
- [X] T006 [P] Define `IShortCodeGenerator` interface with methods `string Encode(long id)` and `long Decode(string code)` in `src/Application/Common/Interfaces/IShortCodeGenerator.cs`
- [X] T007 [P] Implement MediatR `ValidationBehavior<TRequest, TResponse>` pipeline behavior to execute FluentValidation validators before handler invocation in `src/Application/Common/Behaviors/ValidationBehavior.cs`
- [X] T008 [P] Implement MediatR `PerformanceLoggingBehavior<TRequest, TResponse>` pipeline behavior with Serilog contextual logging and threshold warnings in `src/Application/Common/Behaviors/PerformanceLoggingBehavior.cs`
- [X] T009 [P] Implement `CorrelationIdMiddleware` to extract or generate `X-Correlation-Id` header and enrich Serilog `LogContext` in `src/Api/Middleware/CorrelationIdMiddleware.cs`
- [X] T010 Implement `GlobalExceptionHandlerMiddleware` returning standard RFC 9457 / RFC 7807 `application/problem+json` (`ProblemDetails`) with Portuguese client messages for `ShortLinkNotFoundException` (404), `ShortLinkExpiredException` (410), and `ValidationException` (400) in `src/Api/Middleware/GlobalExceptionHandlerMiddleware.cs`
- [X] T011 [P] Create `EndpointGroupBase` abstract base class for API Minimal API routing groups mapped under `/shorturl-api/{GroupName}` in `src/Api/Endpoints/EndpointGroupBase.cs`
- [X] T012 Configure EF Core `ApplicationDbContext` implementing `IApplicationDbContext` with SQLite `PRAGMA journal_mode = WAL;` and `PRAGMA synchronous = NORMAL;` in `src/Infrastructure/Data/ApplicationDbContext.cs`
- [X] T013 Implement `Base62CodeGenerator` implementing `IShortCodeGenerator` using complete 62-character alphabet (`0-9`, `A-Z`, `a-z`), deterministic secret salt permutation (Hashids style), and bijective mapping over 64-bit integer `Id` producing fixed 7-character codes (`^[0-9A-Za-z]{7}$`) in `src/Infrastructure/Services/Base62CodeGenerator.cs`
- [X] T014 Create unit tests for `Base62CodeGenerator` validating bijection, fixed 7-character Base62 alphabet (`[0-9A-Za-z]{7}`), salt determinism, and zero collision guarantees across numeric ranges in `tests/Application.UnitTests/Services/Base62CodeGeneratorTests.cs`
- [X] T015 Create base `CustomWebApplicationFactory` for integration tests with test SQLite database and schema initialization in `tests/Api.IntegrationTests/CustomWebApplicationFactory.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin.

---

## Phase 3: User Story 1 - Encurtar URL com Código Automático e Redirecionar Visitante (Priority: P1) 🎯 MVP

**Goal**: Transformar uma URL longa pública válida em link curto único de 7 caracteres gerado automaticamente pelo sistema em Base62 via `POST /shorturl-api/links`, e permitir que o visitante acesse `GET /{code}` e seja redirecionado imediatamente (`302 Found`) para a URL original sem páginas intermediárias.

**Independent Test**: Enviar `POST /shorturl-api/links` com payload `{"targetUrl": "https://learn.microsoft.com/dotnet"}`, verificar retorno `201 Created` com código Base62 de 7 caracteres (`^[0-9A-Za-z]{7}$`) e `Location: /{code}`, e efetuar `GET /{code}` para confirmar redirecionamento imediato `302 Found` com header `Location` correspondente e header `Cache-Control: no-store, no-cache, must-revalidate`.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T016 [P] [US1] Unit tests for `CreateShortLinkCommandValidator` asserting rejection of URLs without `http`/`https`, length `> 2048`, loopback IPs (`127.0.0.0/8`, `::1`), RFC 1918 private IPs (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), link-local IPs, and `localhost` with Portuguese error messages in `tests/Application.UnitTests/Validators/CreateShortLinkCommandValidatorTests.cs`
- [ ] T017 [P] [US1] Unit tests for `CreateShortLinkCommandHandler` and `ResolveShortLinkCommandHandler` asserting correct mapping, code generation, and not-found exception throwing in `tests/Application.UnitTests/ShortLinks/CreateAndResolveShortLinkTests.cs`
- [ ] T018 [P] [US1] Integration tests for `POST /shorturl-api/links` asserting `201 Created` with valid Base62 code, `Location` header, and `400 Bad Request` ProblemDetails in Portuguese for invalid URLs in `tests/Api.IntegrationTests/Endpoints/CreateShortLinkEndpointTests.cs`
- [ ] T019 [P] [US1] Integration tests for `GET /{code}` asserting `302 Found` with correct `Location` header, `Cache-Control: no-store, no-cache, must-revalidate`, and `404 Not Found` ProblemDetails for non-existent codes in `tests/Api.IntegrationTests/Endpoints/RedirectEndpointTests.cs`

### Implementation for User Story 1

- [ ] T020 [P] [US1] Create `ShortLink` domain entity with properties `Id` (`long`, primary key), `Code` (`string`, fixed length 7), `TargetUrl` (`string`, max length 2048), `CreatedAtUtc` (`DateTime`), `ExpiresAtUtc` (`DateTime`), and `AccessCount` (`long`, default 0) in `src/Domain/Entities/ShortLink.cs`
- [ ] T021 [P] [US1] Create domain exceptions `ShortLinkNotFoundException` and `ShortLinkExpiredException` with Portuguese error messages in `src/Domain/Exceptions/ShortLinkNotFoundException.cs` and `src/Domain/Exceptions/ShortLinkExpiredException.cs`
- [ ] T022 [P] [US1] Configure EF Core mapping `ShortLinkConfiguration` with constraints `TargetUrl` `HasMaxLength(2048).IsRequired()`, `Code` `HasMaxLength(7).IsFixedLength().IsRequired()`, unique index `IX_ShortLinks_Code` on `Code`, and generate EF Core initial migration `InitialCreate` in `src/Infrastructure/Data/Configurations/ShortLinkConfiguration.cs` and `src/Infrastructure/Data/Migrations/`
- [ ] T023 [P] [US1] Create DTO `ShortLinkCreatedDto` with properties `Code`, `ShortUrl`, `TargetUrl`, `CreatedAtUtc`, and `ExpiresAtUtc` in `src/Application/ShortLinks/DTOs/ShortLinkCreatedDto.cs`
- [ ] T024 [P] [US1] Implement `CreateShortLinkCommandValidator` enforcing `TargetUrl` required, length `<= 2048`, scheme `http` or `https`, host public valid (rejecting `localhost`, `127.0.0.0/8`, `::1`, RFC 1918 private IPs, link-local) with Portuguese error messages in `src/Application/Validators/CreateShortLinkCommandValidator.cs`
- [ ] T025 [P] [US1] Implement `ResolveShortLinkCommandValidator` validating `{code}` matches Base62 format `^[0-9A-Za-z]{1,7}$` with Portuguese error messages in `src/Application/Validators/ResolveShortLinkCommandValidator.cs`
- [ ] T026 [US1] Implement `CreateShortLinkCommand` and `CreateShortLinkCommandHandler` persisting new `ShortLink`, generating deterministic 7-character code via `IShortCodeGenerator.Encode(id)`, updating `Code`, and returning `ShortLinkCreatedDto` in `src/Application/ShortLinks/Commands/CreateShortLink/CreateShortLinkCommand.cs` and `src/Application/ShortLinks/Commands/CreateShortLink/CreateShortLinkCommandHandler.cs`
- [ ] T027 [US1] Implement `ResolveShortLinkCommand` and `ResolveShortLinkCommandHandler` retrieving `TargetUrl` by `Code` from `IApplicationDbContext`, throwing `ShortLinkNotFoundException` if not found in `src/Application/ShortLinks/Commands/ResolveShortLink/ResolveShortLinkCommand.cs` and `src/Application/ShortLinks/Commands/ResolveShortLink/ResolveShortLinkCommandHandler.cs`
- [ ] T028 [US1] Implement `ShortLinksEndpoints` inheriting from `EndpointGroupBase` mapping `POST /shorturl-api/links` to dispatch `CreateShortLinkCommand` and return `201 Created` with `Location: /{code}` header in `src/Api/Endpoints/ShortLinksEndpoints.cs`
- [ ] T029 [US1] Implement `RedirectEndpoints` mapping root route `GET /{code}` to dispatch `ResolveShortLinkCommand` and return `302 Found` with headers `Location` and `Cache-Control: no-store, no-cache, must-revalidate` in `src/Api/Endpoints/RedirectEndpoints.cs`

**Checkpoint**: At this point, User Story 1 (MVP) is fully functional and independently testable.

---

## Phase 4: User Story 2 - Acompanhar Total de Acessos do Link (Priority: P2)

**Goal**: Permitir a consulta pública do total de acessos acumulados (`AccessCount`) através de `GET /shorturl-api/links/{code}/stats` para qualquer pessoa que conheça o código, garantindo que cada redirecionamento válido incremente atomicamente o contador no SQLite sem perda de contagem sob concorrência.

**Independent Test**: Criar um link curto, disparar requisições concorrentes de redirecionamento `GET /{code}` e consultar `GET /shorturl-api/links/{code}/stats` confirmando que `accessCount` reflete exatamente a quantidade total de redirecionamentos processados.

### Tests for User Story 2 ⚠️

- [ ] T030 [P] [US2] Unit tests for `GetShortLinkStatsQueryHandler` and `GetShortLinkStatsQueryValidator` validating stats retrieval and non-existent code handling in `tests/Application.UnitTests/ShortLinks/GetShortLinkStatsQueryHandlerTests.cs`
- [ ] T031 [P] [US2] Concurrency unit and integration tests executing simultaneous `ResolveShortLinkCommand` / `GET /{code}` calls against SQLite asserting zero lost updates on `AccessCount` in `tests/Application.UnitTests/ShortLinks/AtomicIncrementAccessCountTests.cs`
- [ ] T032 [P] [US2] Integration tests for `GET /shorturl-api/links/{code}/stats` asserting public access without authentication returning `200 OK` with JSON payload (`code`, `targetUrl`, `createdAtUtc`, `expiresAtUtc`, `isExpired`, `accessCount`) and `404 Not Found` for unknown codes in `tests/Api.IntegrationTests/Endpoints/GetShortLinkStatsEndpointTests.cs`

### Implementation for User Story 2

- [ ] T033 [P] [US2] Create DTO `ShortLinkStatsDto` with properties `Code`, `TargetUrl`, `CreatedAtUtc`, `ExpiresAtUtc`, `IsExpired`, and `AccessCount` in `src/Application/ShortLinks/DTOs/ShortLinkStatsDto.cs`
- [ ] T034 [P] [US2] Implement `GetShortLinkStatsQueryValidator` validating `{code}` is not empty and matches Base62 format `^[0-9A-Za-z]{1,7}$` with Portuguese error messages in `src/Application/Validators/GetShortLinkStatsQueryValidator.cs`
- [ ] T035 [US2] Update `ResolveShortLinkCommandHandler` to execute atomic database increment `ExecuteUpdateAsync(s => s.SetProperty(b => b.AccessCount, b => b.AccessCount + 1))` conditioned to active link (`ExpiresAtUtc >= nowUtc`) in `src/Application/ShortLinks/Commands/ResolveShortLink/ResolveShortLinkCommandHandler.cs`
- [ ] T036 [US2] Implement `GetShortLinkStatsQuery` and `GetShortLinkStatsQueryHandler` querying `IApplicationDbContext.ShortLinks` as read-only (`AsNoTracking`) and mapping to `ShortLinkStatsDto` in `src/Application/ShortLinks/Queries/GetShortLinkStats/GetShortLinkStatsQuery.cs` and `src/Application/ShortLinks/Queries/GetShortLinkStats/GetShortLinkStatsQueryHandler.cs`
- [ ] T037 [US2] Expose `GET /shorturl-api/links/{code}/stats` endpoint in `ShortLinksEndpoints` dispatching `GetShortLinkStatsQuery` and returning `200 OK` in `src/Api/Endpoints/ShortLinksEndpoints.cs`

**Checkpoint**: User Stories 1 and 2 are fully functional and independently testable.

---

## Phase 5: User Story 3 - Definir Data de Expiração do Link (Priority: P3)

**Goal**: Permitir que o criador defina opcionalmente uma data de expiração em UTC na criação do link: quando omitida, definir validade padrão de 10 anos (`CreatedAtUtc.AddYears(10)`); quando informada, normalizar para o fim do dia em UTC (`23:59:59.9999999Z`); recusar datas passadas na criação (`400 Bad Request`); e bloquear acessos após o vencimento com `410 Gone` sem incrementar `AccessCount`.

**Independent Test**: Criar um link sem data e validar que `expiresAtUtc` é `createdAtUtc + 10 anos`; criar um link com data futura e validar redirecionamento; acessar link expirado e validar HTTP `410 Gone` com `ProblemDetails` em português mantendo `accessCount` inalterado.

### Tests for User Story 3 ⚠️

- [ ] T038 [P] [US3] Unit tests for expiration domain logic and validation asserting default 10-year expiration, end-of-day UTC normalization (`23:59:59.9999999Z`), and rejection of past dates in `tests/Domain.UnitTests/Entities/ShortLinkExpirationTests.cs` and `tests/Application.UnitTests/Validators/ExpirationValidationTests.cs`
- [ ] T039 [P] [US3] Integration tests asserting HTTP `410 Gone` on expired link access via `GET /{code}`, verifying `accessCount` is NOT incremented, and verifying `isExpired: true` in `GET /shorturl-api/links/{code}/stats` in `tests/Api.IntegrationTests/Endpoints/ExpiredLinkEndpointTests.cs`

### Implementation for User Story 3

- [ ] T040 [P] [US3] Add expiration helper methods and invariant checks to `ShortLink` entity (`CalculateDefaultExpiration`, `NormalizeExpirationDate`, and `IsExpired(DateTime nowUtc)`) in `src/Domain/Entities/ShortLink.cs`
- [ ] T041 [US3] Update `CreateShortLinkCommandValidator` to validate that `ExpiresAtUtc`, when provided, represents a future date in UTC, returning Portuguese error message when in the past in `src/Application/Validators/CreateShortLinkCommandValidator.cs`
- [ ] T042 [US3] Update `CreateShortLinkCommandHandler` to set `ExpiresAtUtc = CreatedAtUtc.AddYears(10)` when omitted, or normalize the date to `23:59:59.9999999Z` when specified in `src/Application/ShortLinks/Commands/CreateShortLink/CreateShortLinkCommandHandler.cs`
- [ ] T043 [US3] Update `ResolveShortLinkCommandHandler` to check `link.IsExpired(DateTime.UtcNow)` and throw `ShortLinkExpiredException` (mapped to 410 Gone) prior to redirecting or incrementing `AccessCount`, and ensure `GetShortLinkStatsQueryHandler` returns `isExpired: true` in `src/Application/ShortLinks/Commands/ResolveShortLink/ResolveShortLinkCommandHandler.cs` and `src/Application/ShortLinks/Queries/GetShortLinkStats/GetShortLinkStatsQueryHandler.cs`

**Checkpoint**: All user stories (US1, US2, US3) are fully functional and independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, OpenAPI specification, database health checks, and quickstart end-to-end verification.

- [ ] T044 [P] Add C# XML documentation comments (`<summary>`, `<param>`, `<response>`) across all endpoints in `src/Api/Endpoints/`, commands, queries, and DTOs in `src/Application/` for complete OpenAPI metadata generation per constitution
- [ ] T045 Configure OpenAPI / NSwag Swagger UI metadata, title, version, and endpoint descriptions in `src/Api/Program.cs`
- [ ] T046 [P] Add ASP.NET Core Health Checks endpoint (`/health`, `/ready`) validating SQLite database connectivity and disk write availability in `src/Api/Program.cs`
- [ ] T047 Execute and validate all 5 scenarios from `quickstart.md` (`curl` commands for URL creation, 302 redirect, public stats, URL validation error, expired link 410, and restart persistence) against the running application in `specs/001-url-shortener/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - **BLOCKS all user stories**.
- **User Stories (Phases 3, 4, 5)**: Depend on Foundational phase completion.
  - Can proceed sequentially in priority order (P1 → P2 → P3) or in parallel across different handlers/endpoints.
- **Polish (Phase 6)**: Depends on completion of all user story implementations.

### User Story Dependencies

- **User Story 1 (P1)**: Depends only on Foundational (Phase 2). Delivers MVP (creation + redirection).
- **User Story 2 (P2)**: Depends on Foundational (Phase 2) and integrates with the redirect flow from US1 by adding atomic increment and stats query.
- **User Story 3 (P3)**: Depends on Foundational (Phase 2) and refines creation and resolution validation rules with UTC expiration logic.

### Within Each User Story

1. Write tests first and verify failure.
2. Domain entities and exceptions before handlers.
3. Validators before handlers.
4. Handlers before API endpoint mapping.
5. Integration test verification of the completed story.

### Parallel Opportunities

- All Setup tasks marked `[P]` (T002, T003, T004) can run in parallel after T001.
- All Foundational tasks marked `[P]` (T005, T006, T007, T008, T009, T011) can run in parallel.
- Test tasks within each story marked `[P]` (e.g., T016, T017, T018, T019) can be authored in parallel.
- Entity, DTO, and Validator tasks within each story marked `[P]` (e.g., T020, T021, T022, T023, T024, T025) can be created in parallel.

---

## Parallel Example: User Story 1

```bash
# Author tests for User Story 1 in parallel:
Task: "T016 [P] [US1] Unit tests for CreateShortLinkCommandValidator in tests/Application.UnitTests/Validators/CreateShortLinkCommandValidatorTests.cs"
Task: "T017 [P] [US1] Unit tests for CreateShortLinkCommandHandler in tests/Application.UnitTests/ShortLinks/CreateAndResolveShortLinkTests.cs"
Task: "T018 [P] [US1] Integration tests for POST /shorturl-api/links in tests/Api.IntegrationTests/Endpoints/CreateShortLinkEndpointTests.cs"
Task: "T019 [P] [US1] Integration tests for GET /{code} in tests/Api.IntegrationTests/Endpoints/RedirectEndpointTests.cs"

# Implement domain models, exceptions, configuration, and DTOs for User Story 1 in parallel:
Task: "T020 [P] [US1] Create ShortLink domain entity in src/Domain/Entities/ShortLink.cs"
Task: "T021 [P] [US1] Create domain exceptions in src/Domain/Exceptions/ShortLinkNotFoundException.cs"
Task: "T022 [P] [US1] Configure EF Core mapping ShortLinkConfiguration in src/Infrastructure/Data/Configurations/ShortLinkConfiguration.cs"
Task: "T023 [P] [US1] Create DTO ShortLinkCreatedDto in src/Application/ShortLinks/DTOs/ShortLinkCreatedDto.cs"
Task: "T024 [P] [US1] Implement CreateShortLinkCommandValidator in src/Application/Validators/CreateShortLinkCommandValidator.cs"
Task: "T025 [P] [US1] Implement ResolveShortLinkCommandValidator in src/Application/Validators/ResolveShortLinkCommandValidator.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (`T001` - `T004`).
2. Complete Phase 2: Foundational (`T005` - `T015`).
3. Complete Phase 3: User Story 1 (`T016` - `T029`).
4. **STOP and VALIDATE**: Execute Scenario 1, Scenario 2, and Scenario 3 from `quickstart.md`. Confirm Base62 7-character generation, 302 redirect, and rejection of private/local URLs.
5. Deploy/demo MVP.

### Incremental Delivery

1. Complete Setup + Foundational → Solid base with MediatR, FluentValidation, Base62 generator, and SQLite WAL.
2. Deliver User Story 1 (MVP) → Creation and instant redirection working.
3. Deliver User Story 2 → Atomic access counter and public stats endpoint operational.
4. Deliver User Story 3 → UTC expiration semantics (10-year default, end-of-day normalization, 410 Gone) operational.
5. Deliver Polish (Phase 6) → XML docs, OpenAPI, Health Checks, and full quickstart validation.

### Parallel Team Strategy

With multiple developers:
1. Team completes Setup (`T001` - `T004`) and Foundational (`T005` - `T015`) collaboratively.
2. Once Foundational is ready:
   - Developer A: User Story 1 (Commands, Handlers, Endpoints for Create/Redirect).
   - Developer B: User Story 2 (Stats Query, DTO, Atomic Increment).
   - Developer C: User Story 3 (Expiration validation, UTC normalization, Expired link handling).
3. Merge stories and run full test suite (`dotnet test`).

---

## Notes

- All tasks follow strict format: `- [ ] [TaskID] [P?] [Story?] Description with file path`.
- Story labels (`[US1]`, `[US2]`, `[US3]`) are applied strictly to User Story phases.
- Exact file paths are specified for every task.
- Zero extra NuGet dependencies beyond those homologated in the Constitution.
- Commit after each task using format: `feat(scope): description [REQ-ID]`.


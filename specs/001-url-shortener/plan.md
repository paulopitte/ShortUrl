# Implementation Plan: Encurtador de Links (ShortURL)

**Branch**: `001-url-shortener` | **Date**: 2026-10-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-url-shortener/spec.md`, quality checklist from `/specs/001-url-shortener/checklists/requirements.md`, and user technical plan input.

## Summary

Implementa o serviço de encurtamento de links (**ShortURL**) respeitando a especificação de domínio (`spec.md`) e a supremacia da Constituição do projeto (`constitution.md` v1.1.0). O plano técnico informado pelo usuário foi adaptado aos requisitos aprovados no checklist (`checklists/requirements.md`) e às diretrizes arquiteturais da Constituição:

1. **Arquitetura Clean Architecture + CQRS (MediatR)**:
   - **Api**: ASP.NET Core Minimal APIs (.NET 10), um arquivo de grupo de endpoints por recurso herdando de `EndpointGroupBase` (mapeado em `/shorturl-api/links`) mais a rota enxuta de redirecionamento `GET /{code}`. Os endpoints apenas recebem a requisição, despacham via `ISender.Send()` e traduzem o retorno.
   - **Application**: Casos de uso modelados como Commands e Queries (`CreateShortLinkCommand`, `ResolveShortLinkCommand`, `GetShortLinkStatsQuery`), seus respectivos Handlers (donos da orquestração das regras `RN-01` a `RN-06`), validadores `FluentValidation` com mensagens em português e abstrações (`IApplicationDbContext`, `IShortCodeGenerator`).
   - **Domain**: Entidade `ShortLink`, exceções de domínio (`ShortLinkNotFoundException`, `ShortLinkExpiredException`) e regras de invariantes sem dependências externas.
   - **Infrastructure**: Persistência em SQLite via EF Core (`ApplicationDbContext` em modo WAL) e implementação nativa do algoritmo de codificação/ofuscação Base62 estilo Hashids (`Base62CodeGenerator`).

2. **Decisões Técnicas Adaptadas (`D-01` a `D-06`)**:
   - **D-01 (Persistência e Escala — RF-08 / FR-009 / FR-010)**: SQLite via EF Core (com `Journal Mode = WAL`) para persistência durável entre reinícios, isolado por `IApplicationDbContext` para suportar particionamento horizontal e cache de leitura nas metas de alta escala (1.160 gravações/s, 11.600 leituras/s e 36,5 TB em 10 anos).
   - **D-02 (Contador Atômico — RN-04 / FR-007)**: `AccessCount` incrementado por `UPDATE` atômico no banco (`ExecuteUpdateAsync` condicionado a `ExpiresAtUtc >= nowUtc`), e nunca lido e reescrito em memória na aplicação, evitando corrida sob acessos simultâneos.
   - **D-03 (Código Base62 com Ofuscação Bijetiva — FR-003 / FR-004 / OH-01..OH-04)**: Geração exclusivamente automática de **7 caracteres** usando o alfabeto **Base62 completo** (`0-9`, `A-Z`, `a-z`, 62 caracteres, sem espaços ou caracteres especiais) embaralhado por sal secreto estilo Hashids sobre o `Id` numérico sequencial. *(Adaptado do rascunho original para usar o Base62 completo de 62 caracteres exigido pelo checklist `requirements.md` e pela `spec.md`).*
   - **D-04 (Unicidade sem Colisão — RN-01 / RN-06 / OH-02)**: A bijeção matemática sobre o `Id` sequencial (`INTEGER PRIMARY KEY AUTOINCREMENT`) garante unicidade global sem necessidade de `SELECT` prévio de colisão no banco, reforçada por índice único (`UNIQUE INDEX IX_ShortLinks_Code`) no esquema SQLite. *(Códigos personalizados pelo usuário estão fora de escopo conforme `spec.md`).*
   - **D-05 (Segurança de Segredos e Consulta Pública — FR-008 / Constituição VI)**: A consulta de estatísticas (`GET /shorturl-api/links/{code}/stats`) é pública para qualquer pessoa que conheça o código (**FR-008**), eliminando a necessidade de `managementKey`/`ManagementKeyHash` por link; o segredo do sistema (`HashSalt` usado na ofuscação da **FR-004**) nunca é armazenado em texto puro no repositório, sendo injetado via `user-secrets` / variáveis de ambiente / Key Vault.
   - **D-06 (Expiração em UTC — FR-005 / RF-06 / RN-03)**: Quando omitida, `ExpiresAtUtc` recebe `CreatedAtUtc + 10 anos`; quando informada uma data futura, é normalizada até o fim do dia em UTC (`23:59:59.9999999Z`); datas no passado são recusadas com `400 Bad Request` em português, e links vencidos retornam `410 Gone` sem incrementar `AccessCount`.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)

**Primary Dependencies**:
- ASP.NET Core Minimal APIs & OpenAPI / NSwag
- MediatR (CQRS e Pipeline Behaviors)
- FluentValidation (Validação de Commands/Queries em português)
- Entity Framework Core & `Microsoft.EntityFrameworkCore.Sqlite`
- Serilog (Logging estruturado com `CorrelationId`) & OpenTelemetry (Métricas e Traces)

**Storage**: SQLite gerenciado via EF Core Migrations (`Journal Mode = WAL` para leitura/escrita concorrente)

**Testing**: xUnit (`Domain.UnitTests`, `Application.UnitTests`, `Api.IntegrationTests` via `WebApplicationFactory`)

**Target Platform**: Cross-platform server (.NET 10 Linux/Windows containers e execução local)

**Project Type**: Web Service / HTTP REST & Redirect API (`web-service`)

**Performance Goals**:
- Vazão projetada de **1.160 gravações/s** (100 milhões de URLs/dia — `CE-01`)
- Vazão projetada de **11.600 leituras/s** (proporção 10:1 de leitura/escrita — `CE-02`)
- Geração de código curto em $O(1)$ sem round-trip adicional de verificação de colisão no banco (`OH-02`)

**Constraints**:
- Códigos curtos gerados exclusivamente de forma automática com exatos **7 caracteres** em **Base62** (`[0-9A-Za-z]{7}`)
- Todas as mensagens de erro e validação voltadas ao cliente em **português** no formato `ProblemDetails` (RFC 9457 / RFC 7807)
- Validação de `TargetUrl` (`http`/`https`, $\le 2048$ chars, host público) realizada sintaticamente sem I/O de rede externo síncrono
- Zero pacotes NuGet extras fora da Stack Tecnológica homologada na Constituição

**Scale/Scope**:
- Retenção de **10 anos** = **365 bilhões de registros** (~36,5 TB considerando `TargetUrl` média de 100 bytes — `CE-03` / `CE-04`)
- Espaço combinatório de $62^7 = 3.521.614.606.208$ (~3,52 trilhões de códigos únicos)
- Operação em alta disponibilidade 24/7 (`RF-11`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio da Constituição | Status Pré-Phase 0 | Status Pós-Phase 1 | Evidência no Plano / Design |
|---------------------------|--------------------|--------------------|-----------------------------|
| **I. Clean Architecture, Separação de Camadas e CQRS** | ✅ PASS | ✅ PASS | Solução dividida em `Domain`, `Application`, `Infrastructure` e `Api`. Uso de MediatR (`Commands`/`Queries`/`Handlers`) no lugar de um `LinkService` monolítico; endpoints herdam de `EndpointGroupBase` em `/shorturl-api/links` + rota de redirecionamento `GET /{code}` despachando via `ISender.Send()`. |
| **II. Controle Estrito de Dependências** | ✅ PASS | ✅ PASS | Apenas os pacotes base da Constituição são utilizados. A codificação/ofuscação Base62 estilo Hashids é implementada nativamente em C# (`Base62CodeGenerator`), sem bibliotecas de terceiros não homologadas. |
| **III. Cobertura de Testes Obrigatória** | ✅ PASS | ✅ PASS | Projetos `Domain.UnitTests`, `Application.UnitTests` (regras `RN-01`..`RN-06` e gerador Base62) e `Api.IntegrationTests` (happy paths e erros de todos os endpoints HTTP) definidos na estrutura do projeto. |
| **IV. Padronização de Erros e Validações** | ✅ PASS | ✅ PASS | Validadores `FluentValidation` em `src/Application/Validators` retornando `400 Bad Request` (`ProblemDetails`) em português; exceções `ShortLinkNotFoundException` (`404`) e `ShortLinkExpiredException` (`410`) tratadas por middleware global. |
| **V. Observabilidade e Logging Estruturado** | ✅ PASS | ✅ PASS | Serilog com `CorrelationId` e instrumentação de métricas/traces via OpenTelemetry nos fluxos de criação, redirecionamento e consulta. |
| **VI. Configuração, Segredos e Migrações de Dados** | ✅ PASS | ✅ PASS | `ShortUrl:HashSalt` e `ConnectionStrings:DefaultConnection` gerenciados via `user-secrets`/variáveis de ambiente/Key Vault; esquema versionado exclusivamente via EF Core Migrations. |

## Project Structure

### Documentation (this feature)

```text
specs/001-url-shortener/
├── plan.md              # Este arquivo (Plano de Implementação adaptado)
├── research.md          # Phase 0 output (Decisões D-01..D-06 e alinhamento Spec/Constituição)
├── data-model.md        # Phase 1 output (Modelo ShortLink, invariantes e ciclo de vida)
├── quickstart.md        # Phase 1 output (Guia de execução e validação ponta a ponta)
├── contracts/
│   └── http-api.md      # Phase 1 output (Contratos HTTP POST /shorturl-api/links, GET /{code}, GET .../stats)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Domain/
│   ├── Entities/
│   │   └── ShortLink.cs
│   └── Exceptions/
│       ├── ShortLinkNotFoundException.cs
│       └── ShortLinkExpiredException.cs
├── Application/
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── PerformanceLoggingBehavior.cs
│   │   └── Interfaces/
│   │       ├── IApplicationDbContext.cs
│   │       └── IShortCodeGenerator.cs
│   ├── ShortLinks/
│   │   ├── Commands/
│   │   │   ├── CreateShortLink/
│   │   │   │   ├── CreateShortLinkCommand.cs
│   │   │   │   └── CreateShortLinkCommandHandler.cs
│   │   │   └── ResolveShortLink/
│   │   │       ├── ResolveShortLinkCommand.cs
│   │   │       └── ResolveShortLinkCommandHandler.cs
│   │   ├── Queries/
│   │   │   └── GetShortLinkStats/
│   │   │       ├── GetShortLinkStatsQuery.cs
│   │   │       └── GetShortLinkStatsQueryHandler.cs
│   │   └── DTOs/
│   │       ├── ShortLinkCreatedDto.cs
│   │       └── ShortLinkStatsDto.cs
│   └── Validators/
│       ├── CreateShortLinkCommandValidator.cs
│       ├── ResolveShortLinkCommandValidator.cs
│       └── GetShortLinkStatsQueryValidator.cs
├── Infrastructure/
│   ├── Configuration/
│   │   └── ShortUrlOptions.cs
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   ├── Configurations/
│   │   │   └── ShortLinkConfiguration.cs
│   │   └── Migrations/
│   └── Services/
│       └── Base62CodeGenerator.cs
└── Api/
    ├── Endpoints/
    │   ├── EndpointGroupBase.cs
    │   ├── ShortLinksEndpoints.cs
    │   └── RedirectEndpoints.cs
    ├── Middleware/
    │   ├── GlobalExceptionHandlerMiddleware.cs
    │   └── CorrelationIdMiddleware.cs
    └── Program.cs

tests/
├── Domain.UnitTests/
│   └── Entities/
│       └── ShortLinkTests.cs
├── Application.UnitTests/
│   ├── ShortLinks/
│   │   ├── CreateShortLinkCommandHandlerTests.cs
│   │   ├── ResolveShortLinkCommandHandlerTests.cs
│   │   └── GetShortLinkStatsQueryHandlerTests.cs
│   ├── Services/
│   │   └── Base62CodeGeneratorTests.cs
│   └── Validators/
│       └── CreateShortLinkCommandValidatorTests.cs
└── Api.IntegrationTests/
    ├── CustomWebApplicationFactory.cs
    └── Endpoints/
        └── ShortLinksEndpointsTests.cs
```

**Structure Decision**: Adotada a estrutura **Clean Architecture + CQRS** em 4 camadas (`Domain`, `Application`, `Infrastructure`, `Api`) sob `src/` e 3 projetos de teste sob `tests/`, conforme determinado pelo **Princípio I e Princípio III da Constituição**. As regras de negócio (`RN-01` a `RN-06`) que no rascunho inicial ficavam em um `LinkService` foram distribuídas entre a entidade `ShortLink`, os validadores `FluentValidation` e os `Handlers` CQRS do MediatR, utilizando `IApplicationDbContext` e `IShortCodeGenerator`.

## Riscos e Mitigações

| Risco | Impacto | Mitigação no Design |
|-------|---------|---------------------|
| **Perda de contagem em acessos simultâneos ao mesmo link** | Inconsistência em `AccessCount` (violação de `RN-04` / `SC-003`) | **D-02**: Incremento via `UPDATE` atômico diretamente no banco (`ExecuteUpdateAsync`) condicionado à vigência (`ExpiresAtUtc >= nowUtc`), sem leitura e reescrita em memória. |
| **Colisão ou lentidão na geração de códigos sob alta concorrência** | Gargalo de banco de dados em 1.160 gravações/s (`OH-02`) | **D-03 / D-04**: Bijeção determinística Base62 estilo Hashids de 7 caracteres sobre o `Id` sequencial único + índice `UNIQUE` no banco, eliminando consultas prévias de colisão. |
| **Contenção de escrita em arquivo único SQLite sob escala extrema (1.160 RPS)** | Latência de *write lock* em disco único | **D-01**: Ativação de `PRAGMA journal_mode=WAL;` e `synchronous=NORMAL;`, aliada ao desacoplamento via `IApplicationDbContext` que permite *sharding* por faixa de `Id`/`Code` sem alterar a camada de aplicação. |
| **Crescimento da tabela de links em 10 anos (~365 bilhões de registros / 36,5 TB)** | Crescimento contínuo de armazenamento sem expurgo imediato | Retenção de 10 anos faz parte do escopo (`CE-03`/`CE-04`); política automatizada de expurgo/arquivamento de links expirados após 10 anos fica fora de escopo do MVP, mas a coluna `ExpiresAtUtc` já nasce indexada para suportar *cleanup jobs* futuros. |

## Complexity Tracking

> **Nenhuma violação à Constituição foi introduzida.** Todas as escolhas do plano técnico original que divergiam da Constituição ou da `spec.md` foram harmonizadas (ver tabela em `research.md`).

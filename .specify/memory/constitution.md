<!--
Sync Impact Report:
- Version change: 1.0.0 → 1.1.0
- Modified principles:
  - I. Clean Architecture, Separação de Camadas e CQRS (consolidado; unificado idioma e removida duplicidade com seção IV)
  - II. Controle Estrito de Dependências (harmonizado com os pacotes base da Stack Tecnológica)
  - III. Cobertura de Testes Obrigatória (mantido e enxuto)
  - IV. Padronização de Erros e Validações (consolidado ProblemDetails, FluentValidation e exceções de domínio)
  - V. Observabilidade e Logging Estruturado (renumerado de VIII e traduzido)
  - VI. Configuração, Segredos e Migrações de Dados (consolidado IX e X e traduzido)
- Modified sections:
  - Stack Tecnológica (removidas regras duplicadas de endpoints e listadas dependências base autorizadas)
  - Convenções de Código, Nomenclatura e Versionamento (unificado com Development Workflow)
- Templates requiring updates:
  - ✅ .specify/templates/plan-template.md (aligned)
  - ✅ .specify/templates/spec-template.md (aligned)
  - ✅ .specify/templates/tasks-template.md (aligned)
- Follow-up TODOs: None
-->
# ShortUrl Constitution

## Core Principles

### I. Clean Architecture, Separação de Camadas e CQRS
A solução segue rigorosamente Clean Architecture combinada com CQRS:
- **Domain**: Entidades, enums, exceções e eventos de domínio. DEVE ter zero dependências externas.
- **Application**: Casos de uso modelados como Commands (escrita) ou Queries (leitura) via MediatR, Handlers, Validators, DTOs e interfaces (ex.: `IApplicationDbContext`). Depende APENAS de `Domain`.
- **Infrastructure**: Implementa as interfaces da `Application` (EF Core, SQLite e integrações externas).
- **Api**: Minimal APIs (herdando de `EndpointGroupBase` e mapeadas em `/shorturl-api/{GroupName}`), configuração de DI e Middlewares.

**Regras de Isolamento**:
- Endpoints HTTP NUNCA devem acessar o banco de dados diretamente; sua única responsabilidade é mapear rotas, receber payloads e despachar requisições via `ISender.Send()`.
- Regras de negócio NÃO devem residir nos endpoints HTTP; devem estar isoladas no Domínio e orquestradas pelos Handlers da camada de Aplicação (que dependem exclusivamente de abstrações como `IApplicationDbContext`).
- Preocupações transversais (validação, logging de performance e tratamento de exceções) DEVEM ser tratadas via MediatR Pipeline Behaviors e Middlewares.

*Rationale*: Garante testabilidade isolada da lógica de domínio, evita acoplamento entre transporte HTTP e persistência, e mantém responsabilidades claras.

### II. Controle Estrito de Dependências
Além das bibliotecas base já homologadas na seção **Stack Tecnológica**, nenhuma nova dependência ou pacote NuGet externo PODE ser adicionado ao projeto sem justificativa técnica explícita e documentada previamente no plano de implementação (`plan.md`).

*Rationale*: Previne o inchaço da aplicação (*bloat*), reduz a superfície de manutenção/segurança e prioriza os recursos nativos do .NET 10.

### III. Cobertura de Testes Obrigatória (Unitários e Integração)
Toda regra de negócio DEVE possuir testes unitários automatizados correspondentes. Todo endpoint HTTP exposto DEVE contar com ao menos um teste de integração validando o seu caminho feliz (*happy path*).

*Rationale*: Assegura que a lógica central funcione isoladamente e que o fluxo ponta a ponta (roteamento, injeção de dependência e persistência) esteja íntegro.

### IV. Padronização de Erros e Validações
- **Validação de Entrada**: Todo Command ou Query DEVE possuir um validador correspondente com `FluentValidation` na pasta `src/Application/Validators`.
- **Retorno `400 Bad Request`**: Qualquer falha de validação DEVE retornar HTTP `400 Bad Request` no formato padrão `ProblemDetails` (RFC 9457 / RFC 7807), com mensagens redigidas em **português**.
- **Exceções de Domínio**: Handlers DEVEM lançar exceções de domínio específicas (ex.: `ShortUrlNotFoundException`) em vez de exceções genéricas. Um middleware global de exceções é responsável por traduzi-las em respostas HTTP padronizadas com `ProblemDetails` (ex.: `404 Not Found`, `409 Conflict`, `410 Gone`).

*Rationale*: Fornece respostas de erro previsíveis, padronizadas para clientes da API e alinhadas ao idioma esperado pelo usuário final.

### V. Observabilidade e Logging Estruturado
- **Logging Estruturado**: A aplicação DEVE utilizar logging estruturado (Serilog), enriquecendo os logs com contexto da requisição (ex.: `CorrelationId`).
- **Métricas e Traces**: Endpoints e fluxos críticos DEVEM ser instrumentados com métricas (contagem de requisições, latência, taxa de erros) e traces distribuídos via OpenTelemetry.

*Rationale*: Garante visibilidade operacional e diagnóstico rápido em cenários de alta escala.

### VI. Configuração, Segredos e Migrações de Banco de Dados
- **Hierarquia de Configuração**: Precedência estrita: `appsettings.json` -> Variáveis de Ambiente -> Cofre de Segredos (Azure Key Vault em produção / `user-secrets` em desenvolvimento).
- **Segredos**: Chaves de API, *salts* e *connection strings* NUNCA devem ser versionados no repositório.
- **Migrações EF Core**: Alterações de esquema de banco de dados DEVEM ser feitas exclusivamente via EF Core Migrations, garantindo scripts idempotentes e testados.

*Rationale*: Protege credenciais sensíveis e assegura evolução controlada e reproduzível do esquema de dados.

## Stack Tecnológica

- **Runtime & Web**: .NET 10, C#, ASP.NET Core Minimal APIs.
- **Arquitetura & Validação (Pacotes Base)**: MediatR (CQRS e Pipeline Behaviors) e FluentValidation.
- **Persistência**: SQLite gerenciado via Entity Framework Core (EF Core).
- **Documentação da API**: OpenAPI / NSwag. Endpoints e DTOs DEVEM conter comentários XML (`<summary>`) para geração completa do contrato OpenAPI.
- **Observabilidade (Pacotes Base)**: Serilog e OpenTelemetry.
- **Testes Automatizados**: xUnit.

## Convenções de Código, Nomenclatura e Versionamento

- **Idioma**: Nomes de classes, métodos, propriedades, pastas e arquivos DEVEM ser escritos em **inglês**. Mensagens de erro e retorno de validação voltadas ao cliente DEVEM ser escritas em **português**.
- **Nomenclatura de Classes (CQRS)**:
  - **Commands/Queries**: `[Action][Entity]Command` (ex.: `CreateShortUrlCommand`), `Get[Entity]ByIdQuery`.
  - **Handlers**: `[Command/QueryName]Handler` (ex.: `CreateShortUrlCommandHandler`).
  - **Validators**: `[Command/QueryName]Validator` (ex.: `CreateShortUrlCommandValidator`).
- **Git Branches**: `feature/[TICKET-ID]-short-description` ou `fix/[TICKET-ID]-short-description`.
- **Padrão de Commits**: Formato `<tipo>(<escopo>): <descrição> [<ID-REQUISITO>]` (ex.: `feat(shorten): implementa geração de código curto [REQ-01]`).

## Governance

- **Supremacia da Constituição**: Esta constituição tem precedência sobre qualquer plano de implementação ou especificação técnica. Se um plano (`plan.md`) contradisser a constituição, quem DEVE mudar é o plano.
- **Processo de Emenda**: Alterações nesta constituição DEVEM ser realizadas e registradas em um commit separado e dedicado exclusivamente à governança, antes de alterações de código ou plano que dependam dela.
- **Política de Versionamento**: Versionamento Semântico (`MAJOR.MINOR.PATCH`):
  - `MAJOR`: Remoção ou redefinição incompatível de princípios ou regras de governança.
  - `MINOR`: Adição de novo princípio/seção ou expansão material das diretrizes.
  - `PATCH`: Esclarecimentos de redação, correções de digitação ou refinamentos não semânticos.

**Version**: 1.1.0 | **Ratified**: 2026-10-16 | **Last Amended**: 2026-10-16


# Phase 0 Research: Encurtador de Links (ShortURL)

**Feature Branch**: `001-url-shortener`
**Date**: 2026-10-16
**References**: [spec.md](./spec.md) | [requirements.md](./checklists/requirements.md) | [constitution.md](../../.specify/memory/constitution.md)

## Resumo de Alinhamento do Plano Técnico (Adaptação à Spec e Constituição)

O rascunho original do plano técnico foi adaptado para convergir 100% com a especificação validada (`spec.md` e `checklists/requirements.md`) e com a supremacia da Constituição do projeto (`constitution.md` v1.1.0):

| Ponto do Plano Original | Adaptação no Plano Final | Justificativa (Spec / Constituição) |
|-------------------------|--------------------------|-------------------------------------|
| Camada de aplicação com `LinkService` e repositório por agregado | **CQRS com MediatR** (`Commands`, `Queries`, `Handlers`, `Validators`) + abstração `IApplicationDbContext` | **Constituição Princípio I**: Exige Clean Architecture + CQRS via MediatR e proíbe lógica fora dos Handlers/Domain. |
| **D-03**: Alfabeto sem caracteres ambíguos | **Alfabeto Base62 completo** (`0-9`, `A-Z`, `a-z`, 62 caracteres) permutado de forma determinística via sal secreto (estilo Hashids) | **Checklist de Requisitos & FR-003/FR-004**: O alfabeto oficial é Base62 completo (62 caracteres) com ofuscação determinística bijetiva sobre o `Id`. |
| **D-04**: Checagem de colisão no serviço + índice único para código próprio | **Bijeção determinística sem consulta prévia de colisão** + índice único (`UNIQUE`) em `Code` no banco | **FR-003 / FR-004 / Out of Scope**: Códigos personalizados estão fora de escopo; a geração é exclusivamente automática de 7 caracteres a partir do `Id` sequencial, eliminando round-trips de colisão (OH-02). |
| **D-05**: Chave de gerenciamento (`managementKey`) armazenada como hash e exigida no header de `/stats` | **Removida `managementKey`**; consulta de estatísticas é pública por `{code}` e o sal secreto de ofuscação fica em configuração segura (`user-secrets`/Key Vault) | **FR-008 / Out of Scope**: A consulta de `AccessCount` é pública para qualquer pessoa que conheça o código, sem autenticação ou edição/exclusão manual. |
| **Contratos**: `POST /api/links` (com `code` opcional e `managementKey`) e `GET /api/links/{code}/stats` | Rotas mapeadas via `EndpointGroupBase` em `/shorturl-api/links` (`POST /shorturl-api/links`, `GET /shorturl-api/links/{code}/stats`) e rota pública de redirecionamento `GET /{code}` | **Constituição Princípio I** (`/shorturl-api/{GroupName}`) combinada com **FR-001**, **FR-006** e **FR-008**. |

---

## Decisões Técnicas Consolidadas

### D-01: Persistência em SQLite via EF Core (WAL Mode) e Estratégia de Escala (FR-009 / FR-010)
- **Decision**: Utilizar SQLite gerenciado via Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`) com `Journal Mode = WAL` (*Write-Ahead Logging*) e `Synchronous = NORMAL`, isolado atrás da interface `IApplicationDbContext` na camada `Application`.
- **Rationale**:
  - Atende diretamente à **Stack Tecnológica da Constituição** e ao **FR-009 (RF-08)** para persistência durável entre reinicializações.
  - O modo WAL permite leituras concorrentes sem bloqueio durante gravações (essencial para a proporção 10:1 de leitura/escrita do **FR-010**).
  - Para as metas de alta escala (**FR-010**: 1.160 gravações/s, 11.600 leituras/s e 36,5 TB em 10 anos), o isolamento em Clean Architecture (`IApplicationDbContext` + `IShortCodeGenerator`) permite particionamento horizontal (*range/hash sharding* por `Id`/`Code`), cache de leitura em memória/distribuído para `GET /{code}` (como `TargetUrl` e `ExpiresAtUtc` são imutáveis após a criação) e alocação de `Id` por blocos (*HiLo* / sequências distribuídas) sem alterar `Domain` ou `Application`.
- **Alternatives considered**:
  - *In-Memory Database*: Rejeitado por violar FR-009 (perda de dados no reinício).
  - *PostgreSQL / NoSQL direto no MVP*: Rejeitado pois a Constituição padroniza SQLite como banco de dados homologado da solução.

### D-02: Incremento Monotônico e Atômico de `AccessCount` (FR-006 / FR-007 / RN-04)
- **Decision**: Executar o incremento de acessos diretamente no banco de dados via instrução SQL/EF Core atômica (`ExecuteUpdateAsync`) condicionada à vigência do link (`ExpiresAtUtc >= nowUtc`), evitando o antipadrão *read-modify-write* em memória.
- **Rationale**:
  - Sob acessos simultâneos (User Story 2 e Edge Case de concorrência no limite de expiração), ler a entidade na aplicação, somar `1` em C# e chamar `SaveChangesAsync()` gera perda de atualizações (*lost updates*).
  - Um `UPDATE ShortLinks SET AccessCount = AccessCount + 1 WHERE Code = @code AND ExpiresAtUtc >= @nowUtc` (ou filtrando pelo `Id` decodificado bijeçionalmente) garante atomicidade no motor de armazenamento, respeita **RN-03** (não incrementa se expirado) e **RN-04** (nunca diminui).
- **Alternatives considered**:
  - *Lock pessimista na aplicação (`SemaphoreSlim`)*: Rejeitado por serializar threads do servidor web e degradar latência sob 11.600 RPS.
  - *Concorrência otimista (`RowVersion`) com retries*: Rejeitado porque em links virais o alto índice de colisão geraria tempestade de retentativas (*retry storms*).

### D-03: Algoritmo de Geração e Ofuscação Base62 Estilo Hashids de 7 Caracteres (FR-003 / FR-004 / OH-01..OH-04)
- **Decision**: Implementar um serviço de domínio/infraestrutura nativo (`Base62CodeGenerator : IShortCodeGenerator`) sem dependências NuGet externas, utilizando o alfabeto Base62 completo (`0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz`) embaralhado de forma determinística por um `Salt` secreto (padrão *Fisher-Yates shuffle* do Hashids) e bijeção modular sobre o `Id` sequencial de 64 bits (`long`), produzindo sempre códigos de exatos **7 caracteres** (`[0-9A-Za-z]{7}`).
- **Rationale**:
  - **Conformidade com a Constituição (Princípio II)**: Evita adicionar pacotes NuGet externos não listados na Stack Tecnológica (ex.: `Hashids.net`), mantendo zero dependências extras.
  - **Conformidade com FR-003, FR-004 e OH-01..OH-04**:
    1. O espaço de 7 caracteres em Base62 possui $62^7 = 3.521.614.606.208$ (~3,52 trilhões) de combinações, comportando com folga os 365 bilhões de registros de 10 anos (CE-03).
    2. A partir do `Id` sequencial (`1..365.000.000.000`), aplica-se uma permutação bijetiva no domínio $[0, 62^7 - 1]$ (transformação afim modular coprima com $62^7$ derivada do `Salt` + alfabeto Base62 embaralhado pelo `Salt`, conforme ilustrado em `OH-03`/`OH-04`) e converte-se para 7 dígitos em base 62.
    3. Como a função é **estritamente bijetiva** ($f(Id_1) \neq f(Id_2)$ para todo $Id_1 \neq Id_2$), garante-se **zero colisão** sem qualquer `SELECT` prévio no banco de dados (OH-02), além de permitir decodificação reversa em $O(1)$ (`Decode(code) -> Id`) para buscas por chave primária (`INTEGER PRIMARY KEY`).
- **Alternatives considered**:
  - *Alfabeto reduzido sem caracteres ambíguos (Base58)*: Rejeitado pois `spec.md` (FR-003) e `checklists/requirements.md` determinam explicitamente o uso do Base62 completo (`0-9`, `A-Z`, `a-z`).
  - *Geração aleatória (`RandomNumberGenerator`) com loop de checagem no banco*: Rejeitado por violar `FR-004` e `OH-02` (consultas extras de colisão ao banco são custosas em 1.160 RPS).

### D-04: Garantias de Unicidade e Fluxo de Persistência do `Id` Sequencial (FR-004 / Edge Cases)
- **Decision**: Utilizar a chave primária `INTEGER PRIMARY KEY AUTOINCREMENT` (`Id` do tipo `long`) do SQLite como semente sequencial única e manter um índice único (`UNIQUE INDEX IX_ShortLinks_Code`) na coluna `Code`.
- **Rationale**:
  - Como o código depende exclusivamente do `Id` sequencial atribuído pelo banco (ou por um gerador de sequência atômico `HiLo`/transacional), duas requisições simultâneas para a mesma `TargetUrl` (ou URLs distintas) sempre recebem `Id`s diferentes e, portanto, `Code`s de 7 caracteres distintos.
  - O índice único em `Code` atua como invariante de integridade relacional e acelera consultas por `Code` caso a busca não utilize a decodificação direta para `Id`.
- **Alternatives considered**:
  - *Hash MD5/SHA256 da `TargetUrl` truncado*: Rejeitado porque a especificação (Edge Case: *Submissão repetida da mesma URL de destino*) exige que cada solicitação gere um novo registro com `Id`, `Code`, expiração e contador independentes.

### D-05: Segurança de Segredos e Validação de URL Pública sem I/O de Rede (FR-002 / RN-02 / Constituição VI)
- **Decision**:
  1. Armazenar o `Salt` de ofuscação Base62 fora do código-fonte (via `IOptions<ShortUrlOptions>` lendo de `user-secrets` em desenvolvimento e variáveis de ambiente / Azure Key Vault em produção).
  2. Validar a `TargetUrl` no `CreateShortLinkCommandValidator` (FluentValidation) exigindo: tamanho $\le 2048$ caracteres, `Uri.TryCreate` absoluto com esquema `http` ou `https`, host não vazio, proibindo `localhost`, domínios `.local`/`.internal`, endereços IP de loopback (`127.0.0.0/8`, `::1`), ranges privados RFC 1918 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), link-local (`169.254.0.0/16`, `fe80::/10`) e IPs reservados/indefinidos (`0.0.0.0`).
- **Rationale**:
  - Protege o segredo de ofuscação conforme o **Princípio VI da Constituição**.
  - Cumpre **FR-002 (RN-02)** e a premissa de validação sem I/O de rede síncrono (evitando SSRF e mantendo baixa latência em 1.160 gravações/s).
- **Alternatives considered**:
  - *Resolução DNS / Ping HTTP síncrono na criação*: Rejeitado na seção `Assumptions` da `spec.md` por introduzir latência de rede externa e bloquear threads sob alta carga.

### D-06: Semântica de Expiração em UTC (FR-005 / RF-06 / RN-03)
- **Decision**:
  - Quando `expiresAtUtc` não for informado no `POST`, definir `ExpiresAtUtc = CreatedAtUtc.AddYears(10)`.
  - Quando `expiresAtUtc` for informado, validar que a data em UTC não está no passado em relação à data atual em UTC e normalizar o limite de vigência para o último tick daquela data em UTC: `new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Utc).AddTicks(9999999)` (`23:59:59.9999999Z`).
  - Se o link existir mas `DateTime.UtcNow > link.ExpiresAtUtc`, lançar `ShortLinkExpiredException` traduzida pelo middleware global para `410 Gone` (`ProblemDetails` em português), sem incrementar `AccessCount`.
- **Rationale**: Atende integralmente aos cenários de aceitação 1 a 4 da User Story 3 e à premissa `Semântica de Data de Expiração (RF-06)` da `spec.md`.
- **Alternatives considered**:
  - *Expiração nula (`NULL`) para links sem data informada*: Rejeitado pois o **FR-005 (RF-06)** determina explicitamente validade padrão de 10 anos a partir de `CreatedAtUtc`.


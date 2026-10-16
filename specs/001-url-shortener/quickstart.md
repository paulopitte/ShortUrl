# Quickstart & Validation Guide: Encurtador de Links (ShortURL)

**Feature Branch**: `001-url-shortener`
**Date**: 2026-10-16
**References**: [spec.md](./spec.md) | [data-model.md](./data-model.md) | [contracts/http-api.md](./contracts/http-api.md)

## 1. Pré-requisitos

- **.NET 10 SDK** instalado (`dotnet --version` $\ge 10.0.100$)
- Ferramenta `dotnet-ef` para execução de migrações (opcional se aplicada na inicialização em ambiente de desenvolvimento)

---

## 2. Configuração de Segredos e Banco de Dados (Constituição Princípio VI)

Nenhum segredo (como o `Salt` da ofuscação Base62/Hashids ou a *connection string* de produção) deve ser versionado no repositório. Em desenvolvimento local, configure via `user-secrets`:

```powershell
dotnet user-secrets set "ShortUrl:HashSalt" "dev-secret-salt-2026-shorturl" --project src/Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=shorturl.db;Cache=Shared;Mode=ReadWriteCreate;" --project src/Api
```

Aplicar as migrações do EF Core (SQLite com `Journal Mode = WAL`):

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

---

## 3. Execução da Aplicação e dos Testes Automatizados

### Executar a Suíte de Testes (Constituição Princípio III)

```powershell
dotnet test
```
- **Testes Unitários (`tests/Domain.UnitTests`, `tests/Application.UnitTests`)**: Validam a bijeção de 7 caracteres Base62 (`IShortCodeGenerator`), regras de expiração UTC (padrão 10 anos e fim do dia `23:59:59.9999999Z`), validação de URL pública (`RN-02`) e handlers CQRS.
- **Testes de Integração (`tests/Api.IntegrationTests`)**: Validam o fluxo ponta a ponta via `WebApplicationFactory`, incluindo redirecionamento `302`, incremento atômico concorrente de `AccessCount` no SQLite e respostas `ProblemDetails` em português (`400`, `404`, `410`).

### Iniciar a API Localmente

```powershell
dotnet run --project src/Api
```

---

## 4. Cenários de Validação Ponta a Ponta

Consulte os payloads e schemas detalhados em [contracts/http-api.md](./contracts/http-api.md).

### Cenário 1: Encurtar URL com Expiração Padrão de 10 Anos (US1 / US3)

```powershell
curl -i -X POST http://localhost:5000/shorturl-api/links `
  -H "Content-Type: application/json" `
  -d '{"targetUrl": "https://learn.microsoft.com/pt-br/dotnet/core/whats-new/dotnet-10/overview"}'
```
- **Resultado esperado**: HTTP `201 Created`, retornando `code` de 7 caracteres Base62 (`^[0-9A-Za-z]{7}$`), `shortUrl` e `expiresAtUtc` igual a `createdAtUtc + 10 anos`.

### Cenário 2: Redirecionamento Imediato e Incremento Atômico de Acessos (US1 / US2)

```powershell
# Substitua {code} pelo código de 7 caracteres retornado no Cenário 1
curl -i http://localhost:5000/{code}
```
- **Resultado esperado**: HTTP `302 Found` com header `Location` apontando para a URL original.

```powershell
# Consultar o total de acessos publicamente (sem necessidade de header de autenticação)
curl -i http://localhost:5000/shorturl-api/links/{code}/stats
```
- **Resultado esperado**: HTTP `200 OK` com `"accessCount": 1`.

### Cenário 3: Rejeição de URL Inválida ou Host Privado em Português (US1 / FR-002)

```powershell
curl -i -X POST http://localhost:5000/shorturl-api/links `
  -H "Content-Type: application/json" `
  -d '{"targetUrl": "http://127.0.0.1:8080/admin"}'
```
- **Resultado esperado**: HTTP `400 Bad Request` no formato `ProblemDetails` com mensagem de validação em português recusando host privado/local.

### Cenário 4: Expiração em UTC e Bloqueio de Acesso Pós-Vencimento (US3 / FR-005 / FR-006)

```powershell
# Tentar criar link com data no passado em UTC
curl -i -X POST http://localhost:5000/shorturl-api/links `
  -H "Content-Type: application/json" `
  -d '{"targetUrl": "https://exemplo.com", "expiresAtUtc": "2020-01-01"}'
```
- **Resultado esperado**: HTTP `400 Bad Request` (`ProblemDetails` em português informando que a data de expiração deve ser futura).
- Para um link cuja `ExpiresAtUtc` já tenha sido ultrapassada, `GET /{code}` deve retornar HTTP `410 Gone` (`ProblemDetails` em português) e manter `accessCount` inalterado em `GET /shorturl-api/links/{code}/stats`.

### Cenário 5: Persistência Após Reinício do Serviço (FR-009 / SC-004)

1. Pare o processo da API (`Ctrl+C`) e inicie-o novamente (`dotnet run --project src/Api`).
2. Execute `GET /shorturl-api/links/{code}/stats` e `GET /{code}` para o código criado no Cenário 1.
- **Resultado esperado**: O link permanece íntegro no arquivo SQLite e o contador continua incrementando a partir do valor anterior.


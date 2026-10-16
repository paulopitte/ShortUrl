# HTTP API Contracts: Encurtador de Links (ShortURL)

**Feature Branch**: `001-url-shortener`
**Date**: 2026-10-16
**References**: [spec.md](../spec.md) | [data-model.md](../data-model.md) | [constitution.md](../../../.specify/memory/constitution.md)

## Convenções Gerais da API (Constituição)

- **Agrupamento de Rotas (Princípio I)**:
  - Endpoints da API herdam de `EndpointGroupBase` e são mapeados sob `/shorturl-api/{GroupName}` (ex.: `/shorturl-api/links`).
  - O endpoint de redirecionamento público responde na raiz `GET /{code}` (e opcionalmente espelhado em `/shorturl-api/links/{code}/redirect`) para cumprir o requisito de URL o mais curta possível (**FR-001** / **FR-006**).
- **Padronização de Erros (Princípio IV)**:
  - Respostas de erro (`400`, `404`, `410`, `500`) utilizam `application/problem+json` (`ProblemDetails`, RFC 9457 / RFC 7807) com todas as mensagens voltadas ao cliente em **português**.
- **Sem Autenticação ou Chave de Gerenciamento (`FR-003`, `FR-008`, `Out of Scope`)**:
  - Não há campo `code` na entrada do `POST` (geração 100% automática de 7 caracteres em Base62).
  - Não há `managementKey` no retorno do `POST` nem header de chave em `GET .../stats` (consulta pública conforme **FR-008**).

---

## 1. Criar Link Curto

- **Método / Rota**: `POST /shorturl-api/links`
- **CQRS Dispatch**: `ISender.Send(new CreateShortLinkCommand(request.TargetUrl, request.ExpiresAtUtc))`
- **Requisitos Atendidos**: `FR-001`, `FR-002`, `FR-003`, `FR-004`, `FR-005`, `FR-009`

### Request Body (`application/json`)

```json
{
  "targetUrl": "https://exemplo.com.br/artigos/arquitetura-alta-escala-2026",
  "expiresAtUtc": "2027-12-31"
}
```

| Campo | Tipo | Obrigatório | Regras de Validação (`FluentValidation`) |
|-------|------|-------------|------------------------------------------|
| `targetUrl` | `string` | Sim | Não vazio; tamanho máximo de 2048 caracteres; URI absoluta com esquema `http` ou `https`; host público válido (proibido `localhost`, loopback, IPs privados RFC 1918 e link-local). |
| `expiresAtUtc` | `DateTime?` / `string?` (ISO-8601 UTC) | Não | Quando omitido (`null`), assume `CreatedAtUtc + 10 anos`. Quando informado, deve representar uma data futura/vigente em UTC e é normalizado até o fim do dia (`23:59:59.9999999Z`). |

### Responses

#### `201 Created` (`application/json`)
Header `Location: /{code}`

```json
{
  "code": "cJ09A2x",
  "shortUrl": "https://sho.rt/cJ09A2x",
  "targetUrl": "https://exemplo.com.br/artigos/arquitetura-alta-escala-2026",
  "createdAtUtc": "2026-10-16T16:30:00.0000000Z",
  "expiresAtUtc": "2027-12-31T23:59:59.9999999Z"
}
```

#### `400 Bad Request` (`application/problem+json`)
Retornado quando `targetUrl` é inválida (esquema inválido, $>2048$ caracteres, host privado/local) ou quando `expiresAtUtc` está no passado.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Erro de validação na requisição.",
  "status": 400,
  "detail": "Um ou mais erros de validação ocorreram.",
  "errors": {
    "TargetUrl": [
      "A URL de destino deve utilizar o protocolo http ou https e apontar para um endereço público válido."
    ],
    "ExpiresAtUtc": [
      "A data de expiração deve ser uma data futura em UTC."
    ]
  }
}
```

---

## 2. Redirecionar Visitante pelo Código Curto

- **Método / Rota**: `GET /{code}`
- **CQRS Dispatch**: `ISender.Send(new ResolveShortLinkCommand(code))`
- **Requisitos Atendidos**: `FR-006`, `FR-007`, `RN-03`, `RN-04`

### Route Parameters

| Parâmetro | Tipo | Obrigatório | Regras de Validação |
|-----------|------|-------------|---------------------|
| `code` | `string` | Sim | 7 caracteres alfanuméricos Base62 (`^[0-9A-Za-z]{7}$`). |

### Responses

#### `302 Found`
Redirecionamento imediato sem página intermediária e com incremento atômico de `AccessCount` no banco (`AccessCount = AccessCount + 1`).
- Header `Location: https://exemplo.com.br/artigos/arquitetura-alta-escala-2026`
- Header `Cache-Control: no-store, no-cache, must-revalidate` (garante que todo acesso passe pelo servidor para contabilização exata em `AccessCount` conforme **FR-007**).

#### `404 Not Found` (`application/problem+json`)
Quando o código não existe no sistema (`AccessCount` não é alterado).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Link curto não encontrado.",
  "status": 404,
  "detail": "O link curto informado não foi encontrado."
}
```

#### `410 Gone` (`application/problem+json`)
Quando o código existe, mas `DateTime.UtcNow > ExpiresAtUtc` (`AccessCount` não é incrementado).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.11",
  "title": "Link curto expirado.",
  "status": 410,
  "detail": "Este link curto expirou e não está mais disponível para redirecionamento."
}
```

---

## 3. Consultar Estatísticas Públicas de Acessos do Link

- **Método / Rota**: `GET /shorturl-api/links/{code}/stats`
- **CQRS Dispatch**: `ISender.Send(new GetShortLinkStatsQuery(code))`
- **Requisitos Atendidos**: `FR-008` (`RF-07` — consulta pública sem autenticação para qualquer pessoa que conheça o código)

### Route Parameters

| Parâmetro | Tipo | Obrigatório | Regras de Validação |
|-----------|------|-------------|---------------------|
| `code` | `string` | Sim | 7 caracteres alfanuméricos Base62 (`^[0-9A-Za-z]{7}$`). |

### Responses

#### `200 OK` (`application/json`)

```json
{
  "code": "cJ09A2x",
  "targetUrl": "https://exemplo.com.br/artigos/arquitetura-alta-escala-2026",
  "createdAtUtc": "2026-10-16T16:30:00.0000000Z",
  "expiresAtUtc": "2027-12-31T23:59:59.9999999Z",
  "isExpired": false,
  "accessCount": 42
}
```

#### `404 Not Found` (`application/problem+json`)
Quando o código informado não existe.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Link curto não encontrado.",
  "status": 404,
  "detail": "O link curto informado não foi encontrado."
}
```


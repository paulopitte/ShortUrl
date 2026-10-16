# Feature Specification: Encurtador de Links (ShortURL)

**Feature Branch**: `001-url-shortener`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: "Encurtador de links com geração automática de códigos ofuscados (Base62/Hashids), redirecionamento imediato, contagem de acessos, data de expiração em UTC (padrão 10 anos) e alta escala (100M URLs/dia)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Encurtar URL com Código Automático e Redirecionar Visitante (Priority: P1)

Como criador do link, quero transformar uma URL longa em um link curto gerado automaticamente pelo sistema para compartilhá-lo sem risco de quebra. Como visitante, quero acessar o link curto e ser redirecionado imediatamente ao endereço original sem telas intermediárias.

**Why this priority**: É a proposta de valor central do produto (MVP). Sem a geração automática de links curtos e o redirecionamento imediato, o sistema não cumpre seu objetivo básico.

**Independent Test**: Enviar uma URL válida, receber o link curto com código Base62 de 7 caracteres gerado pelo sistema e acessá-lo para verificar o redirecionamento imediato ao destino original.

**Acceptance Scenarios**:

1. **Given** uma URL pública válida (`http` ou `https`, até 2048 caracteres), **When** o criador solicita o encurtamento, **Then** o sistema gera automaticamente um código único de 7 caracteres em Base62, persiste o vínculo e retorna o link curto correspondente.
2. **Given** um link curto ativo (dentro do prazo de validade), **When** um visitante acessa o código desse link, **Then** o sistema redireciona imediatamente para a URL de destino original sem exibir páginas intermediárias.
3. **Given** uma URL inválida (mais de 2048 caracteres, protocolo diferente de `http`/`https` ou host privado/local), **When** o criador tenta encurtá-la, **Then** o sistema recusa a criação e informa o motivo da rejeição em português.

---

### User Story 2 - Acompanhar Total de Acessos do Link (Priority: P2)

Como criador do link ou visitante que conhece o código, quero consultar publicamente quantos acessos o link curto recebeu para medir seu alcance e engajamento.

**Why this priority**: Resolve a necessidade de mensuração de alcance dos links compartilhados, agregando valor analítico direto ao MVP.

**Independent Test**: Criar um link curto, realizar múltiplos acessos válidos (incluindo acessos repetidos e concorrentes) e consultar o código para confirmar que o contador reflete exatamente o total de redirecionamentos realizados.

**Acceptance Scenarios**:

1. **Given** um link curto recém-criado sem acessos, **When** qualquer pessoa consulta o total de acessos pelo código, **Then** o sistema retorna `0`.
2. **Given** um link curto ativo, **When** ocorrem acessos válidos (inclusive repetidos da mesma origem, automatizados ou simultâneos), **Then** cada redirecionamento incrementa o contador em `1` de forma monotônica (sem perda ou redução).
3. **Given** um código inexistente no sistema, **When** alguém tenta acessá-lo ou consultar seu total de acessos, **Then** o sistema responde em português que o link não foi encontrado.

---

### User Story 3 - Definir Data de Expiração do Link (Priority: P3)

Como criador do link, quero definir opcionalmente uma data de expiração em UTC para que o link funcione somente até o prazo estipulado.

**Why this priority**: Oferece controle temporal sobre links de campanhas temporárias, impedindo redirecionamentos e contagens após o vencimento.

**Independent Test**: Criar um link com data de expiração em UTC, validar o redirecionamento antes do vencimento e confirmar que, após o prazo, o acesso é bloqueado e o contador não é incrementado.

**Acceptance Scenarios**:

1. **Given** uma solicitação de criação sem data de expiração informada, **When** o link é criado, **Then** o sistema define automaticamente sua expiração para 10 anos em UTC a partir do instante de criação.
2. **Given** uma solicitação de criação com data de expiração futura em UTC, **When** o link é acessado até o limite de validade (`23:59:59.9999999Z` da data informada), **Then** o sistema redireciona normalmente e incrementa o contador de acessos.
3. **Given** um link cuja data de expiração em UTC já foi ultrapassada, **When** um visitante tenta acessá-lo, **Then** o sistema bloqueia o redirecionamento, informa em português que o link expirou e mantém o contador de acessos inalterado.
4. **Given** uma solicitação de criação com data de expiração no passado em UTC, **When** o criador tenta criar o link, **Then** o sistema recusa a criação e informa em português que a data de expiração deve ser futura.

---

### Edge Cases

- **Criação simultânea em alta concorrência**: Requisições concorrentes de criação de links obtêm identificadores sequenciais (`Id`) distintos, resultando em códigos Base62 de 7 caracteres estritamente únicos sem colisão.
- **Submissão repetida da mesma URL de destino**: Cada nova solicitação de encurtamento gera um novo registro com `Id` e `Code` próprios, permitindo controle de expiração e contagem de acessos independentes por link criado.
- **Concorrência de acessos no limite de expiração**: Acessos processados a partir do instante posterior a `ExpiresAtUtc` são rejeitados atomicamente sem incrementar `AccessCount`.
- **Reinicialização do serviço**: Links criados, datas de expiração e contadores de acesso permanecem íntegros após reinício da aplicação.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (RF-01)**: O sistema DEVE receber uma URL de destino válida e retornar um link curto único pronto para uso, apresentando todas as mensagens de erro e validação em português.
- **FR-002 (RN-02)**: O sistema DEVE validar a URL de destino na criação, exigindo esquema `http` ou `https`, tamanho máximo de 2048 caracteres e host público válido (rejeitando `localhost`, loopback `127.0.0.0/8` e `::1`, IPs privados RFC 1918 e endereços link-local).
- **FR-003 (RF-02 / RN-01 / RN-05)**: O sistema DEVE gerar exclusivamente de forma automática um código único de 7 caracteres para cada link criado, utilizando o alfabeto Base62 (`0-9`, `A-Z`, `a-z`, totalizando 62 caracteres, sem espaços ou caracteres especiais).
- **FR-004 (OH-01..OH-04 / RN-06)**: A geração automática de códigos DEVE aplicar codificação Base62 com ofuscação determinística bijetiva (estilo Hashids com sal secreto) sobre identificadores numéricos sequenciais únicos (`Id`), garantindo unicidade global sem necessidade de consultas prévias de colisão ao banco de dados.
- **FR-005 (RF-06 / RN-03)**: O sistema DEVE armazenar e avaliar a expiração em UTC: quando omitida na criação, a validade padrão DEVE ser de 10 anos a partir de `CreatedAtUtc`; quando informada uma data futura, a validade DEVE estender-se até o final do dia informado (`23:59:59.9999999Z`); datas no passado DEVEM ser recusadas na criação.
- **FR-006 (RF-04 / RN-03)**: Ao receber acesso a um código existente e não expirado, o sistema DEVE redirecionar imediatamente o visitante para a `TargetUrl` sem tela intermediária; se o código não existir ou estiver expirado, DEVE recusar o redirecionamento com o respectivo erro.
- **FR-007 (RF-05 / RN-04)**: Cada redirecionamento válido DEVE incrementar atomicamente o contador de acessos (`AccessCount`) em `1` (incluindo acessos repetidos, automatizados ou simultâneos), garantindo que o valor nunca diminua e não seja alterado em tentativas de acesso a links inexistentes ou expirados.
- **FR-008 (RF-07)**: O sistema DEVE permitir a consulta pública do total acumulado de acessos (`AccessCount`) a partir do código do link, sem exigir autenticação.
- **FR-009 (RF-08 / RF-09)**: Todos os dados do link (`Id`, `Code`, `TargetUrl`, `CreatedAtUtc`, `ExpiresAtUtc` e `AccessCount`) DEVEM ser persistidos em banco de dados para preservar a integridade após reinicializações.
- **FR-010 (RF-00 / RF-10 / RF-11 / CE-01..CE-04)**: A arquitetura DEVE operar em alta disponibilidade (24/7) e ser dimensionada para 100 milhões de novas URLs/dia (~1.160 gravações/s), proporção de 10 leituras por gravação (~11.600 leituras/s) e retenção de 10 anos (365 bilhões de registros, ~36,5 TB considerando `TargetUrl` média de 100 bytes).

### Key Entities *(include if feature involves data)*

- **ShortLink (Link Curto)**:
  - **Id**: Identificador numérico sequencial único usado como semente bijetiva para geração do código curto.
  - **Code**: Código alfanumérico único de 7 caracteres em Base62 gerado automaticamente pelo sistema.
  - **TargetUrl**: URL original de destino (`http`/`https`, pública, até 2048 caracteres).
  - **CreatedAtUtc**: Data e hora exata de criação em UTC.
  - **ExpiresAtUtc**: Instante limite de validade em UTC (`23:59:59.9999999Z` da data informada ou `CreatedAtUtc` + 10 anos por padrão).
  - **AccessCount**: Contador inteiro monotônico crescente (iniciado em `0`) de redirecionamentos válidos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos links válidos criados retornam código único de 7 caracteres em Base62 gerado automaticamente e redirecionam imediatamente ao destino enquanto vigentes.
- **SC-002**: 100% das tentativas de acesso a códigos inexistentes ou expirados são bloqueadas sem redirecionamento e sem incremento de `AccessCount`.
- **SC-003**: 100% dos redirecionamentos válidos sob acessos concorrentes incrementam `AccessCount` sem perda de contagem ou regressão de valor.
- **SC-004**: 100% dos registros e contadores permanecem íntegros após reinicialização da aplicação.
- **SC-005**: A solução atende às metas de capacidade de 1.160 gravações/s, 11.600 leituras/s (10:1), armazenamento de 365 bilhões de links em 10 anos (~36,5 TB) e operação 24/7.

## Out of Scope

- Escolha de código personalizado (*custom alias*) pelo criador do link.
- Cadastro, autenticação e controle de acesso de usuários.
- Domínios personalizados configurados pelo criador.
- Métricas analíticas detalhadas (geolocalização, dispositivo, navegador ou referenciador).
- Edição ou exclusão manual de links após a criação.

## Assumptions

- **Capacidade Combinatória do Base62 (OH-01 / RF-02)**: O espaço de 7 caracteres em Base62 oferece $62^7 \approx 3{,}52 \times 10^{12}$ combinações (~9,6x o volume de 365 bilhões de URLs em 10 anos), viabilizando bijeção determinística sobre o `Id` sequencial sem risco de colisão ou exaustão do espaço de chaves.
- **Validação de URL Pública sem I/O de Rede (RN-02)**: A verificação de "endereço publicamente acessível" é realizada por inspeção sintática e validação de host/IP (bloqueando ranges locais e privados), sem efetuar requisições HTTP/DNS externas síncronas durante a criação para não comprometer a vazão de 1.160 gravações/s.
- **Semântica de Data de Expiração (RF-06)**: O parâmetro de expiração representa uma data de calendário em UTC cuja vigência encerra-se às `23:59:59.9999999Z` do dia especificado; caso um timestamp completo seja enviado com horário específico diferente de `00:00:00Z`, a entrada é normalizada para o fim daquela data em UTC (`23:59:59.9999999Z`).

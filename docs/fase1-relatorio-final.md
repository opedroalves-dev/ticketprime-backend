# Relatório Final — Fase 1: Segurança, Estabilidade e Incrementos

**Projeto:** TicketPrime  
**Data:** 2026-06-01  
**Versão:** 1.0.0  
**Stack:** .NET 8, Minimal API, Dapper, SQL Server, xUnit

---

## Sumário

1. [Itens Implementados](#1-itens-implementados)
2. [Riscos Corrigidos](#2-riscos-corrigidos)
3. [Dívidas Técnicas Remanescentes](#3-dívidas-técnicas-remanescentes)
4. [Decisões Arquiteturais](#4-decisões-arquiteturais)
5. [Mudanças de Contrato](#5-mudanças-de-contrato)
6. [Testes Executados](#6-testes-executados)

---

## 1. Itens Implementados

### 1.1. Fase 1 — Segurança e Estabilidade (v3)

A Fase 1 foi planejada no documento [`docs/plano-fase1-seguranca-estabilidade-v3.md`](docs/plano-fase1-seguranca-estabilidade-v3.md) e executada conforme a ordem definida, com 8 itens cobrindo segurança, resiliência e operabilidade.

| Item | Descrição | Correções | Arquivos |
|:----:|-----------|:---------:|----------|
| **0** | **`.gitignore`** — Criado para proteger arquivos sensíveis (`appsettings.Development.json`, `bin/`, `obj/`, etc.) contra versionamento | C2, F1 | [`.gitignore`](.gitignore) |
| **1** | **Remoção de senha hardcoded** — Senha do SQL Server removida do [`appsettings.json`](src/TicketPrime.Api/appsettings.json); movida para User Secrets (dev) e variável de ambiente (produção) | — | [`appsettings.json`](src/TicketPrime.Api/appsettings.json), [`Program.cs`](src/TicketPrime.Api/Program.cs:11) |
| **2** | **Autenticação admin via API Key + Logging** — Endpoints `/api/admin/*` protegidos com header `X-Api-Key`; logging de acessos autorizados (Information) e não autorizados (Warning) com IP e path | A1, F5, N2, N3 | [`ApiKeyAuthenticationHandler.cs`](src/TicketPrime.Api/Authentication/ApiKeyAuthenticationHandler.cs), [`ApiKeyAuthenticationSchemeOptions.cs`](src/TicketPrime.Api/Authentication/ApiKeyAuthenticationSchemeOptions.cs), [`Program.cs`](src/TicketPrime.Api/Program.cs:24) |
| **3** | **Middleware global de exception handling** — Tratamento padronizado: `BadHttpRequestException` → 400, `ValidationException` → 400, demais exceções → 500 com JSON sanitizado | A2, F3 | [`ExceptionHandlingMiddleware.cs`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs), [`ValidationException.cs`](src/TicketPrime.Api/Middleware/ValidationException.cs), [`Program.cs`](src/TicketPrime.Api/Program.cs:48) |
| **4** | **Transações no fluxo de confirmação de carrinho** — Atomicidade no `POST /api/carrinho/{cpf}/confirmar` com rollback garantido via `ValidationException`; `commandTimeout: 30` em todas as chamadas Dapper | C1, F2, F6, N1 | [`Program.cs`](src/TicketPrime.Api/Program.cs) (endpoint de confirmação) |
| **5** | **Correção de interpolações SQL inseguras** — Substituição de `DROP VIEW` + `CREATE VIEW` por `CREATE OR ALTER VIEW` (atômico); refatoração de WHERE dinâmico para parâmetros opcionais (`@Param IS NULL OR coluna = @Param`) | A3 | [`Program.cs`](src/TicketPrime.Api/Program.cs:300) (views), [`Program.cs`](src/TicketPrime.Api/Program.cs) (filtros admin) |
| **6** | **CORS configurado** — Política `AllowFrontend` com origens explícitas (`localhost:3000`, `localhost:5173`), validação no startup | — | [`Program.cs`](src/TicketPrime.Api/Program.cs:33), [`appsettings.json`](src/TicketPrime.Api/appsettings.json) |
| **7** | **Endpoint `/health`** — Health check simples (F4) que retorna `200 Healthy` ou `503 Unhealthy`; público, sem acesso a banco | F4 | [`Program.cs`](src/TicketPrime.Api/Program.cs:402) |

### 1.2. RF01 — Ingresso Digital com Código Único

Geração de código único alfanumérico de 8 caracteres (A-Z, 0-9) para cada ingresso.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/reservas/{id}/ingresso` | Gerar ingresso digital para reserva existente |
| `GET` | `/api/reservas/{id}/ingresso` | Consultar ingresso por reserva |
| `GET` | `/api/ingressos/{codigo}` | Consultar ingresso pelo código único |

**Tabela criada:** [`Ingressos`](db/ticketprime_incrementos.sql:50) — FK para `Reservas` e `TiposIngresso`, com `CodigoUnico VARCHAR(8) UNIQUE`.

**Regras de negócio:**
- Código único de 8 caracteres alfanuméricos, gerado aleatoriamente
- Colisão tratada com loop de regeneração automática
- Ingresso nasce com `Status = 'Confirmada'`
- Check `LEN(CodigoUnico) = 8` via CONSTRAINT no banco

### 1.3. RF02 — Check-in de Ingresso

Registro de entrada do portador do ingresso no evento.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/ingressos/{codigo}/checkin` | Realizar check-in pelo código único |
| `POST` | `/api/checkin` | Check-in alternativo via body (`{ "CodigoIngresso": "..." }`) |
| `GET` | `/api/eventos/{eventoId}/checkins` | Listar check-ins de um evento |
| `GET` | `/api/eventos/{eventoId}/checkins/stats` | Estatísticas de check-in |

**Tabela criada:** [`CheckIns`](db/ticketprime_incrementos.sql:108) — FK para `Ingressos`, UNIQUE em `IngressoId` (um check-in por ingresso).

**Regras de negócio:**
- Check-in só permitido se ingresso existir e estiver `Status = 'Confirmada'`
- Um único check-in por ingresso (impede reuso)
- Após check-in, status do ingresso passa para `'Utilizada'`

### 1.4. RF03 — Tipos/Lotes de Ingresso

Lotes com preço diferenciado, capacidade própria e período de venda.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/eventos/{eventoId}/lotes` | Criar lote para um evento |
| `GET` | `/api/eventos/{eventoId}/lotes` | Listar lotes de um evento |
| `GET` | `/api/lotes/{loteId}` | Obter lote específico |
| `PUT` | `/api/lotes/{loteId}` | Atualizar lote (dispara histórico se preço alterado) |
| `DELETE` | `/api/lotes/{loteId}` | Remover lote (apenas sem vendas) |
| `POST` | `/api/tipos-ingresso` | Criar tipo de ingresso diretamente |
| `GET` | `/api/eventos/{eventoId}/tipos-ingresso` | Listar tipos de ingresso de um evento |

**Tabela criada:** [`TiposIngresso`](db/ticketprime_incrementos.sql:18) — FK para `Eventos`.

**Regras de negócio:**
- Capacidade não pode ser reduzida abaixo de ingressos já vendidos
- Lote com vendas não pode ser removido
- Alteração de preço registra entrada no histórico (`HistoricoPrecos`)

### 1.5. RF04 — Carrinho/Reserva Temporária

Carrinho com validade de 15 minutos para reserva temporária de ingressos.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/carrinho` | Adicionar itens ao carrinho ativo |
| `GET` | `/api/carrinho/{cpf}` | Visualizar carrinho ativo |
| `DELETE` | `/api/carrinho/{cpf}` | Limpar/excluir carrinho |
| `POST` | `/api/carrinho/{cpf}/confirmar` | Confirmar carrinho e gerar reservas + ingressos |

**Tabelas criadas:** [`Carrinhos`](db/ticketprime_incrementos.sql:138), [`CarrinhoItens`](db/ticketprime_incrementos.sql:173).

**Regras de negócio:**
- Carrinho expira em 15 minutos
- Confirmação do carrinho é transacional (Item 4 da Fase 1)
- Gera reservas + ingressos digitais automaticamente
- Suporte a cupom opcional na confirmação

### 1.6. RF05 — Transparência de Preço

Simulação e histórico de preços com discriminação completa.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/reservas/simular-preco` | Simular preço com discriminação (PrecoBase + Taxa - Desconto = Final) |
| `GET` | `/api/eventos/{eventoId}/historico-precos` | Histórico de alterações de preço do evento |
| `GET` | `/api/lotes/{loteId}/historico-precos` | Histórico de alterações de preço do lote |

**Tabela criada:** [`HistoricoPrecos`](db/ticketprime_incrementos.sql:209).

**Fórmula de cálculo:**
```
PrecoBase     = PrecoPadrao do Evento (ou Preco do TipoIngresso)
TaxaServico   = PrecoBase × 10%
ValorDesconto = PrecoBase × (PorcentagemDesconto / 100) — se cupom válido
ValorFinal    = PrecoBase + TaxaServico - ValorDesconto
```

### 1.7. RF06 — Dashboard/Admin de Eventos

Métricas administrativas com dados agregados de vendas, check-in e ocupação.

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/admin/eventos` | Listar eventos com métricas |
| `GET` | `/api/admin/eventos/{eventoId}` | Dashboard detalhado com métricas por lote |
| `GET` | `/api/admin/eventos/{eventoId}/lotes` | Métricas por lote |
| `GET` | `/api/admin/reservas` | Listar reservas (com filtros: `eventoId`, `status`, `cpf`) |
| `GET` | `/api/admin/eventos/{eventoId}/resumo` | Resumo gerencial do evento |
| `GET` | `/api/admin/eventos/{eventoId}/checkins` | Listar check-ins do evento |
| `GET` | `/api/admin/eventos/{eventoId}/reservas` | Listar reservas do evento |

**Views criadas:**
- [`vw_DashboardEventos`](src/TicketPrime.Api/Program.cs:301) — Métricas agregadas por evento
- [`vw_DashboardLotes`](src/TicketPrime.Api/Program.cs:339) — Métricas por lote

**Proteção:** Todos os endpoints `/api/admin/*` exigem autenticação via API Key (header `X-Api-Key`).

---

## 2. Riscos Corrigidos

### 2.1. Problemas Críticos

| ID | Problema | Descrição | Correção |
|:--:|----------|-----------|----------|
| **C1** | Rollback silenciosamente ignorado | `return Results.BadRequest(...)` dentro do `try` da transação não lançava exceção; o `catch` nunca executava `Rollback()`, deixando dados órfãos | Substituído por `throw new ValidationException("mensagem")` — exceção capturada pelo `catch` que executa `Rollback()` |
| **C2** | Ausência de `.gitignore` | Não existia `.gitignore` no repositório, expondo `appsettings.Development.json`, `bin/`, `obj/` ao versionamento | Criado [`.gitignore`](.gitignore) cobrindo `.NET`, secrets, IDE e arquivos de configuração |

### 2.2. Problemas Altos

| ID | Problema | Descrição | Correção |
|:--:|----------|-----------|----------|
| **A1** | Inconsistência JWT vs API Key | Plano original mencionava `JwtBearer` mas implementação usava API Key | Estratégia unificada: **apenas API Key**, sem dependência de `JwtBearer` |
| **A2** | `BadHttpRequestException` mapeada como 500 | Middleware genérico transformava **todas** as exceções em 500, inclusive requests malformados (deveriam ser 400) | Middleware agora preserva status **400** para `BadHttpRequestException` |
| **A3** | Estratégia `DROP VIEW` + `CREATE VIEW` frágil | Se `CREATE` falhasse, a view era deletada sem recuperação | Substituído por **`CREATE OR ALTER VIEW`** — operação única e atômica |

### 2.3. Correções de Auditoria Final (N1, N2, N3)

| ID | Correção | Descrição | Itens Impactados |
|:--:|----------|-----------|:----------------:|
| **N1** | Timeout de transação | `transaction.CommandTimeout = 30` substituído por `commandTimeout: 30` como parâmetro explícito em cada chamada Dapper | Item 4 |
| **N2** | `ApiKeyAuthenticationSchemeOptions` | Criada classe `ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions`; handler corrigido para herdar `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` | Item 2 |
| **N3** | Fusão autenticação + logging | Antigo Item 8 (logging de acessos) fundido no Item 2 (autenticação) em uma única etapa consolidada | Item 2 |

### 2.4. Tarefas Faltantes Incorporadas

| ID | Tarefa | Item |
|:--:|--------|:----:|
| **F1** | Criar/revisar `.gitignore` | Item 0 |
| **F2** | Corrigir estratégia de rollback das transações | Item 4 |
| **F3** | Tratamento correto para `BadHttpRequestException` | Item 3 |
| **F4** | Adicionar endpoint `/health` | Item 7 |
| **F5** | Adicionar logging de acessos não autorizados | Item 2 (fundido) |
| **F6** | Definir timeout para transações | Item 4 |

---

## 3. Dívidas Técnicas Remanescentes

Registradas formalmente em [`docs/divida-tecnica.md`](docs/divida-tecnica.md).

### TD-001: Logging de `ex.Message` em exceções genéricas no middleware

| Campo | Valor |
|-------|-------|
| **Severidade** | Baixa |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |
| **Descrição** | O [`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs) registra `ex.Message` no log para exceções genéricas. `SqlException` pode conter informações sensíveis na mensagem. |
| **Decisão** | Não bloquear — a mensagem só é registrada em logs internos, não exposta ao cliente HTTP. |
| **Fase sugerida** | Observabilidade / Logging (futura) |

### TD-002: Implementação antecipada de `Authentication/` e `Middleware/` durante Item 1

| Campo | Valor |
|-------|-------|
| **Severidade** | Média |
| **Prioridade** | Média |
| **Status** | 📝 Registrada (aceita temporariamente) |
| **Descrição** | Diretórios [`Authentication/`](src/TicketPrime.Api/Authentication/) e [`Middleware/`](src/TicketPrime.Api/Middleware/) foram implementados antecipadamente durante o Item 1, fora do escopo oficial. |
| **Decisão** | Aceito temporariamente. Manter implementação atual e validar novamente quando os Itens 2 e 3 foram executados oficialmente. |
| **Fase sugerida** | Validado durante Itens 2 e 3 da Fase 1 ✅ |

### TD-003: Race condition — limite de 2 reservas por CPF/evento não é atômico

| Campo | Valor |
|-------|-------|
| **Severidade** | Média |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |
| **Descrição** | No endpoint de confirmação de carrinho, a verificação do limite de 2 reservas por CPF/evento é feita dentro de um loop. Em concorrência, ambas as requisições podem passar na verificação antes do commit. |
| **Decisão** | Não corrigir agora. Problema preexistente, fora do escopo do Item 4. |
| **Fase sugerida** | Concorrência / Consistência (futura) |
| **Ação corretiva sugerida** | `sp_getapplock`, isolamento `SERIALIZABLE` + `UPDLOCK`, fila de confirmação, ou constraint de banco |

---

## 4. Decisões Arquiteturais

### ADR-001: Dapper + SQL Manual com Parâmetros Nomeados

Documentado em [`docs/adr.md`](docs/adr.md).

**Decisão:** Adotar **.NET 8 Minimal API** como framework de apresentação, **Dapper** como micro-ORM e **SQL manual** como estratégia de acesso a dados.

**Diretrizes:**
1. Acesso a dados exclusivamente via Dapper (`QueryAsync`, `ExecuteAsync`)
2. SQL manual com parâmetros nomeados (`@param`) — proibida concatenação/interpolação
3. Regras de negócio implementadas em serviços ([`ReservaService`](src/TicketPrime.Api/Services/ReservaService.cs), [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs))
4. Endpoints Minimal API enxutos, lógica em serviços

### API Key (NÃO JWT) para Autenticação Admin

**Decisão:** Autenticação dos endpoints `/api/admin/*` via API Key simples (header `X-Api-Key`), **sem** dependência de `JwtBearer`.

**Justificativa:**
| Critério | API Key | JWT |
|----------|:-------:|:---:|
| Complexidade | Baixa | Média |
| Dependências externas | Nenhuma | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Adequação ao porte | ✅ Ideal | ❌ Superdimensionado |

### CREATE OR ALTER VIEW (em vez de DROP + CREATE)

**Decisão:** Usar `CREATE OR ALTER VIEW` (disponível desde SQL Server 2016 SP1) como operação única e atômica para criação/atualização de views.

**Motivação:** A estratégia anterior (`DROP VIEW` + `CREATE VIEW`) era frágil — se o `CREATE` falhasse, a view era deletada sem recuperação.

### Rollback via ValidationException

**Decisão:** No fluxo de confirmação de carrinho, validações de negócio que falham disparam `throw new ValidationException("mensagem")` em vez de `return Results.BadRequest(...)`.

**Vantagens:**
1. Único ponto de rollback — o `catch` captura tanto exceções inesperadas quanto validações
2. Impossível esquecer rollback
3. `ValidationException` é tratada pelo middleware, retornando 400 padronizado

### commandTimeout:30 em Cada Chamada Dapper

**Decisão:** Em vez de `transaction.CommandTimeout = 30`, passar `commandTimeout: 30` como parâmetro explícito em cada chamada Dapper dentro da transação.

**Motivação:** Timeout em nível de comando é mais granular e previsível; torna explícito em cada chamada qual é o timeout aplicado.

---

## 5. Mudanças de Contrato

### 5.1. Novos Endpoints da API

Todos os endpoints abaixo foram adicionados **sem quebrar** os contratos existentes (AV1 e AV2):

#### RF01 — Ingresso Digital

| Método | Rota | Status Code | Body Request | Body Response |
|--------|------|:-----------:|--------------|---------------|
| `POST` | `/api/reservas/{id}/ingresso` | 201 | (vazio) | [`IngressoResponse`](src/TicketPrime.Api/Models/IngressoResponse.cs) |
| `GET` | `/api/reservas/{id}/ingresso` | 200 | — | [`IngressoResponse`](src/TicketPrime.Api/Models/IngressoResponse.cs) |
| `GET` | `/api/ingressos/{codigo}` | 200 | — | [`IngressoDetalhadoResponse`](src/TicketPrime.Api/Models/IngressoDetalhadoResponse.cs) |

#### RF02 — Check-in

| Método | Rota | Status Code | Body Request | Body Response |
|--------|------|:-----------:|--------------|---------------|
| `POST` | `/api/ingressos/{codigo}/checkin` | 201 | (vazio) | [`CheckInResponse`](src/TicketPrime.Api/Models/CheckInResponse.cs) |
| `POST` | `/api/checkin` | 201 | [`CheckInRequest`](src/TicketPrime.Api/Models/CheckInResponse.cs) | [`CheckInResponse`](src/TicketPrime.Api/Models/CheckInResponse.cs) |
| `GET` | `/api/eventos/{eventoId}/checkins` | 200 | — | [`CheckInListResponse`](src/TicketPrime.Api/Models/CheckInListResponse.cs) |
| `GET` | `/api/eventos/{eventoId}/checkins/stats` | 200 | — | [`CheckInStatsResponse`](src/TicketPrime.Api/Models/CheckInStatsResponse.cs) |

#### RF03 — Tipos/Lotes

| Método | Rota | Status Code | Body Request | Body Response |
|--------|------|:-----------:|--------------|---------------|
| `POST` | `/api/eventos/{eventoId}/lotes` | 201 | [`CriarLoteRequest`](src/TicketPrime.Api/Models/CriarLoteRequest.cs) | [`LoteResponse`](src/TicketPrime.Api/Models/LoteResponse.cs) |
| `GET` | `/api/eventos/{eventoId}/lotes` | 200 | — | [`List<LoteListaResponse>`](src/TicketPrime.Api/Models/LoteListaResponse.cs) |
| `GET` | `/api/lotes/{loteId}` | 200 | — | [`LoteResponse`](src/TicketPrime.Api/Models/LoteResponse.cs) |
| `PUT` | `/api/lotes/{loteId}` | 200 | [`CriarLoteRequest`](src/TicketPrime.Api/Models/CriarLoteRequest.cs) | [`LoteResponse`](src/TicketPrime.Api/Models/LoteResponse.cs) |
| `DELETE` | `/api/lotes/{loteId}` | 204 | — | (vazio) |
| `POST` | `/api/tipos-ingresso` | 201 | [`CriarTipoIngressoRequest`](src/TicketPrime.Api/Models/CriarTipoIngressoRequest.cs) | [`TipoIngressoResponse`](src/TicketPrime.Api/Models/TipoIngressoResponse.cs) |
| `GET` | `/api/eventos/{eventoId}/tipos-ingresso` | 200 | — | [`List<TipoIngressoResponse>`](src/TicketPrime.Api/Models/TipoIngressoResponse.cs) |

#### RF04 — Carrinho

| Método | Rota | Status Code | Body Request | Body Response |
|--------|------|:-----------:|--------------|---------------|
| `POST` | `/api/carrinho` | 201 | [`CarrinhoRequest`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) | [`CarrinhoResponse`](src/TicketPrime.Api/Models/CarrinhoResponse.cs) |
| `GET` | `/api/carrinho/{cpf}` | 200 | — | [`CarrinhoResponse`](src/TicketPrime.Api/Models/CarrinhoResponse.cs) |
| `DELETE` | `/api/carrinho/{cpf}` | 204 | — | (vazio) |
| `POST` | `/api/carrinho/{cpf}/confirmar` | 201 | [`ConfirmarCarrinhoRequest`](src/TicketPrime.Api/Models/ConfirmarCarrinhoRequest.cs) (opcional) | [`CarrinhoConfirmacaoResponse`](src/TicketPrime.Api/Models/CarrinhoConfirmacaoResponse.cs) |

#### RF05 — Transparência de Preço

| Método | Rota | Status Code | Body Request | Body Response |
|--------|------|:-----------:|--------------|---------------|
| `POST` | `/api/reservas/simular-preco` | 200 | [`SimulacaoPrecoRequest`](src/TicketPrime.Api/Models/SimulacaoPrecoRequest.cs) | [`SimulacaoPrecoResponse`](src/TicketPrime.Api/Models/SimulacaoPrecoResponse.cs) |
| `GET` | `/api/eventos/{eventoId}/historico-precos` | 200 | — | [`EventoHistoricoPrecosResponse`](src/TicketPrime.Api/Models/EventoHistoricoPrecosResponse.cs) |
| `GET` | `/api/lotes/{loteId}/historico-precos` | 200 | — | [`LoteHistoricoPrecosResponse`](src/TicketPrime.Api/Models/LoteHistoricoPrecosResponse.cs) |

#### RF06 — Dashboard/Admin

| Método | Rota | Status Code | Auth | Body Response |
|--------|------|:-----------:|:----:|---------------|
| `GET` | `/api/admin/eventos` | 200 | ✅ API Key | [`List<DashboardEventoListaResponse>`](src/TicketPrime.Api/Models/DashboardEventoListaResponse.cs) |
| `GET` | `/api/admin/eventos/{eventoId}` | 200 | ✅ API Key | [`DashboardEventoDetalhadoResponse`](src/TicketPrime.Api/Models/DashboardEventoDetalhadoResponse.cs) |
| `GET` | `/api/admin/eventos/{eventoId}/lotes` | 200 | ✅ API Key | [`List<DashboardLoteResponse>`](src/TicketPrime.Api/Models/DashboardLoteResponse.cs) |
| `GET` | `/api/admin/reservas` | 200 | ✅ API Key | [`List<AdminReservaResponse>`](src/TicketPrime.Api/Models/AdminReservaResponse.cs) |
| `GET` | `/api/admin/eventos/{eventoId}/resumo` | 200 | ✅ API Key | [`EventoResumoResponse`](src/TicketPrime.Api/Models/EventoResumoResponse.cs) |
| `GET` | `/api/admin/eventos/{eventoId}/checkins` | 200 | ✅ API Key | [`CheckInListResponse`](src/TicketPrime.Api/Models/CheckInListResponse.cs) |
| `GET` | `/api/admin/eventos/{eventoId}/reservas` | 200 | ✅ API Key | [`List<AdminReservaResponse>`](src/TicketPrime.Api/Models/AdminReservaResponse.cs) |

#### Endpoint de Health Check

| Método | Rota | Status Code | Auth | Body Response |
|--------|------|:-----------:|:----:|---------------|
| `GET` | `/health` | 200 / 503 | ❌ | `{ "status": "Healthy"/"Unhealthy", "timestamp": "..." }` |

### 5.2. Novos Models

Foram criados **26 novos arquivos de modelo** em [`src/TicketPrime.Api/Models/`](src/TicketPrime.Api/Models/):

- [`Ingresso.cs`](src/TicketPrime.Api/Models/Ingresso.cs) — Entidade ingresso digital
- [`IngressoResponse.cs`](src/TicketPrime.Api/Models/IngressoResponse.cs) — Response simplificado
- [`IngressoDetalhadoResponse.cs`](src/TicketPrime.Api/Models/IngressoDetalhadoResponse.cs) — Response com joins (evento + usuário)
- [`IngressoPorReservaResponse.cs`](src/TicketPrime.Api/Models/IngressoPorReservaResponse.cs) — Response vinculado à reserva
- [`CheckIn.cs`](src/TicketPrime.Api/Models/CheckIn.cs) — Entidade check-in
- [`CheckInResponse.cs`](src/TicketPrime.Api/Models/CheckInResponse.cs) — Response check-in
- [`CheckInListResponse.cs`](src/TicketPrime.Api/Models/CheckInListResponse.cs) — Lista de check-ins
- [`CheckInStatsResponse.cs`](src/TicketPrime.Api/Models/CheckInStatsResponse.cs) — Estatísticas
- [`TipoIngresso.cs`](src/TicketPrime.Api/Models/TipoIngresso.cs) — Entidade tipo/lote
- [`TipoIngressoResponse.cs`](src/TicketPrime.Api/Models/TipoIngressoResponse.cs) — Response tipo ingresso
- [`CriarTipoIngressoRequest.cs`](src/TicketPrime.Api/Models/CriarTipoIngressoRequest.cs) — Request tipo ingresso
- [`CriarLoteRequest.cs`](src/TicketPrime.Api/Models/CriarLoteRequest.cs) — Request criar lote
- [`LoteResponse.cs`](src/TicketPrime.Api/Models/LoteResponse.cs) — Response lote
- [`LoteListaResponse.cs`](src/TicketPrime.Api/Models/LoteListaResponse.cs) — Response lote com métricas
- [`LoteHistoricoPrecosResponse.cs`](src/TicketPrime.Api/Models/LoteHistoricoPrecosResponse.cs) — Histórico do lote
- [`Carrinho.cs`](src/TicketPrime.Api/Models/Carrinho.cs) — Entidade carrinho
- [`CarrinhoItem.cs`](src/TicketPrime.Api/Models/CarrinhoItem.cs) — Entidade item do carrinho
- [`CarrinhoRequest.cs`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) — Request carrinho
- [`CarrinhoResponse.cs`](src/TicketPrime.Api/Models/CarrinhoResponse.cs) — Response carrinho
- [`CarrinhoConfirmacaoResponse.cs`](src/TicketPrime.Api/Models/CarrinhoConfirmacaoResponse.cs) — Response confirmação
- [`ConfirmarCarrinhoRequest.cs`](src/TicketPrime.Api/Models/ConfirmarCarrinhoRequest.cs) — Request confirmação
- [`HistoricoPreco.cs`](src/TicketPrime.Api/Models/HistoricoPreco.cs) — Entidade histórico
- [`HistoricoPrecoResponse.cs`](src/TicketPrime.Api/Models/HistoricoPrecoResponse.cs) — Response histórico
- [`EventoHistoricoPrecosResponse.cs`](src/TicketPrime.Api/Models/EventoHistoricoPrecosResponse.cs) — Histórico do evento
- [`SimulacaoPrecoRequest.cs`](src/TicketPrime.Api/Models/SimulacaoPrecoRequest.cs) — Request simulação
- [`SimulacaoPrecoResponse.cs`](src/TicketPrime.Api/Models/SimulacaoPrecoResponse.cs) — Response simulação
- [`DashboardEventoListaResponse.cs`](src/TicketPrime.Api/Models/DashboardEventoListaResponse.cs) — Dashboard lista
- [`DashboardEventoDetalhadoResponse.cs`](src/TicketPrime.Api/Models/DashboardEventoDetalhadoResponse.cs) — Dashboard detalhado
- [`AdminReservaResponse.cs`](src/TicketPrime.Api/Models/AdminReservaResponse.cs) — Response admin reservas
- [`EventoResumoResponse.cs`](src/TicketPrime.Api/Models/EventoResumoResponse.cs) — Resumo do evento

### 5.3. Novos Arquivos de Infraestrutura

| Arquivo | Finalidade |
|---------|------------|
| [`ApiKeyAuthenticationHandler.cs`](src/TicketPrime.Api/Authentication/ApiKeyAuthenticationHandler.cs) | Handler de autenticação customizado |
| [`ApiKeyAuthenticationSchemeOptions.cs`](src/TicketPrime.Api/Authentication/ApiKeyAuthenticationSchemeOptions.cs) | Opções do esquema de autenticação |
| [`ExceptionHandlingMiddleware.cs`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs) | Middleware global de exceções |
| [`ValidationException.cs`](src/TicketPrime.Api/Middleware/ValidationException.cs) | Exceção customizada de validação |

### 5.4. Scripts SQL

| Script | Descrição |
|--------|-----------|
| [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql) | Script completo com todas as tabelas incrementais (TiposIngresso, Ingressos, CheckIns, Carrinhos, CarrinhoItens, HistoricoPrecos) + views de dashboard |
| [`db/ticketprime.sql`](db/ticketprime.sql) | Script original com tabelas base (Usuarios, Eventos, Cupons, Reservas) |

---

## 6. Testes Executados

### 6.1. Resumo Quantitativo

| Arquivo de Teste | Quantidade de Testes | Cobertura |
|------------------|:--------------------:|-----------|
| [`EventoValidationTests.cs`](tests/TicketPrime.Tests/EventoValidationTests.cs) | 3 | Validação do model Evento (valores padrão, atribuição, request) |
| [`CupomValidationTests.cs`](tests/TicketPrime.Tests/CupomValidationTests.cs) | 5 | Validação do model Cupom (valores padrão, atribuição, request, comparação) |
| [`UsuarioValidationTests.cs`](tests/TicketPrime.Tests/UsuarioValidationTests.cs) | 5 | Validação do model Usuario (valores padrão, atribuição, request, comparação) |
| [`ReservaServiceTests.cs`](tests/TicketPrime.Tests/ReservaServiceTests.cs) | 15 | Regras de negócio de reserva (CPF, evento, limite, capacidade, cupom, response) |
| [`IncrementoServiceTests.cs`](tests/TicketPrime.Tests/IncrementoServiceTests.cs) | 37 | Regras de negócio dos incrementos (RF01-RF06) |
| **Total** | **65** | |

### 6.2. Detalhamento por Serviço

#### ReservaServiceTests (15 testes)

| Categoria | Testes | Valida |
|-----------|:------:|--------|
| CPF inexistente | 1 | Bloqueio de reserva com CPF não cadastrado |
| Evento inexistente | 1 | Bloqueio de reserva com evento inexistente |
| Limite por CPF/evento | 2 | Bloqueio ao exceder 2 reservas; permissão na 2ª reserva |
| Capacidade do evento | 2 | Bloqueio quando lotado; permissão com vagas |
| Cupom — valor mínimo | 4 | `CupomPodeSerAplicado` com 4 combinações (teoria) |
| Cálculo valor final | 8 | `CalcularValorFinal` com 6 combinações + cupom válido + sem cupom |
| Cupom inexistente | 1 | Rejeição de cupom não cadastrado |
| CPF em evento diferente | 1 | Permissão quando CPF tem limite em outro evento |
| Response com nome do evento | 2 | `ConstruirReservaResponse` com/sem evento |

#### IncrementoServiceTests (37 testes)

| Categoria | Testes | Valida |
|-----------|:------:|--------|
| **RF01 — Ingresso Digital** | 7 | |
| `GerarCodigoUnico` | 3 | Tamanho 8, alfanumérico, unicidade |
| Colisão | 1 | Regeneração automática em caso de colisão |
| `ValidarCodigoUnico` | 1 | 8 casos (theory): vazio, nulo, tamanho errado, caracteres especiais, minúsculas |
| `CriarIngressoDigital` | 3 | Status Confirmada, código único, tipoIngressoId nulo |
| **RF02 — Check-in** | 8 | |
| `RealizarCheckIn` | 5 | Sucesso, ingresso inexistente, já utilizado, cancelado, duplicado, check-in prévio, alteração de status |
| `PodeRealizarCheckIn` | 3 | Confirmado permite, Utilizada bloqueia, check-in existente bloqueia |
| **RF03 — Tipos/Lotes** | 7 | |
| `ValidarTipoIngresso` | 5 | Nome vazio/espaços, preço inválido (0, negativo), capacidade inválida, data início > fim, data início = fim |
| Dados válidos | 1 | Permissão com dados corretos |
| `VerificarDisponibilidadeLote` | 3 | Com vagas, lotado, cancelados não contam |
| **RF04 — Carrinho** | 4 | |
| `ValidarCarrinhoParaConfirmacao` | 4 | Ativo permite, expirado rejeita, Confirmado rejeita, Expirado rejeita |
| `CarrinhoEstaExpirado` | 3 | Não expirado, expirado, Confirmado |
| **RF05 — Simulação de Preço** | 8 | |
| `SimularPreco` | 7 | Sem cupom, cupom vazio, cupom inexistente, cupom válido aplicável, cupom não aplicável, 4 combinações de valor mínimo (theory), taxa = 10%, fórmula correta |
| **RF06 — Dashboard** | 4 | |
| `CalcularDashboardEvento` | 4 | Com reservas/check-ins, sem reservas, capacidade zero, apenas cancelados |
| `CalcularDashboardLista` | 2 | Múltiplos eventos, evento sem ingressos |

### 6.3. Estrutura dos Testes

- **Framework:** xUnit (`[Fact]` e `[Theory]`)
- **Asserts utilizados:** `Assert.Equal`, `Assert.True`, `Assert.False`, `Assert.Contains`, `Assert.NotEqual`, `Assert.NotNull`, `Assert.Null`, `Assert.Matches`, `Assert.DoesNotContain`, `Assert.All`, `Assert.Single`, `Assert.Empty`
- **Padrão:** Testes de unidade puros (sem banco de dados, sem Entity Framework, sem banco em memória, sem TestContainers)
- **Serviços testados:** [`ReservaService`](src/TicketPrime.Api/Services/ReservaService.cs) e [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) — regras de negócio isoladas

### 6.4. Comando para Execução

```bash
dotnet test tests/TicketPrime.Tests/TicketPrime.Tests.csproj
```

---

## Apêndice A — Mapa de Arquivos do Projeto

```
TicketPrime.sln
├── .gitignore                          (Item 0)
├── README.md
├── release_checklist_final.md
├── docs/
│   ├── adr.md                          (ADR-001)
│   ├── contratos_incrementos.md
│   ├── divida-tecnica.md               (TD-001, TD-002, TD-003)
│   ├── estrutura-inicial-ticketprime.md
│   ├── fase1-relatorio-final.md        (este documento)
│   ├── operacao.md
│   ├── plano-fase1-seguranca-estabilidade-v3.md
│   ├── requisitos.md
│   └── spec_incrementos.md
├── db/
│   ├── ticketprime.sql
│   ├── ticketprime_incrementos.sql
│   └── scripts/
│       ├── 001_CreateSchema.sql
│       └── 002_CreateCupons.sql
├── src/TicketPrime.Api/
│   ├── appsettings.json
│   ├── TicketPrime.Api.csproj
│   ├── Program.cs                      (Item 5, 6, 7)
│   ├── Authentication/                 (Item 2)
│   │   ├── ApiKeyAuthenticationHandler.cs
│   │   └── ApiKeyAuthenticationSchemeOptions.cs
│   ├── Middleware/                      (Item 3)
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   └── ValidationException.cs
│   ├── Models/                         (RF01-RF06)
│   │   ├── Ingresso.cs, Carrinho.cs, CarrinhoItem.cs
│   │   ├── CheckIn.cs, HistoricoPreco.cs, TipoIngresso.cs
│   │   ├── *_Request.cs, *_Response.cs
│   │   └── Dashboard*.cs, Admin*.cs
│   └── Services/
│       ├── ReservaService.cs
│       └── IncrementoService.cs
└── tests/TicketPrime.Tests/
    ├── TicketPrime.Tests.csproj
    ├── EventoValidationTests.cs        (3 testes)
    ├── CupomValidationTests.cs         (5 testes)
    ├── UsuarioValidationTests.cs       (5 testes)
    ├── ReservaServiceTests.cs          (15 testes)
    └── IncrementoServiceTests.cs       (37 testes)
```

---

*Relatório gerado em 2026-06-01 com base no estado real do repositório [`ticketprime-backend`](/home/pedro/Downloads/ticketprime-backend).*

# Spec: Incrementos do TicketPrime

> **Documento:** Especificação de novos recursos (Spec-Driven Development)
> **Versão:** 1.0.0
> **Baseado em:** [`docs/requisitos.md`](docs/requisitos.md) (requisitos obrigatórios do professor)
> **Stack:** .NET 8, Minimal API, Dapper, SQL Server, xUnit

---

## Sumário

1. [Visão Geral](#1-visão-geral)
2. [RF01 — Ingresso Digital com Código Único](#2-rf01--ingresso-digital-com-código-único)
3. [RF02 — Check-in de Ingresso](#3-rf02--check-in-de-ingresso)
4. [RF03 — Tipos/Lotes de Ingresso](#4-rf03--tiposlotes-de-ingresso)
5. [RF04 — Carrinho/Reserva Temporária](#5-rf04--carrinhoreserva-temporária)
6. [RF05 — Transparência de Preço](#6-rf05--transparência-de-preço)
7. [RF06 — Dashboard/Admin de Eventos](#7-rf06--dashboardadmin-de-eventos)
8. [Glossário](#8-glossário)
9. [Matriz de Riscos Consolidada](#9-matriz-de-riscos-consolidada)
10. [Plano de Implementação Sugerido](#10-plano-de-implementação-sugerido)

---

## 1. Visão Geral

### 1.1. Objetivo

Este documento especifica **6 novos recursos** para o TicketPrime, seguindo a abordagem **Spec-Driven Development** (SDD). Cada recurso é descrito com objetivo, regras de negócio, endpoints previstos, tabelas necessárias, critérios de aceite e riscos.

### 1.2. Restrições Obrigatórias (Não Alterar)

| # | Restrição | Justificativa |
|---|-----------|---------------|
| 1 | **Não alterar** [`POST /api/eventos`](src/TicketPrime.Api/Program.cs:163) | Endpoint obrigatório do professor (AV1) |
| 2 | **Não alterar** [`GET /api/eventos`](src/TicketPrime.Api/Program.cs:290) | Endpoint obrigatório do professor (AV1) |
| 3 | **Não alterar** [`POST /api/cupons`](src/TicketPrime.Api/Program.cs:299) | Endpoint obrigatório do professor (AV1) |
| 4 | **Não alterar** [`POST /api/usuarios`](src/TicketPrime.Api/Program.cs:114) | Endpoint obrigatório do professor (AV1) |
| 5 | **Não alterar** [`GET /api/reservas/{cpf}`](src/TicketPrime.Api/Program.cs:334) | Endpoint obrigatório do professor (AV2) |
| 6 | **Não alterar** [`POST /api/reservas`](src/TicketPrime.Api/Program.cs:201) | Endpoint obrigatório do professor (AV2) |
| 7 | **Não alterar** nomes das tabelas `Usuarios`, `Eventos`, `Cupons`, `Reservas` | Schema obrigatório do professor |
| 8 | **Manter** Dapper como único ORM | Restrição técnica obrigatória |
| 9 | **Manter** SQL parametrizado (`@param`) | Sem SQL injection |
| 10 | **Manter** Minimal API | Arquitetura definida |
| 11 | **Manter** simplicidade acadêmica | Sem abstrações desnecessárias |

### 1.3. Relação com os Requisitos Existentes

Os novos recursos **complementam** os requisitos obrigatórios sem quebrá-los:

| Recurso Novo | Impacto em Tabelas Existentes | Impacto em Endpoints Existentes |
|---|---|---|
| RF01 — Ingresso Digital | Adiciona coluna `CodigoUnico` em `Reservas` | Nenhum (novos endpoints) |
| RF02 — Check-in | Cria tabela `CheckIns` | Nenhum (novos endpoints) |
| RF03 — Tipos/Lotes | Cria tabela `LotesIngresso` + FK em `Reservas` | Nenhum (novos endpoints) |
| RF04 — Carrinho | Cria tabela `CarrinhoReservas` | Nenhum (novos endpoints) |
| RF05 — Transparência | Cria tabela `HistoricoPrecos` | Nenhum (novos endpoints) |
| RF06 — Dashboard | Apenas consultas (`SELECT`) | Nenhum (novos endpoints) |

---

## 2. RF01 — Ingresso Digital com Código Único

### 2.1. Objetivo

Gerar um **código único alfanumérico** para cada ingresso (reserva) no momento da confirmação, permitindo que o usuário visualize e apresente seu ingresso digital.

### 2.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN01.1 | Toda reserva confirmada deve receber um `CodigoUnico` gerado automaticamente. |
| RN01.2 | O código único deve ter **exatamente 8 caracteres alfanuméricos** (A-Z, 0-9), gerado aleatoriamente. |
| RN01.3 | O código deve ser **único em toda a base** (coluna com UNIQUE CONSTRAINT). |
| RN01.4 | Se houver colisão (raro), o sistema deve regenerar o código automaticamente. |
| RN01.5 | O código deve ser exibido ao usuário na resposta da reserva e no endpoint de consulta. |
| RN01.6 | A reserva existente migra para `status = 'Confirmada'` no mesmo ato. |

### 2.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/reservas/{id}/ingresso` | Obter os dados do ingresso digital de uma reserva |
| `GET` | `/api/ingressos/{codigo}` | Consultar ingresso pelo código único (validação) |

### 2.4. Tabelas Necessárias

**Alteração na tabela `Reservas`** (adicionar colunas — nomes existentes são preservados):

```sql
-- Adicionar colunas à tabela Reservas (sem remover colunas existentes)
ALTER TABLE Reservas
    ADD CodigoUnico VARCHAR(8) NULL,
        StatusReserva VARCHAR(20) NOT NULL DEFAULT 'Confirmada';

-- UNIQUE constraint após popular dados existentes
CREATE UNIQUE INDEX IX_Reservas_CodigoUnico ON Reservas(CodigoUnico)
    WHERE CodigoUnico IS NOT NULL;
```

**Nova tabela (opcional — log de acesso ao ingresso):**

```sql
CREATE TABLE LogAcessoIngressos (
    Id          INT IDENTITY(1,1)   NOT NULL,
    ReservaId   INT                 NOT NULL,
    CodigoUnico VARCHAR(8)          NOT NULL,
    DataAcesso  DATETIME            NOT NULL DEFAULT GETDATE(),
    IpAcesso    VARCHAR(45)         NULL,
    CONSTRAINT PK_LogAcessoIngressos PRIMARY KEY (Id),
    CONSTRAINT FK_LogAcessoIngressos_Reservas FOREIGN KEY (ReservaId)
        REFERENCES Reservas(Id)
);
```

### 2.5. Critérios de Aceite

**Cenário 1: Geração de código único na reserva**
```gherkin
Dado que uma reserva foi confirmada com sucesso
Quando o sistema processar a confirmação
Então a reserva deve possuir um código único de 8 caracteres alfanuméricos
E o código não deve existir em nenhuma outra reserva
```

**Cenário 2: Visualização do ingresso digital**
```gherkin
Dado que existe uma reserva confirmada com código único "ABC123XY"
Quando o usuário solicitar os dados do ingresso pelo código
Então o sistema deve retornar os dados do evento, data, local e código
```

**Cenário 3: Código inexistente**
```gherkin
Dado que o código "ZZZZ9999" não existe na base
Quando alguém consultar este código
Então o sistema deve retornar erro 404 com mensagem "Ingresso não encontrado"
```

### 2.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Colisão na geração do código | Muito Baixa | Médio | Loop de regeneração + UNIQUE INDEX |
| Performance em consultas por código | Baixa | Baixo | Índice único na coluna `CodigoUnico` |
| Alteração acidental do endpoint POST /api/reservas | Média | Alto | A geração do código deve ser feita **dentro** do endpoint existente sem quebrar a assinatura |

---

## 3. RF02 — Check-in de Ingresso

### 3.1. Objetivo

Permitir que o organizador do evento realize o **check-in** do portador do ingresso no dia do evento, validando o código único e registrando a entrada.

### 3.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN02.1 | O check-in só pode ser realizado se o ingresso existir e estiver com `StatusReserva = 'Confirmada'`. |
| RN02.2 | Cada ingresso só pode ter **um único check-in** (impedir reuso). |
| RN02.3 | O check-in deve registrar a data/hora exata da validação. |
| RN02.4 | O check-in pode ser feito por qualquer pessoa que possua o código único (simulação de porteiro/organizador). |
| RN02.5 | Após o check-in, o status da reserva passa para `'Utilizada'`. |

### 3.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/ingressos/{codigo}/checkin` | Realizar check-in de um ingresso pelo código único |
| `GET` | `/api/eventos/{eventoId}/checkins` | Listar todos os check-ins de um evento (admin) |
| `GET` | `/api/eventos/{eventoId}/checkins/stats` | Estatísticas de check-in (presentes vs. ausentes) |

### 3.4. Tabelas Necessárias

```sql
CREATE TABLE CheckIns (
    Id          INT IDENTITY(1,1)   NOT NULL,
    ReservaId   INT                 NOT NULL,
    CodigoUnico VARCHAR(8)          NOT NULL,
    DataCheckIn DATETIME            NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_CheckIns PRIMARY KEY (Id),
    CONSTRAINT UQ_CheckIns_ReservaId UNIQUE (ReservaId),  -- um check-in por reserva
    CONSTRAINT FK_CheckIns_Reservas FOREIGN KEY (ReservaId)
        REFERENCES Reservas(Id)
);
```

### 3.5. Critérios de Aceite

**Cenário 1: Check-in bem-sucedido**
```gherkin
Dado que existe um ingresso confirmado com código "ABC123XY"
Quando o organizador realizar o check-in com este código
Então o sistema deve registrar o check-in com data/hora atual
E o status da reserva deve ser alterado para "Utilizada"
```

**Cenário 2: Check-in duplicado (reuso)**
```gherkin
Dado que o ingresso "ABC123XY" já realizou check-in
Quando um novo check-in for tentado com o mesmo código
Então o sistema deve rejeitar com erro "Ingresso já utilizado"
```

**Cenário 3: Check-in de ingresso inexistente**
```gherkin
Dado que o código "ZZZZ9999" não existe
Quando o check-in for tentado
Então o sistema deve rejeitar com erro 404 "Ingresso não encontrado"
```

**Cenário 4: Check-in de ingresso com status diferente de Confirmada**
```gherkin
Dado que o ingresso "ABC123XY" está com status "Cancelada"
Quando o check-in for tentado
Então o sistema deve rejeitar com erro "Ingresso não está confirmado para check-in"
```

### 3.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Check-in duplicado por condição de corrida | Baixa | Alto | UNIQUE constraint em `ReservaId` + transação |
| Porteiro com código inválido digitar errado | Alta | Baixo | Mensagem de erro clara; interface amigável |

---

## 4. RF03 — Tipos/Lotes de Ingresso

### 4.1. Objetivo

Permitir que um evento tenha **múltiplos lotes/tipos de ingresso** (ex.: Pista, VIP, Meia-Entrada) com preços e capacidades independentes, mantendo a capacidade total do evento como soma dos lotes.

### 4.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN03.1 | Um evento pode ter **um ou mais lotes** de ingresso. |
| RN03.2 | Cada lote possui: nome, preço, capacidade máxima, data de início e fim de venda. |
| RN03.3 | A `CapacidadeTotal` do evento é a **soma das capacidades** de todos os seus lotes. |
| RN03.4 | Uma reserva deve estar associada a **um lote específico**. |
| RN03.5 | O controle de capacidade por CPF (limite 2) continua valendo **por evento**, não por lote. |
| RN03.6 | O `PrecoPadrao` do evento é mantido como referência (pode ser a média ou o menor preço). |
| RN03.7 | Lotes podem ser criados **após** o evento já existir. |
| RN03.8 | Se um evento não tiver lotes cadastrados, o comportamento antigo (reserva direta sem lote) deve ser preservado para compatibilidade — **ou** assumir lote único implícito com `PrecoPadrao`. |

### 4.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/eventos/{eventoId}/lotes` | Criar um novo lote para um evento |
| `GET` | `/api/eventos/{eventoId}/lotes` | Listar lotes de um evento |
| `GET` | `/api/lotes/{loteId}` | Obter dados de um lote específico |
| `PUT` | `/api/lotes/{loteId}` | Atualizar preço/capacidade de um lote |
| `DELETE` | `/api/lotes/{loteId}` | Remover lote (se não houver reservas vinculadas) |

### 4.4. Tabelas Necessárias

```sql
CREATE TABLE LotesIngresso (
    Id              INT IDENTITY(1,1)   NOT NULL,
    EventoId        INT                 NOT NULL,
    Nome            VARCHAR(100)        NOT NULL,
    Preco           DECIMAL(10,2)       NOT NULL,
    Capacidade      INT                 NOT NULL,
    DataInicioVenda DATETIME            NOT NULL,
    DataFimVenda    DATETIME            NOT NULL,
    CONSTRAINT PK_LotesIngresso PRIMARY KEY (Id),
    CONSTRAINT FK_LotesIngresso_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id)
);
```

**Alteração na tabela `Reservas`** (adicionar FK opcional para `LotesIngresso`):

```sql
ALTER TABLE Reservas
    ADD LoteId INT NULL;

ALTER TABLE Reservas
    ADD CONSTRAINT FK_Reservas_LotesIngresso FOREIGN KEY (LoteId)
        REFERENCES LotesIngresso(Id);
```

### 4.5. Critérios de Aceite

**Cenário 1: Criar lote com dados válidos**
```gherkin
Dado que existe um evento "Show da Banda X" com Id 1
Quando o organizador criar um lote "VIP" com preço R$ 300,00 e capacidade 100
Então o lote deve ser criado com sucesso
E a capacidade total do evento deve permanecer inalterada (lote não altera CapacidadeTotal)
```

**Cenário 2: Reserva com lote específico**
```gherkin
Dado que o evento "Show da Banda X" possui lote "VIP" (capacidade 100, 0 reservas)
E lote "Pista" (capacidade 400, 0 reservas)
Quando o usuário reservar 1 ingresso para o lote "VIP"
Então a reserva deve ser associada ao lote "VIP"
E a contagem de reservas do lote "VIP" deve ser 1
```

**Cenário 3: Lote com capacidade esgotada**
```gherkin
Dado que o lote "VIP" tem capacidade 1 e já possui 1 reserva
Quando um usuário tentar reservar para o lote "VIP"
Então o sistema deve rejeitar informando "Lote VIP esgotado"
E o usuário pode optar por outro lote disponível
```

### 4.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Complexidade na migração de reservas existentes | Média | Médio | Coluna `LoteId` nullable; NULL = lote não definido |
| Quebra do POST /api/reservas existente | Média | Alto | Manter compatibilidade: se `LoteId` não for informado, usar lote único implícito ou rejeitar |
| Confusão entre CapacidadeTotal do evento e capacidade dos lotes | Média | Médio | Documentar claramente que CapacidadeTotal é independente; validar soma dos lotes ≤ CapacidadeTotal |

---

## 5. RF04 — Carrinho/Reserva Temporária

### 5.1. Objetivo

Permitir que o usuário adicione ingressos a um **carrinho temporário** com expiração (ex.: 15 minutos), garantindo a reserva provisória da capacidade enquanto finaliza a compra, prevenindo a perda de vagas durante o processo.

### 5.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN04.1 | O carrinho tem **validade de 15 minutos** a partir da criação. |
| RN04.2 | Enquanto o carrinho estiver ativo, os ingressos ficam **temporariamente reservados** (não disponíveis para outros). |
| RN04.3 | Cada CPF pode ter **apenas um carrinho ativo** por vez. |
| RN04.4 | Após expirar o prazo, os itens do carrinho são **liberados** automaticamente. |
| RN04.5 | A confirmação do carrinho gera **uma reserva definitiva** (tabela `Reservas`). |
| RN04.6 | As regras de limite por CPF (RN01 do requisito original) e capacidade continuam valendo no momento da confirmação. |
| RN04.7 | Itens no carrinho **não contam** para o limite de 2 reservas por CPF por evento (são temporários). |

### 5.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/carrinho` | Adicionar item(s) ao carrinho (cria ou atualiza carrinho ativo do CPF) |
| `GET` | `/api/carrinho/{cpf}` | Visualizar carrinho ativo de um CPF |
| `DELETE` | `/api/carrinho/{cpf}` | Limpar/excluir carrinho ativo (libera itens) |
| `POST` | `/api/carrinho/{cpf}/confirmar` | Confirmar carrinho → gerar reservas definitivas |

### 5.4. Tabelas Necessárias

```sql
CREATE TABLE CarrinhoReservas (
    Id              INT IDENTITY(1,1)   NOT NULL,
    UsuarioCpf      VARCHAR(11)         NOT NULL,
    EventoId        INT                 NOT NULL,
    LoteId          INT                 NULL,
    Quantidade      INT                 NOT NULL DEFAULT 1,
    DataCriacao     DATETIME            NOT NULL DEFAULT GETDATE(),
    DataExpiracao   DATETIME            NOT NULL,  -- GETDATE() + 15 min
    Ativo           BIT                 NOT NULL DEFAULT 1,
    CONSTRAINT PK_CarrinhoReservas PRIMARY KEY (Id),
    CONSTRAINT FK_CarrinhoReservas_Usuarios FOREIGN KEY (UsuarioCpf)
        REFERENCES Usuarios(Cpf),
    CONSTRAINT FK_CarrinhoReservas_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id)
);
```

### 5.5. Critérios de Aceite

**Cenário 1: Adicionar item ao carrinho**
```gherkin
Dado que o usuário "12345678901" está autenticado
E que o evento "Show da Banda X" possui capacidade disponível
Quando o usuário adicionar 2 ingressos ao carrinho
Então o carrinho deve conter 2 ingressos para o evento
E a capacidade disponível deve ser reduzida temporariamente em 2
```

**Cenário 2: Carrinho expirado libera capacidade**
```gherkin
Dado que o usuário possui um carrinho com 2 ingressos que expirou há 1 minuto
Quando o sistema verificar carrinhos expirados
Então os itens devem ser marcados como inativos
E a capacidade deve ser liberada para outros usuários
```

**Cenário 3: Confirmação do carrinho gera reserva**
```gherkin
Dado que o usuário possui carrinho ativo com 2 ingressos para o evento "Show da Banda X"
Quando o usuário confirmar o carrinho
Então o sistema deve criar 2 reservas definitivas na tabela Reservas
E o carrinho deve ser marcado como inativo
```

**Cenário 4: Limite de CPF impede confirmação**
```gherkin
Dado que o usuário já possui 2 reservas confirmadas para o evento "Show da Banda X"
E possui 1 ingresso no carrinho para o mesmo evento
Quando o usuário tentar confirmar o carrinho
Então o sistema deve rejeitar a confirmação informando limite de CPF excedido
```

### 5.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Condição de corrida na expiração | Média | Alto | Job periódico ou verificação no ato da consulta/confirmação |
| Usuário acumular múltiplos carrinhos | Baixa | Baixo | Regra: 1 carrinho ativo por CPF |
| Perda de itens por expiração durante pagamento | Média | Médio | Notificar usuário antes de expirar (futuro); renovar prazo se houver atividade |

---

## 6. RF05 — Transparência de Preço

### 6.1. Objetivo

Registrar o **histórico de preços** dos ingressos/lotes ao longo do tempo, permitindo que o usuário visualize a evolução dos preços e eventuais reajustes, promovendo transparência.

### 6.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN05.1 | Toda vez que o preço de um lote ou o `PrecoPadrao` de um evento for alterado, o valor anterior deve ser registrado no histórico. |
| RN05.2 | O histórico deve conter: preço anterior, preço novo, data da alteração e motivo opcional. |
| RN05.3 | O histórico é **apenas de leitura** via API (sem DELETE ou UPDATE). |
| RN05.4 | A criação inicial do evento/lote também gera um registro no histórico (preço anterior = NULL). |

### 6.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/eventos/{eventoId}/historico-precos` | Histórico de preços de um evento (inclui lotes) |
| `GET` | `/api/lotes/{loteId}/historico-precos` | Histórico de preços de um lote específico |

### 6.4. Tabelas Necessárias

```sql
CREATE TABLE HistoricoPrecos (
    Id              INT IDENTITY(1,1)   NOT NULL,
    EventoId        INT                 NULL,   -- NULL se for alteração de lote
    LoteId          INT                 NULL,   -- NULL se for alteração de evento
    PrecoAnterior   DECIMAL(10,2)       NULL,   -- NULL na criação
    PrecoNovo       DECIMAL(10,2)       NOT NULL,
    DataAlteracao   DATETIME            NOT NULL DEFAULT GETDATE(),
    Motivo          VARCHAR(200)        NULL,
    CONSTRAINT PK_HistoricoPrecos PRIMARY KEY (Id),
    CONSTRAINT FK_HistoricoPrecos_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id),
    CONSTRAINT FK_HistoricoPrecos_LotesIngresso FOREIGN KEY (LoteId)
        REFERENCES LotesIngresso(Id)
);
```

### 6.5. Critérios de Aceite

**Cenário 1: Registro de alteração de preço**
```gherkin
Dado que o evento "Show da Banda X" possui PrecoPadrao R$ 150,00
Quando o organizador alterar o preço para R$ 180,00
Então o histórico deve conter um registro com preço anterior R$ 150,00 e preço novo R$ 180,00
```

**Cenário 2: Consulta de histórico**
```gherkin
Dado que o evento "Show da Banda X" teve 3 alterações de preço
Quando o usuário consultar o histórico de preços do evento
Então o sistema deve retornar uma lista ordenada da mais recente para a mais antiga
E cada registro deve conter preço anterior, preço novo e data da alteração
```

**Cenário 3: Histórico vazio**
```gherkin
Dado que o evento "Show da Banda X" nunca teve seu preço alterado
E foi criado com preço R$ 150,00
Quando o usuário consultar o histórico
Então o sistema deve retornar 1 registro referente à criação do evento
```

### 6.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Acúmulo excessivo de registros | Média | Baixo | Histórico é append-only; baixo volume em cenário acadêmico |
| Esquecer de registrar no momento da alteração | Média | Médio | Disparar registro no mesmo método de update (transação) |

---

## 7. RF06 — Dashboard/Admin de Eventos

### 7.1. Objetivo

Fornecer endpoints de **dashboard e administração** para que organizadores acompanhem métricas dos eventos: total de ingressos vendidos, receita, check-ins realizados, ocupação por lote, etc.

### 7.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN06.1 | Os endpoints de dashboard são **somente leitura** (`SELECT`). |
| RN06.2 | Os dados devem ser calculados em tempo real (sem tabelas de agregação prévia). |
| RN06.3 | Os endpoints não exigem autenticação (acadêmico — simplicidade). |
| RN06.4 | Métricas devem incluir: total de ingressos vendidos, receita total (soma dos `ValorFinalPago`), % de ocupação, check-ins realizados, ingressos por lote. |

### 7.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/admin/eventos` | Listar todos os eventos com métricas agregadas |
| `GET` | `/api/admin/eventos/{eventoId}` | Dashboard detalhado de um evento |
| `GET` | `/api/admin/eventos/{eventoId}/lotes` | Métricas por lote de um evento |
| `GET` | `/api/admin/reservas` | Listar todas as reservas do sistema (admin) |
| `GET` | `/api/admin/reservas/hoje` | Reservas realizadas hoje |

### 7.4. Tabelas Necessárias

**Nenhuma tabela nova.** Apenas consultas (`SELECT`) nas tabelas existentes:

- [`Eventos`](db/scripts/001_CreateSchema.sql:30) — dados do evento
- [`Reservas`](db/scripts/001_CreateSchema.sql:92) — contagem de vendas e receita
- [`LotesIngresso`](#44-tabelas-necessárias) — métricas por lote (RF03)
- [`CheckIns`](#34-tabelas-necessárias) — check-ins realizados (RF02)

### 7.5. Critérios de Aceite

**Cenário 1: Dashboard de evento com métricas**
```gherkin
Dado que o evento "Show da Banda X" possui capacidade 500
E tem 200 reservas confirmadas com receita total de R$ 30.000,00
E 150 check-ins realizados
Quando o administrador acessar o dashboard do evento
Então o sistema deve retornar:
  - Total de ingressos vendidos: 200
  - Receita total: R$ 30.000,00
  - Ocupação: 40%
  - Check-ins realizados: 150
  - Check-ins pendentes: 50
```

**Cenário 2: Lista de eventos com métricas**
```gherkin
Dado que existem 5 eventos cadastrados
Quando o administrador acessar /api/admin/eventos
Então o sistema deve retornar uma lista com todos os eventos
E cada evento deve conter suas métricas agregadas
```

**Cenário 3: Evento sem reservas**
```gherkin
Dado que o evento "Palestra X" acabou de ser criado e não possui reservas
Quando o administrador acessar o dashboard do evento
Então o sistema deve retornar métricas zeradas (0 ingressos, R$ 0,00 receita, 0% ocupação)
```

### 7.6. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Performance com muitos eventos e reservas | Média | Médio | Índices nas colunas `EventoId` e `UsuarioCpf` da tabela Reservas |
| Consultas complexas com múltiplos JOINs | Baixa | Baixo | Manter SQL simples; cenário acadêmico com volume reduzido |

---

## 8. Glossário

| Termo | Definição |
|-------|-----------|
| **Ingresso Digital** | Representação eletrônica de uma reserva confirmada, identificada por código único de 8 caracteres |
| **Código Único** | String alfanumérica de 8 caracteres (A-Z, 0-9) gerada aleatoriamente para cada reserva |
| **Check-in** | Ação de validar o ingresso na entrada do evento, registrando data/hora e alterando status |
| **Lote de Ingresso** | Categoria/tipo de ingresso com preço e capacidade próprios dentro de um evento |
| **Carrinho** | Conjunto temporário de itens (ingressos) com validade de 15 minutos, que reserva capacidade provisoriamente |
| **Transparência de Preço** | Histórico público de alterações de preços de eventos e lotes |
| **Dashboard** | Conjunto de métricas e indicadores para administração de eventos |
| **StatusReserva** | Estado atual da reserva: `Confirmada`, `Utilizada`, `Cancelada` |

---

## 9. Matriz de Riscos Consolidada

| Risco | Probabilidade | Impacto | Ação de Mitigação | Gatilho |
|-------|---------------|---------|-------------------|---------|
| Colisão de código único de ingresso | Baixa | Alto | Loop de regeneração + UNIQUE INDEX | Geração simultânea de múltiplos ingressos |
| Check-in duplicado por concorrência | Baixa | Alto | UNIQUE constraint + transação SQL | Duas requisições simultâneas para o mesmo código |
| Carrinho expirado com item sendo confirmado | Média | Alto | Validar expiração no momento da confirmação | Usuário confirma carrinho após 15 min |
| Performance de consultas do dashboard | Média | Médio | Índices em `EventoId` e `UsuarioCpf` na tabela `Reservas` | Muitos eventos com muitas reservas |
| Quebra de endpoint obrigatório existente | Baixa | Crítico | Nunca alterar arquivos de endpoints existentes; criar novos endpoints em arquivo separado | Modificação acidental no [`Program.cs`](src/TicketPrime.Api/Program.cs) |
| Alteração de nome de tabela obrigatória | Baixa | Crítico | Usar apenas `ALTER TABLE ADD`, nunca `RENAME` ou `DROP` | Migração de schema mal planejada |

---

## 10. Plano de Implementação Sugerido

### Fase 1 — Fundação (RF01 + RF03)

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 1.1 | Criar script SQL: `db/scripts/003_LotesIngresso.sql` (tabela `LotesIngresso` + ALTER `Reservas`) | Nenhuma |
| 1.2 | Criar model [`LoteIngresso.cs`](src/TicketPrime.Api/Models/) | Passo 1.1 |
| 1.3 | Implementar endpoints de CRUD de lotes (RF03) | Passo 1.2 |
| 1.4 | Criar script SQL: `db/scripts/004_CodigoUnico.sql` (ALTER `Reservas` + índice) | RF01 |
| 1.5 | Modificar [`POST /api/reservas`](src/TicketPrime.Api/Program.cs:201) para gerar código único (sem quebrar assinatura) | Passo 1.4 |
| 1.6 | Implementar endpoints de ingresso digital (RF01) | Passo 1.5 |

### Fase 2 — Operação (RF02 + RF04)

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 2.1 | Criar script SQL: `db/scripts/005_CheckIns.sql` | Fase 1 |
| 2.2 | Implementar endpoints de check-in (RF02) | Passo 2.1 |
| 2.3 | Criar script SQL: `db/scripts/006_CarrinhoReservas.sql` | Fase 1 |
| 2.4 | Implementar endpoints de carrinho (RF04) | Passo 2.3 |

### Fase 3 — Transparência e Gestão (RF05 + RF06)

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 3.1 | Criar script SQL: `db/scripts/007_HistoricoPrecos.sql` | Fase 1 (RF03) |
| 3.2 | Implementar endpoints de histórico de preços (RF05) | Passo 3.1 |
| 3.3 | Implementar endpoints de dashboard/admin (RF06) | Fase 1 e 2 |

### Fase 4 — Testes

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 4.1 | Criar testes unitários para geração de código único | Fase 1 |
| 4.2 | Criar testes unitários para regras de check-in | Fase 2 |
| 4.3 | Criar testes unitários para lote e carrinho | Fase 1 e 2 |
| 4.4 | Criar testes de integração para novos endpoints | Todas as fases |

---

## Histórico de Revisões

| Versão | Data | Descrição |
|--------|------|-----------|
| 1.0.0 | 2026-05-27 | Versão inicial da especificação dos novos recursos |

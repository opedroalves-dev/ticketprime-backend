# Spec: Incrementos do TicketPrime

> **Documento:** Especificação de novos recursos (Spec-Driven Development)
> **Versão:** 1.5.0
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
| RF01 — Ingresso Digital | Cria tabela `Ingressos` (FK → `Reservas`, FK → `TiposIngresso`) | Nenhum (novos endpoints) |
| RF02 — Check-in | Cria tabela `CheckIns` (FK → `Ingressos`) | Nenhum (novos endpoints) |
| RF03 — Tipos/Lotes | Cria tabela `TiposIngresso` (FK → `Eventos`) | Nenhum (novos endpoints) |
| RF04 — Carrinho | Cria tabelas `Carrinhos` + `CarrinhoItens` (FK → `Usuarios`, `Eventos`, `TiposIngresso`) | Nenhum (novos endpoints) |
| RF05 — Transparência | Cria tabela `HistoricoPrecos` (FK → `Eventos`, `TiposIngresso`) | Nenhum (novos endpoints) |
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
| RN01.6 | O ingresso gerado assume `Status = 'Confirmada'` no mesmo ato. |
| | |
| | **Nota — Fluxos de geração:** O ingresso digital pode ser gerado por dois fluxos: |
| | **(a)** Automático: via confirmação do carrinho (RF04), que gera reserva + ingresso em uma única operação. |
| | **(b)** Manual: via `POST /api/reservas/{id}/ingresso` para reservas existentes que ainda não possuem ingresso (migração/retrocompatibilidade). |

### 2.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/reservas/{id}/ingresso` | Gerar o ingresso digital com código único para uma reserva existente |
| `GET` | `/api/reservas/{id}/ingresso` | Consultar o ingresso digital associado a uma reserva |
| `GET` | `/api/ingressos/{codigo}` | Consultar ingresso pelo código único (validação na entrada) |

### 2.4. Tabelas Necessárias

**Nova tabela `Ingressos`** — representa o ingresso digital vinculado a uma reserva existente:

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | INT IDENTITY | PK |
| `ReservaId` | INT | FK → `Reservas(Id)`, NOT NULL |
| `TipoIngressoId` | INT | FK → `TiposIngresso(Id)`, NULL (migração) |
| `CodigoUnico` | VARCHAR(8) | UNIQUE, CHECK LEN = 8 |
| `Status` | VARCHAR(20) | CHECK ('Confirmada','Utilizada','Cancelada'), DEFAULT 'Confirmada' |
| `ValorBruto` | DECIMAL(10,2) | NOT NULL |
| `ValorDesconto` | DECIMAL(10,2) | DEFAULT 0.00 |
| `TaxaServico` | DECIMAL(10,2) | DEFAULT 0.00 |
| `ValorFinal` | DECIMAL(10,2) | NOT NULL |
| `DataCriacao` | DATETIME | DEFAULT GETDATE() |

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
| Novo endpoint POST /api/reservas/{id}/ingresso pode quebrar se a reserva não existir | Baixa | Médio | Validar existência da reserva antes de gerar o ingresso |

---

## 3. RF02 — Check-in de Ingresso

### 3.1. Objetivo

Permitir que o organizador do evento realize o **check-in** do portador do ingresso no dia do evento, validando o código único e registrando a entrada.

### 3.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN02.1 | O check-in só pode ser realizado se o ingresso existir e estiver com `Ingressos.Status = 'Confirmada'`. |
| RN02.2 | Cada ingresso só pode ter **um único check-in** (impedir reuso). |
| RN02.3 | O check-in deve registrar a data/hora exata da validação. |
| RN02.4 | O check-in pode ser feito por qualquer pessoa que possua o código único (simulação de porteiro/organizador). |
| RN02.5 | Após o check-in, o status do ingresso passa para `'Utilizada'`. |

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
    IngressoId  INT                 NOT NULL,
    DataCheckIn DATETIME            NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_CheckIns PRIMARY KEY (Id),
    CONSTRAINT UQ_CheckIns_IngressoId UNIQUE (IngressoId),  -- um check-in por ingresso
    CONSTRAINT FK_CheckIns_Ingressos FOREIGN KEY (IngressoId)
        REFERENCES Ingressos(Id)
);
```

### 3.5. Critérios de Aceite

**Cenário 1: Check-in bem-sucedido**
```gherkin
Dado que existe um ingresso confirmado com código "ABC123XY"
Quando o organizador realizar o check-in com este código
Então o sistema deve registrar o check-in com data/hora atual
E o status do ingresso deve ser alterado para "Utilizada"
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
| RN03.2 | Cada lote possui: nome, preço, capacidade máxima, taxa de serviço, data de início e fim de venda. |
| RN03.3 | Uma reserva deve estar associada a **um lote específico**. |
| RN03.4 | O controle de capacidade por CPF (limite 2) continua valendo **por evento**, não por lote. |
| RN03.5 | O `PrecoPadrao` do evento é mantido como referência (pode ser a média ou o menor preço). |
| RN03.6 | Lotes podem ser criados **após** o evento já existir. |
| RN03.7 | Se um evento não tiver lotes cadastrados, o comportamento antigo (reserva direta sem lote) deve ser preservado para compatibilidade — **ou** assumir lote único implícito com `PrecoPadrao`. |

### 4.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/eventos/{eventoId}/lotes` | Criar um novo lote para um evento |
| `GET` | `/api/eventos/{eventoId}/lotes` | Listar lotes de um evento |
| `GET` | `/api/lotes/{loteId}` | Obter dados de um lote específico |
| `PUT` | `/api/lotes/{loteId}` | Atualizar preço/capacidade de um lote |
| `DELETE` | `/api/lotes/{loteId}` | Remover lote (se não houver ingressos vinculados) |

### 4.4. Tabelas Necessárias

```sql
CREATE TABLE TiposIngresso (
    Id              INT IDENTITY(1,1)   NOT NULL,
    EventoId        INT                 NOT NULL,
    Nome            VARCHAR(100)        NOT NULL,
    Preco           DECIMAL(10,2)       NOT NULL,
    Capacidade      INT                 NOT NULL,
    TaxaServico     DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
    DataInicioVenda DATETIME            NOT NULL,
    DataFimVenda    DATETIME            NOT NULL,
    CONSTRAINT PK_TiposIngresso PRIMARY KEY (Id),
    CONSTRAINT FK_TiposIngresso_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id)
);
```

**Sem alterações em tabelas existentes.** A FK entre ingresso e lote é feita via coluna `TipoIngressoId` na tabela `Ingressos` (RF01).

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
| Complexidade na migração de reservas existentes | Média | Médio | Coluna `TipoIngressoId` nullable na tabela `Ingressos`; NULL = tipo de ingresso não definido (retrocompatibilidade) |
| Quebra do POST /api/reservas existente | Média | Alto | Manter compatibilidade: se `TipoIngressoId` não for informado no ingresso, usar lote único implícito ou rejeitar |
| Confusão entre CapacidadeTotal do evento e capacidade dos lotes | Média | Médio | Documentar claramente que CapacidadeTotal é independente dos lotes (não há soma automática) |

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

**Tabela `Carrinhos`** — sessão de compra temporária por CPF:

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | INT IDENTITY | PK |
| `UsuarioCpf` | VARCHAR(11) | FK → `Usuarios(Cpf)`, NOT NULL |
| `Status` | VARCHAR(20) | CHECK ('Ativo','Expirado','Confirmado'), DEFAULT 'Ativo' |
| `DataCriacao` | DATETIME | DEFAULT GETDATE() |
| `DataExpiracao` | DATETIME | NOT NULL (GETDATE() + 15 min) |

**Tabela `CarrinhoItens`** — itens do carrinho:

| Coluna | Tipo | Restrições |
|--------|------|------------|
| `Id` | INT IDENTITY | PK |
| `CarrinhoId` | INT | FK → `Carrinhos(Id)`, NOT NULL |
| `EventoId` | INT | FK → `Eventos(Id)`, NOT NULL |
| `TipoIngressoId` | INT | FK → `TiposIngresso(Id)`, NULL |
| `Quantidade` | INT | CHECK > 0, DEFAULT 1 |
| `PrecoUnitario` | DECIMAL(10,2) | NOT NULL |

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

**Novo:** Endpoint de simulação de preço (`POST /api/reservas/simular-preco`) que exibe de forma transparente o PrecoBase, TaxaServico, ValorDesconto e ValorFinal sem criar reserva.

### 6.2. Regras de Negócio

| # | Regra |
|---|-------|
| RN05.0 | O endpoint `POST /api/reservas/simular-preco` deve retornar a discriminação completa dos valores: PrecoBase, TaxaServico, ValorDesconto e ValorFinal. |
| RN05.0.1 | **PrecoBase** = `Evento.PrecoPadrao` — valor base do ingresso. |
| RN05.0.2 | **TaxaServico** = `PrecoBase × 0,10` — taxa de serviço de 10% sobre o PrecoBase (regra simples e documentada). |
| RN05.0.3 | **ValorDesconto** = aplicado conforme regra oficial de cupom: somente se o cupom existir E `PrecoBase >= ValorMinimoRegra`. |
| RN05.0.4 | **ValorFinal** = `PrecoBase + TaxaServico - ValorDesconto` — valor total a pagar. |
| RN05.0.5 | O endpoint **não insere** reserva (apenas simulação). |
| RN05.0.6 | A regra oficial de cupom **não foi alterada**. |
| RN05.1 | Toda vez que o preço de um lote ou o `PrecoPadrao` de um evento for alterado, o valor anterior deve ser registrado no histórico. |
| RN05.2 | O histórico deve conter: preço anterior, preço novo, data da alteração e motivo opcional. |
| RN05.3 | O histórico é **apenas de leitura** via API (sem DELETE ou UPDATE). |
| RN05.4 | A criação inicial do evento/lote também gera um registro no histórico (preço anterior = NULL). |

### 6.3. Endpoints Previstos

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/reservas/simular-preco` | **Novo** Simular preço de reserva com transparência (não insere reserva) |
| `GET` | `/api/eventos/{eventoId}/historico-precos` | Histórico de preços de um evento (inclui lotes) |
| `GET` | `/api/lotes/{loteId}/historico-precos` | Histórico de preços de um lote específico |

### 6.4. Tabelas Necessárias

```sql
CREATE TABLE HistoricoPrecos (
    Id              INT IDENTITY(1,1)   NOT NULL,
    EventoId        INT                 NULL,   -- NULL se for alteração de lote
    TipoIngressoId  INT                 NULL,   -- NULL se for alteração de evento
    PrecoAnterior   DECIMAL(10,2)       NULL,   -- NULL na criação
    PrecoNovo       DECIMAL(10,2)       NOT NULL,
    DataAlteracao   DATETIME            NOT NULL DEFAULT GETDATE(),
    Motivo          VARCHAR(200)        NULL,
    CONSTRAINT PK_HistoricoPrecos PRIMARY KEY (Id),
    CONSTRAINT FK_HistoricoPrecos_Eventos FOREIGN KEY (EventoId)
        REFERENCES Eventos(Id),
    CONSTRAINT FK_HistoricoPrecos_TiposIngresso FOREIGN KEY (TipoIngressoId)
        REFERENCES TiposIngresso(Id)
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

### 7.4. Tabelas Necessárias

**Nenhuma tabela nova.** Apenas consultas (`SELECT`) nas tabelas existentes:

- [`Eventos`](db/scripts/001_CreateSchema.sql:30) — dados do evento
- [`Reservas`](db/scripts/001_CreateSchema.sql:92) — contagem de vendas e receita
- [`TiposIngresso`](#44-tabelas-necessárias) — métricas por lote (RF03)
- [`CheckIns`](#34-tabelas-necessárias) — check-ins realizados (RF02)

### 7.5. Critérios de Aceite

**Cenário 1: Dashboard de evento com métricas**
```gherkin
Dado que o evento "Show da Banda X" possui capacidade 500
E tem 200 reservas confirmadas com receita total de R$ 36.750,00
E 150 check-ins realizados
Quando o administrador acessar o dashboard do evento
Então o sistema deve retornar:
  - Total de ingressos vendidos: 200
  - Receita total: R$ 36.750,00
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
| Limitação da view `vw_DashboardEventos` (LEFT JOIN via `TiposIngresso`) | Média | Baixo | Ingressos sem `TipoIngressoId` (migração) não aparecem nas métricas por lote; documentar nos contratos e considerar view alternativa via `Reservas` |
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
| **Status do Ingresso** | Estado atual do ingresso digital (tabela `Ingressos`): `Confirmada`, `Utilizada`, `Cancelada` |

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
| 1.1 | Criar script SQL incremental: [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql) (tabelas `TiposIngresso`, `Ingressos`, `CheckIns`, `Carrinhos`, `CarrinhoItens`, `HistoricoPrecos`) | Nenhuma |
| 1.2 | Implementar endpoints de CRUD de lotes (RF03) | Passo 1.1 |
| 1.3 | Implementar endpoints de ingresso digital (RF01) | Passo 1.1, 1.2 |
| 1.4 | Implementar endpoints de check-in (RF02) | Passo 1.1, 1.3 |

### Fase 2 — Operação (RF04)

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 2.1 | Implementar endpoints de carrinho (RF04) | Passo 1.1 |

### Fase 3 — Transparência e Gestão (RF05 + RF06)

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 3.1 | Implementar endpoints de histórico de preços (RF05) | Passo 1.1, 1.2 |
| 3.2 | Implementar endpoints de dashboard/admin (RF06) | Passo 1.2, 1.3, 1.4, 2.1 |

### Fase 4 — Testes

| Passo | Descrição | Dependências |
|-------|-----------|--------------|
| 4.1 | Criar testes unitários para geração de código único | Fase 1 |
| 4.2 | Criar testes unitários para regras de check-in | Passo 1.4 |
| 4.3 | Criar testes unitários para lote e carrinho | Fase 1 e 2 |
| 4.4 | Criar testes de integração para novos endpoints | Todas as fases |

---

## Histórico de Revisões

| Versão | Data | Descrição |
|--------|------|-----------|
| 1.5.0 | 2026-05-27 | 7ª revisão: corrigido "reservas vinculadas" → "ingressos vinculados" no DELETE /api/lotes/{loteId}; adicionada nota sobre fluxos de geração de ingresso (automático via carrinho + manual via POST); alinhado ReceitaTotal do Cenário 1 (RF06) para R$ 36.750,00; documentada limitação da vw_DashboardEventos nos riscos |
| 1.4.0 | 2026-05-27 | 6ª revisão: removido endpoint GET /api/admin/reservas/hoje (tabela Reservas não possui coluna DataReserva, impossível filtrar por data sem ALTER TABLE em tabela obrigatória); RF06 agora tem 4 endpoints |
| 1.3.0 | 2026-05-27 | 5ª revisão: corrigidas referências a "status da reserva" para "status do ingresso" (tabela Ingressos, não Reservas); removida RN03.3 (contradizia critério de aceite — CapacidadeTotal não é soma automática dos lotes); corrigido risco RF03 de LoteId para TipoIngressoId; corrigido glossário StatusReserva → Status do Ingresso |
| 1.2.0 | 2026-05-27 | Sincronizado schema do spec com a implementação SQL: Ingressos, TiposIngresso, CheckIns, Carrinhos/CarrinhoItens, HistoricoPrecos; removidas ALTER TABLE em Reservas; removido LogAcessoIngressos |
| 1.1.0 | 2026-05-27 | Adicionado endpoint GET /api/reservas/{id}/ingresso (RF01); descrição do POST refinada |
| 1.0.0 | 2026-05-27 | Versão inicial da especificação dos novos recursos |

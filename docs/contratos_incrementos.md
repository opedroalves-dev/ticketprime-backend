# Contratos dos Novos Endpoints — TicketPrime

> **Documento:** Contratos de API para os novos recursos
> **Versão:** 1.7.0
> **Baseado em:** [`docs/spec_incrementos.md`](docs/spec_incrementos.md), [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql)
> **Stack:** .NET 8, Minimal API, Dapper, SQL Server
> **Serialização JSON:** PascalCase (preservado), case-insensitive na leitura

---

## Sumário

1. [Convenções](#1-convenções)
2. [RF01 — Ingresso Digital](#2-rf01--ingresso-digital)
3. [RF02 — Check-in](#3-rf02--check-in)
4. [RF03 — Tipos/Lotes de Ingresso](#4-rf03--tiposlotes-de-ingresso)
5. [RF04 — Carrinho/Reserva Temporária](#5-rf04--carrinhoreserva-temporária)
6. [RF05 — Transparência de Preço](#6-rf05--transparência-de-preço)
7. [RF06 — Dashboard/Admin](#7-rf06--dashboardadmin)
8. [Models Compartilhados](#8-models-compartilhados)

---

## 1. Convenções

### 1.1. Prefixo de rota
Todas as rotas usam o prefixo `/api`.

### 1.2. Status codes padronizados

| Status | Significado | Uso |
|--------|-------------|-----|
| `200 OK` | Sucesso (consulta/idempotente) | GET, PUT |
| `201 Created` | Recurso criado | POST |
| `204 No Content` | Recurso removido ou desativado | DELETE |
| `400 Bad Request` | Erro de validação | Qualquer verbo |
| `404 Not Found` | Recurso não encontrado | GET, PUT, DELETE |
| `409 Conflict` | Conflito de estado | POST check-in duplicado |

### 1.3. Formato de erro

Todos os erros seguem o padrão já estabelecido no projeto:

```json
{
    "erro": "Mensagem descritiva do erro."
}
```

### 1.4. Tabelas de referência

As tabelas abaixo são usadas pelos novos endpoints e estão definidas em [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql):

| Tabela | Recurso | Descrição |
|--------|---------|-----------|
| [`TiposIngresso`](db/ticketprime_incrementos.sql:18) | RF03 | Lotes/tipos de ingresso por evento |
| [`Ingressos`](db/ticketprime_incrementos.sql:50) | RF01 | Ingresso digital com código único |
| [`CheckIns`](db/ticketprime_incrementos.sql:108) | RF02 | Registro de check-in |
| [`Carrinhos`](db/ticketprime_incrementos.sql:138) | RF04 | Carrinho temporário por CPF |
| [`CarrinhoItens`](db/ticketprime_incrementos.sql:173) | RF04 | Itens do carrinho |
| [`HistoricoPrecos`](db/ticketprime_incrementos.sql:209) | RF05 | Histórico de alterações de preço |

---

## 2. RF01 — Ingresso Digital

### 2.1. Gerar ingresso para reserva existente

Gera o ingresso digital com código único para uma reserva que ainda não possui ingresso (migração/retrocompatibilidade).

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/reservas/{id}/ingresso` |
| **Objetivo** | Gerar o ingresso digital com código único para uma reserva existente |

#### Request

```
POST /api/reservas/5/ingresso
```

Body: **nenhum** (vazio). O ingresso é gerado automaticamente com base nos dados da reserva e do evento vinculado.

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Reserva deve existir | 404 | `"Reserva não encontrada."` |
| Reserva não pode já ter ingresso | 409 | `"Reserva já possui ingresso gerado."` |
| Evento da reserva deve existir | 404 | `"Evento vinculado à reserva não encontrado."` |

#### Response: 201 Created

```json
{
    "Id": 10,
    "ReservaId": 5,
    "TipoIngressoId": null,
    "CodigoUnico": "A7K2X9M4",
    "Status": "Confirmada",
    "ValorBruto": 150.00,
    "ValorDesconto": 0.00,
    "TaxaServico": 0.00,
    "ValorFinal": 150.00,
    "DataCriacao": "2026-05-27T20:00:00"
}
```

---

### 2.2. Consultar ingresso pelo código único

Consulta pública os dados de um ingresso digital. Útil para validação na entrada.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/ingressos/{codigo}` |
| **Objetivo** | Consultar dados do ingresso pelo código único de 8 caracteres |

#### Request

```
GET /api/ingressos/A7K2X9M4
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Código deve ter 8 caracteres | 400 | `"Código deve ter 8 caracteres."` |
| Ingresso deve existir | 404 | `"Ingresso não encontrado."` |

#### Response: 200 OK

```json
{
    "Id": 10,
    "ReservaId": 5,
    "TipoIngresso": {
        "Id": 1,
        "Nome": "Pista",
        "Preco": 150.00
    },
    "CodigoUnico": "A7K2X9M4",
    "Status": "Confirmada",
    "ValorBruto": 150.00,
    "ValorDesconto": 0.00,
    "TaxaServico": 0.00,
    "ValorFinal": 150.00,
    "DataCriacao": "2026-05-27T20:00:00",
    "Evento": {
        "Id": 1,
        "Nome": "Show da Banda X",
        "DataEvento": "2026-12-15T20:00:00"
    },
    "Usuario": {
        "Cpf": "12345678901",
        "Nome": "João Silva"
    }
}
```

#### Response: 404 Not Found

```json
{
    "erro": "Ingresso não encontrado."
}
```

---

### 2.3. Consultar ingresso por reserva

Retorna o ingresso vinculado a uma reserva específica.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/reservas/{id}/ingresso` |
| **Objetivo** | Obter o ingresso digital associado a uma reserva |

#### Request

```
GET /api/reservas/5/ingresso
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Reserva deve existir | 404 | `"Reserva não encontrada."` |
| Reserva deve ter ingresso | 404 | `"Nenhum ingresso gerado para esta reserva."` |

#### Response: 200 OK

```json
{
    "Id": 10,
    "ReservaId": 5,
    "TipoIngressoId": null,
    "CodigoUnico": "A7K2X9M4",
    "Status": "Confirmada",
    "ValorBruto": 150.00,
    "ValorDesconto": 0.00,
    "TaxaServico": 0.00,
    "ValorFinal": 150.00,
    "DataCriacao": "2026-05-27T20:00:00"
}
```

---

## 3. RF02 — Check-in

### 3.1. Realizar check-in

Registra a entrada do portador do ingresso no evento.

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/ingressos/{codigo}/checkin` |
| **Objetivo** | Validar o ingresso na entrada e registrar o check-in |

#### Request

```
POST /api/ingressos/A7K2X9M4/checkin
```

Body: **nenhum**.

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Código deve ter 8 caracteres | 400 | `"Código deve ter 8 caracteres."` |
| Ingresso deve existir | 404 | `"Ingresso não encontrado."` |
| Ingresso deve estar "Confirmada" | 409 | `"Ingresso não está confirmado para check-in. Status atual: Cancelada"` |
| Check-in já realizado | 409 | `"Check-in já realizado para este ingresso."` |

#### Response: 201 Created

```json
{
    "Id": 1,
    "IngressoId": 10,
    "CodigoUnico": "A7K2X9M4",
    "DataCheckIn": "2026-12-15T21:30:00",
    "Mensagem": "Check-in realizado com sucesso. Bem-vindo ao evento!"
}
```

---

### 3.2. Listar check-ins de um evento

Retorna todos os check-ins realizados para um evento específico.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/eventos/{eventoId}/checkins` |
| **Objetivo** | Listar check-ins realizados em um evento |

#### Request

```
GET /api/eventos/1/checkins
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
{
    "EventoId": 1,
    "NomeEvento": "Show da Banda X",
    "TotalCheckIns": 150,
    "CheckIns": [
        {
            "Id": 1,
            "IngressoId": 10,
            "CodigoUnico": "A7K2X9M4",
            "NomeUsuario": "João Silva",
            "UsuarioCpf": "12345678901",
            "TipoIngresso": "Pista",
            "DataCheckIn": "2026-12-15T21:30:00"
        }
    ]
}
```

---

### 3.3. Estatísticas de check-in do evento

Retorna métricas resumidas de check-in para um evento.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/eventos/{eventoId}/checkins/stats` |
| **Objetivo** | Obter estatísticas de presença do evento |

#### Request

```
GET /api/eventos/1/checkins/stats
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
{
    "EventoId": 1,
    "NomeEvento": "Show da Banda X",
    "TotalIngressosVendidos": 200,
    "TotalCheckIns": 150,
    "Pendentes": 50,
    "PercentualPresenca": 75.00
}
```

---

## 4. RF03 — Tipos/Lotes de Ingresso

### 4.1. Criar lote

Cria um novo tipo/lote de ingresso para um evento.

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/eventos/{eventoId}/lotes` |
| **Objetivo** | Cadastrar um novo lote de ingresso para o evento |

#### Request

```
POST /api/eventos/1/lotes
Content-Type: application/json

{
    "Nome": "VIP",
    "Preco": 300.00,
    "Capacidade": 100,
    "TaxaServico": 15.00,
    "DataInicioVenda": "2026-06-01T00:00:00",
    "DataFimVenda": "2026-12-14T23:59:59"
}
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |
| Nome é obrigatório | 400 | `"Nome do lote é obrigatório."` |
| Nome máximo 100 caracteres | 400 | `"Nome não pode exceder 100 caracteres."` |
| Preco deve ser maior que zero | 400 | `"Preço deve ser maior que zero."` |
| Capacidade deve ser maior que zero | 400 | `"Capacidade deve ser maior que zero."` |
| TaxaServico não pode ser negativa | 400 | `"Taxa de serviço não pode ser negativa."` |
| DataInicioVenda é obrigatória | 400 | `"Data de início da venda é obrigatória."` |
| DataFimVenda é obrigatória | 400 | `"Data de fim da venda é obrigatória."` |
| DataFimVenda deve ser após DataInicioVenda | 400 | `"Data de fim da venda deve ser posterior à data de início."` |

#### Response: 201 Created

```json
{
    "Id": 1,
    "EventoId": 1,
    "Nome": "VIP",
    "Preco": 300.00,
    "Capacidade": 100,
    "TaxaServico": 15.00,
    "DataInicioVenda": "2026-06-01T00:00:00",
    "DataFimVenda": "2026-12-14T23:59:59"
}
```

---

### 4.2. Listar lotes de um evento

Retorna todos os lotes cadastrados para um evento.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/eventos/{eventoId}/lotes` |
| **Objetivo** | Listar todos os lotes/tipos de ingresso de um evento |

#### Request

```
GET /api/eventos/1/lotes
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
[
    {
        "Id": 1,
        "EventoId": 1,
        "Nome": "VIP",
        "Preco": 300.00,
        "Capacidade": 100,
        "TaxaServico": 15.00,
        "DataInicioVenda": "2026-06-01T00:00:00",
        "DataFimVenda": "2026-12-14T23:59:59",
        "IngressosVendidos": 45,
        "CapacidadeRestante": 55
    },
    {
        "Id": 2,
        "EventoId": 1,
        "Nome": "Pista",
        "Preco": 150.00,
        "Capacidade": 400,
        "TaxaServico": 10.00,
        "DataInicioVenda": "2026-06-01T00:00:00",
        "DataFimVenda": "2026-12-14T23:59:59",
        "IngressosVendidos": 200,
        "CapacidadeRestante": 200
    }
]
```

---

### 4.3. Obter lote específico

Retorna os dados de um lote específico.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/lotes/{loteId}` |
| **Objetivo** | Consultar dados de um lote pelo seu ID |

#### Request

```
GET /api/lotes/1
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Lote deve existir | 404 | `"Lote não encontrado."` |

#### Response: 200 OK

```json
{
    "Id": 1,
    "EventoId": 1,
    "Nome": "VIP",
    "Preco": 300.00,
    "Capacidade": 100,
    "TaxaServico": 15.00,
    "DataInicioVenda": "2026-06-01T00:00:00",
    "DataFimVenda": "2026-12-14T23:59:59"
}
```

---

### 4.4. Atualizar lote

Atualiza os dados de um lote existente. Dispara registro no histórico de preços se o preço for alterado.

| Campo | Valor |
|-------|-------|
| **Método** | `PUT` |
| **Rota** | `/api/lotes/{loteId}` |
| **Objetivo** | Atualizar preço, capacidade ou período de venda do lote |

#### Request

```
PUT /api/lotes/1
Content-Type: application/json

{
    "Nome": "VIP",
    "Preco": 350.00,
    "Capacidade": 100,
    "TaxaServico": 15.00,
    "DataInicioVenda": "2026-06-01T00:00:00",
    "DataFimVenda": "2026-12-14T23:59:59"
}
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Lote deve existir | 404 | `"Lote não encontrado."` |
| Nome é obrigatório | 400 | `"Nome do lote é obrigatório."` |
| Nome máximo 100 caracteres | 400 | `"Nome não pode exceder 100 caracteres."` |
| Preco deve ser maior que zero | 400 | `"Preço deve ser maior que zero."` |
| TaxaServico não pode ser negativa | 400 | `"Taxa de serviço não pode ser negativa."` |
| Capacidade não pode ser reduzida abaixo de ingressos já vendidos | 400 | `"Capacidade não pode ser menor que a quantidade de ingressos já vendidos para este lote."` |
| DataInicioVenda é obrigatória | 400 | `"Data de início da venda é obrigatória."` |
| DataFimVenda é obrigatória | 400 | `"Data de fim da venda é obrigatória."` |
| DataFimVenda deve ser após DataInicioVenda | 400 | `"Data de fim da venda deve ser posterior à data de início."` |

#### Response: 200 OK

```json
{
    "Id": 1,
    "EventoId": 1,
    "Nome": "VIP",
    "Preco": 350.00,
    "Capacidade": 100,
    "TaxaServico": 15.00,
    "DataInicioVenda": "2026-06-01T00:00:00",
    "DataFimVenda": "2026-12-14T23:59:59"
}
```

---

### 4.5. Remover lote

Remove um lote que não possui ingressos vendidos.

| Campo | Valor |
|-------|-------|
| **Método** | `DELETE` |
| **Rota** | `/api/lotes/{loteId}` |
| **Objetivo** | Excluir um lote sem vendas vinculadas |

#### Request

```
DELETE /api/lotes/1
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Lote deve existir | 404 | `"Lote não encontrado."` |
| Lote não pode ter ingressos vendidos | 409 | `"Não é possível remover um lote com ingressos vendidos."` |

#### Response: 204 No Content

(Sem corpo na resposta.)

---

## 5. RF04 — Carrinho/Reserva Temporária

### 5.1. Adicionar item ao carrinho

Adiciona ingressos ao carrinho ativo do CPF. Se não existir carrinho ativo, cria um novo com validade de 15 minutos.

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/carrinho` |
| **Objetivo** | Adicionar itens ao carrinho ativo do usuário |

#### Request

```
POST /api/carrinho
Content-Type: application/json

{
    "UsuarioCpf": "12345678901",
    "Itens": [
        {
            "EventoId": 1,
            "TipoIngressoId": 1,
            "Quantidade": 2
        }
    ]
}
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| CPF é obrigatório | 400 | `"CPF do usuário é obrigatório."` |
| CPF deve ter 11 dígitos | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| Usuário deve existir | 400 | `"Usuário não encontrado para o CPF informado."` |
| Itens é obrigatório | 400 | `"Carrinho deve conter ao menos um item."` |
| EventoId é obrigatório por item | 400 | `"EventoId é obrigatório para cada item."` |
| Evento deve existir | 400 | `"Evento não encontrado para o Id informado."` |
| Quantidade deve ser maior que zero | 400 | `"Quantidade deve ser maior que zero."` |
| Lote deve existir (se informado) | 400 | `"Tipo de ingresso não encontrado."` |
| Lote deve pertencer ao evento | 400 | `"Tipo de ingresso não pertence ao evento informado."` |
| Capacidade disponível do lote | 400 | `"Capacidade insuficiente no lote informado."` |

#### Response: 201 Created

```json
{
    "CarrinhoId": 1,
    "UsuarioCpf": "12345678901",
    "Status": "Ativo",
    "DataCriacao": "2026-05-27T20:00:00",
    "DataExpiracao": "2026-05-27T20:15:00",
    "MinutosRestantes": 15,
    "Itens": [
        {
            "Id": 1,
            "EventoId": 1,
            "NomeEvento": "Show da Banda X",
            "TipoIngressoId": 1,
            "NomeLote": "VIP",
            "Quantidade": 2,
            "PrecoUnitario": 300.00,
            "Subtotal": 600.00
        }
    ],
    "Total": 600.00
}
```

---

### 5.2. Visualizar carrinho ativo

Retorna o carrinho ativo do CPF, se existir e não estiver expirado.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/carrinho/{cpf}` |
| **Objetivo** | Consultar carrinho ativo do usuário |

#### Request

```
GET /api/carrinho/12345678901
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| CPF deve ter 11 dígitos | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| Carrinho ativo deve existir | 404 | `"Nenhum carrinho ativo encontrado para este CPF."` |
| Se carrinho expirou, retornar status expirado | 200 | (response com status "Expirado") |

#### Response: 200 OK (ativo)

```json
{
    "CarrinhoId": 1,
    "UsuarioCpf": "12345678901",
    "Status": "Ativo",
    "DataCriacao": "2026-05-27T20:00:00",
    "DataExpiracao": "2026-05-27T20:15:00",
    "MinutosRestantes": 10,
    "Itens": [
        {
            "Id": 1,
            "EventoId": 1,
            "NomeEvento": "Show da Banda X",
            "TipoIngressoId": 1,
            "NomeLote": "VIP",
            "Quantidade": 2,
            "PrecoUnitario": 300.00,
            "Subtotal": 600.00
        }
    ],
    "Total": 600.00
}
```

#### Response: 200 OK (expirado)

```json
{
    "CarrinhoId": 1,
    "UsuarioCpf": "12345678901",
    "Status": "Expirado",
    "DataCriacao": "2026-05-27T19:40:00",
    "DataExpiracao": "2026-05-27T19:55:00",
    "MinutosRestantes": 0,
    "Itens": [],
    "Total": 0.00,
    "Mensagem": "Carrinho expirado. Crie um novo carrinho para continuar."
}
```

---

### 5.3. Limpar carrinho

Remove todos os itens do carrinho ativo e marca como inativo.

| Campo | Valor |
|-------|-------|
| **Método** | `DELETE` |
| **Rota** | `/api/carrinho/{cpf}` |
| **Objetivo** | Limpar/excluir o carrinho ativo do usuário |

#### Request

```
DELETE /api/carrinho/12345678901
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| CPF deve ter 11 dígitos | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| Carrinho ativo deve existir | 404 | `"Nenhum carrinho ativo encontrado para este CPF."` |

#### Response: 204 No Content

(Sem corpo na resposta.)

---

### 5.4. Confirmar carrinho

Converte todos os itens do carrinho ativo em reservas definitivas e ingressos digitais.

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/carrinho/{cpf}/confirmar` |
| **Objetivo** | Confirmar carrinho e gerar reservas + ingressos |

#### Request

```
POST /api/carrinho/12345678901/confirmar
```

Body: **nenhum**. Opcionalmente pode aceitar um cupom no body:

```json
{
    "CupomUtilizado": "DESCONTO10"
}
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| CPF deve ter 11 dígitos | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| Carrinho ativo deve existir | 404 | `"Nenhum carrinho ativo encontrado para este CPF."` |
| Carrinho não pode estar expirado | 400 | `"Carrinho expirado. Crie um novo carrinho."` |
| Limite de 2 reservas por CPF por evento | 400 | `"CPF já possui o limite máximo de 2 reservas para o evento X."` |
| Capacidade do lote deve estar disponível | 400 | `"Capacidade insuficiente no lote X."` |
| Cupom deve existir (se informado) | 400 | `"Cupom não encontrado."` |

#### Response: 201 Created

```json
{
    "Mensagem": "Carrinho confirmado com sucesso.",
    "CarrinhoId": 1,
    "ReservasCriadas": [
        {
            "ReservaId": 10,
            "IngressoId": 15,
            "CodigoUnico": "B8L3M7N2",
            "EventoId": 1,
            "NomeEvento": "Show da Banda X",
            "TipoIngresso": "VIP",
            "ValorFinal": 300.00,
            "Status": "Confirmada"
        },
        {
            "ReservaId": 11,
            "IngressoId": 16,
            "CodigoUnico": "C9P4Q8R5",
            "EventoId": 1,
            "NomeEvento": "Show da Banda X",
            "TipoIngresso": "VIP",
            "ValorFinal": 300.00,
            "Status": "Confirmada"
        }
    ],
    "TotalPago": 600.00
}
```

---

## 6. RF05 — Transparência de Preço

### 6.0. Simular preço de reserva/ingresso

Endpoint de simulação que retorna a discriminação completa dos valores (PrecoBase, TaxaServico, ValorDesconto, ValorFinal) sem criar reserva.

| Campo | Valor |
|-------|-------|
| **Método** | `POST` |
| **Rota** | `/api/reservas/simular-preco` |
| **Objetivo** | Simular o preço de uma reserva com transparência total dos valores |

#### Request

```
POST /api/reservas/simular-preco
Content-Type: application/json

{
    "UsuarioCpf": "12345678901",
    "EventoId": 1,
    "CupomUtilizado": "DESCONTO10"
}
```

#### Regras de Cálculo

| Campo | Fórmula | Descrição |
|-------|---------|-----------|
| `PrecoBase` | `Evento.PrecoPadrao` | Preço base do ingresso (PrecoPadrao do Evento) |
| `TaxaServico` | `PrecoBase × 0,10` | 10% sobre o PrecoBase (regra simples e documentada) |
| `ValorDesconto` | `PrecoBase × (PorcentagemDesconto / 100)` | Desconto do cupom, aplicado somente se cupom existir E `PrecoBase >= ValorMinimoRegra` |
| `ValorFinal` | `PrecoBase + TaxaServico - ValorDesconto` | Valor total a pagar |

> **Nota:** A regra de cupom não foi alterada. O desconto só é aplicado quando `PrecoPadrao >= ValorMinimoRegra`, conforme o comportamento oficial.

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| CPF é obrigatório | 400 | `"CPF do usuário é obrigatório."` |
| CPF deve ter 11 dígitos | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| EventoId deve ser maior que zero | 400 | `"EventoId deve ser maior que zero."` |
| Evento deve existir | 404 | `"Evento não encontrado para o Id informado."` |

#### Response: 200 OK (sem cupom)

```json
{
    "PrecoBase": 150.00,
    "TaxaServico": 15.00,
    "ValorDesconto": 0.00,
    "ValorFinal": 165.00
}
```

#### Response: 200 OK (com cupom válido)

```json
{
    "PrecoBase": 150.00,
    "TaxaServico": 15.00,
    "ValorDesconto": 15.00,
    "ValorFinal": 150.00
}
```

#### Response: 200 OK (cupom não aplicado por ValorMinimoRegra)

```json
{
    "PrecoBase": 80.00,
    "TaxaServico": 8.00,
    "ValorDesconto": 0.00,
    "ValorFinal": 88.00
}
```

---

### 6.1. Histórico de preços do evento

Retorna o histórico de alterações de preço de um evento.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/eventos/{eventoId}/historico-precos` |
| **Objetivo** | Listar alterações de preço do evento (mais recentes primeiro) |

#### Request

```
GET /api/eventos/1/historico-precos
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
{
    "EventoId": 1,
    "NomeEvento": "Show da Banda X",
    "Historico": [
        {
            "Id": 4,
            "PrecoAnterior": 150.00,
            "PrecoNovo": 180.00,
            "DataAlteracao": "2026-07-15T10:30:00",
            "Motivo": "Reajuste sazonal",
            "TipoIngressoId": null,
            "NomeLote": null
        },
        {
            "Id": 3,
            "PrecoAnterior": 300.00,
            "PrecoNovo": 350.00,
            "DataAlteracao": "2026-06-20T14:00:00",
            "Motivo": "Alteração de preço do lote VIP",
            "TipoIngressoId": 1,
            "NomeLote": "VIP"
        },
        {
            "Id": 2,
            "PrecoAnterior": null,
            "PrecoNovo": 300.00,
            "DataAlteracao": "2026-06-01T08:00:00",
            "Motivo": "Preço inicial do lote VIP",
            "TipoIngressoId": 1,
            "NomeLote": "VIP"
        },
        {
            "Id": 1,
            "PrecoAnterior": null,
            "PrecoNovo": 150.00,
            "DataAlteracao": "2026-05-01T08:00:00",
            "Motivo": "Preço inicial do evento",
            "TipoIngressoId": null,
            "NomeLote": null
        }
    ]
}
```

---

### 6.2. Histórico de preços do lote

Retorna o histórico de alterações de preço de um lote específico.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/lotes/{loteId}/historico-precos` |
| **Objetivo** | Listar alterações de preço do lote (mais recentes primeiro) |

#### Request

```
GET /api/lotes/1/historico-precos
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Lote deve existir | 404 | `"Lote não encontrado."` |

#### Response: 200 OK

```json
{
    "LoteId": 1,
    "NomeLote": "VIP",
    "EventoId": 1,
    "NomeEvento": "Show da Banda X",
    "Historico": [
        {
            "Id": 2,
            "PrecoAnterior": 300.00,
            "PrecoNovo": 350.00,
            "DataAlteracao": "2026-06-20T14:00:00",
            "Motivo": "Alteração de preço do lote"
        },
        {
            "Id": 1,
            "PrecoAnterior": null,
            "PrecoNovo": 300.00,
            "DataAlteracao": "2026-06-01T08:00:00",
            "Motivo": "Preço inicial do lote"
        }
    ]
}
```

---

## 7. RF06 — Dashboard/Admin

> **Nota sobre métricas por lote:** As consultas de dashboard utilizam as views `vw_DashboardEventos` e `vw_DashboardLotes` (definidas em [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql:274)), que fazem LEFT JOIN via `TiposIngresso` → `Ingressos`. Ingressos com `TipoIngressoId IS NULL` (migração/retrocompatibilidade) **não são contemplados** nessas métricas por lote. Para cenários que exigem contagem de todos os ingressos independentemente de lote, deve-se utilizar uma query alternativa baseada diretamente em `Reservas`.

### 7.1. Listar eventos com métricas

Retorna todos os eventos com métricas agregadas de vendas, check-in e ocupação.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/admin/eventos` |
| **Objetivo** | Dashboard geral com métricas de todos os eventos |

#### Request

```
GET /api/admin/eventos
```

#### Validações

Nenhuma (sempre retorna lista, podendo ser vazia).

#### Response: 200 OK

```json
[
    {
        "EventoId": 1,
        "NomeEvento": "Show da Banda X",
        "DataEvento": "2026-12-15T20:00:00",
        "CapacidadeTotal": 500,
        "PrecoPadrao": 150.00,
        "TotalIngressosVendidos": 200,
        "ReceitaTotal": 36750.00,
        "PercentualOcupacao": 40.00,
        "TotalCheckIns": 150,
        "PendentesCheckIn": 50,
        "TotalCancelados": 5
    },
    {
        "EventoId": 2,
        "NomeEvento": "Teatro A",
        "DataEvento": "2026-11-20T19:00:00",
        "CapacidadeTotal": 300,
        "PrecoPadrao": 80.00,
        "TotalIngressosVendidos": 300,
        "ReceitaTotal": 24000.00,
        "PercentualOcupacao": 100.00,
        "TotalCheckIns": 280,
        "PendentesCheckIn": 20,
        "TotalCancelados": 0
    }
]
```

---

### 7.2. Dashboard detalhado de um evento

Retorna métricas detalhadas de um evento específico, incluindo dados por lote.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/admin/eventos/{eventoId}` |
| **Objetivo** | Dashboard completo de um evento |

#### Request

```
GET /api/admin/eventos/1
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
{
    "EventoId": 1,
    "NomeEvento": "Show da Banda X",
    "DataEvento": "2026-12-15T20:00:00",
    "CapacidadeTotal": 500,
    "PrecoPadrao": 150.00,
    "TotalIngressosVendidos": 200,
    "ReceitaTotal": 36750.00,
    "PercentualOcupacao": 40.00,
    "TotalCheckIns": 150,
    "PendentesCheckIn": 50,
    "TotalCancelados": 5,
    "Lotes": [
        {
            "TipoIngressoId": 1,
            "NomeLote": "VIP",
            "PrecoAtual": 300.00,
            "CapacidadeLote": 100,
            "TaxaServico": 15.00,
            "IngressosVendidos": 45,
            "CapacidadeRestante": 55,
            "ReceitaLote": 13500.00,
            "CheckInsRealizados": 40
        },
        {
            "TipoIngressoId": 2,
            "NomeLote": "Pista",
            "PrecoAtual": 150.00,
            "CapacidadeLote": 400,
            "TaxaServico": 10.00,
            "IngressosVendidos": 155,
            "CapacidadeRestante": 245,
            "ReceitaLote": 23250.00,
            "CheckInsRealizados": 110
        }
    ]
}
```

---

### 7.3. Métricas por lote do evento

Retorna métricas detalhadas por lote de um evento.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/admin/eventos/{eventoId}/lotes` |
| **Objetivo** | Métricas de vendas e check-in por lote |

#### Request

```
GET /api/admin/eventos/1/lotes
```

#### Validações

| Regra | Código | Mensagem |
|-------|--------|----------|
| Evento deve existir | 404 | `"Evento não encontrado."` |

#### Response: 200 OK

```json
[
    {
        "TipoIngressoId": 1,
        "NomeLote": "VIP",
        "PrecoAtual": 300.00,
        "CapacidadeLote": 100,
        "TaxaServico": 15.00,
        "IngressosVendidos": 45,
        "CapacidadeRestante": 55,
        "ReceitaLote": 13500.00,
        "CheckInsRealizados": 40
    },
    {
        "TipoIngressoId": 2,
        "NomeLote": "Pista",
        "PrecoAtual": 150.00,
        "CapacidadeLote": 400,
        "TaxaServico": 10.00,
        "IngressosVendidos": 155,
        "CapacidadeRestante": 245,
        "ReceitaLote": 23250.00,
        "CheckInsRealizados": 110
    }
]
```

---

### 7.4. Listar todas as reservas (admin)

Retorna todas as reservas do sistema, com dados do evento e do ingresso.

| Campo | Valor |
|-------|-------|
| **Método** | `GET` |
| **Rota** | `/api/admin/reservas` |
| **Objetivo** | Listar todas as reservas do sistema |

#### Request

```
GET /api/admin/reservas
```

#### Parâmetros opcionais de query

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `eventoId` | int | Filtrar por evento |
| `status` | string | Filtrar por status do ingresso |
| `cpf` | string | Filtrar por CPF do usuário |

```
GET /api/admin/reservas?eventoId=1&status=Confirmada
```

#### Validações

Nenhuma (retorna lista vazia se não houver reservas).

#### Response: 200 OK

```json
[
    {
        "ReservaId": 5,
        "UsuarioCpf": "12345678901",
        "NomeUsuario": "João Silva",
        "EventoId": 1,
        "NomeEvento": "Show da Banda X",
        "DataEvento": "2026-12-15T20:00:00",
        "IngressoId": 10,
        "CodigoUnico": "A7K2X9M4",
        "StatusIngresso": "Confirmada",
        "TipoIngresso": "Pista",
        "ValorBruto": 150.00,
        "ValorDesconto": 0.00,
        "TaxaServico": 10.00,
        "ValorFinal": 150.00,
        "CupomUtilizado": null,
        "CheckInRealizado": false
    }
]
```

---

## 8. Models Compartilhados

### 8.1. Request models

#### CriarLoteRequest

Usado em: `POST /api/eventos/{eventoId}/lotes`, `PUT /api/lotes/{loteId}` (mesmo modelo reutilizado)

```csharp
public class CriarLoteRequest
{
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Capacidade { get; set; }
    public decimal TaxaServico { get; set; }
    public DateTime DataInicioVenda { get; set; }
    public DateTime DataFimVenda { get; set; }
}
```

#### CarrinhoRequest

```csharp
public class CarrinhoRequest
{
    public string UsuarioCpf { get; set; } = string.Empty;
    public List<CarrinhoItemRequest> Itens { get; set; } = new();
}

public class CarrinhoItemRequest
{
    public int EventoId { get; set; }
    public int? TipoIngressoId { get; set; }
    public int Quantidade { get; set; } = 1;
}
```

#### ConfirmarCarrinhoRequest

```csharp
public class ConfirmarCarrinhoRequest
{
    public string? CupomUtilizado { get; set; }
}
```

#### SimulacaoPrecoRequest

Usado em: `POST /api/reservas/simular-preco`

```csharp
public class SimulacaoPrecoRequest
{
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string? CupomUtilizado { get; set; }
}
```

### 8.2. Response models

#### LoteResponse

Usado em: `POST /api/eventos/{eventoId}/lotes`, `GET /api/lotes/{loteId}`, `PUT /api/lotes/{loteId}`

```csharp
public class LoteResponse
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Capacidade { get; set; }
    public decimal TaxaServico { get; set; }
    public DateTime DataInicioVenda { get; set; }
    public DateTime DataFimVenda { get; set; }
}
```

#### LoteListaResponse

Usado em: `GET /api/eventos/{eventoId}/lotes` (estende `LoteResponse` com métricas)

```csharp
public class LoteListaResponse
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Capacidade { get; set; }
    public decimal TaxaServico { get; set; }
    public DateTime DataInicioVenda { get; set; }
    public DateTime DataFimVenda { get; set; }
    public int IngressosVendidos { get; set; }
    public int CapacidadeRestante { get; set; }
}
```

#### IngressoResponse

Usado em: `GET /api/ingressos/{codigo}` (versão simplificada), `GET /api/reservas/{id}/ingresso`, `POST /api/reservas/{id}/ingresso`

```csharp
public class IngressoResponse
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public int? TipoIngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorBruto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal TaxaServico { get; set; }
    public decimal ValorFinal { get; set; }
    public DateTime DataCriacao { get; set; }
}
```

#### IngressoDetalhadoResponse

Usado em: `GET /api/ingressos/{codigo}` (versão detalhada com joins)

```csharp
public class IngressoDetalhadoResponse
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public TipoIngressoResumo? TipoIngresso { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorBruto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal TaxaServico { get; set; }
    public decimal ValorFinal { get; set; }
    public DateTime DataCriacao { get; set; }
    public EventoResumo? Evento { get; set; }
    public UsuarioResumo? Usuario { get; set; }
}

public class TipoIngressoResumo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
}

public class EventoResumo
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
}

public class UsuarioResumo
{
    public string Cpf { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}
```

#### CheckInResponse

Usado em: `POST /api/ingressos/{codigo}/checkin`

```csharp
public class CheckInResponse
{
    public int Id { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public DateTime DataCheckIn { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}
```

#### CheckInListResponse / CheckInItemResponse

Usado em: `GET /api/eventos/{eventoId}/checkins`

```csharp
public class CheckInListResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int TotalCheckIns { get; set; }
    public List<CheckInItemResponse> CheckIns { get; set; } = new();
}

public class CheckInItemResponse
{
    public int Id { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public string UsuarioCpf { get; set; } = string.Empty;
    public string? TipoIngresso { get; set; }
    public DateTime DataCheckIn { get; set; }
}
```

#### CheckInStatsResponse

Usado em: `GET /api/eventos/{eventoId}/checkins/stats`

```csharp
public class CheckInStatsResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int TotalIngressosVendidos { get; set; }
    public int TotalCheckIns { get; set; }
    public int Pendentes { get; set; }
    public decimal PercentualPresenca { get; set; }
}
```

#### CarrinhoResponse / CarrinhoItemResponse

Usado em: `POST /api/carrinho`, `GET /api/carrinho/{cpf}`

```csharp
public class CarrinhoResponse
{
    public int CarrinhoId { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataExpiracao { get; set; }
    public int MinutosRestantes { get; set; }
    public List<CarrinhoItemResponse> Itens { get; set; } = new();
    public decimal Total { get; set; }
    public string? Mensagem { get; set; }
}

public class CarrinhoItemResponse
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public int? TipoIngressoId { get; set; }
    public string? NomeLote { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
```

#### CarrinhoConfirmacaoResponse / ReservaConfirmadaResponse

Usado em: `POST /api/carrinho/{cpf}/confirmar`

```csharp
public class CarrinhoConfirmacaoResponse
{
    public string Mensagem { get; set; } = string.Empty;
    public int CarrinhoId { get; set; }
    public List<ReservaConfirmadaResponse> ReservasCriadas { get; set; } = new();
    public decimal TotalPago { get; set; }
}

public class ReservaConfirmadaResponse
{
    public int ReservaId { get; set; }
    public int IngressoId { get; set; }
    public string CodigoUnico { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public string TipoIngresso { get; set; } = string.Empty;
    public decimal ValorFinal { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

#### HistoricoPrecoResponse

Usado como item individual nos wrappers abaixo.

```csharp
public class HistoricoPrecoResponse
{
    public int Id { get; set; }
    public decimal? PrecoAnterior { get; set; }
    public decimal PrecoNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
    public string? Motivo { get; set; }
    public int? TipoIngressoId { get; set; }    // null = alteração no evento; preenchido = alteração em lote específico
    public string? NomeLote { get; set; }        // null se for alteração no evento
}
```

#### EventoHistoricoPrecosResponse

Usado em: `GET /api/eventos/{eventoId}/historico-precos`

```csharp
public class EventoHistoricoPrecosResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public List<HistoricoPrecoResponse> Historico { get; set; } = new();
}
```

#### LoteHistoricoPrecosResponse

Usado em: `GET /api/lotes/{loteId}/historico-precos`

```csharp
public class LoteHistoricoPrecosResponse
{
    public int LoteId { get; set; }
    public string NomeLote { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public List<HistoricoPrecoResponse> Historico { get; set; } = new();
}
```

#### DashboardEventoListaResponse

Usado em: `GET /api/admin/eventos`

```csharp
public class DashboardEventoListaResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int CapacidadeTotal { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int TotalIngressosVendidos { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal PercentualOcupacao { get; set; }
    public int TotalCheckIns { get; set; }
    public int PendentesCheckIn { get; set; }
    public int TotalCancelados { get; set; }
}
```

#### DashboardEventoDetalhadoResponse / DashboardLoteResponse

Usado em: `GET /api/admin/eventos/{eventoId}`, `GET /api/admin/eventos/{eventoId}/lotes`

```csharp
public class DashboardEventoDetalhadoResponse
{
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int CapacidadeTotal { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int TotalIngressosVendidos { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal PercentualOcupacao { get; set; }
    public int TotalCheckIns { get; set; }
    public int PendentesCheckIn { get; set; }
    public int TotalCancelados { get; set; }
    public List<DashboardLoteResponse> Lotes { get; set; } = new();
}

public class DashboardLoteResponse
{
    public int TipoIngressoId { get; set; }
    public string NomeLote { get; set; } = string.Empty;
    public decimal PrecoAtual { get; set; }
    public int CapacidadeLote { get; set; }
    public decimal TaxaServico { get; set; }
    public int IngressosVendidos { get; set; }
    public int CapacidadeRestante { get; set; }
    public decimal ReceitaLote { get; set; }
    public int CheckInsRealizados { get; set; }
}
```

#### AdminReservaResponse

Usado em: `GET /api/admin/reservas`

```csharp
public class AdminReservaResponse
{
    public int ReservaId { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string NomeEvento { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public int? IngressoId { get; set; }
    public string? CodigoUnico { get; set; }
    public string? StatusIngresso { get; set; }
    public string? TipoIngresso { get; set; }
    public decimal? ValorBruto { get; set; }
    public decimal? ValorDesconto { get; set; }
    public decimal? TaxaServico { get; set; }
    public decimal? ValorFinal { get; set; }
    public string? CupomUtilizado { get; set; }
    public bool CheckInRealizado { get; set; }
}
```

#### SimulacaoPrecoResponse

Usado em: `POST /api/reservas/simular-preco`

```csharp
public class SimulacaoPrecoResponse
{
    public decimal PrecoBase { get; set; }
    public decimal TaxaServico { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorFinal { get; set; }
}
```

## Histórico de Revisões

| Versão | Data | Descrição |
|--------|------|-----------|
| 1.8.0 | 2026-05-27 | 8ª revisão: adicionado endpoint POST /api/reservas/simular-preco (seção 6.0) com transparência de preço (PrecoBase, TaxaServico, ValorDesconto, ValorFinal); adicionados modelos SimulacaoPrecoRequest (8.1) e SimulacaoPrecoResponse (8.2) |
| 1.7.0 | 2026-05-27 | 7ª revisão: adicionados TipoIngressoId (int?) e NomeLote (string?) ao HistoricoPrecoResponse para identificar alterações de lote no histórico do evento; JSON 6.1 atualizado com registros de lote; adicionados modelos LoteResponse e LoteListaResponse na seção 8.2; adicionada nota sobre limitação da view vw_DashboardEventos na seção RF06 |
| 1.6.0 | 2026-05-27 | 6ª revisão: removido endpoint /api/admin/reservas/hoje e modelos AdminReservasHojeResponse/AdminReservaHojeItem (tabela Reservas não possui coluna DataReserva); removido campo DataReserva de AdminReservaResponse; corrigida inconsistência numérica ReceitaTotal 30000→36750 (45×300 + 155×150 = 36750) |
| 1.5.0 | 2026-05-27 | 5ª revisão: CarrinhoItemResponse.TipoIngressoId alterado de int para int? (SQL CarrinhoItens.TipoIngressoId é NULL); NomeLote alterado para string? (null se TipoIngressoId for null); CheckInItemResponse.TipoIngresso alterado para string? (null se ingresso sem lote) |
| 1.4.0 | 2026-05-27 | 4ª revisão: TipoIngressoId adicionado na resposta JSON de GET /api/reservas/{id}/ingresso (estava inconsistente com o model IngressoResponse) |
| 1.3.0 | 2026-05-27 | 3ª revisão: HistoricoPrecoResponse.Motivo alterado para string? (SQL NULL); CriarLoteRequest documentado como reusável no PUT; verificação cruzada com Program.cs, SQL e spec_incrementos.md |
| 1.2.0 | 2026-05-27 | 2ª revisão: header versão corrigido; MinutosRestantes adicionado na resposta 5.1; TaxaServico adicionado nos lotes de 7.2; models wrapper EventoHistoricoPrecosResponse e LoteHistoricoPrecosResponse adicionados |
| 1.1.0 | 2026-05-27 | Revisão: validações adicionadas no PUT /api/lotes/{loteId}; DELETE /api/carrinho/{cpf} corrigido para 204; models C# de resposta completados na seção 8.2 |
| 1.0.0 | 2026-05-27 | Versão inicial dos contratos dos novos recursos |

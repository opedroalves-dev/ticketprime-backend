# Planejamento — Etapa 11b: Migrar Confirmação do Carrinho para CarrinhoService.ConfirmarAsync

> **Parte da correção C1** (aprovada pelo V4 em 03/06/2026): Dividir o domínio Carrinho em duas etapas — **11a (CRUD não transacional)** e **11b (confirmação transacional)**.
>
> **Base:** [`plans/etapa11a-planejamento.md`](plans/etapa11a-planejamento.md) (seção 10 — "Como a Etapa 11a prepara a Etapa 11b")
>
> **Stack:** .NET 8, Minimal API, Dapper, SQL Server
> **Risco:** Alto (transação multi-domínio) | **Correção:** C1

---

## 1. Objetivo da Etapa 11b

Migrar o endpoint [`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:744) — atualmente **inline** em [`Program.cs`](src/TicketPrime.Api/Program.cs) com SQL e transação manual — para o método [`CarrinhoService.ConfirmarAsync()`](src/TicketPrime.Api/Services/CarrinhoService.cs), preservando:

- ✅ O contrato da API (rota, método HTTP, request, response, status codes)
- ✅ A transação atômica com rollback em caso de falha
- ✅ O uso de `ValidationException` para erros de negócio (mapeado para 400 pelo middleware)
- ✅ O `commandTimeout: 30` em todas as queries dentro da transação
- ✅ A geração de códigos únicos para ingressos (8 caracteres, sem caracteres ambíguos)
- ✅ As validações de limite de reservas (2 por CPF/evento) e capacidade de lote

O endpoint inline (~200 linhas) será substituído por ~8 linhas que delegam ao service, seguindo o mesmo padrão dos endpoints migrados na Etapa 11a.

---

## 2. Arquivos Alterados

| Arquivo | Tipo de Alteração | Descrição |
|---------|:-----------------:|-----------|
| [`src/TicketPrime.Api/Services/CarrinhoService.cs`](src/TicketPrime.Api/Services/CarrinhoService.cs) | **Modificado** | Adicionar método `ConfirmarAsync()`, novas dependências (`IDbConnection`, `ICupomRepository`) |
| [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs) | **Modificado** | Substituir endpoint inline por delegação ao service |

---

## 3. Arquivos Criados

**Nenhum.** A Etapa 11b **não cria** novos arquivos — toda a lógica é adicionada ao [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs) existente (criado na Etapa 11a).

---

## 4. Repositórios Envolvidos

### 4.1. [`ICarrinhoRepository`](src/TicketPrime.Api/Repositories/ICarrinhoRepository.cs) (Criado na Etapa 11a)

| Método | Uso na Confirmação |
|--------|--------------------|
| `ObterAtivoPorCpfAsync(cpf, transaction)` | Buscar carrinho ativo do CPF (validação inicial, fora da transação) |
| `ContarItensAsync(carrinhoId, transaction)` | Verificar se carrinho tem itens antes de iniciar a transação |
| `AtualizarStatusAsync(id, "Confirmado", transaction)` | Marcar carrinho como confirmado após sucesso |
| `RemoverItensAsync(carrinhoId, transaction)` | Limpar itens do carrinho após confirmação |

**Observação:** A busca do carrinho (`ObterAtivoPorCpfAsync`) e a contagem de itens (`ContarItensAsync`) são feitas **fora da transação**, como validações prévias. Os métodos de atualização (`AtualizarStatusAsync`, `RemoverItensAsync`) são chamados **dentro da transação** com o parâmetro `transaction`.

### 4.2. [`IReservaRepository`](src/TicketPrime.Api/Repositories/IReservaRepository.cs) (Criado na Etapa 10b)

| Método | Uso na Confirmação |
|--------|--------------------|
| `InserirAsync(reserva, transaction)` | Inserir cada reserva gerada na confirmação, retornando o `Id` |
| `ContarPorCpfEEventoAsync(cpf, eventoId, transaction)` | Verificar limite de 2 reservas por CPF/evento (dentro da transação) |

**Importante:** O `InserirAsync()` já existe e retorna `int Id` — exatamente o que o fluxo atual faz com `OUTPUT INSERTED.Id`. Não precisa de alteração.

### 4.3. [`IIngressoRepository`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs) (Criado na Etapa 8)

| Método | Uso na Confirmação |
|--------|--------------------|
| `GerarCodigoUnicoAsync(transaction, commandTimeout: 30)` | Gerar código único de 8 caracteres para cada ingresso |
| `InserirAsync(ingresso, transaction)` | Inserir cada ingresso vinculado à reserva recém-criada |
| `ContarPorTipoAsync(tipoIngressoId, transaction)` | Verificar capacidade do lote (ingressos vendidos vs. capacidade) |

**Importante:** O `InserirAsync()` retorna `(int Id, DateTime DataCriacao)` — o `Id` é usado para montar o `ReservaConfirmadaResponse`. O `GerarCodigoUnicoAsync()` já aceita `commandTimeout` como parâmetro opcional.

### 4.4. [`IEventoRepository`](src/TicketPrime.Api/Repositories/IEventoRepository.cs) (Criado na Etapa 5)

| Método | Uso na Confirmação |
|--------|--------------------|
| `ObterPorIdAsync(eventoId, transaction)` | Buscar dados do evento (Nome, PrecoPadrao) para cada item |

### 4.5. [`ICupomRepository`](src/TicketPrime.Api/Repositories/ICupomRepository.cs) (Criado na Etapa 3)

| Método | Uso na Confirmação |
|--------|--------------------|
| `ObterPorCodigoAsync(codigo, transaction)` | Validar cupom informado e obter `PorcentagemDesconto` / `ValorMinimoRegra` |

**NOVO:** Este repositório **não está** no construtor atual do [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs:22-28) — será adicionado na Etapa 11b.

### 4.6. [`ITipoIngressoRepository`](src/TicketPrime.Api/Repositories/ITipoIngressoRepository.cs) (Criado na Etapa 7)

| Método | Uso na Confirmação |
|--------|--------------------|
| `ObterPorIdAsync(tipoIngressoId, transaction)` | Buscar dados do lote (Nome, Capacidade) para cada item que tenha `TipoIngressoId` |

---

## 5. Fluxo Transacional Passo a Passo

### 5.1. Validações Pré-Transação (fora da transação)

```
1. Validar CPF (11 dígitos numéricos)
   └─ Se inválido → retornar (null, "CPF deve conter 11 dígitos numéricos.", 400)

2. Validar request body
   └─ Extrair CupomUtilizado (pode ser null/empty)

3. Buscar carrinho ativo via ICarrinhoRepository.ObterAtivoPorCpfAsync(cpf)
   └─ Se null → retornar (null, "Nenhum carrinho ativo encontrado para este CPF.", 404)

4. Validar expiração do carrinho (DataExpiracao <= DateTime.Now)
   └─ Se expirado → retornar (null, "Carrinho expirado. Crie um novo carrinho.", 400)

5. Verificar se carrinho possui itens via ICarrinhoRepository.ContarItensAsync(carrinho.Id)
   └─ Se 0 itens → retornar (null, "Carrinho vazio. Adicione itens antes de confirmar.", 400)
```

### 5.2. Transação (dentro de `BeginTransaction`/`Commit`/`Rollback`)

```
6. Iniciar transação: using var transaction = _db.BeginTransaction()

7. Validar cupom (se informado)
   └─ ICupomRepository.ObterPorCodigoAsync(cupomUtilizado, transaction)
   └─ Se null → throw new ValidationException("Cupom não encontrado.")

8. Obter itens do carrinho (SQL direto via _db.QueryAsync com transaction)
   └─ SELECT ci.Id, ci.CarrinhoId, ci.EventoId, ci.TipoIngressoId, ci.Quantidade, ci.PrecoUnitario
       FROM CarrinhoItens ci WHERE ci.CarrinhoId = @CarrinhoId

9. Para cada item no carrinho:
   a. Buscar evento via IEventoRepository.ObterPorIdAsync(item.EventoId, transaction)
      └─ Se null → throw new ValidationException("Evento {Id} não encontrado.")

   b. Se TipoIngressoId informado:
      └─ Buscar lote via ITipoIngressoRepository.ObterPorIdAsync(item.TipoIngressoId.Value, transaction)
      └─ Extrair nomeLote = lote?.Nome ?? ""

   c. Para cada unidade (for q = 0; q < item.Quantidade; q++):

      c.1. Verificar limite de 2 reservas por CPF/evento
           └─ IReservaRepository.ContarPorCpfEEventoAsync(carrinho.UsuarioCpf, item.EventoId, transaction)
           └─ Se >= 2 → throw new ValidationException("CPF já possui o limite máximo de 2 reservas para o evento {Id}.")

      c.2. Verificar capacidade do lote (se TipoIngressoId informado)
           └─ ITipoIngressoRepository.ObterPorIdAsync(item.TipoIngressoId.Value, transaction)
           └─ IIngressoRepository.ContarPorTipoAsync(item.TipoIngressoId.Value, transaction)
           └─ Se vendidos >= lote.Capacidade → throw new ValidationException("Capacidade insuficiente no lote {Id}.")

      c.3. Calcular valor
           └─ valorBruto = item.PrecoUnitario
           └─ valorDesconto = cupom?.PorcentagemDesconto aplicado se PrecoPadrao >= ValorMinimoRegra
           └─ taxaServico = 0
           └─ valorFinal = valorBruto - valorDesconto

      c.4. Inserir reserva via IReservaRepository.InserirAsync(reserva, transaction)
           └─ reserva = { UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago }
           └─ Retorna reservaId

      c.5. Gerar código único via IIngressoRepository.GerarCodigoUnicoAsync(transaction, 30)

      c.6. Inserir ingresso via IIngressoRepository.InserirAsync(ingresso, transaction)
           └─ ingresso = { ReservaId, TipoIngressoId, CodigoUnico, Status="Confirmada",
                           ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao=GETDATE() }
           └─ Retorna (ingressoId, dataCriacao)

      c.7. Adicionar ao carrinhoConfirmacaoResponse.ReservasCriadas
           └─ ReservaConfirmadaResponse { ReservaId, IngressoId, CodigoUnico,
                                          EventoId, NomeEvento, TipoIngresso=nomeLote,
                                          ValorFinal, Status="Confirmada" }

      c.8. Acumular totalPago += valorFinal

10. Marcar carrinho como Confirmado
    └─ ICarrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Confirmado", transaction)

11. Limpar itens do carrinho
    └─ ICarrinhoRepository.RemoverItensAsync(carrinho.Id, transaction)

12. transaction.Commit()

13. Montar CarrinhoConfirmacaoResponse
    └─ { Mensagem="Carrinho confirmado com sucesso.", CarrinhoId, ReservasCriadas, TotalPago }

14. Retornar CarrinhoConfirmacaoResponse
```

### 5.3. Assinatura do Método

```csharp
public async Task<(CarrinhoConfirmacaoResponse? Response, string? Erro, int StatusCode)>
    ConfirmarAsync(string cpf, ConfirmarCarrinhoRequest? request)
```

**Por que `ConfirmarCarrinhoRequest?` (nullable)?** O request body é opcional — o campo `CupomUtilizado` pode ser omitido. O endpoint atual usa `[FromBody] ConfirmarCarrinhoRequest? request` (nullable). Preservamos o mesmo comportamento: se `request` for null, `cupomUtilizado` será null.

**Por que retornar `StatusCode` explicitamente?** Seguindo o padrão usado em `AdicionarItensAsync()` (Etapa 11a), que retorna `(Response?, Erro?, StatusCode)`. O endpoint em [`Program.cs`](src/TicketPrime.Api/Program.cs) usa `Results.Json(new { erro }, statusCode: statusCode)` para mapear diferentes status.

---

## 6. Como Rollback Será Garantido

O rollback segue o mesmo padrão do endpoint inline atual: **`try/catch` com `Rollback()` explícito + `throw`**.

```csharp
_db.Open(); // Garantir que a conexão está aberta
using var transaction = _db.BeginTransaction();
try
{
    // ... todas as operações dentro da transação ...

    transaction.Commit();
    return (response, null, 201);
}
catch
{
    transaction.Rollback();
    throw; // Relança para o ExceptionHandlingMiddleware tratar
}
```

**Garantias:**
- `using var transaction` garante `Dispose()` mesmo em caso de exceção antes do `Commit()`
- O `catch` faz `Rollback()` explícito antes do `throw`, assegurando que o banco retorna ao estado anterior
- O `throw` relança a exceção, que é capturada pelo [`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs):
  - `ValidationException` → 400 BadRequest
  - `BadHttpRequestException` → 400 BadRequest
  - Outras exceções (`SqlException`, etc.) → 500 Internal ServerError
- Assim como no endpoint atual, o `_db.Open()` é chamado explicitamente antes de iniciar a transação, garantindo que a conexão esteja aberta para o Dapper

---

## 7. Como ValidationException Será Usada

A [`ValidationException`](src/TicketPrime.Api/Middleware/ValidationException.cs) é a mesma usada no endpoint inline atual. Ela é uma `Exception` simples com apenas uma `message` no construtor.

**Onde é lançada (dentro da transação):**

| Cenário | Mensagem | Status Code |
|---------|----------|:-----------:|
| Cupom informado não encontrado | `"Cupom não encontrado."` | 400 |
| Evento do item não encontrado | `"Evento {Id} não encontrado."` | 400 |
| Limite de 2 reservas excedido | `"CPF já possui o limite máximo de 2 reservas para o evento {Id}."` | 400 |
| Capacidade do lote insuficiente | `"Capacidade insuficiente no lote {Id}."` | 400 |

**Como funciona o fluxo:**
1. Dentro do `try` da transação, uma condição de negócio é violada
2. `throw new ValidationException("mensagem")` é executado
3. O `catch` captura, faz `transaction.Rollback()`, e `throw` relança
4. O [`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs:36-42) captura o `ValidationException`
5. Retorna `400 BadRequest` com `{ "erro": "mensagem" }`

**Diferença do endpoint atual:** No endpoint inline, o `ValidationException` é lançado dentro do `try` da transação. No service, será exatamente o mesmo comportamento.

---

## 8. Como commandTimeout: 30 Será Preservado

O `commandTimeout: 30` é um parâmetro do Dapper que define o timeout do comando SQL em segundos. No endpoint inline atual, ele é passado em **todas** as chamadas Dapper dentro da transação.

### 8.1. Chamadas via Repositório

Os repositórios atuais **não expõem `commandTimeout`** em suas interfaces — usam o timeout padrão do Dapper (30 segundos, que é o default do SqlConnection). No entanto, o [`IIngressoRepository.GerarCodigoUnicoAsync`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs:59) já tem o parâmetro `int? commandTimeout = 30` com valor default 30.

**Estratégia:** Como o Dapper usa `commandTimeout = 30` por padrão (igual ao `SqlCommand.CommandTimeout` default), e o endpoint inline só passava `commandTimeout: 30` explicitamente (que é o valor default), **não há necessidade de propagar `commandTimeout` pelas interfaces dos repositórios**. A chamada:

```csharp
await _db.ExecuteScalarAsync<int>(sql, params, transaction: transaction, commandTimeout: 30);
```

é equivalente a:

```csharp
await _db.ExecuteScalarAsync<int>(sql, params, transaction: transaction);
```

Pois o Dapper usa `commandTimeout: 30` como default quando não especificado.

### 8.2. Exceção: GerarCodigoUnicoAsync

O método [`IIngressoRepository.GerarCodigoUnicoAsync`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs:59) tem `int? commandTimeout = 30` como parâmetro opcional. Chamaremos explicitamente com `commandTimeout: 30` para manter a semântica:

```csharp
var codigoUnico = await _ingressoRepository.GerarCodigoUnicoAsync(transaction, 30);
```

### 8.3. Queries Diretas via _db

Para as queries que não têm método encapsulado em repositório (como buscar itens do carrinho: `SELECT FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId`), usaremos `_db.QueryAsync` diretamente com `transaction`:

```csharp
var itensCarrinho = await _db.QueryAsync<CarrinhoItem>(sql, new { CarrinhoId = carrinho.Id }, transaction: transaction);
```

---

## 9. Como Códigos Únicos dos Ingressos Serão Gerados

A geração de código único **já está encapsulada** no método [`IIngressoRepository.GerarCodigoUnicoAsync`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs:59) (movido de [`Program.cs`](src/TicketPrime.Api/Program.cs) na Etapa 8).

**Algoritmo (inalterado):**
1. Caracteres permitidos: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`
   - Excluídos: `I`, `O`, `0`, `1` (caracteres ambíguos)
2. Gera string aleatória de 8 caracteres usando `Random.Shared`
3. Verifica colisão no banco: `SELECT COUNT(1) FROM Ingressos WHERE CodigoUnico = @Codigo`
4. Se colidir, tenta novamente (loop `do/while`)
5. Retorna o código único não colidente

**Chamada no CarrinhoService:**

```csharp
var codigoUnico = await _ingressoRepository.GerarCodigoUnicoAsync(transaction, 30);
```

Exatamente como é chamado no endpoint inline atual ([`Program.cs:879`](src/TicketPrime.Api/Program.cs:879)).

---

## 10. Como Limite de Reservas/Capacidade Serão Validados

### 10.1. Limite de 2 Reservas por CPF/Evento

**Atual (inline):** Query SQL direta dentro da transação:
```sql
SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId
```

**Novo (service):** Usa [`IReservaRepository.ContarPorCpfEEventoAsync`](src/TicketPrime.Api/Repositories/IReservaRepository.cs:25) — que executa o **mesmo SQL**.

```csharp
var reservasCpfEvento = await _reservaRepository
    .ContarPorCpfEEventoAsync(carrinho.UsuarioCpf, item.EventoId, transaction);

if (reservasCpfEvento >= 2)
    throw new ValidationException($"CPF já possui o limite máximo de 2 reservas para o evento {item.EventoId}.");
```

**Importante:** A validação é feita **dentro do loop de unidades** (`for q`), assim como no endpoint inline. Cada unidade do mesmo item verifica o limite novamente porque a reserva da unidade anterior já foi inserida.

### 10.2. Capacidade do Lote

**Atual (inline):** Query SQL direta dentro da transação:
```sql
SELECT COUNT(1) FROM Ingressos WHERE TipoIngressoId = @TipoIngressoId AND Status IN ('Confirmada', 'Utilizada')
```

**Novo (service):** Usa [`IIngressoRepository.ContarPorTipoAsync`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs:16) — que executa o **mesmo SQL**.

```csharp
if (item.TipoIngressoId.HasValue)
{
    var lote = await _tipoIngressoRepository.ObterPorIdAsync(item.TipoIngressoId.Value, transaction);

    if (lote is not null)
    {
        var vendidos = await _ingressoRepository
            .ContarPorTipoAsync(item.TipoIngressoId.Value, transaction);

        if (vendidos >= lote.Capacidade)
            throw new ValidationException($"Capacidade insuficiente no lote {item.TipoIngressoId}.");
    }
}
```

**Nota:** A validação de capacidade do lote é feita **dentro do loop de unidades**, assim como no endpoint inline. Isso significa que a cada unidade confirmada, a contagem de vendidos é re-consultada, garantindo que não haja estouro de capacidade.

---

## 11. Como Contratos, Mensagens e Status Codes Serão Preservados

### 11.1. Contrato do Request

| Campo | Tipo | Obrigatório | Descrição |
|-------|:----:|:-----------:|-----------|
| `cupomUtilizado` | `string?` | Não | Código do cupom (case-insensitive no banco) |

**Request body é opcional** (nullable) — mesma regra do endpoint atual.

### 11.2. Contrato do Response (201 Created)

```json
{
  "mensagem": "Carrinho confirmado com sucesso.",
  "carrinhoId": 1,
  "reservasCriadas": [
    {
      "reservaId": 1,
      "ingressoId": 1,
      "codigoUnico": "ABC123XY",
      "eventoId": 1,
      "nomeEvento": "Show do Rock",
      "tipoIngresso": "VIP",
      "valorFinal": 150.00,
      "status": "Confirmada"
    }
  ],
  "totalPago": 300.00
}
```

**Property naming:** `null` (PascalCase preservado) conforme configurado em [`Program.cs:42`](src/TicketPrime.Api/Program.cs:42):
```csharp
options.SerializerOptions.PropertyNamingPolicy = null;
```

### 11.3. Status Codes e Mensagens de Erro

| Cenário | Status | Mensagem |
|---------|:------:|----------|
| CPF inválido | 400 | `"CPF deve conter 11 dígitos numéricos."` |
| Carrinho não encontrado | 404 | `"Nenhum carrinho ativo encontrado para este CPF."` |
| Carrinho expirado | 400 | `"Carrinho expirado. Crie um novo carrinho."` |
| Carrinho vazio | 400 | `"Carrinho vazio. Adicione itens antes de confirmar."` |
| Cupom não encontrado | 400 | `"Cupom não encontrado."` |
| Evento não encontrado | 400 | `"Evento {Id} não encontrado."` |
| Limite de reservas | 400 | `"CPF já possui o limite máximo de 2 reservas para o evento {Id}."` |
| Lote sem capacidade | 400 | `"Capacidade insuficiente no lote {Id}."` |
| Sucesso | 201 | Corpo com `CarrinhoConfirmacaoResponse` |

### 11.4. Header de Location

O endpoint atual retorna:
```csharp
return Results.Created($"/api/carrinho/{cpf}/confirmar", response);
```

O novo endpoint no [`Program.cs`](src/TicketPrime.Api/Program.cs) manterá a mesma chamada:
```csharp
return Results.Created($"/api/carrinho/{cpf}/confirmar", response);
```

---

## 12. Riscos

| # | Risco | Probabilidade | Impacto | Mitigação |
|:-:|-------|:-------------:|:-------:|-----------|
| R1 | **Injeção de `IDbConnection` no CarrinhoService pode quebrar tests existentes** | Média | Alto | Tests da Etapa 11a que instanciam `CarrinhoService` diretamente precisarão adicionar `IDbConnection` e `ICupomRepository` ao construtor. Verificar se existem tais testes. |
| R2 | **Perda de atomicidade se esquecer de passar `transaction` para algum repositório** | Baixa | Crítico | Todos os repositórios seguem C6 (`IDbTransaction? transaction = null`). Se o parâmetro não for passado, a operação executa fora da transação — **quebrando a atomicidade**. **Mitigação:** Code review obrigatório para verificar se TODAS as chamadas dentro do bloco `try` recebem `transaction`. |
| R3 | **Deadlock em alta concorrência** | Baixa | Alto | A transação faz várias consultas e inserções dentro de um loop. Se muitos usuários confirmarem para o mesmo evento/lote simultaneamente, pode haver deadlock. **Mitigação:** O escopo da transação é pequeno (um carrinho por vez) e as consultas são rápidas (< 30s timeout). |
| R4 | **Quebra de contrato se mensagem de erro mudar** | Baixa | Médio | Frontend pode depender das mensagens exatas de erro. **Mitigação:** Copiar mensagens textuais exatas do endpoint inline, sem alterações. |
| R5 | **Registrar `ICupomRepository` ausente em [`Program.cs`](src/TicketPrime.Api/Program.cs)** | Média | Médio | O DI do `CupomRepository` já está registrado em [`Program.cs:22`](src/TicketPrime.Api/Program.cs:22) (`builder.Services.AddScoped<ICupomRepository, CupomRepository>()`). Mas o construtor do `CarrinhoService` precisará do `ICupomRepository` — o DI resolverá automaticamente. |
| R6 | **Conexão não aberta ao iniciar transação** | Baixa | Médio | O Dapper abre a conexão automaticamente se estiver fechada, mas `BeginTransaction()` exige `ConnectionState.Open`. **Mitigação:** Chamar `if (_db.State != ConnectionState.Open) _db.Open()` antes de `BeginTransaction()`, conforme o endpoint inline atual. |

---

## 13. Critérios de Aceite

- [ ] **CA1:** `dotnet build` compila sem erros
- [ ] **CA2:** O endpoint [`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs) agora chama `CarrinhoService.ConfirmarAsync()` em vez de ter SQL inline
- [ ] **CA3:** Nenhuma rota, método HTTP, request body ou response body foi alterado
- [ ] **CA4:** Nenhuma mensagem de erro foi alterada (mesmo texto, mesmo status code)
- [ ] **CA5:** A transação continua atômica — se qualquer `ValidationException` for lançada, todas as operações são revertidas
- [ ] **CA6:** O rollback funciona — em caso de exceção, `transaction.Rollback()` é chamado e a exceção é relançada
- [ ] **CA7:** O `commandTimeout: 30` é preservado em todas as queries dentro da transação (via default do Dapper ou explícito em `GerarCodigoUnicoAsync`)
- [ ] **CA8:** A geração de código único continua usando [`IIngressoRepository.GerarCodigoUnicoAsync`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs:59) com `transaction` e `commandTimeout: 30`
- [ ] **CA9:** Nenhum método dos repositórios existentes foi alterado (apenas reutilizados)
- [ ] **CA10:** Os 4 endpoints CRUD da Etapa 11a continuam funcionando sem alterações
- [ ] **CA11:** Teste manual: confirmar carrinho com cupom válido → 201 + ingressos criados
- [ ] **CA12:** Teste manual: confirmar carrinho com cupom inválido → 400 + rollback
- [ ] **CA13:** Teste manual: confirmar carrinho com capacidade insuficiente → 400 + rollback
- [ ] **CA14:** Teste manual: confirmar carrinho excedendo limite de 2 reservas → 400 + rollback

---

## 14. Estratégia de Rollback

### 14.1. Plano de Contingência

```bash
# Antes de iniciar, criar checkpoint
git add -A && git commit -m "checkpoint antes da Etapa 11b"

# Em caso de falha, reverter:
git checkout HEAD~1  # ou git revert HEAD

# Esforço estimado: ~15 minutos
```

### 14.2. Arquivos a Reverter

| Arquivo | Ação |
|---------|------|
| [`src/TicketPrime.Api/Services/CarrinhoService.cs`](src/TicketPrime.Api/Services/CarrinhoService.cs) | Reverter para versão anterior (remover método `ConfirmarAsync`, remover `IDbConnection` e `ICupomRepository` do construtor) |
| [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs) | Reverter para versão anterior (restaurar endpoint inline com transação) |

### 14.3. Impacto Durante Rollback

- Nenhum dado é perdido (rollback é apenas de código)
- O endpoint de confirmação volta a ser inline, exatamente como antes
- Os 4 endpoints CRUD da Etapa 11a **não são afetados** (suas alterações estão em arquivos separados)

---

## 15. Impacto no Program.cs

### 15.1. O que muda

**Antes (inline, ~200 linhas):**
```csharp
// Linhas 743-943: POST /api/carrinho/{cpf}/confirmar
// - Validações pré-transação
// - Transação manual com try/catch/rollback
// - SQL Dapper direto (cupom, itens, eventos, lotes, reservas, ingressos)
// - Geração de código único
// - Construção do CarrinhoConfirmacaoResponse
```

**Depois (~10 linhas):**
```csharp
// 5.4. Confirmar carrinho
app.MapPost("/api/carrinho/{cpf}/confirmar", async (CarrinhoService service, string cpf, [FromBody] ConfirmarCarrinhoRequest? request) =>
{
    var (response, erro, statusCode) = await service.ConfirmarAsync(cpf, request);
    if (erro is not null)
        return Results.Json(new { erro }, statusCode: statusCode);
    return Results.Created($"/api/carrinho/{cpf}/confirmar", response);
});
```

### 15.2. Linhas removidas de Program.cs

- **Linhas 744-943:** Endpoint inline `POST /api/carrinho/{cpf}/confirmar` (~200 linhas) — removidas por completo

### 15.3. Linhas adicionadas em Program.cs

- **~10 linhas** para o endpoint enxuto que delega ao `CarrinhoService.ConfirmarAsync()`

### 15.4. DI já existente (NÃO precisa ser alterado)

```csharp
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();    // Já existe (11a)
builder.Services.AddScoped<CarrinhoService>();                             // Já existe (11a)
builder.Services.AddScoped<ICupomRepository, CupomRepository>();           // Já existe (Etapa 3)
```

O DI do [`ICupomRepository`](src/TicketPrime.Api/Repositories/ICupomRepository.cs) já está registrado desde a Etapa 3 ([`Program.cs:22`](src/TicketPrime.Api/Program.cs:22)). Como o [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs) receberá `ICupomRepository` no construtor, o .NET resolve automaticamente. **Nenhuma linha nova de DI é necessária.**

### 15.5. Linhas NÃO alteradas

- Todos os outros endpoints (RF01-RF06, admin, health, etc.)
- Função `ConstruirResponseAsync` em [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs) — permanece como está
- Métodos CRUD da Etapa 11a (`CriarAsync`, `AdicionarItensAsync`, `ObterAtivoAsync`, `CancelarAsync`) — nenhuma alteração
- Repositórios existentes — nenhuma alteração

---

## 16. O que NÃO Será Alterado

| Item | Justificativa |
|------|---------------|
| [`CarrinhoService.CriarAsync`](src/TicketPrime.Api/Services/CarrinhoService.cs:41) | CRUD não transacional da Etapa 11a — intacto |
| [`CarrinhoService.AdicionarItensAsync`](src/TicketPrime.Api/Services/CarrinhoService.cs:85) | CRUD não transacional da Etapa 11a — intacto |
| [`CarrinhoService.ObterAtivoAsync`](src/TicketPrime.Api/Services/CarrinhoService.cs:177) | CRUD não transacional da Etapa 11a — intacto |
| [`CarrinhoService.CancelarAsync`](src/TicketPrime.Api/Services/CarrinhoService.cs:210) | CRUD não transacional da Etapa 11a — intacto |
| [`CarrinhoService.ConstruirResponseAsync`](src/TicketPrime.Api/Services/CarrinhoService.cs:242) | Método auxiliar privado da Etapa 11a — intacto |
| [`ICarrinhoRepository`](src/TicketPrime.Api/Repositories/ICarrinhoRepository.cs) | Nenhum método adicionado ou alterado |
| [`CarrinhoRepository`](src/TicketPrime.Api/Repositories/CarrinhoRepository.cs) | Nenhum método adicionado ou alterado |
| [`IReservaRepository`](src/TicketPrime.Api/Repositories/IReservaRepository.cs) | Nenhum método adicionado ou alterado |
| [`ReservaRepository`](src/TicketPrime.Api/Repositories/ReservaRepository.cs) | Nenhum método adicionado ou alterado |
| [`IIngressoRepository`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs) | Nenhum método adicionado ou alterado |
| [`IngressoRepository`](src/TicketPrime.Api/Repositories/IngressoRepository.cs) | Nenhum método adicionado ou alterado |
| [`IEventoRepository`](src/TicketPrime.Api/Repositories/IEventoRepository.cs) | Nenhum método adicionado ou alterado |
| [`EventoRepository`](src/TicketPrime.Api/Repositories/EventoRepository.cs) | Nenhum método adicionado ou alterado |
| [`ICupomRepository`](src/TicketPrime.Api/Repositories/ICupomRepository.cs) | Nenhum método adicionado ou alterado |
| [`CupomRepository`](src/TicketPrime.Api/Repositories/CupomRepository.cs) | Nenhum método adicionado ou alterado |
| [`ITipoIngressoRepository`](src/TicketPrime.Api/Repositories/ITipoIngressoRepository.cs) | Nenhum método adicionado ou alterado |
| [`TipoIngressoRepository`](src/TicketPrime.Api/Repositories/TipoIngressoRepository.cs) | Nenhum método adicionado ou alterado |
| [`ValidationException`](src/TicketPrime.Api/Middleware/ValidationException.cs) | Nenhuma alteração — mesmo uso |
| [`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs) | Nenhuma alteração |
| [`CarrinhoConfirmacaoResponse`](src/TicketPrime.Api/Models/CarrinhoConfirmacaoResponse.cs) | Nenhuma alteração |
| [`ReservaConfirmadaResponse`](src/TicketPrime.Api/Models/CarrinhoConfirmacaoResponse.cs:11) | Nenhuma alteração |
| [`ConfirmarCarrinhoRequest`](src/TicketPrime.Api/Models/ConfirmarCarrinhoRequest.cs) | Nenhuma alteração |
| Tabelas `Carrinhos`, `CarrinhoItens`, `Reservas`, `Ingressos` | Banco inalterado |
| Testes existentes | Nenhum teste é modificado |

---

## 17. Resumo Visual da Migração

```
ANTES (Program.cs):
┌──────────────────────────────────────────────────────────┐
│ POST /api/carrinho/{cpf}/confirmar (~200 linhas inline)  │
│                                                          │
│  1. Valida CPF, busca carrinho, valida expiração/itens   │
│  2. Inicia transação manual (BeginTransaction)            │
│  3. Query cupom (SQL direto)                             │
│  4. Query itens carrinho (SQL direto)                    │
│  5. Loop itens → loop unidades:                          │
│     - Query evento (SQL direto)                          │
│     - Query lote (SQL direto)                            │
│     - ReservaRepository.GerarCodigoUnicoAsync()          │
│     - Query limite reservas (SQL direto)                 │
│     - Query capacidade lote (SQL direto)                 │
│     - INSERT Reserva (SQL direto)                        │
│     - INSERT Ingresso (SQL direto)                       │
│  6. UPDATE Carrinho Status = Confirmado (SQL direto)     │
│  7. DELETE CarrinhoItens (SQL direto)                    │
│  8. Commit / Rollback                                    │
└──────────────────────────────────────────────────────────┘

DEPOIS (Program.cs ~10 linhas + CarrinhoService):
┌──────────────────────────────────────────────────────────┐
│ POST /api/carrinho/{cpf}/confirmar (~10 linhas)           │
│   → delega ao CarrinhoService.ConfirmarAsync()            │
└──────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────┐
│ CarrinhoService.ConfirmarAsync(cpf, request)              │
│                                                          │
│  Dependências: ICarrinhoRepo, IReservaRepo,              │
│                IIngressoRepo, IEventoRepo,                │
│                ICupomRepo, ITipoIngressoRepo, IDbConnection│
│                                                          │
│  1. Valida CPF, busca carrinho, valida expiração/itens   │
│     (fora da transação → ICarrinhoRepository)             │
│  2. Inicia transação (_db.BeginTransaction)               │
│  3. Valida cupom (ICupomRepository.ObterPorCodigoAsync)   │
│  4. Query itens carrinho (_db.QueryAsync direto)          │
│  5. Loop itens → loop unidades:                          │
│     - Evento (IEventoRepository.ObterPorIdAsync)          │
│     - Lote (ITipoIngressoRepository.ObterPorIdAsync)      │
│     - Limite (IReservaRepository.ContarPorCpfEEventoAsync)│
│     - Capacidade (IIngressoRepository.ContarPorTipoAsync) │
│     - INSERT Reserva (IReservaRepository.InserirAsync)    │
│     - Código único (IIngressoRepository.GerarCodigoUnico) │
│     - INSERT Ingresso (IIngressoRepository.InserirAsync)  │
│  6. Marcar Confirmado (ICarrinhoRepo.AtualizarStatusAsync)│
│  7. Limpar itens (ICarrinhoRepo.RemoverItensAsync)        │
│  8. Commit / Rollback                                     │
└──────────────────────────────────────────────────────────┘
```

## 18. Dependências a Adicionar no Construtor do CarrinhoService

**Construtor atual (Etapa 11a):**
```csharp
public CarrinhoService(
    ICarrinhoRepository carrinhoRepository,
    IUsuarioRepository usuarioRepository,
    IEventoRepository eventoRepository,
    ITipoIngressoRepository tipoIngressoRepository,
    IIngressoRepository ingressoRepository,
    IReservaRepository reservaRepository)
```

**Construtor novo (Etapa 11b):**
```csharp
public CarrinhoService(
    ICarrinhoRepository carrinhoRepository,
    IUsuarioRepository usuarioRepository,
    IEventoRepository eventoRepository,
    ITipoIngressoRepository tipoIngressoRepository,
    IIngressoRepository ingressoRepository,
    IReservaRepository reservaRepository,
    ICupomRepository cupomRepository,       // NOVO
    IDbConnection db)                       // NOVO
```

**Justificativa para `IDbConnection`:**
- Necessário para gerenciar a transação (`BeginTransaction()`, `Commit()`, `Rollback()`)
- Necessário para queries diretas que não têm equivalente em repositório (ex: buscar itens do carrinho com `SELECT Id, EventoId, TipoIngressoId, Quantidade, PrecoUnitario FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId`)
- **Alternativa rejeitada:** Adicionar método `ObterItensPorCarrinhoIdAsync` ao `ICarrinhoRepository`. Embora possível, isso aumentaria a interface do repositório com um método que só é usado na confirmação. O método `ObterItensResponseAsync` já existe mas retorna `CarrinhoItemResponse` (com joins), não os dados crus necessários para o loop de processamento. A query direta é mais simples e mantém o repositório focado em CRUD.

**Justificativa para `ICupomRepository`:**
- Necessário para validar o cupom dentro da transação
- Já está registrado no DI desde a Etapa 3
- `ObterPorCodigoAsync` retorna o `Cupom` com `PorcentagemDesconto` e `ValorMinimoRegra`

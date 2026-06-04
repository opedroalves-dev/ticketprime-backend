# Planejamento — Etapa 11a: Migrar Domínio Carrinho CRUD (Não Transacional)

> **Parte da correção C1** (aprovada pelo V4 em 03/06/2026): Dividir o domínio Carrinho em duas etapas — **11a (CRUD não transacional)** e **11b (confirmação transacional)**.
>
> **Base:** [`plans/fase2-plano.md`](plans/fase2-plano.md) (seção Etapa 11a, linhas 477-501)
>
> **Stack:** .NET 8, Minimal API, Dapper, SQL Server
> **Risco:** Médio | **Correção:** C1, C6

---

## 1. Objetivo

Migrar os **4 endpoints CRUD (não transacionais)** do domínio Carrinho — atualmente implementados com SQL e validação **inline** em [`Program.cs`](src/TicketPrime.Api/Program.cs) — para a arquitetura **Repository + Service**, seguindo o mesmo padrão estabelecido nas Etapas 3-9.

O endpoint de **confirmação** ([`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:951)) **não será migrado nesta etapa** — ele permanece inline em [`Program.cs`](src/TicketPrime.Api/Program.cs) e será tratado na Etapa 11b por envolver transação multi-domínio (separação de risco C1).

---

## 2. Arquivos Criados

### 2.1. [`src/TicketPrime.Api/Repositories/ICarrinhoRepository.cs`](src/TicketPrime.Api/Repositories/ICarrinhoRepository.cs) (novo)

Interface seguindo a **convenção C6** (todos os métodos com `IDbTransaction? transaction = null` como último parâmetro):

| Método | Descrição | SQL encapsulado |
|--------|-----------|-----------------|
| `ObterPorIdAsync(int id, ...)` | Retorna carrinho pelo ID (usado no POST itens) | `SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE Id = @Id` |
| `ObterAtivoPorCpfAsync(string cpf, ...)` | Retorna carrinho ativo de um CPF (GET e DELETE) | `SELECT ... FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'` |
| `ObterAtivoOuExpiradoPorCpfAsync(string cpf, ...)` | Retorna o carrinho mais recente (ativo ou expirado) de um CPF (GET visualizar usa `Status IN ('Ativo','Expirado')`) | `SELECT ... FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status IN ('Ativo','Expirado') ORDER BY Id DESC` |
| `CriarAsync(string usuarioCpf, ...)` | Insere carrinho com `Status='Ativo'` e `DataExpiracao = DATEADD(MINUTE, 15, GETDATE())`. Retorna o Id gerado. | `INSERT INTO Carrinhos ... OUTPUT INSERTED.Id ...` |
| `AtualizarStatusAsync(int id, string status, ...)` | Atualiza status do carrinho (usado para expirar e limpar) | `UPDATE Carrinhos SET Status = @Status WHERE Id = @Id` |
| `ExisteAtivoPorCpfAsync(string cpf, ...)` | Verifica se CPF já tem carrinho ativo | `SELECT COUNT(1) FROM Carrinhos WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'` |
| `ObterItensResponseAsync(int carrinhoId, ...)` | Retorna itens com `NomeEvento`, `NomeLote` e `Subtotal` para montar o response | `SELECT ci.Id, ci.EventoId, e.Nome AS NomeEvento, ci.TipoIngressoId, ti.Nome AS NomeLote, ci.Quantidade, ci.PrecoUnitario, (ci.Quantidade * ci.PrecoUnitario) AS Subtotal FROM CarrinhoItens ci INNER JOIN Eventos e ON e.Id = ci.EventoId LEFT JOIN TiposIngresso ti ON ti.Id = ci.TipoIngressoId WHERE ci.CarrinhoId = @CarrinhoId` |
| `InserirItemAsync(CarrinhoItem item, ...)` | Insere um item no carrinho | `INSERT INTO CarrinhoItens (CarrinhoId, EventoId, TipoIngressoId, Quantidade, PrecoUnitario) VALUES (...)` |
| `RemoverItensAsync(int carrinhoId, ...)` | Remove todos os itens de um carrinho (DELETE limpar) | `DELETE FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId` |
| `ContarItensAsync(int carrinhoId, ...)` | Conta itens de um carrinho (validação) | `SELECT COUNT(1) FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId` |

**Total: 10 métodos** — todos com `IDbTransaction? transaction = null` (C6). O parâmetro é opcional para operações CRUD simples, mas **essencial** na Etapa 11b quando a confirmação precisar participar da transação multi-domínio.

### 2.2. [`src/TicketPrime.Api/Repositories/CarrinhoRepository.cs`](src/TicketPrime.Api/Repositories/CarrinhoRepository.cs) (novo)

Implementação concreta que:
- Injeta `IDbConnection _db` via construtor
- Usa **Dapper** (`QueryAsync`, `QuerySingleOrDefaultAsync`, `ExecuteAsync`, `ExecuteScalarAsync`)
- Todos os SQLs com **parâmetros nomeados** (`@param`) — sem concatenação, sem interpolação
- Todos os métodos repassam `transaction` ao Dapper (C6)

### 2.3. [`src/TicketPrime.Api/Services/CarrinhoService.cs`](src/TicketPrime.Api/Services/CarrinhoService.cs) (novo)

Service que orquestra validações e chamadas aos repositórios **apenas para CRUD não transacional**.

**Injeção de dependências:**
```csharp
public class CarrinhoService
{
    private readonly ICarrinhoRepository _carrinhoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly ITipoIngressoRepository _tipoIngressoRepository;
    private readonly IIngressoRepository _ingressoRepository;
    private readonly IDbConnection _db;

    public CarrinhoService(
        ICarrinhoRepository carrinhoRepository,
        IUsuarioRepository usuarioRepository,
        IEventoRepository eventoRepository,
        ITipoIngressoRepository tipoIngressoRepository,
        IIngressoRepository ingressoRepository,
        IDbConnection db)
    { ... }
}
```

**Métodos CRUD (4 operações):**

#### a) `CriarAsync(CriarCarrinhoRequest request) -> (CarrinhoResponse? Response, string? Erro)`

Substitui o endpoint [`POST /api/carrinho`](src/TicketPrime.Api/Program.cs:735).

Fluxo:
1. Validar CPF (formato 11 dígitos, não vazio)
2. Buscar usuário via `_usuarioRepository.ObterPorCpfAsync()` — retorna erro 400 se não existir
3. Verificar se já existe carrinho ativo via `_carrinhoRepository.ExisteAtivoPorCpfAsync()`
4. Se existir carrinho ativo:
   a. Obter o carrinho via `_carrinhoRepository.ObterAtivoPorCpfAsync()`
   b. Se expirado (`DataExpiracao <= DateTime.Now`), atualizar status para `'Expirado'` via `_carrinhoRepository.AtualizarStatusAsync()`
   c. Se **não** expirado, retornar erro "Já existe um carrinho ativo para este CPF."
5. Criar novo carrinho via `_carrinhoRepository.CriarAsync()` — que define `Status='Ativo'` e `DataExpiracao = NOW + 15min`
6. Montar `CarrinhoResponse` completo (com itens vazio, total = 0, minutosRestantes calculado)
7. Retornar `Results.Created(...)`

#### b) `AdicionarItensAsync(int carrinhoId, AdicionarItensRequest request) -> (CarrinhoResponse? Response, string? Erro, int StatusCode)`

Substitui o endpoint [`POST /api/carrinho/{id}/itens`](src/TicketPrime.Api/Program.cs:785).

Fluxo:
1. Buscar carrinho via `_carrinhoRepository.ObterPorIdAsync()` — erro 404 se não existir
2. Validar status do carrinho (`Status != "Ativo"` → erro 400)
3. Validar expiração (`DataExpiracao <= DateTime.Now` → expirar e erro 400)
4. Validar lista de itens (não nula, não vazia)
5. Para **cada item**:
   a. Validar `EventoId > 0`
   b. Buscar evento via `_eventoRepository.ObterPorIdAsync()` — erro 400 se não existir
   c. Validar `Quantidade > 0`
   d. Determinar `PrecoUnitario`:
      - Se `TipoIngressoId` informado: buscar via `_tipoIngressoRepository.ObterPorIdAsync()`, validar se pertence ao evento, verificar disponibilidade do lote (consultar ingressos vendidos + itens em outros carrinhos ativos)
      - Senão: usar `evento.PrecoPadrao`
   e. Verificar limite de 2 reservas por CPF por evento (consultar tabela `Reservas`)
   f. Inserir item via `_carrinhoRepository.InserirItemAsync()`
6. Montar `CarrinhoResponse` completo via método auxiliar
7. Retornar `Results.Ok(response)`

#### c) `ObterAtivoAsync(string cpf) -> (CarrinhoResponse? Response, string? Erro)`

Substitui o endpoint [`GET /api/carrinho/{cpf}`](src/TicketPrime.Api/Program.cs:885).

Fluxo:
1. Validar formato do CPF — se inválido, retorna `"CPF deve conter 11 dígitos numéricos."` (mapeado para 400 pelo endpoint)
2. Buscar carrinho (ativo ou expirado) via `_carrinhoRepository.ObterAtivoOuExpiradoPorCpfAsync()`
3. Se não encontrado → retorna `"Carrinho não encontrado para este CPF."` (mapeado para 404 pelo endpoint)
4. Se ativo mas expirado (`DataExpiracao <= DateTime.Now`):
   a. Atualizar status para `'Expirado'` via `_carrinhoRepository.AtualizarStatusAsync()`
   b. Incluir mensagem "Carrinho expirado. Crie um novo carrinho para continuar."
5. Montar `CarrinhoResponse` completo
6. Retornar `Results.Ok(response)`

#### d) `CancelarAsync(string cpf) -> (bool Sucesso, string? Erro)`

Substitui o endpoint [`DELETE /api/carrinho/{cpf}`](src/TicketPrime.Api/Program.cs:921).

Fluxo:
1. Validar formato do CPF
2. Buscar carrinho ativo via `_carrinhoRepository.ObterAtivoPorCpfAsync()` — erro 404 se não existir
3. Validar expiração (se já expirou sem ter sido marcado, atualizar status)
4. Remover itens via `_carrinhoRepository.RemoverItensAsync()`
5. Atualizar status para `'Expirado'` via `_carrinhoRepository.AtualizarStatusAsync()`
6. Retornar `Results.NoContent()`

#### Método auxiliar interno: `ConstruirResponseAsync(int carrinhoId) -> CarrinhoResponse`

Extraído da função inline [`ConstruirCarrinhoResponseAsync`](src/TicketPrime.Api/Program.cs:698) para dentro do service (como método privado). Mantém a mesma lógica:
- Busca dados do carrinho
- Busca itens com joins (Eventos, TiposIngresso)
- Calcula `Total = Sum(Subtotal)`
- Calcula `MinutosRestantes` (se ativo)

---

## 3. Arquivos Alterados

### 3.1. [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs)

**a) Registrar DI do novo repositório e service:**
```csharp
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
builder.Services.AddScoped<CarrinhoService>();
```

**b) Substituir os 4 endpoints inline por chamadas ao service:**

**Antes (inline, ~247 linhas no total):**
```csharp
// Linha 698-732: ConstruirCarrinhoResponseAsync (função auxiliar)
// Linha 735-782: POST /api/carrinho
// Linha 785-882: POST /api/carrinho/{id}/itens
// Linha 885-918: GET /api/carrinho/{cpf}
// Linha 921-948: DELETE /api/carrinho/{cpf}
```

**Depois (~40 linhas):**
```csharp
// 5.1. Criar carrinho vazio
app.MapPost("/api/carrinho", async (CarrinhoService service, [FromBody] CriarCarrinhoRequest request) =>
{
    var (response, erro) = await service.CriarAsync(request);
    return erro is not null
        ? Results.BadRequest(new { erro })
        : Results.Created($"/api/carrinho/{response!.CarrinhoId}", response);
});

// 5.2. Adicionar itens ao carrinho
app.MapPost("/api/carrinho/{id}/itens", async (CarrinhoService service, int id, [FromBody] AdicionarItensRequest request) =>
{
    var (response, erro, statusCode) = await service.AdicionarItensAsync(id, request);
    if (erro is not null)
        return Results.Json(new { erro }, statusCode: statusCode);
    return Results.Ok(response);
});

// 5.3. Visualizar carrinho ativo
app.MapGet("/api/carrinho/{cpf}", async (CarrinhoService service, string cpf) =>
{
    var (response, erro) = await service.ObterAtivoAsync(cpf);
    if (erro is not null)
        return Results.Json(new { erro }, statusCode: erro switch
        {
            string e when e.Contains("dígitos") => 400,
            _ => 404
        });

    return Results.Ok(response);
});

// 5.4. Limpar carrinho
app.MapDelete("/api/carrinho/{cpf}", async (CarrinhoService service, string cpf) =>
{
    var (sucesso, erro) = await service.CancelarAsync(cpf);
    if (!sucesso)
        return Results.Json(new { erro }, statusCode: erro switch
        {
            string e when e.Contains("obrigatório") || e.Contains("dígitos") => 400,
            _ => 404
        });
    return Results.NoContent();
});
```

**c) Manter o endpoint de confirmação [`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:951) intacto.**
- Este endpoint continua usando `IDbConnection db` diretamente, com sua transação manual `try/catch/rollback`
- A função auxiliar `ConstruirCarrinhoResponseAsync` é removida de [`Program.cs`](src/TicketPrime.Api/Program.cs) (movida para `CarrinhoService`)
- O endpoint de confirmação precisará de ajuste na Etapa 11b para não quebrar, mas na Etapa 11a ele permanece funcional porque:
  - Ele **não** usa `ConstruirCarrinhoResponseAsync` (ele monta `CarrinhoConfirmacaoResponse`, não `CarrinhoResponse`)
  - Ele usa `IDbConnection` diretamente, que continua disponível via DI

---

## 4. Dependências

### 4.1. Pré-requisitos (JÁ EXECUTADOS)

| Etapa | Dependência | Status esperado |
|:-----:|-------------|:---------------:|
| **Etapa 2** | Convenção C6 estabelecida | Todos os repositórios existentes têm `IDbTransaction?` |
| **Etapa 3** | [`IUsuarioRepository`](src/TicketPrime.Api/Repositories/IUsuarioRepository.cs) / [`UsuarioRepository`](src/TicketPrime.Api/Repositories/UsuarioRepository.cs) | `ObterPorCpfAsync()` disponível |
| **Etapa 5** | [`IEventoRepository`](src/TicketPrime.Api/Repositories/IEventoRepository.cs) / [`EventoRepository`](src/TicketPrime.Api/Repositories/EventoRepository.cs) | `ObterPorIdAsync()` disponível |
| **Etapa 7** | [`ITipoIngressoRepository`](src/TicketPrime.Api/Repositories/ITipoIngressoRepository.cs) / [`TipoIngressoRepository`](src/TicketPrime.Api/Repositories/TipoIngressoRepository.cs) | `ObterPorIdAsync()` disponível |
| **Etapa 8** | [`IIngressoRepository`](src/TicketPrime.Api/Repositories/IIngressoRepository.cs) | `GerarCodigoUnicoAsync()` disponível (não usado na 11a, mas necessário para verificação de disponibilidade de lote) |

### 4.2. Repositórios Reutilizados

| Repositório | Métodos usados | Onde |
|-------------|----------------|------|
| `IUsuarioRepository` | `ObterPorCpfAsync()` | `CarrinhoService.CriarAsync()` — validar se usuário existe |
| `IEventoRepository` | `ObterPorIdAsync()` | `CarrinhoService.AdicionarItensAsync()` — validar evento de cada item |
| `ITipoIngressoRepository` | `ObterPorIdAsync()` | `CarrinhoService.AdicionarItensAsync()` — validar lote de cada item |
| `IIngressoRepository` | (indireto via SQL de contagem) | Disponibilidade de lote — consulta inline via `_db.ExecuteScalarAsync` ou via repositório |
| `ICarrinhoRepository` | Todos os 10 métodos | Toda operação CRUD |

### 4.3. Dependências da Etapa 11b

A Etapa 11a **não depende** da Etapa 10b (Reservas). O `CarrinhoService` da 11a faz a verificação de limite de 2 reservas por CPF/evento diretamente via SQL no repositório (consultando a tabela `Reservas`), sem depender de `IReservaRepository` — isso porque a Etapa 10b ainda não foi executada ou, se foi, a interface existe.

**Nota de compatibilidade:** Se a Etapa 10b já foi executada, o `CarrinhoService` pode (opcionalmente) receber `IReservaRepository` por DI para reutilizar `ContarPorCpfEEventoAsync()`. Caso contrário, a verificação é feita via `IDbConnection` diretamente no método. **Decisão desta etapa:** usar `IDbConnection` diretamente para a verificação de limite, para não criar dependência da Etapa 10b e permitir paralelismo (conforme plano original: "Etapa 11a não depende da Etapa 10b — pode avançar em paralelo").

---

## 5. Riscos

| # | Risco | Probabilidade | Impacto | Mitigação |
|:-:|-------|:-------------:|:-------:|-----------|
| R1 | **Quebrar contrato do endpoint** ao mudar de `IDbConnection` inline para service | Baixa | Alto | Manter mesmas validações, mesmas mensagens de erro, mesmos HTTP status codes. Testar manualmente cada endpoint após migração. |
| R2 | **Esquecer de registrar DI** do `CarrinhoService` ou `ICarrinhoRepository` | Média | Médio | Checklist incluir verificação de `builder.Services.AddScoped<...>()` |
| R3 | **Endpoint de confirmação (11b) quebrar** porque a função `ConstruirCarrinhoResponseAsync` foi removida de [`Program.cs`](src/TicketPrime.Api/Program.cs) | Baixa | Alto | O endpoint de confirmação **não usa** `ConstruirCarrinhoResponseAsync` — ele monta `CarrinhoConfirmacaoResponse` diretamente. A função removida só é usada pelos 4 endpoints CRUD. |
| R4 | **Mudança de comportamento** na verificação de disponibilidade de lote | Baixa | Médio | Extrair a lógica exata do inline, sem alterar regras. A consulta de "reservados em carrinhos ativos" deve ser idêntica. |
| R5 | **Commit misturar 11a + 11b** (violação C1) | Média | Médio | **Proibido** tocar no endpoint `POST /api/carrinho/{cpf}/confirmar`. Se houver necessidade de alteração, criar checkpoint separado. |

---

## 6. Critérios de Aceite

- [ ] **CA1:** `dotnet build` compila sem erros
- [ ] **CA2:** `dotnet test` passa todos os testes existentes (103/103) **sem modificações**
- [ ] **CA3:** Nenhum endpoint CRUD mudou de rota, método HTTP, request body ou response body
- [ ] **CA4:** Nenhuma regra de negócio foi alterada (mesmas validações, mesmos cálculos)
- [ ] **CA5:** Nenhuma tabela, coluna, constraint, índice ou view foi alterada no banco
- [ ] **CA6:** O endpoint [`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:951) permanece **intacto** em [`Program.cs`](src/TicketPrime.Api/Program.cs) — sem qualquer alteração
- [ ] **CA7:** Convenção C6 respeitada — todos os métodos do `CarrinhoRepository` possuem `IDbTransaction? transaction = null`
- [ ] **CA8:** A função `ConstruirCarrinhoResponseAsync` foi removida de [`Program.cs`](src/TicketPrime.Api/Program.cs) (movida para `CarrinhoService`)
- [ ] **CA9:** Teste manual dos 4 endpoints via curl:
  - `POST /api/carrinho` → 201 Created (carrinho novo), 400 BadRequest (CPF inválido, usuário inexistente, carrinho ativo existente)
  - `POST /api/carrinho/{id}/itens` → 200 OK, 404 (carrinho inexistente), 400 (carrinho expirado, capacidade insuficiente)
  - `GET /api/carrinho/{cpf}` → 200 OK (carrinho ativo/expirado), 400 (CPF inválido), 404 (sem carrinho)
  - `DELETE /api/carrinho/{cpf}` → 204 No Content, 404 (sem carrinho ativo)

---

## 7. Rollback

```bash
# Antes de iniciar, criar checkpoint
git add -A && git commit -m "checkpoint antes da Etapa 11a"

# Em caso de falha, reverter os arquivos criados/modificados:
git checkout HEAD~1  # ou git revert

# Esforço estimado: ~25 minutos
```

**Arquivos a reverter:**
- Remover: `Repositories/ICarrinhoRepository.cs`, `Repositories/CarrinhoRepository.cs`, `Services/CarrinhoService.cs`
- Reverter: `Program.cs` (restaurar endpoints inline + função auxiliar)
- Remover DI adicionada em `Program.cs`

---

## 8. Impacto no [`Program.cs`](src/TicketPrime.Api/Program.cs)

### 8.1. Linhas removidas
- **Linhas 697-732:** Função auxiliar `ConstruirCarrinhoResponseAsync` → removida (movida para `CarrinhoService` como método privado)
- **Linhas 735-782:** Endpoint `POST /api/carrinho` → substituído por 4 linhas
- **Linhas 785-882:** Endpoint `POST /api/carrinho/{id}/itens` → substituído por 7 linhas
- **Linhas 885-918:** Endpoint `GET /api/carrinho/{cpf}` → substituído por 6 linhas
- **Linhas 921-948:** Endpoint `DELETE /api/carrinho/{cpf}` → substituído por 11 linhas

**Total removido: ~112 linhas**

### 8.2. Linhas adicionadas
- **2 linhas** de registro DI (`AddScoped<ICarrinhoRepository, CarrinhoRepository>()` + `AddScoped<CarrinhoService>()`)
- **~28 linhas** para os 4 endpoints enxutos

**Saldo líquido: ~82 linhas a menos em [`Program.cs`](src/TicketPrime.Api/Program.cs)**

### 8.3. Linhas NÃO alteradas
- **Linhas 951-1150:** Endpoint `POST /api/carrinho/{cpf}/confirmar` — **intacto**
- Este endpoint continua usando `IDbConnection db` e `IIngressoRepository` diretamente, com sua transação `try/catch/rollback`
- Na Etapa 11b, este endpoint será migrado para `CarrinhoService.ConfirmarAsync()`

---

## 9. O que NÃO será alterado

| Item | Justificativa |
|------|---------------|
| [`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:951) | **C1:** confirmação transacional fica para Etapa 11b |
| [`CarrinhoConfirmacaoResponse`](src/TicketPrime.Api/Models/CarrinhoConfirmacaoResponse.cs) | Modelo usado apenas pela confirmação (11b) |
| [`ConfirmarCarrinhoRequest`](src/TicketPrime.Api/Models/ConfirmarCarrinhoRequest.cs) | Usado apenas pela confirmação (11b) |
| [`CarrinhoRequest`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) | Já existe — contém `CarrinhoItemRequest` que é reutilizado via `AdicionarItensRequest` |
| [`Carrinho`](src/TicketPrime.Api/Models/Carrinho.cs) + [`CarrinhoItem`](src/TicketPrime.Api/Models/CarrinhoItem.cs) | Modelos de domínio já existentes, usados pelo repository |
| [`CarrinhoResponse`](src/TicketPrime.Api/Models/CarrinhoResponse.cs) + [`CarrinhoItemResponse`](src/TicketPrime.Api/Models/CarrinhoItemResponse.cs) | DTOs já existentes, usados pelo service |
| [`CriarCarrinhoRequest`](src/TicketPrime.Api/Models/CriarCarrinhoRequest.cs) | Já extraído na Etapa 1 |
| [`AdicionarItensRequest`](src/TicketPrime.Api/Models/AdicionarItensRequest.cs) | Já extraído na Etapa 1 |
| Tabelas `Carrinhos` e `CarrinhoItens` | Banco inalterado (CA5) |
| Testes existentes | Nenhum teste é modificado (CA2) |

---

## 10. Como a Etapa 11a prepara a Etapa 11b

### 10.1. Base para o `CarrinhoService.ConfirmarAsync()`

A Etapa 11a cria o [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs) com os métodos CRUD. A Etapa 11b adicionará **apenas o método** `ConfirmarAsync()` a este mesmo service, sem modificar os métodos CRUD já testados.

### 10.2. Repositório com C6 pronto para transação

O [`ICarrinhoRepository`](src/TicketPrime.Api/Repositories/ICarrinhoRepository.cs) criado na 11a já possui **todos os métodos com `IDbTransaction? transaction = null`** (C6). Isso significa que na Etapa 11b, quando o `CarrinhoService.ConfirmarAsync()` abrir uma transação com `_db.BeginTransaction()`, ele poderá chamar:

```csharp
// CarrinhoService.ConfirmarAsync() — Etapa 11b
using var transaction = _db.BeginTransaction();

// Todos os repositórios recebem a mesma transação
await _carrinhoRepository.AtualizarStatusAsync(carrinho.Id, "Confirmado", transaction);
await _carrinhoRepository.RemoverItensAsync(carrinho.Id, transaction);
await _reservaRepository.InserirAsync(reserva, transaction);
// ... etc

transaction.Commit();
```

Sem a Etapa 11a, a Etapa 11b teria que criar o repositório do zero **e** lidar com a transação ao mesmo tempo — o que viola o princípio de um problema por etapa (C1).

### 10.3. Separação clara de responsabilidades

| Responsabilidade | Etapa 11a | Etapa 11b |
|------------------|:---------:|:---------:|
| CRUD de carrinho (criar, adicionar itens, visualizar, limpar) | ✅ | ❌ |
| Validação de CPF, usuário, evento, lote, disponibilidade | ✅ | ❌ |
| Cálculo de expiração (15 min) | ✅ | ❌ |
| Confirmação transacional com criação de reservas + ingressos | ❌ | ✅ |
| Gerenciamento de transação (`BeginTransaction`/`Commit`/`Rollback`) | ❌ | ✅ |
| Integração com `IReservaRepository`, `IIngressoRepository`, `ICupomRepository` | ❌ | ✅ |

### 10.4. O que a Etapa 11b NÃO precisará refazer

- ❌ **Não** precisará criar `ICarrinhoRepository` ou `CarrinhoRepository` (já existem da 11a)
- ❌ **Não** precisará mover a função `ConstruirCarrinhoResponseAsync` (já está no service da 11a)
- ❌ **Não** precisará se preocupar com a convenção C6 (já estabelecida na 11a)
- ✅ **Apenas** adicionará `ConfirmarAsync()` ao `CarrinhoService` existente
- ✅ **Apenas** substituirá o endpoint inline em [`Program.cs`](src/TicketPrime.Api/Program.cs)

### 10.5. Risco zero de misturar CRUD com transação

Como a Etapa 11a é comitada **antes** de iniciar a 11b, não há risco de um commit único misturar as duas responsabilidades. O V4 aprovou explicitamente esta separação (C1).

---

## Resumo Visual da Migração

```
ANTES (Program.cs ~247 linhas de carrinho inline):
┌─────────────────────────────────────────────────────┐
│ ConstruirCarrinhoResponseAsync (função auxiliar)     │
│ POST /api/carrinho         (47 linhas SQL+validação) │
│ POST /api/carrinho/{id}/itens (97 linhas)            │
│ GET  /api/carrinho/{cpf}   (33 linhas)               │
│ DELETE /api/carrinho/{cpf} (28 linhas)               │
│ POST /api/carrinho/{cpf}/confirmar (200 linhas) ← 11b│
└─────────────────────────────────────────────────────┘

DEPOIS (Program.cs ~40 linhas):
┌─────────────────────────────────────────────────────┐
│ POST /api/carrinho         (4 linhas → CarrinhoService)│
│ POST /api/carrinho/{id}/itens (7 linhas → service)    │
│ GET  /api/carrinho/{cpf}   (6 linhas → service)       │
│ DELETE /api/carrinho/{cpf} (11 linhas → service)      │
│ POST /api/carrinho/{cpf}/confirmar (200 linhas) ← 11b │
└─────────────────────────────────────────────────────┘

NOVOS ARQUIVOS:
┌──────────────────────────────┐
│ ICarrinhoRepository.cs (C6)  │ ← 10 métodos
│ CarrinhoRepository.cs        │ ← Dapper + SQL
│ CarrinhoService.cs           │ ← 4 métodos CRUD
└──────────────────────────────┘
```

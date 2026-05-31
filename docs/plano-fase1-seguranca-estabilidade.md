# Plano de Execução — Fase 1: Segurança e Estabilidade

**Projeto:** TicketPrime  
**Data:** 2026-05-28  
**Versão:** 2.0 (revisado após auditoria arquitetural)  
**Responsável:** Arquitetura de Software  

---

## Sumário

1. [Resumo das correções da auditoria](#resumo-das-correções-da-auditoria)
2. [Item 0 — Criar/revisar .gitignore](#item-0--criarrevisar-gitignore)
3. [Item 1 — Remover senha hardcoded do appsettings.json](#item-1--remover-senha-hardcoded-do-appsettingsjson)
4. [Item 2 — Implementar autenticação mínima para endpoints admin (API Key)](#item-2--implementar-autenticação-mínima-para-endpoints-admin-api-key)
5. [Item 3 — Adicionar middleware global de exception handling](#item-3--adicionar-middleware-global-de-exception-handling)
6. [Item 4 — Adicionar transações no fluxo de confirmação de carrinho](#item-4--adicionar-transações-no-fluxo-de-confirmação-de-carrinho)
7. [Item 5 — Corrigir interpolações SQL inseguras](#item-5--corrigir-interpolações-sql-inseguras)
8. [Item 6 — Configurar CORS para futuro frontend](#item-6--configurar-cors-para-futuro-frontend)
9. [Item 7 — Endpoint /health](#item-7--endpoint-health)
10. [Item 8 — Logging de acessos não autorizados](#item-8--logging-de-acessos-não-autorizados)
11. [Ordem de implementação recomendada](#ordem-de-implementação-recomendada)
12. [Resumo de dependências](#resumo-de-dependências)
13. [Impacto nos testes](#impacto-nos-testes)
14. [Critérios de aceite globais](#critérios-de-aceite-globais)

---

## Resumo das correções da auditoria

### Problemas críticos corrigidos nesta versão

| ID | Problema | Correção aplicada |
|:--:|----------|-------------------|
| **C1** | `return Results.BadRequest(...)` dentro da transação **não dispara o `catch`**, deixando dados órfãos | Adotar exceção customizada (`ValidationException`) que é capturada pelo `catch`, garantindo rollback automático |
| **C2** | Plano assumia existência de `.gitignore` | Adicionado **Passo 0** obrigatório: criar/revisar `.gitignore` antes de qualquer alteração |

### Problemas altos corrigidos nesta versão

| ID | Problema | Correção aplicada |
|:--:|----------|-------------------|
| **A1** | Inconsistência: Item 2 mencionava `JwtBearer` mas implementava API Key | Estratégia **unificada**: apenas **API Key** (header `X-Api-Key`), sem JWT. Justificativa na seção |
| **A2** | `BadHttpRequestException` era transformada em 500 pelo middleware | Middleware agora preserva status 400 para `BadHttpRequestException` |
| **A3** | Estratégia `DROP VIEW` + `CREATE VIEW` frágil (perda de view se CREATE falhar) | Substituído por `CREATE OR ALTER VIEW` (SQL Server 2016+) — seguro e atômico |

### Tarefas faltantes adicionadas

| ID | Tarefa | Item |
|:--:|--------|:----:|
| **F1** | Criar/revisar `.gitignore` | Item 0 |
| **F2** | Corrigir estratégia de rollback das transações | Item 4 (C1) |
| **F3** | Tratamento correto para `BadHttpRequestException` | Item 3 (A2) |
| **F4** | Adicionar endpoint `/health` | Item 7 |
| **F5** | Adicionar logging de acessos não autorizados | Item 8 |
| **F6** | Definir timeout para transações | Item 4 |

---

## Item 0 — Criar/revisar .gitignore

### Objetivo

Garantir que arquivos com credenciais, segredos e artefatos de build **nunca sejam versionados**. Este é um pré-requisito obrigatório **anterior** a qualquer alteração de configuração ou segurança.

### Estado atual

**Não existe arquivo `.gitignore` no repositório.** Isso significa que:
- `appsettings.Development.json` (que conterá a senha) seria versionado
- Pastas `bin/`, `obj/`, `node_modules/` seriam versionadas
- Qualquer segredo adicionado acidentalmente ficaria exposto no histórico do Git

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| `.gitignore` | **NOVO** — na raiz do repositório |

### Conteúdo do .gitignore

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.cache
*.dll
*.exe
*.pdb
*.vs/

# Configuração com credenciais (nunca versionar)
appsettings.Development.json
appsettings.Staging.json

# User Secrets (diretório local)
%APPDATA%/Microsoft/UserSecrets/
~/.microsoft/usersecrets/

# IDE
.idea/
.vscode/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# Logs
*.log
```

### Dependências

Nenhuma.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Arquivos sensíveis já versionados antes do .gitignore | Média | Alto | Usar `git rm --cached` para remover do tracking sem deletar local |
| Desenvolvedor ignorar .gitignore e versionar secrets manualmente | Baixa | Alto | Adicionar validação em pré-commit hook ou CI |

### Estratégia de rollback

Remover o arquivo `.gitignore` via `git rm --cached .gitignore` (mantendo local) ou reverter o commit.

### Critérios de aceite

- [x] `.gitignore` existe na raiz do repositório
- [x] `appsettings.Development.json` está listado no `.gitignore`
- [x] Pastas `bin/`, `obj/` estão listadas no `.gitignore`
- [x] Nenhum arquivo sensível está sendo trackeado pelo Git após a criação do `.gitignore`
- [x] `git status` não mostra arquivos de build ou configuração de desenvolvimento

---

## Item 1 — Remover senha hardcoded do appsettings.json

### Objetivo

Eliminar a exposição da senha do SQL Server em texto plano no arquivo de configuração versionado, substituindo por uma fonte segura de segredos.

### Estado atual

No arquivo [`src/TicketPrime.Api/appsettings.json`](../src/TicketPrime.Api/appsettings.json:10), a connection string contém a senha em texto plano:

```json
"DefaultConnection": "Server=localhost;Database=TicketPrimeDb;User Id=sa;Password=Ph*130206;TrustServerCertificate=True;"
```

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/appsettings.json`](../src/TicketPrime.Api/appsettings.json) | Remover `Password` da connection string |
| [`src/TicketPrime.Api/appsettings.Development.json`](../src/TicketPrime.Api/appsettings.Development.json) | Manter connection string completa (protegido pelo `.gitignore` do Item 0) |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:7) | Adicionar lógica para ler do **User Secrets** (desenvolvimento) ou **variáveis de ambiente** (produção) |

### Alterações necessárias

1. **Connection string** → Mover a connection string completa com senha para `UserSecrets` no ambiente de desenvolvimento.
2. **appsettings.json** → Remover `Password` da connection string ou usar um placeholder.
3. **appsettings.Development.json** → Conterá a connection string completa, protegido pelo `.gitignore` criado no Item 0.
4. **Program.cs** → A leitura da connection string continua a mesma via `builder.Configuration.GetConnectionString("DefaultConnection")`, pois o .NET resolve a hierarquia: `UserSecrets` > `appsettings.Development.json` > `appsettings.json`.

### Dependências

Nenhuma. O `Microsoft.Extensions.Configuration.UserSecrets` já está disponível no SDK do .NET 8.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Connection string não encontrada em produção | Baixa | Alto | Configurar variável de ambiente `ConnectionStrings__DefaultConnection` no ambiente de produção |
| Desenvolvedor esquecer de configurar User Secrets | Média | Médio | Documentar no `README.md` o comando `dotnet user-secrets set` |
| appsettings.Development.json versionado acidentalmente | Baixa | Alto | **Mitigado pelo Item 0** — `.gitignore` já protege este arquivo |
| Quebra de testes de integração que usam a connection string | Baixa | Médio | Testes de integração devem configurar sua própria connection string via ambiente ou `appsettings.Testing.json` |

### Estratégia de rollback

Reverter o commit que removeu a senha do `appsettings.json`. A connection string permanecerá funcional pois o User Secrets e variáveis de ambiente têm precedência sobre o `appsettings.json`.

### Critérios de aceite

- [x] `appsettings.json` versionado NÃO contém senha em texto plano
- [x] A aplicação inicia corretamente em desenvolvimento com `dotnet user-secrets`
- [x] A aplicação inicia corretamente em produção com variável de ambiente
- [x] Nenhum warning ou erro de configuração no startup

---

## Item 2 — Implementar autenticação mínima para endpoints admin (API Key)

### Objetivo

Proteger os endpoints administrativos (`/api/admin/*`) com autenticação via **API Key** simples (header `X-Api-Key`), sem a complexidade de um sistema completo de usuários/roles.

### Decisão arquitetural: API Key (NÃO JWT)

**Estratégia escolhida: API Key apenas.**

**Justificativa:**

| Critério | API Key | JWT |
|----------|:-------:|:---:|
| Complexidade de implementação | Baixa | Média |
| Dependências externas | Nenhuma | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Gerenciamento de tokens | N/A | Refresh token, expiração, revogação |
| Adequação ao porte do projeto | ✅ Ideal | ❌ Superdimensionado |
| Rotação de chave | Arquivo de configuração | Requer reinício ou lógica adicional |
| Performance | Comparação de hash | Validação de assinatura + expiração |

**Conclusão:** Para um sistema com ~6 endpoints admin internos, API Key é a abordagem mais simples, segura e de baixa manutenção.

### Estado atual

Todos os endpoints em [`Program.cs`](../src/TicketPrime.Api/Program.cs:1892) começando com `/api/admin/` (linhas 1892-2141) são públicos e não possuem qualquer proteção.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/appsettings.json`](../src/TicketPrime.Api/appsettings.json) | Adicionar seção `"Authentication:ApiKey"` (hash da chave) |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs) | Adicionar middleware de autenticação e validar API Key |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs) | Adicionar `.RequireAuthorization()` nos endpoints `/api/admin/*` |

### Alterações necessárias

1. **appsettings.json** → Adicionar:
   ```json
   "Authentication": {
     "ApiKey": "troque-aqui-por-uma-chave-segura"
   }
   ```
   **Nota:** A chave NÃO ficará hardcoded em produção — usará `UserSecrets` em dev e variável de ambiente `Authentication__ApiKey` em produção.

2. **Program.cs** → Configurar esquema de autenticação customizado que valida o header `X-Api-Key` contra o valor configurado.

3. **Program.cs** → Adicionar `app.UseAuthentication()` e `app.UseAuthorization()` no pipeline.

4. **Endpoints admin** → Adicionar `.RequireAuthorization()` nos mapeamentos `/api/admin/*`.

### Abordagem técnica

Implementar um **`ApiKeyAuthenticationHandler`** customizado (herdando `AuthenticationHandler<AuthenticationSchemeOptions>`) que:
- Lê o header `X-Api-Key`
- Compara com o hash SHA256 armazenado em `appsettings.json`
- Retorna `AuthenticateResult.Success()` ou `Fail()`
- Usa `AuthenticationBuilder.AddScheme<...>()` — **sem dependência de pacote JwtBearer**

### Dependências

Nenhuma. Implementação via `AuthenticationHandler<T>` nativa do ASP.NET Core.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| API Key em texto plano no appsettings.json | Média | Médio | Usar User Secrets ou variável de ambiente `Authentication__ApiKey` |
| Performance: toda requisição admin valida a chave | Baixa | Baixo | Validação é apenas comparação de hash SHA256 |
| Desenvolvedor esquecer de configurar a chave | Média | Médio | Adicionar validação no startup que lança exceção se não configurada |

### Estratégia de rollback

Remover o middleware de autenticação, as chamadas `.RequireAuthorization()` e a seção `Authentication:ApiKey` do `appsettings.json`.

### Critérios de aceite

- [x] Requisições para `/api/admin/*` sem header `X-Api-Key` retornam `401 Unauthorized`
- [x] Requisições com `X-Api-Key` inválida retornam `401 Unauthorized`
- [x] Requisições com `X-Api-Key` válida prosseguem normalmente
- [x] Endpoints não-admin (`/api/eventos`, `/api/reservas`, etc.) continuam públicos
- [x] Testes existentes não são afetados (nenhum testa endpoints admin)
- [x] Nenhuma dependência do pacote `JwtBearer` é adicionada

---

## Item 3 — Adicionar middleware global de exception handling

### Objetivo

Garantir que nenhuma exceção não tratada vaze para o cliente como `500 Internal Server Error` sem informações controladas, substituindo por respostas JSON padronizadas e logs estruturados.

### Estado atual

Não há nenhum middleware de tratamento de exceções. Se qualquer exceção ocorrer durante o processamento de uma requisição, o ASP.NET Core retorna um `500` com detalhes do erro (em desenvolvimento) ou uma página HTML genérica (em produção), dependendo da configuração de `UseDeveloperExceptionPage`.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:22) | Adicionar `app.UseMiddleware<ExceptionHandlingMiddleware>()` |
| `src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs` | **NOVO** — Classe do middleware |

### Comportamentos específicos

O middleware deve tratar **3 categorias** de exceção:

| Tipo de exceção | Status HTTP | Comportamento |
|-----------------|:-----------:|---------------|
| `BadHttpRequestException` | **400** | Erro do cliente (request malformado) — NÃO transformar em 500 |
| `ValidationException` (custom) | **400** | Erro de validação de negócio — disparado por rollback de transação (ver Item 4) |
| `Exception` (genérica) | **500** | Erro interno do servidor — logar stack trace completo |

**Correção A2:** `BadHttpRequestException` é uma exceção lançada pelo ASP.NET Core quando o request está malformado (ex: content-type inválido, body muito grande). O plano original transformava tudo em 500. Agora preservamos o status 400.

### Alterações necessárias

1. **Criar** o arquivo `src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs`:

```csharp
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex)
        {
            // A2: BadHttpRequestException → 400, não 500
            _logger.LogWarning(ex, "Request malformado: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { erro = ex.Message });
        }
        catch (ValidationException ex)
        {
            // C1: Exceção customizada de validação → 400
            _logger.LogWarning(ex, "Erro de validação: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { erro = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var error = _env.IsDevelopment()
                ? new { erro = ex.Message, detalhes = ex.StackTrace }
                : new { erro = "Ocorreu um erro interno no servidor." };
            await context.Response.WriteAsJsonAsync(error);
        }
    }
}
```

2. **Criar** `src/TicketPrime.Api/Middleware/ValidationException.cs`:

```csharp
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
```

3. **Program.cs** → Inserir o middleware no pipeline **antes de qualquer endpoint**, logo após o `builder.Build()`.

### Dependências

Nenhuma. Apenas classes nativas do ASP.NET Core.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Middleware mascarar erros importantes em desenvolvimento | Baixa | Baixo | Em desenvolvimento, incluir stack trace na resposta |
| Erro no próprio middleware (loop infinito) | Muito baixa | Alto | O middleware não faz operações que possam lançar exceções além de serialização JSON |
| Vazar informações sensíveis em produção | Baixa (com a implementação correta) | Alto | Nunca incluir stack trace ou inner exception em produção |

### Estratégia de rollback

Remover a linha `app.UseMiddleware<ExceptionHandlingMiddleware>()` do `Program.cs` e deletar os arquivos do middleware e da exceção customizada.

### Critérios de aceite

- [x] `BadHttpRequestException` retorna `400` com JSON (NÃO 500)
- [x] `ValidationException` retorna `400` com JSON
- [x] Exceções não tratadas retornam `500` com JSON: `{ "erro": "Ocorreu um erro interno no servidor." }`
- [x] Em desenvolvimento, o JSON inclui `detalhes` com o stack trace
- [x] A exceção completa é registrada no `ILogger` (visível no console/seq/arquivo)
- [x] Endpoints que retornam `400`/`404`/`409` intencionalmente continuam funcionando (não são capturados)

---

## Item 4 — Adicionar transações no fluxo de confirmação de carrinho

### Objetivo

Garantir atomicidade no fluxo de confirmação de carrinho ([`/api/carrinho/{cpf}/confirmar`](../src/TicketPrime.Api/Program.cs:1651)), evitando estados inconsistentes onde parte das reservas/ingressos são criados e outra parte falha, deixando dados órfãos.

### Estado atual

O endpoint de confirmação (linhas 1651-1817) executa **múltiplas operações** de INSERT em `Reservas`, `Ingressos`, `Carrinhos` e DELETE em `CarrinhoItens` **sem transação**. Se uma das operações falhar no meio do loop, as operações anteriores já foram persistidas e não há rollback.

### Correção C1 — Rollback com exceção customizada

**Problema identificado:** O plano original usava `return Results.BadRequest(...)` dentro das validações no meio da transação. Como `BadRequest` retorna um `IResult` normalmente (não lança exceção), o `catch` do bloco `try/catch` **NUNCA** era disparado, e o rollback nunca ocorria.

**Solução adotada:** Substituir `return Results.BadRequest(...)` por `throw new ValidationException("mensagem")`.

**Vantagens:**
1. **Único ponto de rollback** — o `catch` captura tanto exceções inesperadas quanto validações de negócio
2. **Impossível esquecer rollback** — diferentemente de chamar `transaction.Rollback()` explicitamente antes de cada `return`
3. **Reuso** — a `ValidationException` é tratada pelo middleware do Item 3, retornando 400 padronizado
4. **Consistência** — todo fluxo anômalo passa pelo mesmo caminho (catch → rollback → relançamento)

### Correção F6 — Timeout para transações

Adicionar `CommandTimeout` na abertura da transação para evitar locks prolongados:

```csharp
using var transaction = db.BeginTransaction();
// Timeout de 30 segundos para a transação
transaction.CommandTimeout = 30;
```

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1651) | Envolver todo o bloco de confirmação em uma transação SQL |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1651) | Substituir `return Results.BadRequest(...)` por `throw new ValidationException(...)` |

### Alterações necessárias

1. **Program.cs** → No endpoint de confirmação, após obter a conexão (`IDbConnection`), iniciar uma transação com `db.BeginTransaction()`.
2. **Timeout** → `transaction.CommandTimeout = 30`.
3. **Passar a transação** para todas as chamadas Dapper usando o parâmetro `transaction:`.
4. **Commit** ao final, se tudo ocorrer bem.
5. **Rollback** em caso de qualquer exceção **ou validação**.
6. **Validações** → Trocam `return Results.BadRequest(...)` por `throw new ValidationException("mensagem")`.

### Código esboço da mudança

```csharp
app.MapPost("/api/carrinho/{cpf}/confirmar", async (IDbConnection db, string cpf, ...) =>
{
    using var transaction = db.BeginTransaction();
    transaction.CommandTimeout = 30; // F6: timeout
    
    try
    {
        // ... validações existentes ...
        // ONDE ANTES TINHA: return Results.BadRequest(...)
        // AGORA TEM: throw new ValidationException("mensagem")
        
        // ... operações Dapper com transaction: transaction ...
        
        transaction.Commit();
        return Results.Created(..., response);
    }
    catch
    {
        transaction.Rollback();
        throw; // Relança para o middleware de exception handling
    }
});
```

### Dependências

Nenhuma. `IDbConnection.BeginTransaction()` já está disponível via `System.Data`.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Transação longa (muitos itens no carrinho) | Baixa | Médio | Carrinhos têm no máximo ~10 itens; transação dura milissegundos. Timeout de 30s (F6) |
| Deadlock no SQL Server | Muito baixa | Alto | As operações seguem ordem consistente (Reservas → Ingressos); usar `READ COMMITTED` |
| Rollback não chamado em cenário de exceção | Baixa | Alto | `using` garante dispose; `catch` garante rollback antes do `throw` |
| ValidationException não capturada se middleware não estiver instalado | Média | Médio | **Ordem de implementação:** Item 3 (middleware) vem ANTES do Item 4 |

### Estratégia de rollback

Reverter as alterações no endpoint de confirmação, removendo o gerenciamento de transação e restaurando `return Results.BadRequest(...)` originais. Remover arquivo `ValidationException.cs`.

### Critérios de aceite

- [x] Se a confirmação do carrinho falhar após criar algumas reservas, **todas** as reservas e ingressos criados são desfeitos
- [x] Se uma validação de negócio falha no meio do loop (ex: limite de 2 reservas), o rollback é executado
- [x] Nenhum registro órfão permanece no banco após falha
- [x] Em caso de sucesso, todas as operações são persistidas (commit)
- [x] Timeout de 30 segundos configurado na transação
- [x] Testes existentes que validam regras de negócio do carrinho continuam passando

---

## Item 5 — Corrigir interpolações SQL inseguras

### Objetivo

Eliminar construções SQL que concatenam strings diretamente (susceptíveis a SQL injection), substituindo por parâmetros ou white-list validation.

### Estado atual

Foram identificados **2 pontos** de interpolação SQL insegura:

#### 5.1. Função `CriarOuRecriarView` — [`Program.cs`](../src/TicketPrime.Api/Program.cs:361)

```csharp
await db.ExecuteAsync($"IF EXISTS (SELECT * FROM sys.views WHERE name = @NomeView) DROP VIEW {nomeView}",
    new { NomeView = nomeView });
```

O nome da view é interpolado diretamente no SQL.

**Correção A3:** Além da segurança, a estratégia de `DROP VIEW` + `CREATE VIEW` é **frágil**: se o `CREATE` falhar (ex: erro de sintaxe na view), a view foi deletada e não pode ser recuperada. A abordagem segura é usar `CREATE OR ALTER VIEW` (disponível desde SQL Server 2016 SP1):

```csharp
// Abordagem definitiva: substituir DROP+CREATE por CREATE OR ALTER VIEW
// A string createSql deve conter "CREATE OR ALTER VIEW" em vez de "CREATE VIEW"
```

Isso exige modificar a função `CriarOuRecriarView` para:
1. Validar o nome da view com `QUOTENAME()` (segurança)
2. Usar `CREATE OR ALTER VIEW` em vez de `DROP` + `CREATE` (atomicidade)

```csharp
static async Task CriarOuRecriarView(IDbConnection db, string nomeView, string createSql)
{
    // A3: CREATE OR ALTER VIEW é atômico e seguro
    // A string createSql já deve conter "CREATE OR ALTER VIEW"
    await db.ExecuteAsync(createSql);
    Console.WriteLine($"View {nomeView} verificada/criada com sucesso.");
}
```

**Impacto:** As strings de criação das views (`vw_DashboardEventos` e `vw_DashboardLotes`) devem ser alteradas de `CREATE VIEW` para `CREATE OR ALTER VIEW`.

#### 5.2. Construção dinâmica de WHERE — [`Program.cs`](../src/TicketPrime.Api/Program.cs:1993)

```csharp
var whereClause = conditions.Count > 0
    ? "WHERE " + string.Join(" AND ", conditions)
    : "";

var sql = $@"... {whereClause} ...";
```

Aqui os **nomes das colunas** são interpolados, mas os **valores** são passados via `DynamicParameters` (seguro). O risco é menor porque os nomes de coluna vêm de um white-list (`eventoId`, `status`, `cpf`), mas ainda é uma construção que dificulta manutenção e não segue boas práticas.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:361) | Substituir `DROP VIEW` por `CREATE OR ALTER VIEW` |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1995) | Refatorar query dinâmica para usar SQL fixo com parâmetros opcionais |

### Alterações necessárias

**5.1.** Modificar a função `CriarOuRecriarView` para aceitar que `createSql` já contenha `CREATE OR ALTER VIEW` e remover o `DROP VIEW`:

```csharp
static async Task CriarOuRecriarView(IDbConnection db, string nomeView, string createSql)
{
    await db.ExecuteAsync(createSql);
    Console.WriteLine($"View {nomeView} verificada/criada com sucesso.");
}
```

E modificar as chamadas para usar `CREATE OR ALTER VIEW`:
```csharp
await CriarOuRecriarView(db, "vw_DashboardEventos", @"
    CREATE OR ALTER VIEW vw_DashboardEventos ...");
```

**5.2.** Refatorar o endpoint `/api/admin/reservas` para usar uma única consulta com parâmetros opcionais:

```csharp
var sql = @"
    SELECT ...
    FROM Reservas r
    ... (joins)
    WHERE (@EventoId IS NULL OR r.EventoId = @EventoId)
      AND (@Status IS NULL OR i.Status = @Status)
      AND (@Cpf IS NULL OR r.UsuarioCpf = @Cpf)
    ORDER BY r.Id DESC";
```

### Dependências

Nenhuma.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Performance da query com `OR ... IS NULL` | Média | Baixo | SQL Server lida bem com esse padrão; índices existentes cobrem as colunas |
| Mudança no comportamento da query | Baixa | Médio | Testar manualmente cada combinação de filtro |
| `CREATE OR ALTER VIEW` não disponível no SQL Server | Muito baixa | Alto | Disponível desde SQL Server 2016 SP1 (compatível com a versão atual) |

### Estratégia de rollback

Reverter as alterações nas linhas afetadas do `Program.cs`, restaurando `DROP VIEW` + `CREATE VIEW` e a concatenação original.

### Critérios de aceite

- [x] Nenhuma concatenação de strings SQL permanece no código
- [x] Todas as queries usam parâmetros para valores
- [x] A função `CriarOuRecriarView` usa `CREATE OR ALTER VIEW` (DROP removido)
- [x] O endpoint `/api/admin/reservas` com filtros retorna os mesmos resultados de antes
- [x] As views continuam sendo criadas/recriadas corretamente

---

## Item 6 — Configurar CORS para futuro frontend

### Objetivo

Permitir que um futuro frontend (ex: React, Vue, Angular) hospedado em outra origem possa consumir a API, configurando CORS (Cross-Origin Resource Sharing) de forma segura.

### Estado atual

Não há nenhuma configuração de CORS no [`Program.cs`](../src/TicketPrime.Api/Program.cs). Por padrão, o ASP.NET Core bloqueia requisições de origens diferentes.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs) | Adicionar serviços CORS e middleware |
| [`src/TicketPrime.Api/appsettings.json`](../src/TicketPrime.Api/appsettings.json) | Adicionar seção `"Cors:AllowedOrigins"` |

### Alterações necessárias

1. **appsettings.json** → Adicionar:
   ```json
   "Cors": {
     "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
   }
   ```
2. **Program.cs** → Adicionar:
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("AllowFrontend", policy =>
       {
           var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
               ?? throw new InvalidOperationException("CORS AllowedOrigins não configurado.");
           policy.WithOrigins(origins)
                 .AllowAnyHeader()
                 .AllowAnyMethod();
       });
   });
   
   app.UseCors("AllowFrontend");
   ```

### Dependências

Nenhuma. `AddCors` e `UseCors` são nativos do ASP.NET Core.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Configurar origens muito permissivas (`AllowAnyOrigin()`) | Baixa (não faremos) | Alto | Usar `WithOrigins(...)` com lista explícita |
| CORS bloquear requisições legítimas em produção | Média | Médio | Adicionar a origem de produção assim que conhecida |
| Esquecer de configurar `AllowedOrigins` | Média | Baixo | Lançar `InvalidOperationException` no startup se não configurado |

### Estratégia de rollback

Remover as linhas `AddCors` e `UseCors` do `Program.cs` e a seção `Cors` do `appsettings.json`.

### Critérios de aceite

- [x] Requisições de `http://localhost:3000` e `http://localhost:5173` são permitidas
- [x] Requisições de outras origens são bloqueadas com `403` ou sem header `Access-Control-Allow-Origin`
- [x] Métodos `GET`, `POST`, `PUT`, `DELETE`, `OPTIONS` (preflight) funcionam
- [x] A aplicação lança erro no startup se `Cors:AllowedOrigins` não estiver configurado

---

## Item 7 — Endpoint /health

### Objetivo

Disponibilizar um endpoint de health check para:
- Orquestradores (Kubernetes, Docker Swarm) verificarem se a aplicação está saudável
- Monitoramento externo (Pingdom, New Relic)
- Diagnóstico rápido em produção

### Estado atual

Não existe nenhum endpoint de health check. O único endpoint raiz é um redirect para `/index.html`.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:367) | Adicionar endpoint `GET /health` |

### Alterações necessárias

Adicionar endpoint simples que verifica conectividade com o banco:

```csharp
app.MapGet("/health", async (IDbConnection db) =>
{
    try
    {
        await db.ExecuteScalarAsync<int>("SELECT 1");
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.StatusCode(503, new { status = "unhealthy", erro = ex.Message });
    }
});
```

O endpoint:
- Retorna `200 OK` com `{ "status": "healthy" }` se o banco responder
- Retorna `503 Service Unavailable` se o banco não responder
- **Não requer autenticação** — health checks de orquestradores precisam ser públicos

### Dependências

Nenhuma.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Health check expor informação interna no erro | Baixa | Baixo | Limitar mensagem de erro, nunca incluir stack trace |
| Falso positivo se banco cai entre health check e requisição real | Baixa | Médio | Aceitável para health check simples |

### Estratégia de rollback

Remover o endpoint `GET /health` do `Program.cs`.

### Critérios de aceite

- [x] `GET /health` retorna `200` com `{ "status": "healthy" }` quando banco está acessível
- [x] `GET /health` retorna `503` quando banco está inacessível
- [x] Endpoint não requer autenticação
- [x] O health check é rápido (consulta `SELECT 1`)

---

## Item 8 — Logging de acessos não autorizados

### Objetivo

Registrar tentativas de acesso a endpoints admin sem autenticação ou com chave inválida, permitindo auditoria e detecção de tentativas de ataque.

### Estado atual

Não há logging específico para tentativas de acesso não autorizado. O middleware de autenticação (a ser implementado no Item 2) rejeita requisições sem log.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| `src/TicketPrime.Api/Authentication/ApiKeyAuthenticationHandler.cs` | Adicionar `ILogger` para logging de falhas |

### Alterações necessárias

No `ApiKeyAuthenticationHandler`, adicionar logging:

```csharp
protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
{
    if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
    {
        _logger.LogWarning("Tentativa de acesso a endpoint admin sem header X-Api-Key. Path: {Path}", Request.Path);
        return AuthenticateResult.Fail("API Key não fornecida.");
    }

    var configuredKey = Options.ApiKey; // obtido da configuração
    if (apiKeyHeader != configuredKey)
    {
        _logger.LogWarning("Tentativa de acesso a endpoint admin com API Key inválida. Path: {Path}", Request.Path);
        return AuthenticateResult.Fail("API Key inválida.");
    }

    _logger.LogInformation("Acesso autorizado a endpoint admin. Path: {Path}", Request.Path);
    
    var claims = new[] { new Claim(ClaimTypes.Name, "Admin") };
    var identity = new ClaimsIdentity(claims, Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);
    return AuthenticateResult.Success(ticket);
}
```

### Dependências

Nenhuma. `ILogger<T>` é nativo do ASP.NET Core.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Flood de logs em caso de ataque | Baixa | Baixo | Logs são `Warning` — sistemas de logging podem rate-limit |
| IP do atacante não é registrado | Média | Baixo | Adicionar `Context.Connection.RemoteIpAddress` ao log |

### Estratégia de rollback

Remover as chamadas de logging do `ApiKeyAuthenticationHandler`.

### Critérios de aceite

- [x] Tentativas sem header `X-Api-Key` são registradas com nível `Warning`
- [x] Tentativas com `X-Api-Key` inválida são registradas com nível `Warning`
- [x] O log inclui o path do endpoint acessado
- [x] Acessos autorizados são registrados com nível `Information`

---

## Ordem de implementação recomendada

### Nova ordem de execução

| Passo | Item | Motivo |
|:-----:|------|--------|
| **0** | **Item 0 — .gitignore** | **Pré-requisito obrigatório.** Sem ele, qualquer alteração de configuração corre risco de versionar credenciais |
| 1 | **Item 3 — Middleware de exception handling** | Deve vir primeiro para capturar erros dos demais itens. Inclui `ValidationException` necessária para o Item 4 |
| 2 | **Item 1 — Remover senha hardcoded** | Segurança crítica; depende do `.gitignore` (Item 0) para proteger `appsettings.Development.json` |
| 3 | **Item 7 — Endpoint /health** | Simples, independente, útil para validar que a aplicação está rodando durante os demais passos |
| 4 | **Item 5 — Corrigir SQL injection** | Segurança; altera queries existentes. Inclui correção A3 (`CREATE OR ALTER VIEW`) |
| 5 | **Item 4 — Transações no carrinho** | Estabilidade; altera lógica de negócio. Depende do Item 3 (`ValidationException`) |
| 6 | **Item 8 — Logging de acessos não autorizados** | Depende do Item 2 (Authentication Handler). Pode ser implementado junto |
| 7 | **Item 2 — Autenticação admin** | Deve vir após os itens de estabilidade estarem sólidos |
| 8 | **Item 6 — Configurar CORS** | Baixo risco; independe dos demais |

### Justificativa

1. **Item 0 (.gitignore)** é pré-requisito absoluto — sem ele, não podemos proteger credenciais.
2. **Item 3 (Middleware)** precisa estar no ar antes de qualquer mudança que possa gerar exceções não tratadas.
3. **Item 1 (Senha)** depende do Item 0 para proteger o arquivo de desenvolvimento.
4. **Item 7 (Health)** é rápido e fornece um endpoint básico de validação.
5. **Item 5 (SQL Injection)** inclui a correção A3 que substitui `DROP VIEW` por `CREATE OR ALTER VIEW` — essencial para não quebrar as views durante alterações futuras.
6. **Item 4 (Transações)** é a mudança mais delicada. Depende do Item 3 (`ValidationException`) e deve vir após as correções de segurança.
7. **Item 8 (Logging)** e **Item 2 (Auth)** são acoplados — implementar o handler de autenticação já com logging.
8. **Item 6 (CORS)** é o mais simples e pode ficar por último sem impacto nos demais.

---

## Resumo de dependências

### Pacotes NuGet

| Pacote | Versão | Item | Motivo |
|--------|--------|:----:|--------|
| Nenhum | — | Todos | **Nenhum pacote extra é necessário.** Todos os itens usam APIs nativas do ASP.NET Core 8 |

### Dependências entre itens

| Item | Depende de |
|:----:|------------|
| 0 (.gitignore) | Nenhuma |
| 1 (Senha) | Item 0 (.gitignore protege `appsettings.Development.json`) |
| 2 (Auth) | Item 8 (logging no handler) |
| 3 (Middleware) | Nenhuma |
| 4 (Transações) | Item 3 (`ValidationException`) |
| 5 (SQL Injection) | Nenhuma |
| 6 (CORS) | Nenhuma |
| 7 (Health) | Nenhuma |
| 8 (Logging auth) | Nenhuma (independente, mas idealmente junto com Item 2) |

---

## Novos riscos

Além dos riscos já documentados em cada item, os seguintes riscos **globais** foram identificados:

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| ValidationException não tratar todos os pontos de retorno no fluxo de transação | Média | Alto | Revisão de código obrigatória; criar teste que simula falha no meio do loop |
| CREATE OR ALTER VIEW não suportado em versões antigas do SQL Server | Muito baixa | Alto | Verificar versão do SQL Server no ambiente de produção (requer SQL Server 2016 SP1+) |
| Esquecer de configurar API Key em novo ambiente | Média | Médio | Validação em startup conforme Item 2; documentação no README |
| Timeout de transação muito curto para carrinhos grandes | Baixa | Baixo | 30 segundos é mais que suficiente para ~10 itens |
| .gitignore não cobrir todos os padrões necessários | Média | Baixo | Revisar com a equipe; adicionar padrões conforme necessidade |

---

## Impacto nos testes

### Testes existentes

| Arquivo de teste | Impacto |
|------------------|---------|
| [`tests/TicketPrime.Tests/ReservaServiceTests.cs`](../tests/TicketPrime.Tests/ReservaServiceTests.cs) | **Nenhum** — Testa `ReservaService` (regras de negócio puras, sem banco) |
| [`tests/TicketPrime.Tests/IncrementoServiceTests.cs`](../tests/TicketPrime.Tests/IncrementoServiceTests.cs) | **Nenhum** — Testa `IncrementoService` (regras de negócio puras, sem banco) |
| [`tests/TicketPrime.Tests/EventoValidationTests.cs`](../tests/TicketPrime.Tests/EventoValidationTests.cs) | **Nenhum** (testes de validação) |
| [`tests/TicketPrime.Tests/CupomValidationTests.cs`](../tests/TicketPrime.Tests/CupomValidationTests.cs) | **Nenhum** (testes de validação) |
| [`tests/TicketPrime.Tests/UsuarioValidationTests.cs`](../tests/TicketPrime.Tests/UsuarioValidationTests.cs) | **Nenhum** (testes de validação) |

### Novos testes necessários

| Item | Testes sugeridos |
|:----:|------------------|
| 0 (.gitignore) | Verificar que `.gitignore` existe e cobre os padrões necessários |
| 2 (Autenticação) | Testar requisições admin com/sem API Key, chave válida/inválida |
| 3 (Exception handling) | Testar: (a) exceção genérica → 500, (b) `BadHttpRequestException` → 400, (c) `ValidationException` → 400 |
| 4 (Transações) | Testar falha no meio do fluxo e verificar que **nenhum** dado foi persistido (rollback funciona) |
| 5 (SQL injection) | Testar que caracteres maliciosos em parâmetros não quebram a query; testar `CREATE OR ALTER VIEW` |
| 6 (CORS) | Testar requisições com `Origin` header permitida e não permitida |
| 7 (Health) | Testar `/health` retorna 200 quando banco OK; testar comportamento com banco indisponível |
| 8 (Logging) | Verificar que logs de acesso não autorizado são emitidos |

---

## Critérios de aceite globais

### Funcionais

- [x] **Item 0:** `.gitignore` criado e protegendo arquivos sensíveis
- [x] **Item 1:** Nenhuma credencial em texto plano em arquivos versionados
- [x] **Item 2:** Endpoints admin protegidos por API Key
- [x] **Item 3:** Exceções tratadas com JSON padronizado; `BadHttpRequestException` → 400
- [x] **Item 4:** Transações com rollback garantido via `ValidationException`
- [x] **Item 5:** Nenhuma concatenação SQL; `CREATE OR ALTER VIEW` substitui `DROP VIEW`
- [x] **Item 6:** CORS configurado para origens permitidas
- [x] **Item 7:** Endpoint `/health` disponível e funcional
- [x] **Item 8:** Acessos não autorizados registrados em log

### Não funcionais

- [x] Nenhum pacote NuGet adicional é necessário
- [x] Nenhuma das alterações quebra a API pública (contratos request/response preservados)
- [x] Nenhum model ou service existente é modificado (alterações apenas em `Program.cs`, novos arquivos)
- [x] Testes unitários existentes continuam passando sem modificações
- [x] Ordem de implementação respeita dependências entre itens

### Checklist de segurança

- [x] Credenciais nunca versionadas (Item 0 + Item 1)
- [x] SQL injection eliminado (Item 5)
- [x] Autenticação em endpoints admin (Item 2)
- [x] Logging de acessos não autorizados (Item 8)
- [x] Rollback de transações em caso de falha (Item 4)
- [x] Timeout de transações para evitar locks (Item 4)
- [x] Tratamento padronizado de exceções (Item 3)
- [x] Health check para monitoramento (Item 7)

---

## Considerações finais

1. **Nenhuma dessas alterações quebra a API pública.** Todos os contratos dos endpoints (request/response) permanecem idênticos.
2. **A Fase 1 não introduz novas funcionalidades.** É puramente segurança e estabilidade sobre o que já existe.
3. **Todas as alterações são feitas no `Program.cs` ou em novos arquivos.** Nenhum model ou service existente é modificado.
4. **A correção C1 (rollback via exceção customizada) foi a decisão arquitetural mais significativa**, pois impacta diretamente a consistência dos dados e o padrão de tratamento de erros em toda a aplicação.
5. **A correção A3 (CREATE OR ALTER VIEW) elimina um ponto de falha catastrófico** onde uma falha no meio da recriação de views poderia deixar o dashboard quebrado.

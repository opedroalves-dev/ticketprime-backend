# Plano de Execução — Fase 1: Segurança e Estabilidade (v3)

**Projeto:** TicketPrime  
**Data:** 2026-05-28  
**Versão:** 3.1 (revisão final pós-auditoria — correções N1, N2, N3)  
**Responsável:** Arquitetura de Software  

---

## Sumário

1. [Resumo das correções da auditoria](#resumo-das-correções-da-auditoria)
2. [Item 0 — Criar/revisar .gitignore](#item-0--criarrevisar-gitignore)
3. [Item 1 — Remover senha hardcoded do appsettings.json](#item-1--remover-senha-hardcoded-do-appsettingsjson)
4. [Item 2 — Implementar autenticação admin (API Key) com logging de acessos](#item-2--implementar-autenticação-admin-api-key-com-logging-de-acessos)
5. [Item 3 — Adicionar middleware global de exception handling](#item-3--adicionar-middleware-global-de-exception-handling)
6. [Item 4 — Adicionar transações no fluxo de confirmação de carrinho](#item-4--adicionar-transações-no-fluxo-de-confirmação-de-carrinho)
7. [Item 5 — Corrigir interpolações SQL inseguras](#item-5--corrigir-interpolações-sql-inseguras)
8. [Item 6 — Configurar CORS para futuro frontend](#item-6--configurar-cors-para-futuro-frontend)
9. [Item 7 — Endpoint /health](#item-7--endpoint-health)
10. [Ordem de implementação recomendada](#ordem-de-implementação-recomendada)
11. [Resumo de dependências](#resumo-de-dependências)
12. [Impacto nos testes](#impacto-nos-testes)
13. [Critérios de aceite globais](#critérios-de-aceite-globais)

---

## Resumo das correções da auditoria

### Problemas críticos

| ID | Problema | Descrição | Correção aplicada |
|:--:|----------|-----------|-------------------|
| **C1** | Rollback silenciosamente ignorado | `return Results.BadRequest(...)` dentro do bloco `try` da transação **não lança exceção**, portanto o `catch` nunca é executado e o `Rollback()` nunca ocorre, deixando dados órfãos no banco | Substituir `return Results.BadRequest(...)` por `throw new ValidationException("mensagem")` — a exceção é capturada pelo `catch`, que executa `transaction.Rollback()` e relança para o middleware de exception handling (Item 3) |
| **C2** | Ausência de .gitignore | O plano original assumia a existência de um arquivo `.gitignore` sem verificar. Na prática, **não existia** `.gitignore` no repositório, expondo `appsettings.Development.json`, pastas `bin/`, `obj/` e outros artefatos ao versionamento | Adicionar **Item 0 obrigatório**: criar/revisar `.gitignore` **antes** de qualquer alteração no `appsettings.json` ou `Program.cs`. Sem este passo, nenhuma outra alteração de segurança deve ser iniciada |

### Problemas altos

| ID | Problema | Descrição | Correção aplicada |
|:--:|----------|-----------|-------------------|
| **A1** | Inconsistência JWT vs API Key | O Item 2 do plano original mencionava `JwtBearer` como esquema de autenticação, mas a implementação descrita usava API Key via header `X-Api-Key`. Havia contradição entre especificação e implementação | Estratégia **unificada** e documentada: **apenas API Key** (header `X-Api-Key`), sem dependência de JwtBearer. Justificativa formal incluída na seção do Item 2 |
| **A2** | `BadHttpRequestException` mapeada como 500 | O middleware de exception handling genérico transformava **todas** as exceções em `500 Internal Server Error`, inclusive `BadHttpRequestException` (request malformado), que deveria ser `400 Bad Request` | O middleware agora preserva o status **400** para `BadHttpRequestException` e registra como `LogWarning` em vez de `LogError` |
| **A3** | Estratégia `DROP VIEW` + `CREATE VIEW` frágil | A função `CriarOuRecriarView` executava `DROP VIEW` e depois `CREATE VIEW` em chamadas separadas. Se o `CREATE` falhasse (ex: erro de sintaxe), a view era deletada sem possibilidade de recuperação, deixando o dashboard quebrado | Substituir por `CREATE OR ALTER VIEW` (disponível desde SQL Server 2016 SP1) — operação única e atômica. Remover completamente o `DROP VIEW` |

### Correções obrigatórias da auditoria final (N1, N2, N3)

| ID | Correção | Descrição | Itens impactados |
|:--:|----------|-----------|:----------------:|
| **N1** | Substituir estratégia de timeout de transação | Em vez de `transaction.CommandTimeout = 30`, todas as chamadas Dapper (`ExecuteAsync` / `QueryAsync`) dentro da transação devem receber `commandTimeout: 30` como parâmetro explícito | Item 4 |
| **N2** | Adicionar `ApiKeyAuthenticationSchemeOptions` e corrigir herança do handler | Criar classe `ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions`; alterar handler para herdar `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` | Item 2 |
| **N3** | Fundir autenticação admin e logging de acesso não autorizado em uma única etapa | Unificar Item 2 (autenticação) e antigo Item 8 (logging) em um único item consolidado. Atualizar ordem de execução, riscos e critérios de aceite | Item 2 (fundido) |

### Tarefas faltantes incorporadas

| ID | Tarefa | Item | Correção relacionada |
|:--:|--------|:----:|----------------------|
| **F1** | Criar/revisar `.gitignore` | Item 0 | C2 |
| **F2** | Corrigir estratégia de rollback das transações | Item 4 | C1 |
| **F3** | Tratamento correto para `BadHttpRequestException` | Item 3 | A2 |
| **F4** | Adicionar endpoint `/health` | Item 7 | — |
| **F5** | Adicionar logging de acessos não autorizados | Item 2 (fundido) | — |
| **F6** | Definir timeout para transações | Item 4 | — |

### Matriz de rastreabilidade correção → item

| Correção | Item(s) impactados | Tipo |
|:---------|:------------------:|:----:|
| C1 | Item 4 (rollback via exceção) | Crítico |
| C2 | Item 0 (novo) | Crítico |
| A1 | Item 2 (consistência API Key) | Alto |
| A2 | Item 3 (BadHttpRequestException → 400) | Alto |
| A3 | Item 5 (CREATE OR ALTER VIEW) | Alto |
| F1 | Item 0 | Tarefa faltante |
| F2 | Item 4 | Tarefa faltante |
| F3 | Item 3 | Tarefa faltante |
| F4 | Item 7 | Tarefa faltante |
| F5 | Item 2 (fundido) | Tarefa faltante |
| F6 | Item 4 | Tarefa faltante |
| **N1** | Item 4 | Auditoria final |
| **N2** | Item 2 | Auditoria final |
| **N3** | Item 2 (fundido) | Auditoria final |

---

## Item 0 — Criar/revisar .gitignore

### Objetivo

Garantir que arquivos com credenciais, segredos e artefatos de build **nunca sejam versionados**. Este é um pré-requisito obrigatório **anterior** a qualquer alteração de configuração ou segurança.

### Estado atual

**Não existe arquivo `.gitignore` no repositório.** (Correção C2/F1) Isso significa que:
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
3. **appsettings.Development.json** → Conterá a connection string completa, protegido pelo `.gitignore` criado no Item 0 (correção C2/F1).
4. **Program.cs** → A leitura da connection string continua a mesma via `builder.Configuration.GetConnectionString("DefaultConnection")`, pois o .NET resolve a hierarquia: `UserSecrets` > `appsettings.Development.json` > `appsettings.json`.

### Dependências

| Item | Dependência | Motivo |
|:----:|:-----------:|--------|
| Item 0 | `.gitignore` | Proteger `appsettings.Development.json` de versionamento |

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

## Item 2 — Implementar autenticação admin (API Key) com logging de acessos

> **Correção N3:** Este item unifica o antigo Item 2 (autenticação) e o antigo Item 8 (logging de acessos não autorizados) em uma única etapa da Fase 1.

### Objetivo

Proteger os endpoints administrativos (`/api/admin/*`) com autenticação via **API Key** simples (header `X-Api-Key`), registrando em log todas as tentativas de acesso (autorizadas e não autorizadas) para auditoria e detecção de ataques.

### Decisão arquitetural: API Key (NÃO JWT) — Correção A1

**Problema identificado (A1):** O plano original mencionava `JwtBearer` em algumas seções, mas a implementação descrita usava API Key. Havia inconsistência.

**Estratégia unificada: API Key apenas.**

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

Todos os endpoints em [`Program.cs`](../src/TicketPrime.Api/Program.cs:1892) começando com `/api/admin/` (linhas 1892-2141) são públicos e não possuem qualquer proteção. Não há logging específico para tentativas de acesso.

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/appsettings.json`](../src/TicketPrime.Api/appsettings.json) | Adicionar seção `"Authentication:ApiKey"` (hash da chave) |
| `src/TicketPrime.Api/Authentication/ApiKeyAuthenticationSchemeOptions.cs` | **NOVO** — Classe de opções do esquema de autenticação |
| `src/TicketPrime.Api/Authentication/ApiKeyAuthenticationHandler.cs` | **NOVO** — Handler de autenticação customizado com logging |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs) | Configurar esquema de autenticação, adicionar middleware e `.RequireAuthorization()` |

### Alterações necessárias

1. **appsettings.json** → Adicionar:
   ```json
   "Authentication": {
     "ApiKey": "troque-aqui-por-uma-chave-segura"
   }
   ```
   **Nota:** A chave NÃO ficará hardcoded em produção — usará `UserSecrets` em dev e variável de ambiente `Authentication__ApiKey` em produção.

2. **Criar** `ApiKeyAuthenticationSchemeOptions.cs` — Classe de opções do esquema (correção N2):
   ```csharp
   public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
   {
       public string ApiKey { get; set; } = string.Empty;
   }
   ```

3. **Criar** `ApiKeyAuthenticationHandler.cs` — Handler customizado que:
   - Herda de `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` (correção N2)
   - Lê o header `X-Api-Key`
   - Compara com o valor configurado via `Options.ApiKey`
   - **Registra logs de auditoria** (F5 — ver seção de logging abaixo)
   - Retorna `AuthenticateResult.Success()` ou `Fail()`

4. **Program.cs** → Configurar esquema de autenticação customizado (`AuthenticationBuilder.AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>()` — **sem dependência de pacote JwtBearer**).

5. **Program.cs** → Adicionar `app.UseAuthentication()` e `app.UseAuthorization()` no pipeline.

6. **Endpoints admin** → Adicionar `.RequireAuthorization()` nos mapeamentos `/api/admin/*`.

### Logging de acessos (F5 — fundido neste item pelo N3)

No `ApiKeyAuthenticationHandler`, adicionar logging de auditoria:

```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            Logger.LogWarning(
                "Tentativa de acesso a endpoint admin sem header X-Api-Key. " +
                "Path: {Path}, IP: {RemoteIp}",
                Request.Path, Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("API Key não fornecida.");
        }

        var configuredKey = Options.ApiKey;
        if (apiKeyHeader != configuredKey)
        {
            Logger.LogWarning(
                "Tentativa de acesso a endpoint admin com API Key inválida. " +
                "Path: {Path}, IP: {RemoteIp}",
                Request.Path, Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("API Key inválida.");
        }

        Logger.LogInformation("Acesso autorizado a endpoint admin. Path: {Path}", Request.Path);

        var claims = new[] { new Claim(ClaimTypes.Name, "Admin") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
```

### Dependências

Nenhuma (independente). O logging é embutido no próprio handler.

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| API Key em texto plano no appsettings.json | Média | Médio | Usar User Secrets ou variável de ambiente `Authentication__ApiKey` |
| Performance: toda requisição admin valida a chave | Baixa | Baixo | Validação é apenas comparação de strings |
| Desenvolvedor esquecer de configurar a chave | Média | Médio | Adicionar validação no startup que lança exceção se não configurada |
| Flood de logs em caso de ataque | Baixa | Baixo | Logs são `Warning` — sistemas de logging podem rate-limit |
| IP do atacante não é registrado | Média | Baixo | `Context.Connection.RemoteIpAddress` incluído no log |

### Estratégia de rollback

Remover o middleware de autenticação, as chamadas `.RequireAuthorization()`, a seção `Authentication:ApiKey` do `appsettings.json`, e deletar os arquivos `ApiKeyAuthenticationHandler.cs` e `ApiKeyAuthenticationSchemeOptions.cs`.

### Critérios de aceite

- [x] Requisições para `/api/admin/*` sem header `X-Api-Key` retornam `401 Unauthorized`
- [x] Requisições com `X-Api-Key` inválida retornam `401 Unauthorized`
- [x] Requisições com `X-Api-Key` válida prosseguem normalmente
- [x] Endpoints não-admin (`/api/eventos`, `/api/reservas`, etc.) continuam públicos
- [x] Testes existentes não são afetados (nenhum testa endpoints admin)
- [x] Nenhuma dependência do pacote `JwtBearer` é adicionada
- [x] Tentativas sem header `X-Api-Key` são registradas com nível `Warning` (IP + path)
- [x] Tentativas com `X-Api-Key` inválida são registradas com nível `Warning` (IP + path)
- [x] Acessos autorizados são registrados com nível `Information`
- [x] Handler herda de `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` (N2)
- [x] Classe `ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions` criada (N2)

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
| `src/TicketPrime.Api/Middleware/ValidationException.cs` | **NOVO** — Exceção customizada (usada pelo Item 4 / C1) |

### Comportamentos específicos

O middleware deve tratar **3 categorias** de exceção:

| Tipo de exceção | Status HTTP | Comportamento |
|-----------------|:-----------:|---------------|
| `BadHttpRequestException` | **400** | **Correção A2/F3:** Erro do cliente (request malformado) — NÃO transformar em 500. Log como `Warning` |
| `ValidationException` (custom) | **400** | Erro de validação de negócio — disparado por rollback de transação (ver Item 4 / C1) |
| `Exception` (genérica) | **500** | Erro interno do servidor — logar stack trace completo como `Error` |

**Detalhamento da correção A2/F3:** `BadHttpRequestException` é uma exceção lançada pelo ASP.NET Core quando o request está malformado (ex: content-type inválido, body muito grande). O middleware deve capturá-la **antes** do catch genérico de `Exception` e retornar `400 Bad Request` em vez de `500 Internal Server Error`.

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
            // A2/F3: BadHttpRequestException → 400, não 500
            _logger.LogWarning(ex, "Request malformado: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { erro = ex.Message });
        }
        catch (ValidationException ex)
        {
            // C1/F2: Exceção customizada de validação → 400
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

| Item | Dependência | Motivo |
|:----:|:-----------:|--------|
| Item 4 | ValidationException | A exceção customizada deste item é usada pelo fluxo de transações para disparar rollback |

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Middleware mascarar erros importantes em desenvolvimento | Baixa | Baixo | Em desenvolvimento, incluir stack trace na resposta |
| Erro no próprio middleware (loop infinito) | Muito baixa | Alto | O middleware não faz operações que possam lançar exceções além de serialização JSON |
| Vazar informações sensíveis em produção | Baixa (com a implementação correta) | Alto | Nunca incluir stack trace ou inner exception em produção |

### Estratégia de rollback

Remover a linha `app.UseMiddleware<ExceptionHandlingMiddleware>()` do `Program.cs` e deletar os arquivos do middleware e da exceção customizada.

### Critérios de aceite

- [x] `BadHttpRequestException` retorna `400` com JSON (NÃO 500) — correção A2/F3
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

### Correção C1/F2 — Rollback com exceção customizada

**Problema identificado (C1):** O plano original usava `return Results.BadRequest(...)` dentro das validações no meio da transação. Como `BadRequest` retorna um `IResult` normalmente (não lança exceção), o `catch` do bloco `try/catch` **NUNCA** era disparado, e o rollback nunca ocorria. Isso deixava dados órfãos no banco.

**Solução adotada (C1/F2):** Substituir `return Results.BadRequest(...)` por `throw new ValidationException("mensagem")`.

**Vantagens:**
1. **Único ponto de rollback** — o `catch` captura tanto exceções inesperadas quanto validações de negócio
2. **Impossível esquecer rollback** — diferentemente de chamar `transaction.Rollback()` explicitamente antes de cada `return`
3. **Reuso** — a `ValidationException` é tratada pelo middleware do Item 3, retornando 400 padronizado
4. **Consistência** — todo fluxo anômalo passa pelo mesmo caminho (catch → rollback → relançamento)

### Correção N1 — Substituir estratégia de timeout de transação

**Problema identificado (N1):** O plano v2 usava `transaction.CommandTimeout = 30` para definir o timeout no objeto da transação. A auditoria final determinou que o timeout deve ser passado como parâmetro explícito `commandTimeout: 30` **em cada chamada Dapper** (`ExecuteAsync` / `QueryAsync`).

**Motivação (N1):**
- O `commandTimeout` em nível de comando é mais granular e previsível
- Evita comportamento inesperado se a transação for reutilizada em diferentes contextos
- Torna explícito em cada chamada qual é o timeout aplicado
- Alinhado com as boas práticas do Dapper para operações transacionais

### Arquivos afetados

| Arquivo | Tipo de alteração |
|---------|-------------------|
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1651) | Envolver todo o bloco de confirmação em uma transação SQL |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1651) | Substituir `return Results.BadRequest(...)` por `throw new ValidationException(...)` (C1) |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1651) | Adicionar `commandTimeout: 30` em TODAS as chamadas Dapper dentro da transação (N1) |

### Alterações necessárias

1. **Program.cs** → No endpoint de confirmação, após obter a conexão (`IDbConnection`), iniciar uma transação com `db.BeginTransaction()`.
2. **Timeout (N1)** → NÃO usar `transaction.CommandTimeout = 30`. Em vez disso, passar `commandTimeout: 30` como parâmetro em **cada** chamada `ExecuteAsync` e `QueryAsync`.
3. **Passar a transação** para todas as chamadas Dapper usando o parâmetro `transaction:`.
4. **Commit** ao final, se tudo ocorrer bem.
5. **Rollback (C1/F2)** em caso de qualquer exceção **ou validação**.
6. **Validações (C1)** → Trocam `return Results.BadRequest(...)` por `throw new ValidationException("mensagem")`.

### Código esboço da mudança

```csharp
app.MapPost("/api/carrinho/{cpf}/confirmar", async (IDbConnection db, string cpf, ...) =>
{
    using var transaction = db.BeginTransaction();
    // N1: NÃO usar transaction.CommandTimeout = 30
    //     Em vez disso, passar commandTimeout: 30 em cada chamada Dapper
    
    try
    {
        // Exemplo de chamada Dapper com commandTimeout:
        var reservaId = await db.QuerySingleAsync<int>(
            sql, parameters, transaction: transaction, commandTimeout: 30);

        // ... validações existentes ...
        // C1: ONDE ANTES TINHA: return Results.BadRequest(...)
        //      AGORA TEM: throw new ValidationException("mensagem")
        
        // ... mais operações Dapper com transaction: transaction, commandTimeout: 30 ...
        
        transaction.Commit();
        return Results.Created(..., response);
    }
    catch
    {
        transaction.Rollback(); // C1/F2: rollback garantido
        throw; // Relança para o middleware de exception handling (Item 3)
    }
});
```

### Dependências

| Item | Dependência | Motivo |
|:----:|:-----------:|--------|
| Item 3 | `ValidationException` | Exceção customizada definida no Item 3 é usada para disparar rollback |

### Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| Transação longa (muitos itens no carrinho) | Baixa | Médio | Carrinhos têm no máximo ~10 itens; transação dura milissegundos. `commandTimeout: 30` em cada comando (N1) |
| Deadlock no SQL Server | Muito baixa | Alto | As operações seguem ordem consistente (Reservas → Ingressos); usar `READ COMMITTED` |
| Rollback não chamado em cenário de exceção | Baixa | Alto | `using` garante dispose; `catch` garante rollback antes do `throw` |
| ValidationException não capturada se middleware não estiver instalado | Média | Médio | **Ordem de implementação:** Item 3 (middleware) vem ANTES do Item 4 |
| **Esquecer `commandTimeout: 30` em alguma chamada Dapper (N1)** | Média | Baixo | Revisão de código obrigatória; todas as chamadas dentro do bloco `try` devem ter o parâmetro |

### Estratégia de rollback

Reverter as alterações no endpoint de confirmação, removendo o gerenciamento de transação e restaurando `return Results.BadRequest(...)` originais. Remover arquivo `ValidationException.cs`.

### Critérios de aceite

- [x] Se a confirmação do carrinho falhar após criar algumas reservas, **todas** as reservas e ingressos criados são desfeitos (C1)
- [x] Se uma validação de negócio falha no meio do loop (ex: limite de 2 reservas), o rollback é executado (C1)
- [x] Nenhum registro órfão permanece no banco após falha
- [x] Em caso de sucesso, todas as operações são persistidas (commit)
- [x] **Timeout de 30 segundos passado como `commandTimeout: 30` em TODAS as chamadas Dapper dentro da transação (N1)**
- [x] **NÃO é usado `transaction.CommandTimeout` (N1)**
- [x] Testes existentes que validam regras de negócio do carrinho continuam passando

---

## Item 5 — Corrigir interpolações SQL inseguras

### Objetivo

Eliminar construções SQL que concatenam strings diretamente (susceptíveis a SQL injection), substituindo por parâmetros ou white-list validation.

### Estado atual

Foram identificados **2 pontos** de interpolação SQL insegura:

#### 5.1. Função `CriarOuRecriarView` — [`Program.cs`](../src/TicketPrime.Api/Program.cs:361)

O nome da view é interpolado diretamente no SQL.

**Correção A3:** Além da segurança, a estratégia de `DROP VIEW` + `CREATE VIEW` é **frágil**: se o `CREATE` falhar (ex: erro de sintaxe na view), a view foi deletada e não pode ser recuperada. A abordagem segura é usar `CREATE OR ALTER VIEW` (disponível desde SQL Server 2016 SP1):

```csharp
// A3: Abordagem definitiva: substituir DROP+CREATE por CREATE OR ALTER VIEW
// A string createSql deve conter "CREATE OR ALTER VIEW" em vez de "CREATE VIEW"
```

Isso exige modificar a função `CriarOuRecriarView` para:
1. Validar o nome da view com `QUOTENAME()` (segurança)
2. Usar `CREATE OR ALTER VIEW` em vez de `DROP` + `CREATE` (atomicidade — correção A3)

```csharp
static async Task CriarOuRecriarView(IDbConnection db, string nomeView, string createSql)
{
    // A3: CREATE OR ALTER VIEW é atômico e seguro
    // A string createSql já deve conter "CREATE OR ALTER VIEW"
    await db.ExecuteAsync(createSql);
    Console.WriteLine($"View {nomeView} verificada/criada com sucesso.");
}
```

**Impacto (A3):** As strings de criação das views (`vw_DashboardEventos` e `vw_DashboardLotes`) devem ser alteradas de `CREATE VIEW` para `CREATE OR ALTER VIEW`.

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
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:361) | Substituir `DROP VIEW` por `CREATE OR ALTER VIEW` (A3) |
| [`src/TicketPrime.Api/Program.cs`](../src/TicketPrime.Api/Program.cs:1995) | Refatorar query dinâmica para usar SQL fixo com parâmetros opcionais |

### Alterações necessárias

**5.1 (A3).** Modificar a função `CriarOuRecriarView` para aceitar que `createSql` já contenha `CREATE OR ALTER VIEW` e remover o `DROP VIEW`:

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
- [x] A função `CriarOuRecriarView` usa `CREATE OR ALTER VIEW` (DROP removido) — correção A3
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

## Item 7 — Endpoint /health (F4)

### Objetivo

Disponibilizar um endpoint de health check (tarefa F4) para:
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

O endpoint (F4):
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

## Ordem de implementação recomendada

### Nova ordem de execução (v3 — correção N3 aplicada)

| Passo | Item | Correções | Motivo |
|:-----:|------|:----------:|--------|
| **0** | **Item 0 — .gitignore** | **C2, F1** | **Pré-requisito obrigatório.** Sem ele, qualquer alteração de configuração corre risco de versionar credenciais |
| 1 | **Item 3 — Middleware de exception handling** | **A2, F3** | Deve vir primeiro para capturar erros dos demais itens. Inclui `ValidationException` necessária para o Item 4 |
| 2 | **Item 1 — Remover senha hardcoded** | — | Segurança crítica; depende do `.gitignore` (Item 0) para proteger `appsettings.Development.json` |
| 3 | **Item 7 — Endpoint /health** | **F4** | Simples, independente, útil para validar que a aplicação está rodando durante os demais passos |
| 4 | **Item 5 — Corrigir SQL injection** | **A3** | Segurança; altera queries existentes. Inclui correção A3 (`CREATE OR ALTER VIEW`) |
| 5 | **Item 4 — Transações no carrinho** | **C1, F2, F6, N1** | Estabilidade; altera lógica de negócio. Depende do Item 3 (`ValidationException`). Inclui `commandTimeout: 30` nas chamadas Dapper (N1) |
| 6 | **Item 2 — Autenticação admin com logging (fundido N3)** | **A1, F5, N2, N3** | **Fundido com antigo Item 8 (N3).** Inclui `ApiKeyAuthenticationSchemeOptions` (N2) e logging de acessos (F5) |
| 7 | **Item 6 — Configurar CORS** | — | Baixo risco; independe dos demais |

### Justificativa

| Passo | Justificativa detalhada |
|:-----:|-------------------------|
| 0 | **Item 0 (.gitignore)** — Correção **C2/F1**. É pré-requisito absoluto — sem ele, não podemos proteger credenciais. Qualquer alteração no `appsettings.json` sem `.gitignore` exporia senhas no Git |
| 1 | **Item 3 (Middleware)** — Correções **A2/F3**. Precisa estar no ar antes de qualquer mudança que possa gerar exceções não tratadas. Também define a `ValidationException` que o Item 4 necessita |
| 2 | **Item 1 (Senha)** — Depende do Item 0 para proteger o arquivo de desenvolvimento. É a correção de segurança mais crítica após o middleware |
| 3 | **Item 7 (Health)** — Tarefa **F4**. É rápido e fornece um endpoint básico de validação para acompanhar se a aplicação está de pé durante as demais alterações |
| 4 | **Item 5 (SQL Injection)** — Correção **A3**. Inclui a substituição de `DROP VIEW` por `CREATE OR ALTER VIEW` — essencial para não quebrar as views durante alterações futuras |
| 5 | **Item 4 (Transações)** — Correções **C1/F2/F6/N1**. É a mudança mais delicada. Depende do Item 3 (`ValidationException`) e deve vir após as correções de segurança. Inclui `commandTimeout: 30` em todas as chamadas Dapper (N1) |
| 6 | **Item 2 (Auth + Logging)** — Correções **A1/F5/N2/N3**. Fundido com o antigo Item 8 (N3). Implementa `ApiKeyAuthenticationSchemeOptions` (N2) e logging de acessos não autorizados (F5) em uma única etapa |
| 7 | **Item 6 (CORS)** — É o mais simples e pode ficar por último sem impacto nos demais |

### Mapa de dependências atualizado (v3)

```
Item 0 (.gitignore) ────────────────────────────────────────► Item 1 (Senha)
                                                                     │
Item 3 (Middleware) ──► ValidationException ──► Item 4 (Transações)
        │                                            │
        └──► BadHttpRequestException → 400 (A2/F3)   └──► commandTimeout:30 (N1)

Item 5 (SQL Injection) ←── A3 (CREATE OR ALTER VIEW)

Item 7 (Health) ─── F4 (tarefa faltante)

Item 2 (Auth + Logging) ─── A1, F5, N2, N3 (fundido)
        │
        ├── ApiKeyAuthenticationSchemeOptions (N2)
        └── Logging de acessos (F5)

Item 6 (CORS)
```

---

## Resumo de dependências

### Pacotes NuGet

| Pacote | Versão | Item | Motivo |
|--------|--------|:----:|--------|
| Nenhum | — | Todos | **Nenhum pacote extra é necessário.** Todos os itens usam APIs nativas do ASP.NET Core 8 |

### Dependências entre itens

| Item | Depende de | Correções vinculadas |
|:----:|------------|:--------------------:|
| 0 (.gitignore) | Nenhuma | C2, F1 |
| 1 (Senha) | Item 0 (.gitignore protege `appsettings.Development.json`) | — |
| 2 (Auth + Logging) | Nenhuma | A1, F5, N2, N3 |
| 3 (Middleware) | Nenhuma | A2, F3 |
| 4 (Transações) | Item 3 (`ValidationException`) | C1, F2, F6, N1 |
| 5 (SQL Injection) | Nenhuma | A3 |
| 6 (CORS) | Nenhuma | — |
| 7 (Health) | Nenhuma | F4 |

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

| Item | Testes sugeridos | Correção validada |
|:----:|------------------|:-----------------:|
| 0 (.gitignore) | Verificar que `.gitignore` existe e cobre os padrões necessários | C2, F1 |
| 2 (Auth + Logging) | Testar requisições admin com/sem API Key, chave válida/inválida; verificar logs de acesso (IP + path) | A1, F5, N2, N3 |
| 3 (Exception handling) | Testar: (a) exceção genérica → 500, (b) `BadHttpRequestException` → 400, (c) `ValidationException` → 400 | A2, F3 |
| 4 (Transações) | Testar falha no meio do fluxo e verificar que **nenhum** dado foi persistido (rollback funciona); verificar `commandTimeout: 30` em todas as chamadas Dapper (N1) | C1, F2, F6, N1 |
| 5 (SQL injection) | Testar que caracteres maliciosos em parâmetros não quebram a query; testar `CREATE OR ALTER VIEW` | A3 |
| 6 (CORS) | Testar requisições com `Origin` header permitida e não permitida | — |
| 7 (Health) | Testar `/health` retorna 200 quando banco OK; testar comportamento com banco indisponível | F4 |

---

## Critérios de aceite globais

### Funcionais

- [x] **Item 0:** `.gitignore` criado e protegendo arquivos sensíveis (C2/F1)
- [x] **Item 1:** Nenhuma credencial em texto plano em arquivos versionados
- [x] **Item 2 (fundido N3):** Endpoints admin protegidos por API Key; logging de acessos autorizados e não autorizados com IP e path; `ApiKeyAuthenticationSchemeOptions` criado (N2); handler herda de `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` (N2); nenhuma menção a JWT (A1)
- [x] **Item 3:** Exceções tratadas com JSON padronizado; `BadHttpRequestException` → 400 (A2/F3)
- [x] **Item 4:** Transações com rollback garantido via `ValidationException`; `commandTimeout: 30` em todas as chamadas Dapper (N1), sem `transaction.CommandTimeout` (C1/F2/F6/N1)
- [x] **Item 5:** Nenhuma concatenação SQL; `CREATE OR ALTER VIEW` substitui `DROP VIEW` (A3)
- [x] **Item 6:** CORS configurado para origens permitidas
- [x] **Item 7:** Endpoint `/health` disponível e funcional (F4)

### Não funcionais

- [x] Nenhum pacote NuGet adicional é necessário
- [x] Nenhuma das alterações quebra a API pública (contratos request/response preservados)
- [x] Nenhum model ou service existente é modificado (alterações apenas em `Program.cs`, novos arquivos)
- [x] Testes unitários existentes continuam passando sem modificações
- [x] Ordem de implementação respeita dependências entre itens

### Checklist de segurança

- [x] Credenciais nunca versionadas (Item 0 + Item 1) — C2/F1
- [x] SQL injection eliminado (Item 5) — A3
- [x] Autenticação em endpoints admin com logging de auditoria (Item 2 fundido) — A1/F5/N2/N3
- [x] Logging de acessos não autorizados fundido na autenticação (Item 2) — N3
- [x] Rollback de transações em caso de falha (Item 4) — C1/F2
- [x] Timeout de transações via `commandTimeout: 30` nas chamadas Dapper (Item 4) — N1
- [x] Tratamento padronizado de exceções (Item 3) — A2/F3
- [x] Health check para monitoramento (Item 7) — F4

---

## Considerações finais

1. **Nenhuma dessas alterações quebra a API pública.** Todos os contratos dos endpoints (request/response) permanecem idênticos.
2. **A Fase 1 não introduz novas funcionalidades.** É puramente segurança e estabilidade sobre o que já existe.
3. **Todas as alterações são feitas no `Program.cs` ou em novos arquivos.** Nenhum model ou service existente é modificado.
4. **A correção C1 (rollback via exceção customizada) foi a decisão arquitetural mais significativa**, pois impacta diretamente a consistência dos dados e o padrão de tratamento de erros em toda a aplicação.
5. **A correção A3 (CREATE OR ALTER VIEW) elimina um ponto de falha catastrófico** onde uma falha no meio da recriação de views poderia deixar o dashboard quebrado.
6. **As tarefas F4 e F5** (health check e logging) preenchem lacunas de operabilidade e auditabilidade essenciais para produção.
7. **A correção N1** substitui `transaction.CommandTimeout` por `commandTimeout: 30` em cada chamada Dapper, tornando o timeout mais explícito e granular.
8. **A correção N2** adiciona a classe `ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions` e corrige a herança do handler para `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>`.
9. **A correção N3** funde a autenticação admin (Item 2) com o logging de acessos não autorizados (antigo Item 8) em uma única etapa consolidada, eliminando dependências cruzadas e simplificando a execução.
10. **A ordem de execução foi refinada para 8 passos** (anteriormente 9), com a fusão do Item 8 no Item 2, garantindo que cada item tenha suas dependências satisfeitas antes de ser implementado, minimizando retrabalho e riscos de regressão.

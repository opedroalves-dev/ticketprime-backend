# Planejamento — Etapa 2: Criar Interface e Implementação Base de Repositórios

**Projeto:** TicketPrime — Fase 2: Separação de Camadas e Redução do Acoplamento
**Data:** 2026-06-03
**Risco:** Baixo
**Correção:** C6 (convenção `IDbTransaction? transaction = null`)

---

## 1. Objetivo da Etapa 2

Estabelecer o **padrão de repositório** como camada de acesso a dados, criando a estrutura base de interfaces e implementações, e instituindo a **convenção C6 obrigatória**: todos os métodos de repositório que executam SQL DEVEM aceitar `IDbTransaction? transaction = null` como último parâmetro.

Isso permite que:
1. Os endpoints em [`Program.cs`](src/TicketPrime.Api/Program.cs) deixem de acessar `IDbConnection` diretamente e passem a depender de interfaces de repositório.
2. Na Etapa 11b (confirmação de carrinho transacional), múltiplos repositórios compartilhem a mesma transação via `IDbTransaction?`.

**Importante:** Esta etapa **não migra** nenhum endpoint. Ela apenas **cria a infraestrutura** que será usada pelas etapas seguintes (3 a 12).

---

## 2. Arquivos que serão alterados

| Arquivo | Tipo de Alteração | Descrição |
|---------|:-----------------:|-----------|
| [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs) | Modificação | Adicionar registros DI: `builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>()` |
| [`src/TicketPrime.Api/TicketPrime.Api.csproj`](src/TicketPrime.Api/TicketPrime.Api.csproj) | Nenhuma | Não há dependências novas a adicionar (Dapper e SqlClient já estão no csproj) |

---

## 3. Arquivos que serão criados

### 3.1. Interface base (padrão, sem métodos genéricos)

| Arquivo | Conteúdo |
|---------|----------|
| `src/TicketPrime.Api/Repositories/IUsuarioRepository.cs` | Interface com métodos: `ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null)`, `InserirAsync(Usuario usuario, IDbTransaction? transaction = null)`, `ExisteAsync(string cpf, IDbTransaction? transaction = null)` |

### 3.2. Implementação concreta (exemplo inicial)

| Arquivo | Conteúdo |
|---------|----------|
| `src/TicketPrime.Api/Repositories/UsuarioRepository.cs` | Implementação injetando `IDbConnection` no construtor. Cada método encapsula a consulta SQL correspondente e **aplica C6** (`IDbTransaction? transaction = null` como último parâmetro). |

### 3.3. Expansão futura (etapas 3-12)

> **Nota:** As interfaces e implementações dos demais domínios (Evento, Cupom, Reserva, Ingresso, CheckIn, TipoIngresso, Carrinho, HistoricoPreco, Dashboard) serão criadas em suas respectivas etapas, **nesta Etapa 2**. A Etapa 2 cria APENAS o `UsuarioRepository` como arquétipo do padrão.

---

## 4. Dependências da etapa

### 4.1. Pré-requisitos (já atendidos)

- [x] **Etapa 1 concluída:** Requests Models extraídos para [`Models/`](src/TicketPrime.Api/Models/)
- [x] **Build OK:** `dotnet build` compila sem erros
- [x] **Testes OK:** `dotnet test` passa 103/103
- [x] **Checkpoint Git:** `git tag fase2-checkpoint-inicial` existe

### 4.2. Dependências para etapas futuras

| Etapa | Depende da Etapa 2? | Motivo |
|:-----:|:-------------------:|--------|
| 3-9, 10b, 11a, 11b, 12 | **Sim** | Todas usam o padrão Repository + C6 estabelecido aqui |
| 10a | **Não** | Extração de `RegrasReserva` é puramente dentro de [`Services/`](src/TicketPrime.Api/Services/), sem banco |

### 4.3. Nenhuma dependência externa

- Nenhum pacote NuGet novo (Dapper e Microsoft.Data.SqlClient já estão no csproj)
- Nenhuma dependência de banco de dados
- Nenhuma dependência de infraestrutura externa

---

## 5. Riscos

| # | Risco | Probabilidade | Impacto | Mitigação |
|:-:|-------|:-------------:|:-------:|-----------|
| R2.1 | **Nome de namespace incorreto** — `TicketPrime.Api.Repositories` não ser reconhecido | Baixa | Médio | Usar `namespace TicketPrime.Api.Repositories;` (file-scoped) consistente com o resto do projeto |
| R2.2 | **Esquecer de registrar DI** para `IUsuarioRepository`/`UsuarioRepository` | Média | Médio | Checklist pós-implementação incluir verificação de `builder.Services.AddScoped<>()` em [`Program.cs`](src/TicketPrime.Api/Program.cs) |
| R2.3 | **Convenção C6 não aplicada corretamente** — método sem `IDbTransaction?` | Média | Alto (futuro) | Revisão de código obrigatória; violação da convenção bloqueia o PR |
| R2.4 | **Quebra de testes existentes** por importação acidental | Muito Baixa | Alto | Nenhum teste existente referencia `Repositories/` — risco teórico |
| R2.5 | **Conflito com models inline removidos na Etapa 1** — algum `using` removido acidentalmente | Baixa | Médio | `dotnet build` detecta any missing using; CI valida |

---

## 6. Critérios de aceite

### 6.1. Obrigatórios

- [ ] **CA2.1:** [`Repositories/UsuarioRepository.cs`](src/TicketPrime.Api/Repositories/UsuarioRepository.cs) compila sem erros
- [ ] **CA2.2:** Convenção **C6** aplicada em todos os métodos de [`UsuarioRepository`](src/TicketPrime.Api/Repositories/UsuarioRepository.cs) — `IDbTransaction? transaction = null` como último parâmetro
- [ ] **CA2.3:** [`IUsuarioRepository`](src/TicketPrime.Api/Repositories/IUsuarioRepository.cs) registrado no DI em [`Program.cs`](src/TicketPrime.Api/Program.cs)
- [ ] **CA2.4:** `dotnet build` compila com zero erros
- [ ] **CA2.5:** `dotnet test` passa 103/103 **sem modificações**
- [ ] **CA2.6:** Nenhum endpoint existente foi alterado (não há migração de endpoint nesta etapa)
- [ ] **CA2.7:** Nenhuma classe [`Models/`](src/TicketPrime.Api/Models/) foi alterada
- [ ] **CA2.8:** Nenhum arquivo de teste foi alterado

### 6.2. Verificações de qualidade

- [ ] **CA2.9:** Nomes de métodos seguem padrão do projeto (PascalCase, Async suffix)
- [ ] **CA2.10:** SQL encapsulado é idêntico ao SQL inline atual em [`Program.cs`](src/TicketPrime.Api/Program.cs) — sem alteração de queries
- [ ] **CA2.11:** Nenhum warning novo de compilação (exceto possíveis nullability warnings pré-existentes)

---

## 7. Estratégia de rollback

### 7.1. Procedimento

```bash
# Opção 1 — Reverter commit (recomendado)
git revert HEAD --no-edit

# Opção 2 — Checkout manual (se houver checkpoint)
git checkout HEAD~1
```

### 7.2. Esforço estimado

| Ação | Tempo |
|------|:-----:|
| Remover diretório `Repositories/` | ~1 min |
| Remover registros DI do [`Program.cs`](src/TicketPrime.Api/Program.cs) | ~1 min |
| Executar `dotnet build` e `dotnet test` para confirmar estado anterior | ~2 min |
| **Total** | **~4 min** |

### 7.3. Verificação pós-rollback

```bash
dotnet build    # zero erros
dotnet test     # 103/103
```

---

## 8. Impacto esperado no Program.cs

### 8.1. Linhas adicionadas

**No bloco de DI (`builder.Services`), após a linha 15 (registro de `IDbConnection`):**

```csharp
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
```

Total: **1 linha adicionada** (expansível conforme outros repositórios forem criados nas etapas seguintes).

### 8.2. Nenhuma outra alteração em Program.cs

- Nenhum endpoint é modificado
- Nenhuma configuração é alterada
- Nenhuma middleware é alterada
- Nenhum SQL é movido (isso ocorre nas Etapas 3-12)

### 8.3. Evolução esperada do Program.cs ao longo da Fase 2

| Etapa | Linhas adicionadas/removidas | Acumulado |
|:-----:|:---------------------------:|:---------:|
| 1 | -43 linhas (requests inline removidos) | ~2222 |
| **2** | **+1 linha (DI do UsuarioRepository)** | **~2223** |
| 3 | ~-20 linhas + 1 DI | ~2204 |
| 4 | ~-15 linhas + 1 DI | ~2190 |
| 5 | ~-25 linhas + 1 DI | ~2166 |
| ... | (progressivo) | ... |
| 12 | ~-20 linhas + 1 DI | ~300-400 |

---

## 9. O que NÃO será alterado

### 🚫 Blindado (não tocar)

| Item | Motivo |
|------|--------|
| **Contratos da API** (rotas, métodos HTTP, request/response bodies) | CA3 — nenhum endpoint é migrado nesta etapa |
| **Banco de Dados** (tabelas, colunas, constraints, índices, views) | CA5 — SQL permanece idêntico ao atual |
| **Regras de Negócio** (validações, cálculos, limites) | CA4 — repositório é apenas encapsulamento de SQL |
| **Autenticação e Autorização** | CA6 — nenhuma alteração |
| **CORS** | CA7 — nenhuma alteração |
| **Testes existentes** (103/103) | CA2 — nenhuma linha de teste é alterada |
| **Models** (36 arquivos em [`Models/`](src/TicketPrime.Api/Models/)) | Já extraídos na Etapa 1 |
| **Services existentes** ([`ReservaService`](src/TicketPrime.Api/Services/ReservaService.cs), [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs)) | Permanecem puros, sem injeção de repositório |
| **Middleware** ([`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs)) | Sem alterações |
| **Authentication** ([`ApiKeyAuthenticationHandler`](src/TicketPrime.Api/Authentication/ApiKeyAuthenticationHandler.cs)) | Sem alterações |
| **Estrutura de diretórios existente** | Nomes de pastas fixos conforme requisito |
| **`GerarCodigoUnicoAsync`** em [`Program.cs`](src/TicketPrime.Api/Program.cs) | Permanece inline — será movido na Etapa 8 |

### ✅ O que é alterado (apenas)

1. **Criação** do diretório [`Repositories/`](src/TicketPrime.Api/Repositories/) com `IUsuarioRepository.cs` e `UsuarioRepository.cs`
2. **Adição** de 1 linha de DI em [`Program.cs`](src/TicketPrime.Api/Program.cs)

---

## 10. Checklist de Implementação

- [ ] Criar diretório `src/TicketPrime.Api/Repositories/`
- [ ] Criar `src/TicketPrime.Api/Repositories/IUsuarioRepository.cs` com:
  - `Task<Usuario?> ObterPorCpfAsync(string cpf, IDbTransaction? transaction = null)` — C6
  - `Task<bool> ExisteAsync(string cpf, IDbTransaction? transaction = null)` — C6
  - `Task InserirAsync(Usuario usuario, IDbTransaction? transaction = null)` — C6
- [ ] Criar `src/TicketPrime.Api/Repositories/UsuarioRepository.cs` com:
  - Construtor injetando `IDbConnection`
  - Implementação dos 3 métodos encapsulando SQL idêntico ao de [`Program.cs`](src/TicketPrime.Api/Program.cs)
  - **Verificar C6** em cada método
- [ ] Adicionar `using TicketPrime.Api.Repositories;` no topo de [`Program.cs`](src/TicketPrime.Api/Program.cs)
- [ ] Adicionar `builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>()` no bloco DI de [`Program.cs`](src/TicketPrime.Api/Program.cs)
- [ ] Executar `dotnet build` (zero erros)
- [ ] Executar `dotnet test` (103/103 aprovados)
- [ ] Revisar se nenhum endpoint foi alterado
- [ ] Commitar com mensagem: `feat: cria Repositories/ com UsuarioRepository e convenção C6 (IDbTransaction?)`

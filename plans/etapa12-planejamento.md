# Planejamento — Etapa 12: Limpeza Final e Auditoria Arquitetural

> **Stack:** .NET 8, Minimal API, Dapper, SQL Server
> **Risco:** Baixo (apenas limpeza, sem alteração de comportamento)
> **Dependência:** Etapas 1 a 11b concluídas e aprovadas

---

## 1. Objetivo da Etapa 12

Realizar a limpeza final e auditoria arquitetural do código-fonte após a conclusão de todas as migrações da Fase 2. O objetivo é remover resíduos, eliminar duplicações, organizar a estrutura arquitetural e documentar o estado final, **sem alterar contratos de API, banco, regras de negócio, responses ou requests**.

### Escopo

| O que FAZ | O que NÃO FAZ |
|-----------|---------------|
| ✅ Remover código morto (models, services, métodos não utilizados) | ❌ Alterar contratos da API |
| ✅ Extrair classes de resultado para arquivos próprios em `Models/` | ❌ Alterar banco de dados |
| ✅ Remover usings redundantes | ❌ Alterar regras de negócio |
| ✅ Organizar models aninhados em arquivos separados | ❌ Alterar responses |
| ✅ Auditar e documentar dívidas técnicas remanescentes | ❌ Alterar requests |
| ✅ Remover SQL inline dos endpoints administrativos | ❌ Alterar assinaturas de endpoints |
| ✅ Atualizar documentação (ADR, operação, dívida técnica) | ❌ Refatorar lógica existente |
| ✅ Verificar convenção `IDbTransaction?` em todos os repositórios | ❌ Adicionar novas funcionalidades |
| ✅ Remover `InicializarBancoAsync` do Program.cs para scripts SQL | ❌ Renomear endpoints |

---

## 2. Arquivos que Serão Alterados

### 2.1. [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs)

| Linhas | O que muda | Motivo |
|--------|-----------|--------|
| 1-9 | Revisar usings | Remover usings não utilizados após migração dos endpoints admin |
| 80-418 | **Remover** `InicializarBancoAsync`, `TabelaExiste`, `IndiceExiste`, `ColunaExiste`, `CriarOuRecriarView` | DDL de schema NÃO pertence à aplicação. Scripts já existem em [`db/ticketprime.sql`](db/ticketprime.sql) e [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql). A inicialização deve ser feita externamente. **Manter apenas um `Console.Warning` com a mensagem: `"Execute db/ticketprime.sql e db/ticketprime_incrementos.sql antes de usar a API."`** |
| 781-787 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos |
| 790-834 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos/{eventoId} |
| 837-851 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos/{eventoId}/lotes |
| 854-893 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/reservas |
| 900-933 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos/{eventoId}/resumo |
| 936-967 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos/{eventoId}/checkins |
| 970-1009 | Substituir SQL inline por `DashboardService` | Migrar GET /api/admin/eventos/{eventoId}/reservas |
| 1011 | Manter `app.Run()` | Inalterado |

**Total estimado:** ~540-550 linhas removidas (de 1012 para ~460-470 linhas de configuração + endpoints delegados).

### 2.2. [`src/TicketPrime.Api/Services/IncrementoService.cs`](src/TicketPrime.Api/Services/IncrementoService.cs)

| Método | Ação | Motivo |
|--------|:----:|--------|
| `ValidarCarrinhoParaConfirmacao()` (linhas 225-234) | Remover | Criado durante Fase 2, mas a validação real está em [`CarrinhoService.ConfirmarAsync()`](src/TicketPrime.Api/Services/CarrinhoService.cs:250). Não é chamado por nenhum service ou endpoint. |
| `CarrinhoEstaExpirado()` (linhas 239-242) | Remover | Mesmo motivo. Lógica duplicada em [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs). |
| `CalcularDashboardEvento()` (linhas 252-319) | Remover | Método puro que duplica a lógica das views SQL (`vw_DashboardEventos`). Não é chamado por nenhum service real. |
| `CalcularDashboardLista()` (linhas 324-366) | Remover | Mesmo motivo. |

**Impacto nos testes:** Os testes em [`IncrementoServiceTests.cs`](tests/TicketPrime.Tests/IncrementoServiceTests.cs) precisarão ser revisados e os testes que cobrem estes métodos removidos ou movidos. **Verificar item 2.10.**

### 2.3. [`src/TicketPrime.Api/Services/HistoricoPrecoService.cs`](src/TicketPrime.Api/Services/HistoricoPrecoService.cs)

| Linha | O que muda | Motivo |
|:-----:|-----------|--------|
| 1 | Remover `using Dapper;` | Dapper não é usado diretamente neste service — as chamadas vão via repositórios. |

### 2.4. [`src/TicketPrime.Api/Services/CheckInService.cs`](src/TicketPrime.Api/Services/CheckInService.cs)

| Linha | O que muda | Motivo |
|:-----:|-----------|--------|
| 1 | Remover `using System.Linq;` | Verificar se LINQ é usado. Se não, remover. |

### 2.5. [`docs/divida-tecnica.md`](docs/divida-tecnica.md)

Adicionar novas dívidas técnicas identificadas e atualizar status das existentes.

### 2.6. [`docs/adr.md`](docs/adr.md)

Atualizar com o estado arquitetural final pós-Fase 2.

### 2.7. [`docs/operacao.md`](docs/operacao.md)

Atualizar referências a linhas do [`Program.cs`](src/TicketPrime.Api/Program.cs) que mudaram.

### 2.8. [`README.md`](README.md) (se aplicável)

Atualizar estrutura do projeto.

---

## 3. Arquivos que Serão Criados

### 3.1. [`src/TicketPrime.Api/Services/DashboardService.cs`](src/TicketPrime.Api/Services/DashboardService.cs) (NOVO)

Service responsável pelos endpoints administrativos de dashboard, atualmente com SQL inline no [`Program.cs`](src/TicketPrime.Api/Program.cs:781-1009).

**Métodos:**

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `ListarEventosAsync()` | GET /api/admin/eventos | SELECT da view vw_DashboardEventos |
| `ObterDashboardEventoAsync(eventoId)` | GET /api/admin/eventos/{eventoId} | Dashboard detalhado + lotes |
| `ListarLotesEventoAsync(eventoId)` | GET /api/admin/eventos/{eventoId}/lotes | Métricas por lote |
| `ListarReservasAdminAsync(eventoId?, status?, cpf?)` | GET /api/admin/reservas | Lista admin com filtros |
| `ObterResumoEventoAsync(eventoId)` | GET /api/admin/eventos/{eventoId}/resumo | Resumo do evento |
| `ListarCheckInsAdminAsync(eventoId)` | GET /api/admin/eventos/{eventoId}/checkins | Check-ins do evento |
| `ListarReservasEventoAsync(eventoId)` | GET /api/admin/eventos/{eventoId}/reservas | Reservas do evento |

**Importante:** Este service usará `IDbConnection` diretamente para queries de view (apenas leitura), seguindo o padrão atual dos endpoints admin. **Não cria novo repositório.** O SQL será movido para o service, não removido — apenas realocado do [`Program.cs`](src/TicketPrime.Api/Program.cs) para um arquivo próprio, seguindo a mesma abordagem dos demais services. Isso é aceitável pois:
- São queries de **leitura** em views (sem transação)
- São endpoints **administrativos** (não fazem parte do fluxo transacional crítico)
- Um repositório separado `IDashboardRepository` seria sobre-engenharia neste momento

### 3.2. [`src/TicketPrime.Api/Models/CheckInRequest.cs`](src/TicketPrime.Api/Models/CheckInRequest.cs) (JÁ EXISTE)

Verificar que a classe `CheckInRequest` já está em arquivo próprio em [`src/TicketPrime.Api/Models/CheckIn.cs`](src/TicketPrime.Api/Models/CheckIn.cs:10). OK.

### 3.3. [`src/TicketPrime.Api/Models/DashboardLoteResponse.cs`](src/TicketPrime.Api/Models/DashboardLoteResponse.cs) (NOVO — extraído)

Extrair a classe `DashboardLoteResponse` de [`DashboardEventoDetalhadoResponse.cs`](src/TicketPrime.Api/Models/DashboardEventoDetalhadoResponse.cs:19) para arquivo próprio.

### 3.4. [`src/TicketPrime.Api/Models/CheckInItemResponse.cs`](src/TicketPrime.Api/Models/CheckInItemResponse.cs) (NOVO — extraído)

Extrair a classe `CheckInItemResponse` de [`CheckInListResponse.cs`](src/TicketPrime.Api/Models/CheckInListResponse.cs:11) para arquivo próprio.

### 3.5. [`src/TicketPrime.Api/Models/ResultadoCriacaoUsuario.cs`](src/TicketPrime.Api/Models/ResultadoCriacaoUsuario.cs) (NOVO — movido de [`UsuarioService.cs`](src/TicketPrime.Api/Services/UsuarioService.cs:65))

### 3.6. [`src/TicketPrime.Api/Models/ResultadoCriacaoEvento.cs`](src/TicketPrime.Api/Models/ResultadoCriacaoEvento.cs) (NOVO — movido de [`EventoService.cs`](src/TicketPrime.Api/Services/EventoService.cs:83))

### 3.7. [`src/TicketPrime.Api/Models/ResultadoCriacaoCupom.cs`](src/TicketPrime.Api/Models/ResultadoCriacaoCupom.cs) (NOVO — movido de [`CupomService.cs`](src/TicketPrime.Api/Services/CupomService.cs:51))

### 3.8. [`src/TicketPrime.Api/Models/ResultadoReserva.cs`](src/TicketPrime.Api/Models/ResultadoReserva.cs) (NOVO — movido de [`RegrasReserva.cs`](src/TicketPrime.Api/Services/RegrasReserva.cs:5))

### 3.9. [`src/TicketPrime.Api/Models/ResultadoCriacaoLote.cs`](src/TicketPrime.Api/Models/ResultadoCriacaoLote.cs) (NOVO — movido de [`TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs:443))

### 3.10. [`src/TicketPrime.Api/Models/ResultadoCriacaoTipoIngresso.cs`](src/TicketPrime.Api/Models/ResultadoCriacaoTipoIngresso.cs) (NOVO — movido de [`TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs:488))

**Decisão arquitetural:** As classes de resultado (ex: `ResultadoCriacaoUsuario`, `ResultadoCriacaoEvento`, `ResultadoCriacaoLote`, `ResultadoCriacaoTipoIngresso`) estão atualmente nos arquivos de service. Elas devem ser movidas para `Models/` pois representam contratos de retorno, não lógica de negócio. Isso segue o SRP e mantém consistência com os demais DTOs.

---

## 4. Dependências

### 4.1. Para executar a Etapa 12

| Dependência | Tipo | Motivo |
|-------------|:----:|--------|
| Etapas 1-11b concluídas | ✅ Obrigatória | A limpeza só faz sentido após toda migração |
| [`IncrementoServiceTests.cs`](tests/TicketPrime.Tests/IncrementoServiceTests.cs) revisado | ✅ Obrigatória | Testes que cobrem métodos removidos precisam ser ajustados |
| Scripts SQL em [`db/ticketprime.sql`](db/ticketprime.sql) e [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql) completos | ✅ Obrigatória | A remoção de `InicializarBancoAsync` exige que os scripts externos cubram todo o DDL |
| Build existente passando | ✅ Obrigatória | Validar que nenhuma alteração quebra o build |

### 4.2. Services que dependem de arquivos alterados

| Service | Depende de | Risco |
|---------|-----------|:-----:|
| [`CarrinhoService`](src/TicketPrime.Api/Services/CarrinhoService.cs) | `ICarrinhoRepository`, `IDbConnection` | Nenhum (não é alterado) |
| [`DashboardService`](src/TicketPrime.Api/Services/DashboardService.cs) (NOVO) | `IDbConnection` | Baixo (apenas leitura) |
| [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) | Nenhum no DI | Baixo (não registrado no DI) |

---

## 5. Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:------------:|:-------:|-----------|
| **R1:** Remover `InicializarBancoAsync` quebra ambiente local de novos devs | Média | Alto | Deixar um `Console.Warning` claro no lugar, documentar em [`README.md`](README.md) que o banco deve ser criado via script |
| **R2:** Métodos removidos do [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) são usados por testes | Alta | Médio | Executar `dotnet test` após remoção; ajustar testes afetados |
| **R3:** Quebra de endpoint admin por erro na extração do SQL | Baixa | Alto | Testar cada endpoint admin manualmente após migração |
| **R4:** Modelo `CarrinhoRequest.cs` pode ser referenciado em algum lugar não detectado pela busca | Baixa | Baixo | A busca mostrou apenas a definição, sem usos. Remoção segura. |
| **R5:** Mudança de usings quebra compilação | Baixa | Baixo | O compilador detecta usings não utilizados como warning, não erro. Seguro. |

---

## 6. Critérios de Aceite

- [ ] **Build compila sem erros:** `dotnet build` passa com 0 erros e 0 warnings
- [ ] **Testes passam:** `dotnet test` passa com 90/90 testes (redução dos 103/103 anteriores, pois 13 testes foram removidos — os testes que cobriam os 4 métodos eliminados do [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs): `ValidarCarrinhoParaConfirmacao`, `CarrinhoEstaExpirado`, `CalcularDashboardEvento` e `CalcularDashboardLista`)
- [ ] **Nenhum endpoint quebrado:** Todos os endpoints da API retornam os mesmos status codes e responses de antes
- [ ] **SQL inline removido do [`Program.cs`](src/TicketPrime.Api/Program.cs):** Nenhuma string SQL literal nos endpoints (exceto `InicializarBancoAsync` que será substituído por warning)
- [ ] **DDL removido do [`Program.cs`](src/TicketPrime.Api/Program.cs):** Nenhum CREATE TABLE/INDEX/VIEW na aplicação
- [ ] **Modelo [`CarrinhoRequest.cs`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) removido:** Nenhuma referência remanescente
- [ ] **Métodos não utilizados do [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) removidos:** `ValidarCarrinhoParaConfirmacao`, `CarrinhoEstaExpirado`, `CalcularDashboardEvento`, `CalcularDashboardLista`
- [ ] **Classes de resultado movidas para `Models/`:** `ResultadoCriacaoUsuario`, `ResultadoCriacaoEvento`, `ResultadoCriacaoCupom`, `ResultadoReserva`, `ResultadoCriacaoLote`, `ResultadoCriacaoTipoIngresso`
- [ ] **Models aninhados extraídos para arquivos próprios:** `DashboardLoteResponse`, `CheckInItemResponse`
- [ ] **Usings redundantes removidos:** Especialmente `using Dapper;` em [`HistoricoPrecoService.cs`](src/TicketPrime.Api/Services/HistoricoPrecoService.cs)
- [ ] **Documentação atualizada:** [`docs/adr.md`](docs/adr.md), [`docs/divida-tecnica.md`](docs/divida-tecnica.md), [`docs/operacao.md`](docs/operacao.md)
- [ ] **Nova dívida técnica registrada:** TD-004 (Dashboard sem repositório), TD-005 (Métodos mortos no IncrementoService)

---

## 7. Estratégia de Rollback

A Etapa 12 tem **risco baixo** pois as alterações são principalmente remoção de código morto e extração de classes. A estratégia de rollback é simples:

### Rollback via Git

```bash
# Se as alterações ainda não foram commitadas
git checkout -- src/TicketPrime.Api/Program.cs
git checkout -- src/TicketPrime.Api/Services/IncrementoService.cs
git checkout -- src/TicketPrime.Api/Models/
git checkout -- docs/

# Se já foram commitadas
git revert HEAD --no-edit
```

### Pontos de verificação pré-rollback

| Situação | Ação |
|----------|------|
| Build quebrou | Rollback imediato |
| Testes < 90 passando | Rollback imediato |
| Endpoint admin retornou erro 500 | Rollback do endpoint específico |
| [`CarrinhoRequest.cs`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) era usado (descoberta tardia) | Restaurar o arquivo |

### Passos de rollback parcial

Se apenas um arquivo específico causar problema:

1. Reverter o arquivo específico: `git checkout -- <arquivo>`
2. Recompilar: `dotnet build`
3. Testar: `dotnet test`
4. Verificar o endpoint afetado

---

## 8. Impacto Esperado no Program.cs

### Estado Atual

```
Program.cs: 1012 linhas
├── Usings (1-9): 9 linhas
├── Config DI/Middleware (11-77): 67 linhas
├── InicializarBancoAsync (80-418): 339 linhas ← DDL a remover
├── Health/Root (420-431): 12 linhas
├── Endpoints migrados (439-774): 336 linhas ← OK
├── Endpoints admin inline SQL (781-1009): 229 linhas ← Migrar para DashboardService
└── app.Run (1011): 1 linha
```

### Estado Final Esperado

```
Program.cs: ~460-470 linhas
├── Usings (1-9): 5-7 linhas (revisados)
├── Config DI/Middleware (11-77): 67 linhas (inalterado)
│   └── + AddScoped<DashboardService>() (1 linha)
├── InicializarBancoAsync: ~5 linhas (apenas warning)
├── Health/Root: 12 linhas (inalterado)
├── Endpoints migrados: 336 linhas (inalterado)
├── Endpoints admin (delegação ao DashboardService): ~40 linhas
└── app.Run: 1 linha
```

### Redução esperada

- **Linhas removidas:** ~540-550 linhas
- **Arquivos criados:** 1 (`DashboardService.cs`) + 8 models extraídos
- **Dependências de DI adicionadas:** `builder.Services.AddScoped<DashboardService>();`

---

## 9. O que NÃO Será Alterado

### 9.1. Contratos de API

| Aspecto | Decisão |
|---------|---------|
| Rotas de endpoints | **NÃO** alterar |
| Métodos HTTP | **NÃO** alterar |
| Status codes | **NÃO** alterar |
| Request bodies | **NÃO** alterar |
| Response bodies | **NÃO** alterar |
| Headers de autenticação | **NÃO** alterar |

### 9.2. Banco de Dados

| Aspecto | Decisão |
|---------|---------|
| Tabelas | **NÃO** alterar |
| Views | **NÃO** alterar — apenas remover a criação delas do `Program.cs` |
| Índices | **NÃO** alterar |
| Stored procedures | **NÃO** existem e não serão criadas |
| Scripts SQL em [`db/ticketprime.sql`](db/ticketprime.sql) e [`db/ticketprime_incrementos.sql`](db/ticketprime_incrementos.sql) | **NÃO** alterar (já estão corretos) |

### 9.3. Regras de Negócio

| Aspecto | Decisão |
|---------|---------|
| Limite de 2 reservas por CPF/evento | **NÃO** alterar |
| Cálculo de desconto de cupom | **NÃO** alterar |
| Validação de CPF | **NÃO** alterar |
| Geração de código único | **NÃO** alterar |
| Fluxo de confirmação de carrinho | **NÃO** alterar |
| Lógica de check-in | **NÃO** alterar |

### 9.4. Architecture Patterns

| Aspecto | Decisão |
|---------|---------|
| Convenção `IDbTransaction?` | **NÃO** alterar (já está correta — corrigida na Etapa 2) |
| Padrão de retorno `(Response?, Erro?, StatusCode)` | **NÃO** alterar |
| Uso de `ValidationException` | **NÃO** alterar |
| Registro de DI em [`Program.cs`](src/TicketPrime.Api/Program.cs) | **NÃO** reordenar, apenas adicionar `DashboardService` |
| Middleware de exception handling | **NÃO** alterar |
| Autenticação por ApiKey | **NÃO** alterar |

### 9.5. Health Check de Banco (Futuro)

| Aspecto | Decisão |
|---------|---------|
| Health check de conectividade com SQL Server | **AVALIAR em etapa futura** — fora do escopo da Etapa 12. Não implementar agora para não ampliar escopo. Caso seja priorizado, criar um endpoint `GET /api/health/database` que execute `SELECT 1` e retorne o status da conexão. |

---

## 10. Como Validar que a Fase 2 foi Concluída com Sucesso

### 10.1. Checklist de Validação Final

- [x] **Etapa 1:** Setup inicial concluído
- [x] **Etapa 2:** Infra de repositórios + convenção `IDbTransaction?` estabelecida
- [x] **Etapa 3:** CupomRepository + CupomService migrados
- [x] **Etapa 4:** UsuarioRepository + UsuarioService migrados
- [x] **Etapa 5:** EventoRepository + EventoService migrados
- [x] **Etapa 6:** ReservaRepository + ReservaService migrados
- [x] **Etapa 7:** TipoIngressoRepository + TipoIngressoService migrados
- [x] **Etapa 8:** IngressoRepository + IngressoService migrados
- [x] **Etapa 9:** CheckInRepository + CheckInService migrados
- [x] **Etapa 10a:** RegrasReserva extraídas (C3)
- [x] **Etapa 10b:** ReservaService refatorado com repositórios
- [x] **Etapa 11a:** CarrinhoRepository + CarrinhoService CRUD migrados (C1)
- [x] **Etapa 11b:** CarrinhoService.ConfirmarAsync migrado (C1)
- [ ] **Etapa 12:** Limpeza final e auditoria concluída ← **ESTAMOS AQUI**

### 10.2. Métricas de Sucesso

| Métrica | Alvo | Atual (pré-Etapa 12) | Esperado (pós-Etapa 12) |
|---------|:---:|:--------------------:|:-----------------------:|
| Linhas do [`Program.cs`](src/TicketPrime.Api/Program.cs) | < 470 | 1012 | ~460-470 |
| Endpoints com SQL inline | 0 | 7 | 0 |
| Models em `Models/` | Todos | 42 | 50 (8 novos extraídos) |
| Services em `Services/` | Completos | 12 | 12 (+1 novo, -0 removido) |
| Repositories em `Repositories/` | Completos | 18 | 18 |
| Testes passando | 90 | 103 | 90 |
| Dívidas técnicas registradas | Documentadas | 3 (TD-001, TD-002, TD-003) | 5 (+ TD-004, TD-005) |

### 10.3. Comando de Validação Final

```bash
# 1. Compilar
dotnet build

# 2. Testar
dotnet test

# 3. Verificar warnings de usings não utilizados
dotnet build 2>&1 | grep -i "warning CS"

# 4. Verificar linhas do Program.cs
wc -l src/TicketPrime.Api/Program.cs

# 5. Verificar se há SQL inline nos endpoints (busca por ExecuteAsync/QueryAsync em Program.cs)
grep -n "QueryAsync\|ExecuteAsync\|ExecuteScalarAsync" src/TicketPrime.Api/Program.cs
```

### 10.4. O que a Fase 2 entregou

| Antes | Depois |
|-------|--------|
| [`Program.cs`](src/TicketPrime.Api/Program.cs) com 2265 linhas | [`Program.cs`](src/TicketPrime.Api/Program.cs) com ~460-470 linhas |
| SQL, regras e validação tudo misturado | Camadas separadas: `Services/`, `Repositories/`, `Models/` |
| 2 services puros (sem DB) | 12 services com repositórios injetados |
| 0 repositórios | 18 arquivos (9 interfaces + 9 implementações) |
| 6 request models inline no [`Program.cs`](src/TicketPrime.Api/Program.cs) | Todos os models em arquivos próprios em `Models/` |
| Sem convenção `IDbTransaction?` | Convenção estabelecida e aplicada |
| DDL de banco na aplicação | DDL em scripts SQL externos |

---

## Anexo A: Diagnóstico Detalhado por Arquivo

### A.1. [`Program.cs`](src/TicketPrime.Api/Program.cs) — Usings

| Using | Linha | Necessário? | Motivo |
|-------|:----:|:-----------:|--------|
| `using Dapper;` | 1 | ✅ Sim | Usado nos endpoints admin (QueryAsync, ExecuteAsync) — **será movido para DashboardService** |
| `using Microsoft.AspNetCore.Mvc;` | 2 | ✅ Sim | Usado para `[FromBody]`, `[FromQuery]` |
| `using Microsoft.Data.SqlClient;` | 3 | ⚠️ Parcial | Só usado em `InicializarBancoAsync` — será removido |
| `using System.Data;` | 4 | ✅ Sim | Usado para `IDbConnection` no DI |
| `using TicketPrime.Api.Authentication;` | 5 | ✅ Sim | Esquema de autenticação |
| `using TicketPrime.Api.Middleware;` | 6 | ✅ Sim | ExceptionHandlingMiddleware |
| `using TicketPrime.Api.Models;` | 7 | ⚠️ Parcial | Usado apenas indiretamente via services |
| `using TicketPrime.Api.Repositories;` | 8 | ⚠️ Parcial | Usado apenas para DI |
| `using TicketPrime.Api.Services;` | 9 | ✅ Sim | Todos os services |

**Após Etapa 12:** Os usings 3, 7 e 8 podem ser removidos se `InicializarBancoAsync` for eliminado e os endpoints admin migrados para `DashboardService`.

### A.2. [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) — Métodos Usados vs. Não Usados

| Método | Usado por | Status |
|--------|-----------|:------:|
| `GerarCodigoUnico()` | Apenas testes | ⚠️ Mantido (testes dependem) |
| `CriarIngressoDigital()` | Apenas testes | ⚠️ Mantido |
| `ValidarCodigoUnico()` | Apenas testes | ⚠️ Mantido |
| `RealizarCheckIn()` | Apenas testes | ⚠️ Mantido |
| `PodeRealizarCheckIn()` | Apenas testes | ⚠️ Mantido |
| `ValidarTipoIngresso()` | Apenas testes | ⚠️ Mantido |
| `VerificarDisponibilidadeLote()` | Apenas testes | ⚠️ Mantido |
| `SimularPreco()` | Apenas testes | ⚠️ Mantido |
| `ValidarCarrinhoParaConfirmacao()` | Ninguém | 🔴 **Remover** |
| `CarrinhoEstaExpirado()` | Ninguém | 🔴 **Remover** |
| `CalcularDashboardEvento()` | Ninguém | 🔴 **Remover** |
| `CalcularDashboardLista()` | Ninguém | 🔴 **Remover** |

**Decisão sobre os métodos mantidos:** Os métodos de negócio puro (`GerarCodigoUnico`, `CriarIngressoDigital`, etc.) são usados exclusivamente pelos testes [`IncrementoServiceTests.cs`](tests/TicketPrime.Tests/IncrementoServiceTests.cs). Eles representam a especificação original das regras de negócio em formato testável. São mantidos pois:
1. Os testes existem e passam (64 casos de teste)
2. Servem como documentação executável das regras
3. Não causam impacto em produção (não estão no DI)

### A.3. Models com Problemas de Organização

| Model | Localização Atual | Problema | Ação |
|-------|-------------------|:--------:|:----:|
| `DashboardLoteResponse` | Dentro de [`DashboardEventoDetalhadoResponse.cs`](src/TicketPrime.Api/Models/DashboardEventoDetalhadoResponse.cs:19) | Modelo aninhado em arquivo de outro modelo | Extrair para arquivo próprio |
| `CheckInItemResponse` | Dentro de [`CheckInListResponse.cs`](src/TicketPrime.Api/Models/CheckInListResponse.cs:11) | Modelo aninhado em arquivo de outro modelo | Extrair para arquivo próprio |
| `CarrinhoRequest` | [`Models/CarrinhoRequest.cs`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) | **NÃO é usado por nenhum endpoint ou service** | 🔴 **Remover** |
| `ResultadoCriacaoUsuario` | [`Services/UsuarioService.cs`](src/TicketPrime.Api/Services/UsuarioService.cs:65) | Classe de resultado dentro de service | Mover para `Models/` |
| `ResultadoCriacaoEvento` | [`Services/EventoService.cs`](src/TicketPrime.Api/Services/EventoService.cs:83) | Classe de resultado dentro de service | Mover para `Models/` |
| `ResultadoCriacaoCupom` | [`Services/CupomService.cs`](src/TicketPrime.Api/Services/CupomService.cs:51) | Classe de resultado dentro de service | Mover para `Models/` |
| `ResultadoReserva` | [`Services/RegrasReserva.cs`](src/TicketPrime.Api/Services/RegrasReserva.cs:5) | Classe de resultado dentro de RegrasReserva | Mover para `Models/` |
| `ResultadoCriacaoLote` | [`Services/TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs:443) | Classe de resultado dentro de service | Mover para `Models/` |
| `ResultadoCriacaoTipoIngresso` | [`Services/TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs:488) | Classe de resultado dentro de service | Mover para `Models/` |

### A.4. Dívidas Técnicas a Registrar (NOVAS)

#### TD-004: DashboardService usa IDbConnection diretamente

| Campo | Valor |
|-------|-------|
| **ID** | TD-004 |
| **Data de registro** | 2026-06-04 |
| **Origem** | Etapa 12 — Limpeza final Fase 2 |
| **Severidade** | Baixa |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |
| **Descrição** | O [`DashboardService`] foi criado na Etapa 12 para remover o SQL inline dos endpoints admin do [`Program.cs`]. No entanto, ele injeta `IDbConnection` diretamente em vez de usar um repositório. Isso foi aceito pois são queries de leitura em views, sem transação. Para consistência arquitetural completa, um `IDashboardRepository` poderia ser criado em uma fase futura. |
| **Ação corretiva sugerida** | Criar `IDashboardRepository` + `DashboardRepository` e mover as queries do [`DashboardService`] para o repositório. |

#### TD-005: IncrementoService com métodos não utilizados em produção

| Campo | Valor |
|-------|-------|
| **ID** | TD-005 |
| **Data de registro** | 2026-06-04 |
| **Origem** | Etapa 12 — Limpeza final Fase 2 |
| **Severidade** | Baixa |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |
| **Descrição** | O [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) contém 368 linhas com regras de negócio puras que são usadas exclusivamente pelos testes unitários (64 casos em [`IncrementoServiceTests.cs`](tests/TicketPrime.Tests/IncrementoServiceTests.cs)). Embora os métodos `ValidarCarrinhoParaConfirmacao`, `CarrinhoEstaExpirado`, `CalcularDashboardEvento` e `CalcularDashboardLista` tenham sido removidos, os demais métodos permanecem como documentação executável. Avaliar na Fase 3 se estes métodos devem ser consolidados ou removidos. |
| **Ação corretiva sugerida** | Avaliar se os testes do [`IncrementoService`](src/TicketPrime.Api/Services/IncrementoService.cs) devem ser movidos para testes de integração que validem os services reais, permitindo a remoção completa do `IncrementoService`. |

---

## Anexo B: Ordem de Execução Sugerida

A execução deve seguir esta ordem para minimizar riscos:

| Passo | Descrição | Arquivos | Risco |
|:-----:|-----------|----------|:-----:|
| 1 | Remover `CarrinhoRequest.cs` | [`Models/CarrinhoRequest.cs`](src/TicketPrime.Api/Models/CarrinhoRequest.cs) | 🔴 Mínimo |
| 2 | Extrair `DashboardLoteResponse` para arquivo próprio | [`Models/DashboardEventoDetalhadoResponse.cs`](src/TicketPrime.Api/Models/DashboardEventoDetalhadoResponse.cs) | 🟡 Baixo |
| 3 | Extrair `CheckInItemResponse` para arquivo próprio | [`Models/CheckInListResponse.cs`](src/TicketPrime.Api/Models/CheckInListResponse.cs) | 🟡 Baixo |
| 4 | Mover `ResultadoCriacaoUsuario` para `Models/` | [`Services/UsuarioService.cs`](src/TicketPrime.Api/Services/UsuarioService.cs) | 🟡 Baixo |
| 5 | Mover `ResultadoCriacaoEvento` para `Models/` | [`Services/EventoService.cs`](src/TicketPrime.Api/Services/EventoService.cs) | 🟡 Baixo |
| 6 | Mover `ResultadoCriacaoCupom` para `Models/` | [`Services/CupomService.cs`](src/TicketPrime.Api/Services/CupomService.cs) | 🟡 Baixo |
| 7 | Mover `ResultadoReserva` para `Models/` | [`Services/RegrasReserva.cs`](src/TicketPrime.Api/Services/RegrasReserva.cs) | 🟡 Baixo |
| 7a | Mover `ResultadoCriacaoLote` para `Models/` | [`Services/TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs) | 🟡 Baixo |
| 7b | Mover `ResultadoCriacaoTipoIngresso` para `Models/` | [`Services/TipoIngressoService.cs`](src/TicketPrime.Api/Services/TipoIngressoService.cs) | 🟡 Baixo |
| 8 | Remover métodos mortos do `IncrementoService` | [`Services/IncrementoService.cs`](src/TicketPrime.Api/Services/IncrementoService.cs) | 🟡 Médio (testes) |
| 9 | Remover usings redundantes | [`Services/HistoricoPrecoService.cs`](src/TicketPrime.Api/Services/HistoricoPrecoService.cs) | 🟢 Mínimo |
| 10 | Criar `DashboardService` | NOVO: [`Services/DashboardService.cs`](src/TicketPrime.Api/Services/DashboardService.cs) | 🟠 Médio |
| 11 | Migrar endpoints admin do `Program.cs` para `DashboardService` | [`Program.cs`](src/TicketPrime.Api/Program.cs) | 🟠 Médio |
| 12 | Substituir `InicializarBancoAsync` por warning | [`Program.cs`](src/TicketPrime.Api/Program.cs) | 🟡 Médio |
| 13 | Limpar usings do `Program.cs` | [`Program.cs`](src/TicketPrime.Api/Program.cs) | 🟢 Mínimo |
| 14 | Atualizar documentação | `docs/*.md` | 🟢 Mínimo |
| 15 | **VALIDAR:** `dotnet build` + `dotnet test` | — | 🔴 Crítico |

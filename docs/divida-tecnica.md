# Registro de Dívida Técnica — TicketPrime

**Última atualização:** 2026-05-28

---

## TD-001: Logging de `ex.Message` em exceções genéricas no middleware

| Campo | Valor |
|-------|-------|
| **ID** | TD-001 |
| **Data de registro** | 2026-05-28 |
| **Origem** | Revisão de segurança do plano Fase 1 (v3) |
| **Severidade** | Baixa |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |

### Descrição

O [`ExceptionHandlingMiddleware`](src/TicketPrime.Api/Middleware/ExceptionHandlingMiddleware.cs) registra `ex.Message` no log para exceções genéricas (`catch (Exception ex)`). Em cenários específicos, uma `SqlException` pode conter informações sensíveis na mensagem (ex: nomes de objetos internos, detalhes de schema do banco), potencialmente expondo detalhes em logs internos.

### Risco associado

- **Cenário:** Ocorrência de `SqlException` não tratada (ex: falha de conexão, violação de constraint não mapeada).
- **Efeito:** A mensagem da exceção, contendo detalhes internos do banco, é registrada no `ILogger` como `LogError`.
- **Impacto:** Baixo — logs internos, não expostos ao cliente. A resposta HTTP já é sanitizada (em produção, retorna apenas `"Ocorreu um erro interno no servidor."`).

### Decisão

**Não bloquear a implementação da Fase 1.** A severidade é baixa pois:
- A mensagem só é registrada em logs internos (não exposta ao cliente HTTP)
- A resposta para o cliente já é sanitizada em produção
- O risco é restrito a cenários de falha não tratada, que são exceção

### Fase sugerida para correção

**Observabilidade / Logging (futura)** — fora do escopo da Fase 1.

### Ação corretiva sugerida

Revisar estratégia de logging do `ExceptionHandlingMiddleware` para evitar dependência de `ex.Message` em logs de erro. Avaliar sanitização de mensagens sensíveis e adoção de logging estruturado baseado em `CorrelationId`.

### Critérios de aceite para correção futura

- [ ] `ex.Message` não é usado diretamente em logs de exceções genéricas
- [ ] Mensagens sensíveis de `SqlException` são sanitizadas antes do log
- [ ] Logging estruturado com `CorrelationId` implementado
- [ ] Testes validam que logs não contêm detalhes internos do banco

---

## TD-002: Implementação antecipada de `Authentication/` e `Middleware/` durante Item 1

| Campo | Valor |
|-------|-------|
| **ID** | TD-002 |
| **Data de registro** | 2026-05-28 |
| **Origem** | Code review do Item 1 — Remoção de senha hardcoded |
| **Severidade** | Média |
| **Prioridade** | Média |
| **Status** | 📝 Registrada (aceita temporariamente) |

### Descrição

Os diretórios e artefatos abaixo foram implementados antecipadamente durante a execução do Item 1 (remoção de senha hardcoded do `appsettings.json`), fora do escopo oficial:

- [`src/TicketPrime.Api/Authentication/`](src/TicketPrime.Api/Authentication/) — esquema de autenticação por ApiKey
- [`src/TicketPrime.Api/Middleware/`](src/TicketPrime.Api/Middleware/) — middleware de tratamento de exceções
- [`src/TicketPrime.Api/Program.cs`](src/TicketPrime.Api/Program.cs) — registros de `AddAuthentication`, `UseAuthentication`, `UseAuthorization`, `UseMiddleware`
- Seções `Authentication` e `Cors` em [`src/TicketPrime.Api/appsettings.json`](src/TicketPrime.Api/appsettings.json) (revertidas por scope creep)

### Risco associado

- **Cenário:** Os itens 2 (autenticação) e 3 (middleware) podem conter ajustes ou requisitos diferentes do que foi implementado antecipadamente.
- **Efeito:** Necessidade de refatorar ou substituir o que já foi implementado, causando retrabalho.
- **Impacto:** Médio — o código antecipado pode não atender aos requisitos finais dos itens 2 e 3.

### Decisão

**Aceito temporariamente.** Não remover. Manter a implementação atual e validar novamente quando os Itens 2 e 3 forem oficialmente executados.

### Fase sugerida para correção

**Itens 2 e 3 da Fase 1** — validar e ajustar durante a implementação oficial.

### Ação corretiva sugerida

Ao executar os Itens 2 e 3, revisar a implementação antecipada contra os requisitos oficiais e ajustar conforme necessário.

### Critérios de aceite para correção futura

- [ ] Item 2 (autenticação) validado contra implementação antecipada em [`src/TicketPrime.Api/Authentication/`](src/TicketPrime.Api/Authentication/)
- [ ] Item 3 (middleware) validado contra implementação antecipada em [`src/TicketPrime.Api/Middleware/`](src/TicketPrime.Api/Middleware/)
- [ ] `Program.cs` revisado para garantir que registros de autenticação e middleware estão corretos
- [ ] Testes passam após validação

---

## TD-003: Race condition — limite de 2 reservas por CPF/evento não é atômico

| Campo | Valor |
|-------|-------|
| **ID** | TD-003 |
| **Data de registro** | 2026-05-28 |
| **Origem** | Revisão do Item 4 (transação no fluxo de confirmação de carrinho) |
| **Severidade** | Média |
| **Prioridade** | Baixa |
| **Status** | 📝 Registrada |

### Descrição

No endpoint de confirmação de carrinho ([`POST /api/carrinho/{cpf}/confirmar`](src/TicketPrime.Api/Program.cs:1696)), a verificação do limite de 2 reservas por CPF por evento é feita dentro de um loop `for (int q = 0; q < item.Quantidade; q++)`, medindo uma reserva de cada vez. Em cenários de concorrência (duas requisições simultâneas para o mesmo CPF+Evento), ambas podem passar na verificação antes que qualquer uma das reservas seja persistida, permitindo que o limite seja ultrapassado.

### Risco associado

- **Cenário:** Duas requisições `POST /api/carrinho/{cpf}/confirmar` chegando simultaneamente para o mesmo CPF e mesmo evento, onde cada uma tenta criar 2 reservas (total de 4), mas o limite é 2.
- **Efeito:** Ambas as transações podem passar na verificação `COUNT(1) < 2` antes do commit da outra, resultando em até 4 reservas para o mesmo CPF/evento.
- **Impacto:** Médio — violação de regra de negócio (limite de 2 reservas), sem perda financeira direta.

### Decisão

**Não corrigir agora.** O problema é preexistente (já existia antes da transação ser introduzida) e está fora do escopo do Item 4 (atomicidade do fluxo). A correção exigiria uma abordagem de concorrência mais ampla (ex: `sp_getapplock`, `SERIALIZABLE` + otimizações, ou fila de confirmação).

### Fase sugerida para correção

**Concorrência / Consistência (futura)** — etapa dedicada a cenários de race condition.

### Ação corretiva sugerida

Adotar uma das seguintes estratégias:
1. **`sp_getapplock`** no SQL Server para serializar confirmações por CPF
2. **Isolamento `SERIALIZABLE`** na transação + `UPDLOCK` na consulta de contagem
3. **Fila de confirmação** processada sequencialmente (ex: Hangfire, RabbitMQ)
4. **Constraint de banco** via trigger ou tabela auxiliar com unique constraint composta

### Critérios de aceite para correção futura

- [ ] Duas requisições simultâneas para o mesmo CPF+Evento não ultrapassam o limite de 2 reservas
- [ ] Teste de concorrência implementado (ex: `Parallel.ForEach` ou `Task.WhenAll`)
- [ ] Performance não degrada significativamente para cenário normal (1 requisição)

---

## Template para novas dívidas

| Campo | Descrição |
|-------|-----------|
| **ID** | TD-NNN (sequencial) |
| **Data de registro** | ISO 8601 |
| **Origem** | Onde foi identificada (review, auditoria, bug) |
| **Severidade** | Baixa / Média / Alta / Crítica |
| **Prioridade** | Baixa / Média / Alta |
| **Status** | 📝 Registrada / 🔧 Em correção / ✅ Corrigida |

# Release Checklist Final — TicketPrime

Checklist de verificação para entrega da AV2, elaborado com base no estado real do projeto e alinhado ao workflow *Spec-Driven Development* e ao enunciado oficial.

---

## 1. Estrutura Oficial de Pastas

- [x] Diretório [`docs/`](docs/) existe com os documentos de documentação
- [x] Diretório [`db/`](db/) existe com os scripts de banco de dados
- [x] Diretório [`src/`](src/) existe com o código-fonte da API
- [x] Diretório [`tests/`](tests/) existe com os projetos de teste
- [x] Nenhum dos diretórios obrigatórios foi renomeado

---

## 2. Documentação `/docs`

- [x] [`docs/requisitos.md`](docs/requisitos.md) — contém visão geral, entidades, regras de negócio, restrições técnicas, histórias de usuário (BDD), endpoints (AV1 e AV2) e glossário
- [x] [`docs/adr.md`](docs/adr.md) — contém ADR-001 documentando a decisão arquitetural de uso de Dapper + SQL manual com parâmetros nomeados
- [x] [`docs/operacao.md`](docs/operacao.md) — contém matriz de riscos operacionais, métrica TSR, SLO e Error Budget Policy
- [x] [`docs/estrutura-inicial-ticketprime.md`](docs/estrutura-inicial-ticketprime.md) — contém o plano inicial de criação do projeto

---

## 3. README.md

- [x] [`README.md`](README.md) — contém visão geral, tecnologias utilizadas, estrutura do repositório, instruções de restore/build/run/test, lista de endpoints (AV1 e AV2) e regras de segurança do projeto

---

## 4. Script SQL (`db/ticketprime.sql`)

- [x] [`db/ticketprime.sql`](db/ticketprime.sql) — script completo com as tabelas `Usuarios`, `Eventos`, `Cupons` e `Reservas`, incluindo constraints de chave primária e estrangeira
- [x] [`db/scripts/001_CreateSchema.sql`](db/scripts/001_CreateSchema.sql) — script de criação do schema inicial (tabelas `Usuarios` e `Eventos`)
- [x] [`db/scripts/002_CreateCupons.sql`](db/scripts/002_CreateCupons.sql) — script de criação da tabela `Cupons` com guard `IF NOT EXISTS`

---

## 5. Dapper Parametrizado (Ausência de SQL Injection)

- [x] Todas as consultas em [`Program.cs`](src/TicketPrime.Api/Program.cs) utilizam **parâmetros nomeados** (`@param`) — nenhuma string SQL utiliza concatenação ou interpolação
- [x] Não há ocorrência de `$"..."` (interpolação) em strings SQL no código-fonte
- [x] Não há ocorrência de `+` (concatenação) em strings SQL no código-fonte
- [x] O projeto depende exclusivamente de [`Dapper`](src/TicketPrime.Api/TicketPrime.Api.csproj:11) e [`Microsoft.Data.SqlClient`](src/TicketPrime.Api/TicketPrime.Api.csproj:12) para acesso a dados

---

## 6. Endpoints AV1

- [x] `POST /api/usuarios` — cadastro de usuário com validações (CPF, nome, e-mail) — [`Program.cs:110`](src/TicketPrime.Api/Program.cs:110)
- [x] `POST /api/eventos` — cadastro de evento com validações (nome, capacidade, preço) — [`Program.cs:159`](src/TicketPrime.Api/Program.cs:159)
- [x] `GET /api/eventos` — listagem de todos os eventos — [`Program.cs:286`](src/TicketPrime.Api/Program.cs:286)
- [x] `POST /api/cupons` — cadastro de cupom com validações (código, porcentagem, valor mínimo) — [`Program.cs:295`](src/TicketPrime.Api/Program.cs:295)

---

## 7. Endpoints AV2

- [x] `POST /api/reservas` — criação de reserva com validações (CPF existente, evento existente, limite de 2 por CPF/evento, capacidade do evento, cálculo de desconto com cupom) — [`Program.cs:197`](src/TicketPrime.Api/Program.cs:197)
- [x] `GET /api/reservas/{cpf}` — consulta de reservas por CPF com `INNER JOIN` para obter nome do evento — [`Program.cs:330`](src/TicketPrime.Api/Program.cs:330)

---

## 8. Testes xUnit

- [x] [`tests/TicketPrime.Tests/EventoValidationTests.cs`](tests/TicketPrime.Tests/EventoValidationTests.cs) — 3 testes (valores padrão, atribuição de propriedades, valores do request)
- [x] [`tests/TicketPrime.Tests/CupomValidationTests.cs`](tests/TicketPrime.Tests/CupomValidationTests.cs) — 5 testes (valores padrão, atribuição de propriedades, valores do request, comparação, códigos diferentes)
- [x] [`tests/TicketPrime.Tests/UsuarioValidationTests.cs`](tests/TicketPrime.Tests/UsuarioValidationTests.cs) — 5 testes (valores padrão, atribuição de propriedades, valores do request, comparação, propriedades diferentes)
- [x] [`tests/TicketPrime.Tests/ReservaServiceTests.cs`](tests/TicketPrime.Tests/ReservaServiceTests.cs) — 26 testes (CPF inexistente, evento inexistente, limite de 2 reservas por CPF/evento, capacidade esgotada, vagas disponíveis, regra de valor mínimo do cupom, cálculo de valor final com e sem cupom, resposta com nome do evento, evento inexistente na resposta)

---

## 9. Assert nos Testes

- [x] Todos os testes utilizam `Assert` (`Assert.Equal`, `Assert.True`, `Assert.False`, `Assert.Contains`, `Assert.NotEqual`, `Assert.NotNull`, `Assert.Null`) — verificado em todos os 4 arquivos de teste

---

## 10. Ausência de Entity Framework

- [x] Nenhuma referência ao pacote `Microsoft.EntityFrameworkCore` no [`TicketPrime.Api.csproj`](src/TicketPrime.Api/TicketPrime.Api.csproj)
- [x] Nenhuma referência ao pacote `Microsoft.EntityFrameworkCore` no [`TicketPrime.Tests.csproj`](tests/TicketPrime.Tests/TicketPrime.Tests.csproj)
- [x] Nenhum using ou chamada a APIs do Entity Framework no código-fonte

---

## 11. Ausência de Banco em Memória

- [x] Não há uso de `UseInMemoryDatabase` em nenhum arquivo do projeto
- [x] Todas as operações de banco utilizam [`SqlConnection`](src/TicketPrime.Api/Program.cs:3) real apontando para SQL Server
- [x] A string de conexão em [`appsettings.json`](src/TicketPrime.Api/appsettings.json:9-11) referencia `Server=localhost;Database=TicketPrimeDb`

---

## 12. Ausência de Secrets Hardcoded em Arquivos `.cs`

- [x] A connection string está definida em [`appsettings.json`](src/TicketPrime.Api/appsettings.json:9-11) (arquivo de configuração), não em arquivos `.cs`
- [x] Nenhum arquivo `.cs` contém strings de conexão, senhas ou segredos embutidos

---

## 13. Revisão Final

- [x] Estrutura de diretórios verificada e conforme o enunciado
- [x] Documentação completa e alinhada ao estado real do projeto
- [x] Todos os endpoints implementados e mapeados
- [x] Todas as consultas Dapper utilizam parâmetros nomeados
- [x] Nenhuma violação de segurança por SQL Injection identificada
- [x] Nenhuma referência a Entity Framework ou banco em memória
- [x] Testes compilam e utilizam Assert em todos os cenários
- [x] README.md atualizado com instruções e endpoints
- [x] Scripts SQL versionados em `/db/scripts/` e script completo em `/db/ticketprime.sql`

---

## Resumo Quantitativo

| Categoria | Itens Verificados | Itens Conformes |
|-----------|-------------------|-----------------|
| Estrutura de pastas | 5 | 5 |
| Documentação | 4 | 4 |
| Endpoints AV1 | 4 | 4 |
| Endpoints AV2 | 2 | 2 |
| Testes xUnit | 4 arquivos / 39 testes | 4 / 39 |
| Restrições técnicas | 5 | 5 |
| **Total** | **24** | **24** |

---

*Checklist gerado em 15/05/2026 com base no estado real do repositório [`ticketprime-backend`](/home/pedro/Downloads/ticketprime-backend).*

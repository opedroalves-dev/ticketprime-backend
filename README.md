# TicketPrime

Backend/API do TicketPrime para venda de ingressos, cadastro de eventos, cupons, usuários e reservas, utilizando **.NET 8**, **Minimal API**, **Dapper** e **SQL Server**.

### Membros

```
| Gabriel de Jesus L - 06009870
| Pedro Henrique Alves - 06003335
```

---

## Objetivo do Sistema

O **TicketPrime** é uma API backend acadêmica para venda de ingressos, cadastro de eventos, cupons, usuários e reservas, com armazenamento persistente em SQL Server e acesso a dados via Dapper com parâmetros nomeados.

---

## Tecnologias Utilizadas

| Tecnologia | Versão | Finalidade |
|---|---|---|
| .NET | 8.0 | Runtime e Framework |
| Minimal API | — | Camada de apresentação (API REST) |
| Dapper | 2.1.35 | Acesso a dados (ADO.NET) |
| SQL Server | 2022+ | Banco de dados relacional |
| xUnit | 2.9.3 | Testes unitários e de integração |

---

## Estrutura Obrigatória do Repositório

```
/
├── docs/
│   └── requisitos.md
├── db/
│   ├── ticketprime.sql
│   └── scripts/
│       ├── 001_CreateSchema.sql
│       └── 002_CreateCupons.sql
├── src/
│   └── TicketPrime.Api/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── TicketPrime.Api.csproj
│       └── Models/
│           ├── Cupom.cs
│           ├── Evento.cs
│           └── Usuario.cs
├── tests/
│   └── TicketPrime.Tests/
│       ├── CupomValidationTests.cs
│       ├── EventoValidationTests.cs
│       ├── ReservaServiceTests.cs
│       ├── UsuarioValidationTests.cs
│       └── TicketPrime.Tests.csproj
├── TicketPrime.sln
└── README.md
```

---

## Como Restaurar Dependências

```bash
dotnet restore
```

---

## Como Compilar o Projeto

```bash
dotnet build
```

---

## Como Executar a API

```bash
dotnet run --project src/TicketPrime.Api
```

A API será iniciada na porta configurada em `src/TicketPrime.Api/Properties/launchSettings.json`.

---

## Como Executar os Testes

```bash
dotnet test
```

---

## Como Executar o Script SQL

Conecte-se à instância do SQL Server e execute o script completo [`db/ticketprime.sql`](db/ticketprime.sql):

```bash
sqlcmd -S localhost -U sa -P YourPassword123! -i db/ticketprime.sql
```

Ou execute incrementalmente os scripts de migração em ordem:

```bash
sqlcmd -S localhost -U sa -P YourPassword123! -i db/scripts/001_CreateSchema.sql
sqlcmd -S localhost -U sa -P YourPassword123! -i db/scripts/002_CreateCupons.sql
```

---

## Endpoints

### AV1

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/api/eventos` | Cadastrar um novo evento |
| `GET` | `/api/eventos` | Listar todos os eventos |
| `POST` | `/api/cupons` | Cadastrar um novo cupom |
| `POST` | `/api/usuarios` | Cadastrar um novo usuário |

### AV2

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/api/reservas/{cpf}` | Listar reservas de um usuário pelo CPF |
| `POST` | `/api/reservas` | Criar uma nova reserva |

---

## Regras de Segurança do Projeto

- **Sem Entity Framework** — acesso a dados exclusivamente com Dapper.
- **Sem banco em memória** — apenas SQL Server real.
- **Sem SQL injection** — todas as consultas deverão utilizar parâmetros nomeados (`@param`).
- **Sem concatenação SQL** — nenhuma string SQL deverá ser construída por concatenação.
- **Sem interpolação SQL** — nenhuma string SQL deverá utilizar interpolação (`$"..."`).
- **Nomes de pastas fixos** — as pastas `/docs`, `/db`, `/src` e `/tests` não poderão ser renomeadas.

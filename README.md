# TicketPrime

Sistema de gerenciamento de tickets utilizando **.NET 8**, **Minimal API**, **Dapper** e **SQL Server**.

## Tecnologias

| Tecnologia | Versão | Finalidade |
|---|---|---|
| .NET | 8.0 | Runtime e Framework |
| Minimal API | — | Camada de apresentação (API REST) |
| Dapper | 2.1.35 | Acesso a dados (ADO.NET) |
| SQL Server | 2022+ | Banco de dados relacional |
| xUnit | 2.9.3 | Testes unitários e de integração |

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server 2022+](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou [VS Code](https://code.visualstudio.com/)

## Estrutura do Projeto

```
/
├── docs/                          # Documentação do projeto
├── db/                            # Scripts de banco de dados
│   └── scripts/
│       └── 001_CreateSchema.sql   # Script inicial de schema
├── src/                           # Código fonte
│   └── TicketPrime.Api/           # Projeto da Minimal API
│       ├── Program.cs             # Entry point da aplicação
│       ├── appsettings.json       # Configurações (connection string)
│       ├── appsettings.Development.json
│       └── TicketPrime.Api.csproj
├── tests/                         # Projetos de teste
│   └── TicketPrime.Tests/         # Testes xUnit
│       ├── UnitTest1.cs           # Teste dummy inicial
│       └── TicketPrime.Tests.csproj
├── TicketPrime.sln                # Arquivo de solução
├── README.md
└── release_checklist_final.md
```

## Configuração

### 1. Connection String

Edite o arquivo [`src/TicketPrime.Api/appsettings.json`](src/TicketPrime.Api/appsettings.json) e ajuste a connection string `DefaultConnection` para o seu ambiente SQL Server:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TicketPrimeDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
  }
}
```

### 2. Banco de Dados

Execute o script [`db/scripts/001_CreateSchema.sql`](db/scripts/001_CreateSchema.sql) no SQL Server para criar o banco e o schema inicial.

### 3. Restaurar Pacotes

```bash
dotnet restore
```

### 4. Executar a Aplicação

```bash
dotnet run --project src/TicketPrime.Api
```

### 5. Executar os Testes

```bash
dotnet test
```

## Restrições do Projeto

- ❌ **Não** utilizar Entity Framework (nem EF Core, nem EF6)
- ❌ **Não** utilizar banco em memória (InMemory Database)
- ❌ **Não** alterar nomes de pastas (`/docs`, `/db`, `/src`, `/tests`)
- ❌ **Não** alterar nomes de rotas
- ❌ **Não** utilizar SQL interpolation (`$"SELECT * FROM {tabela}"`)
- ❌ **Não** utilizar concatenação SQL (`"SELECT * FROM " + tabela`)
- ✅ Utilizar **Dapper** com parâmetros nomeados (`@param`) para acesso a dados

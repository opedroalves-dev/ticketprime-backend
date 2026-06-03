using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TicketPrime.Api.Authentication;
using TicketPrime.Api.Middleware;
using TicketPrime.Api.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(connectionString));

// Configura JSON para aceitar tanto camelCase quanto PascalCase no corpo da requisição
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = null; // Preserva os nomes originais (PascalCase)
});

builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", opts =>
    {
        opts.ApiKey = builder.Configuration["Authentication:ApiKey"]
            ?? throw new InvalidOperationException("Authentication:ApiKey não configurada.");
    });

builder.Services.AddAuthorization();

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

var app = builder.Build();

// Middleware global de exception handling (Item 3)
app.UseMiddleware<TicketPrime.Api.Middleware.ExceptionHandlingMiddleware>();

// Middleware de CORS (Item 6)
app.UseCors("AllowFrontend");

// Middleware de autenticação e autorização (Item 2)
app.UseAuthentication();
app.UseAuthorization();

// Inicialização do banco de dados
await InicializarBancoAsync(connectionString);

static async Task InicializarBancoAsync(string connStr)
{
    // Primeiro conecta ao master para criar o banco se não existir
    var masterBuilder = new SqlConnectionStringBuilder(connStr)
    {
        InitialCatalog = "master"
    };

    using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
    await masterConn.OpenAsync();

    var existeDb = await masterConn.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM sys.databases WHERE name = 'TicketPrimeDb'");

    if (existeDb == 0)
    {
        await masterConn.ExecuteAsync("CREATE DATABASE TicketPrimeDb");
        Console.WriteLine("Banco TicketPrimeDb criado com sucesso.");
    }
    else
    {
        Console.WriteLine("Banco TicketPrimeDb já existe.");
    }

    // Agora conecta ao TicketPrimeDb para criar as tabelas
    using var db = new SqlConnection(connStr);
    await db.OpenAsync();

    // Tabela Usuarios
    await db.ExecuteAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Usuarios')
        CREATE TABLE Usuarios (
            Cpf         VARCHAR(11)     NOT NULL,
            Nome        VARCHAR(100)    NOT NULL,
            Email       VARCHAR(150)    NOT NULL,
            CONSTRAINT PK_Usuarios PRIMARY KEY (Cpf)
        )");

    // Tabela Eventos
    await db.ExecuteAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Eventos')
        CREATE TABLE Eventos (
            Id               INT IDENTITY(1,1)   NOT NULL,
            Nome             VARCHAR(200)        NOT NULL,
            CapacidadeTotal  INT                 NOT NULL,
            DataEvento       DATETIME            NOT NULL,
            PrecoPadrao      DECIMAL(10,2)       NOT NULL,
            CONSTRAINT PK_Eventos PRIMARY KEY (Id)
        )");

    // Tabela Cupons
    await db.ExecuteAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Cupons')
        CREATE TABLE Cupons (
            Codigo              VARCHAR(50)    NOT NULL,
            PorcentagemDesconto DECIMAL(5,2)   NOT NULL,
            ValorMinimoRegra    DECIMAL(10,2)  NOT NULL,
            CONSTRAINT PK_Cupons PRIMARY KEY (Codigo)
        )");

    // Tabela Reservas
    await db.ExecuteAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservas')
        CREATE TABLE Reservas (
            Id              INT IDENTITY(1,1)   NOT NULL,
            UsuarioCpf      VARCHAR(11)         NOT NULL,
            EventoId        INT                 NOT NULL,
            CupomUtilizado  VARCHAR(50)         NULL,
            ValorFinalPago  DECIMAL(10,2)       NOT NULL,
            CONSTRAINT PK_Reservas PRIMARY KEY (Id),
            CONSTRAINT FK_Reservas_Usuarios FOREIGN KEY (UsuarioCpf)
                REFERENCES Usuarios(Cpf),
            CONSTRAINT FK_Reservas_Eventos FOREIGN KEY (EventoId)
                REFERENCES Eventos(Id),
            CONSTRAINT FK_Reservas_Cupons FOREIGN KEY (CupomUtilizado)
                REFERENCES Cupons(Codigo)
        )");

    // ========== TABELAS INCREMENTAIS (RF01-RF05) ==========

    // TiposIngresso (RF03)
    if (!await TabelaExiste(db, "TiposIngresso"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE TiposIngresso (
                Id              INT IDENTITY(1,1)   NOT NULL,
                EventoId        INT                 NOT NULL,
                Nome            VARCHAR(100)        NOT NULL,
                Preco           DECIMAL(10,2)       NOT NULL,
                Capacidade      INT                 NOT NULL,
                TaxaServico     DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
                DataInicioVenda DATETIME            NOT NULL,
                DataFimVenda    DATETIME            NOT NULL,
                Lote            VARCHAR(100)        NULL,
                CONSTRAINT PK_TiposIngresso PRIMARY KEY (Id),
                CONSTRAINT FK_TiposIngresso_Eventos FOREIGN KEY (EventoId)
                    REFERENCES Eventos(Id)
            )");
        Console.WriteLine("Tabela TiposIngresso criada.");
    }
    else
    {
        // Adiciona coluna Lote se não existir (para novos endpoints /api/tipos-ingresso)
        if (!await ColunaExiste(db, "TiposIngresso", "Lote"))
        {
            await db.ExecuteAsync("ALTER TABLE TiposIngresso ADD Lote VARCHAR(100) NULL");
            Console.WriteLine("Coluna Lote adicionada à tabela TiposIngresso.");
        }
    }

    // Ingressos (RF01)
    if (!await TabelaExiste(db, "Ingressos"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE Ingressos (
                Id              INT IDENTITY(1,1)   NOT NULL,
                ReservaId       INT                 NOT NULL,
                TipoIngressoId  INT                 NULL,
                CodigoUnico     VARCHAR(8)          NOT NULL,
                Status          VARCHAR(20)         NOT NULL DEFAULT 'Confirmada',
                ValorBruto      DECIMAL(10,2)       NOT NULL,
                ValorDesconto   DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
                TaxaServico     DECIMAL(10,2)       NOT NULL DEFAULT 0.00,
                ValorFinal      DECIMAL(10,2)       NOT NULL,
                DataCriacao     DATETIME            NOT NULL DEFAULT GETDATE(),
                CONSTRAINT PK_Ingressos PRIMARY KEY (Id),
                CONSTRAINT UQ_Ingressos_CodigoUnico UNIQUE (CodigoUnico),
                CONSTRAINT FK_Ingressos_Reservas FOREIGN KEY (ReservaId)
                    REFERENCES Reservas(Id),
                CONSTRAINT FK_Ingressos_TiposIngresso FOREIGN KEY (TipoIngressoId)
                    REFERENCES TiposIngresso(Id),
                CONSTRAINT CK_Ingressos_Status CHECK (
                    Status IN ('Confirmada', 'Utilizada', 'Cancelada')
                ),
                CONSTRAINT CK_Ingressos_CodigoUnico_Tamanho CHECK (
                    LEN(CodigoUnico) = 8
                )
            )");
        Console.WriteLine("Tabela Ingressos criada.");
    }

    // CheckIns (RF02)
    if (!await TabelaExiste(db, "CheckIns"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE CheckIns (
                Id              INT IDENTITY(1,1)   NOT NULL,
                IngressoId      INT                 NOT NULL,
                DataCheckIn     DATETIME            NOT NULL DEFAULT GETDATE(),
                CONSTRAINT PK_CheckIns PRIMARY KEY (Id),
                CONSTRAINT UQ_CheckIns_IngressoId UNIQUE (IngressoId),
                CONSTRAINT FK_CheckIns_Ingressos FOREIGN KEY (IngressoId)
                    REFERENCES Ingressos(Id)
            )");
        Console.WriteLine("Tabela CheckIns criada.");
    }

    // Carrinhos (RF04)
    if (!await TabelaExiste(db, "Carrinhos"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE Carrinhos (
                Id              INT IDENTITY(1,1)   NOT NULL,
                UsuarioCpf      VARCHAR(11)         NOT NULL,
                Status          VARCHAR(20)         NOT NULL DEFAULT 'Ativo',
                DataCriacao     DATETIME            NOT NULL DEFAULT GETDATE(),
                DataExpiracao   DATETIME            NOT NULL,
                CONSTRAINT PK_Carrinhos PRIMARY KEY (Id),
                CONSTRAINT FK_Carrinhos_Usuarios FOREIGN KEY (UsuarioCpf)
                    REFERENCES Usuarios(Cpf),
                CONSTRAINT CK_Carrinhos_Status CHECK (
                    Status IN ('Ativo', 'Expirado', 'Confirmado')
                )
            )");
        Console.WriteLine("Tabela Carrinhos criada.");
    }

    // CarrinhoItens (RF04)
    if (!await TabelaExiste(db, "CarrinhoItens"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE CarrinhoItens (
                Id              INT IDENTITY(1,1)   NOT NULL,
                CarrinhoId      INT                 NOT NULL,
                EventoId        INT                 NOT NULL,
                TipoIngressoId  INT                 NULL,
                Quantidade      INT                 NOT NULL DEFAULT 1,
                PrecoUnitario   DECIMAL(10,2)       NOT NULL,
                CONSTRAINT PK_CarrinhoItens PRIMARY KEY (Id),
                CONSTRAINT FK_CarrinhoItens_Carrinhos FOREIGN KEY (CarrinhoId)
                    REFERENCES Carrinhos(Id),
                CONSTRAINT FK_CarrinhoItens_Eventos FOREIGN KEY (EventoId)
                    REFERENCES Eventos(Id),
                CONSTRAINT FK_CarrinhoItens_TiposIngresso FOREIGN KEY (TipoIngressoId)
                    REFERENCES TiposIngresso(Id),
                CONSTRAINT CK_CarrinhoItens_Quantidade CHECK (
                    Quantidade > 0
                )
            )");
        Console.WriteLine("Tabela CarrinhoItens criada.");
    }

    // HistoricoPrecos (RF05)
    if (!await TabelaExiste(db, "HistoricoPrecos"))
    {
        await db.ExecuteAsync(@"
            CREATE TABLE HistoricoPrecos (
                Id              INT IDENTITY(1,1)   NOT NULL,
                EventoId        INT                 NULL,
                TipoIngressoId  INT                 NULL,
                PrecoAnterior   DECIMAL(10,2)       NULL,
                PrecoNovo       DECIMAL(10,2)       NOT NULL,
                DataAlteracao   DATETIME            NOT NULL DEFAULT GETDATE(),
                Motivo          VARCHAR(200)        NULL,
                CONSTRAINT PK_HistoricoPrecos PRIMARY KEY (Id),
                CONSTRAINT FK_HistoricoPrecos_Eventos FOREIGN KEY (EventoId)
                    REFERENCES Eventos(Id),
                CONSTRAINT FK_HistoricoPrecos_TiposIngresso FOREIGN KEY (TipoIngressoId)
                    REFERENCES TiposIngresso(Id)
            )");
        Console.WriteLine("Tabela HistoricoPrecos criada.");
    }

    // ========== ÍNDICES ADICIONAIS ==========
    if (!await IndiceExiste(db, "IX_Ingressos_ReservaId"))
        await db.ExecuteAsync("CREATE INDEX IX_Ingressos_ReservaId ON Ingressos(ReservaId)");

    if (!await IndiceExiste(db, "IX_Ingressos_Status"))
        await db.ExecuteAsync("CREATE INDEX IX_Ingressos_Status ON Ingressos(Status)");

    if (!await IndiceExiste(db, "IX_Carrinhos_UsuarioCpf_Status"))
        await db.ExecuteAsync("CREATE INDEX IX_Carrinhos_UsuarioCpf_Status ON Carrinhos(UsuarioCpf, Status) WHERE Status = 'Ativo'");

    if (!await IndiceExiste(db, "IX_HistoricoPrecos_EventoId"))
        await db.ExecuteAsync("CREATE INDEX IX_HistoricoPrecos_EventoId ON HistoricoPrecos(EventoId, DataAlteracao DESC)");

    if (!await IndiceExiste(db, "IX_HistoricoPrecos_TipoIngressoId"))
        await db.ExecuteAsync("CREATE INDEX IX_HistoricoPrecos_TipoIngressoId ON HistoricoPrecos(TipoIngressoId, DataAlteracao DESC)");

    // ========== VIEWS (A3: CREATE OR ALTER VIEW — operação atômica) ==========
    await CriarOuRecriarView(db, @"
        CREATE OR ALTER VIEW vw_DashboardEventos
        AS
        SELECT
            e.Id                           AS EventoId,
            e.Nome                         AS NomeEvento,
            e.DataEvento,
            e.CapacidadeTotal,
            e.PrecoPadrao,
            ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN 1 ELSE 0
            END), 0)                        AS TotalIngressosVendidos,
            ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN ig.ValorFinal ELSE 0
            END), 0.00)                     AS ReceitaTotal,
            CASE
                WHEN e.CapacidadeTotal > 0
                THEN ROUND(
                    CAST(ISNULL(SUM(CASE
                        WHEN ig.Status IN ('Confirmada', 'Utilizada')
                        THEN 1 ELSE 0
                    END), 0) AS DECIMAL(10,2)) / e.CapacidadeTotal * 100, 2)
                ELSE 0.00
            END                             AS PercentualOcupacao,
            ISNULL(COUNT(DISTINCT ci.Id), 0) AS TotalCheckIns,
            ISNULL(SUM(CASE
                WHEN ig.Status = 'Confirmada' THEN 1 ELSE 0
            END), 0)                        AS PendentesCheckIn,
            ISNULL(SUM(CASE
                WHEN ig.Status = 'Cancelada' THEN 1 ELSE 0
            END), 0)                        AS TotalCancelados
        FROM Eventos e
        LEFT JOIN TiposIngresso ti ON ti.EventoId = e.Id
        LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
        LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
        GROUP BY e.Id, e.Nome, e.DataEvento, e.CapacidadeTotal, e.PrecoPadrao");

    await CriarOuRecriarView(db, @"
        CREATE OR ALTER VIEW vw_DashboardLotes
        AS
        SELECT
            ti.Id                           AS TipoIngressoId,
            ti.EventoId,
            ti.Nome                         AS NomeLote,
            ti.Preco                        AS PrecoAtual,
            ti.Capacidade                   AS CapacidadeLote,
            ti.TaxaServico,
            ti.DataInicioVenda,
            ti.DataFimVenda,
            ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN 1 ELSE 0
            END), 0)                        AS IngressosVendidos,
            ti.Capacidade - ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN 1 ELSE 0
            END), 0)                        AS CapacidadeRestante,
            ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada')
                THEN ig.ValorFinal ELSE 0
            END), 0.00)                     AS ReceitaLote,
            ISNULL(COUNT(DISTINCT ci.Id), 0) AS CheckInsRealizados
        FROM TiposIngresso ti
        LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
        LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
        GROUP BY ti.Id, ti.EventoId, ti.Nome, ti.Preco, ti.Capacidade,
                 ti.TaxaServico, ti.DataInicioVenda, ti.DataFimVenda");

    Console.WriteLine("Tabelas/views incrementais verificadas/criadas com sucesso.");
}

static async Task<bool> TabelaExiste(IDbConnection db, string nome)
{
    return await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM sys.tables WHERE name = @Nome", new { Nome = nome }) > 0;
}

static async Task<bool> IndiceExiste(IDbConnection db, string nome)
{
    return await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM sys.indexes WHERE name = @Nome", new { Nome = nome }) > 0;
}

static async Task<bool> ColunaExiste(IDbConnection db, string tabela, string coluna)
{
    return await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID(@Tabela) AND name = @Coluna",
        new { Tabela = tabela, Coluna = coluna }) > 0;
}

static async Task CriarOuRecriarView(IDbConnection db, string createSql)
{
    // A3: CREATE OR ALTER VIEW é atômico — não há mais DROP VIEW separado
    await db.ExecuteAsync(createSql);
}

// Health check (Item 7 / F4) — GET /health
// Retorna 200 OK com status "Healthy" e timestamp ISO 8601.
// Não requer autenticação, não acessa banco de dados,
// não expõe dados sensíveis.
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow.ToString("o")
    });
});

app.MapGet("/", () => Results.Redirect("/index.html"));

// ==========================================================
// ENDPOINTS EXISTENTES (preservados)
// ==========================================================

app.MapPost("/api/usuarios", async (IDbConnection db, [FromBody] UsuarioRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Cpf))
        return Results.BadRequest(new { erro = "CPF é obrigatório." });

    if (!request.Cpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter apenas números." });

    if (request.Cpf.Length != 11)
        return Results.BadRequest(new { erro = "CPF deve ter 11 dígitos." });

    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });

    if (request.Nome.Length > 100)
        return Results.BadRequest(new { erro = "Nome não pode exceder 100 caracteres." });

    if (string.IsNullOrWhiteSpace(request.Email))
        return Results.BadRequest(new { erro = "Email é obrigatório." });

    if (request.Email.Length > 150)
        return Results.BadRequest(new { erro = "Email não pode exceder 150 caracteres." });

    if (!request.Email.Contains('@') ||
        request.Email.IndexOf('@') == 0 ||
        request.Email.IndexOf('@') == request.Email.Length - 1)
        return Results.BadRequest(new { erro = "Email inválido." });

    var existe = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
        new { request.Cpf });

    if (existe > 0)
        return Results.BadRequest(new { erro = "CPF já cadastrado." });

    await db.ExecuteAsync(
        "INSERT INTO Usuarios (Cpf, Nome, Email) VALUES (@Cpf, @Nome, @Email)",
        new { request.Cpf, request.Nome, request.Email });

    var usuario = new Usuario
    {
        Cpf = request.Cpf,
        Nome = request.Nome,
        Email = request.Email
    };

    return Results.Created($"/api/usuarios/{request.Cpf}", usuario);
});

app.MapPost("/api/eventos", async (IDbConnection db, [FromBody] EventoRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });

    if (request.Nome.Length > 200)
        return Results.BadRequest(new { erro = "Nome não pode exceder 200 caracteres." });

    if (request.CapacidadeTotal <= 0)
        return Results.BadRequest(new { erro = "CapacidadeTotal deve ser maior que zero." });

    if (request.PrecoPadrao < 0)
        return Results.BadRequest(new { erro = "PrecoPadrao não pode ser negativo." });

    var sql = @"INSERT INTO Eventos (Nome, CapacidadeTotal, DataEvento, PrecoPadrao)
                OUTPUT INSERTED.Id
                VALUES (@Nome, @CapacidadeTotal, @DataEvento, @PrecoPadrao)";

    var id = await db.QuerySingleAsync<int>(sql, new
    {
        request.Nome,
        request.CapacidadeTotal,
        request.DataEvento,
        request.PrecoPadrao
    });

    // Registra preço inicial no histórico (RF05)
    await db.ExecuteAsync(@"
        INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
        VALUES (@EventoId, NULL, NULL, @PrecoNovo, 'Preço inicial do evento')",
        new { EventoId = id, PrecoNovo = request.PrecoPadrao });

    var evento = new Evento
    {
        Id = id,
        Nome = request.Nome,
        CapacidadeTotal = request.CapacidadeTotal,
        DataEvento = request.DataEvento,
        PrecoPadrao = request.PrecoPadrao
    };

    return Results.Created($"/api/eventos/{id}", evento);
});

app.MapPost("/api/reservas", async (IDbConnection db, [FromBody] ReservaRequest request) =>
{
    // Validações de entrada
    if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
        return Results.BadRequest(new { erro = "CPF do usuário é obrigatório." });

    if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    if (request.EventoId <= 0)
        return Results.BadRequest(new { erro = "EventoId deve ser maior que zero." });

    // R1: Validar se UsuarioCpf existe
    var usuarioExiste = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
        new { Cpf = request.UsuarioCpf });

    if (usuarioExiste == 0)
        return Results.BadRequest(new { erro = "Usuário não encontrado para o CPF informado." });

    // R1: Validar se EventoId existe e obter dados do evento
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
        new { Id = request.EventoId });

    if (evento is null)
        return Results.BadRequest(new { erro = "Evento não encontrado para o Id informado." });

    // R2: Mesmo CPF não pode ter mais de 2 reservas para o mesmo EventoId
    var reservasCpfEvento = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId",
        new { UsuarioCpf = request.UsuarioCpf, EventoId = request.EventoId });

    if (reservasCpfEvento >= 2)
        return Results.BadRequest(new { erro = "CPF já possui o limite máximo de 2 reservas para este evento." });

    // R3: Verificar capacidade total do evento
    var totalReservasEvento = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Reservas WHERE EventoId = @EventoId",
        new { EventoId = request.EventoId });

    if (totalReservasEvento >= evento.CapacidadeTotal)
        return Results.BadRequest(new { erro = "Evento lotado. Não há vagas disponíveis." });

    // R4: Processar cupom de desconto, se informado
    decimal valorFinalPago = evento.PrecoPadrao;

    if (!string.IsNullOrWhiteSpace(request.CupomUtilizado))
    {
        var cupom = await db.QuerySingleOrDefaultAsync<Cupom>(
            "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
            new { Codigo = request.CupomUtilizado });

        if (cupom is null)
            return Results.BadRequest(new { erro = "Cupom não encontrado." });

        // Aplica desconto apenas se PrecoPadrao >= ValorMinimoRegra
        if (evento.PrecoPadrao >= cupom.ValorMinimoRegra)
        {
            valorFinalPago = evento.PrecoPadrao - (evento.PrecoPadrao * cupom.PorcentagemDesconto / 100m);
        }
    }

    // Inserir a reserva
    var sql = @"INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
                OUTPUT INSERTED.Id
                VALUES (@UsuarioCpf, @EventoId, @CupomUtilizado, @ValorFinalPago)";

    var reservaId = await db.QuerySingleAsync<int>(sql, new
    {
        UsuarioCpf = request.UsuarioCpf,
        EventoId = request.EventoId,
        CupomUtilizado = string.IsNullOrWhiteSpace(request.CupomUtilizado) ? null : request.CupomUtilizado,
        ValorFinalPago = valorFinalPago
    });

    var reservaResponse = new ReservaResponse
    {
        Id = reservaId,
        UsuarioCpf = request.UsuarioCpf,
        EventoId = request.EventoId,
        NomeEvento = evento.Nome,
        CupomUtilizado = string.IsNullOrWhiteSpace(request.CupomUtilizado) ? null : request.CupomUtilizado,
        ValorFinalPago = valorFinalPago
    };

    return Results.Created($"/api/reservas/{reservaId}", reservaResponse);
});

// ==========================================================
// RF05 — TRANSPARÊNCIA DE PREÇO: Simulador de Preço
// ==========================================================
// Endpoint de simulação que Exibe PrecoBase, TaxaServico,
// ValorDesconto e ValorFinal sem criar reserva.
//
// Regras:
//   - PrecoBase   = PrecoPadrao do Evento
//   - TaxaServico = PrecoBase × 0,10 (10% sobre o PrecoBase)
//   - ValorDesconto = PrecoBase × (PorcentagemDesconto / 100)
//                     Aplicado somente se cupom existir E
//                     PrecoBase >= ValorMinimoRegra do cupom
//   - ValorFinal  = PrecoBase + TaxaServico - ValorDesconto
//
// NÃO insere reserva (apenas simulação).
// NÃO altera a regra oficial de cupom.
// ==========================================================

app.MapPost("/api/reservas/simular-preco", async (IDbConnection db, [FromBody] SimulacaoPrecoRequest request) =>
{
    // Validações de entrada
    if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
        return Results.BadRequest(new { erro = "CPF do usuário é obrigatório." });

    if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    if (request.EventoId <= 0)
        return Results.BadRequest(new { erro = "EventoId deve ser maior que zero." });

    // Buscar evento para obter PrecoPadrao
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
        new { Id = request.EventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado para o Id informado." });

    // Calcular PrecoBase (PrecoPadrao do Evento)
    decimal precoBase = evento.PrecoPadrao;

    // Calcular TaxaServico (10% do PrecoBase — regra simples e documentada)
    decimal taxaServico = Math.Round(precoBase * 0.10m, 2);

    // Calcular ValorDesconto com base no cupom (regra oficial)
    decimal valorDesconto = 0;

    if (!string.IsNullOrWhiteSpace(request.CupomUtilizado))
    {
        var cupom = await db.QuerySingleOrDefaultAsync<Cupom>(
            "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
            new { Codigo = request.CupomUtilizado });

        if (cupom is not null)
        {
            // Aplica desconto somente se PrecoBase >= ValorMinimoRegra (regra oficial)
            if (precoBase >= cupom.ValorMinimoRegra)
            {
                valorDesconto = Math.Round(precoBase * cupom.PorcentagemDesconto / 100m, 2);
            }
        }
    }

    // Calcular ValorFinal = PrecoBase + TaxaServico - ValorDesconto
    decimal valorFinal = precoBase + taxaServico - valorDesconto;

    var response = new SimulacaoPrecoResponse
    {
        PrecoBase = precoBase,
        TaxaServico = taxaServico,
        ValorDesconto = valorDesconto,
        ValorFinal = valorFinal
    };

    return Results.Ok(response);
});

app.MapGet("/api/eventos", async (IDbConnection db) =>
{
    const string sql = "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos";

    var eventos = await db.QueryAsync<Evento>(sql);

    return Results.Ok(eventos);
});

app.MapPost("/api/cupons", async (IDbConnection db, [FromBody] CupomRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Codigo))
        return Results.BadRequest(new { erro = "Código é obrigatório." });

    if (request.Codigo.Length > 50)
        return Results.BadRequest(new { erro = "Código não pode exceder 50 caracteres." });

    if (request.PorcentagemDesconto <= 0)
        return Results.BadRequest(new { erro = "PorcentagemDesconto deve ser maior que zero." });

    if (request.ValorMinimoRegra < 0)
        return Results.BadRequest(new { erro = "ValorMinimoRegra não pode ser negativo." });

    var existe = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Cupons WHERE Codigo = @Codigo",
        new { request.Codigo });

    if (existe > 0)
        return Results.BadRequest(new { erro = "Código já existe." });

    await db.ExecuteAsync(
        "INSERT INTO Cupons (Codigo, PorcentagemDesconto, ValorMinimoRegra) VALUES (@Codigo, @PorcentagemDesconto, @ValorMinimoRegra)",
        new { request.Codigo, request.PorcentagemDesconto, request.ValorMinimoRegra });

    var cupom = new Cupom
    {
        Codigo = request.Codigo,
        PorcentagemDesconto = request.PorcentagemDesconto,
        ValorMinimoRegra = request.ValorMinimoRegra
    };

    return Results.Created($"/api/cupons/{request.Codigo}", cupom);
});

app.MapGet("/api/reservas/{cpf}", async (IDbConnection db, string cpf) =>
{
    // Primeiro verifica se o CPF existe como usuário
    var usuarioExiste = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Usuarios WHERE Cpf = @Cpf",
        new { Cpf = cpf });

    if (usuarioExiste == 0)
        return Results.NotFound(new { erro = "CPF não encontrado." });

    // Consulta reservas do CPF
    const string sql = @"
        SELECT r.Id, r.UsuarioCpf, r.EventoId, e.Nome AS NomeEvento, r.CupomUtilizado, r.ValorFinalPago
        FROM Reservas r
        INNER JOIN Eventos e ON r.EventoId = e.Id
        WHERE r.UsuarioCpf = @Cpf";

    var reservas = await db.QueryAsync<ReservaResponse>(sql, new { Cpf = cpf });

    return Results.Ok(reservas.AsList());
});

// ==========================================================
// RF01 — INGRESSO DIGITAL (3 endpoints)
// ==========================================================

// 2.1. Gerar ingresso para reserva existente
app.MapPost("/api/reservas/{id}/ingresso", async (IDbConnection db, int id) =>
{
    // Verificar se reserva existe
    var reserva = await db.QuerySingleOrDefaultAsync<Reserva>(
        "SELECT Id, UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago FROM Reservas WHERE Id = @Id",
        new { Id = id });

    if (reserva is null)
        return Results.NotFound(new { erro = "Reserva não encontrada." });

    // Verificar se reserva já possui ingresso
    var ingressoExistente = await db.QuerySingleOrDefaultAsync<Ingresso>(
        "SELECT Id FROM Ingressos WHERE ReservaId = @ReservaId",
        new { ReservaId = id });

    if (ingressoExistente is not null)
        return Results.Conflict(new { erro = "Reserva já possui ingresso gerado." });

    // Obter evento vinculado
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome, PrecoPadrao FROM Eventos WHERE Id = @Id",
        new { Id = reserva.EventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento vinculado à reserva não encontrado." });

    // Gerar código único de 8 caracteres
    var codigoUnico = await GerarCodigoUnicoAsync(db);

    // Calcular valores
    decimal valorBruto = evento.PrecoPadrao;
    decimal valorDesconto = 0;
    decimal taxaServico = 0;
    decimal valorFinal = valorBruto;

    // Se a reserva usou cupom, calcular desconto
    if (!string.IsNullOrWhiteSpace(reserva.CupomUtilizado))
    {
        var cupom = await db.QuerySingleOrDefaultAsync<Cupom>(
            "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
            new { Codigo = reserva.CupomUtilizado });

        if (cupom is not null && evento.PrecoPadrao >= cupom.ValorMinimoRegra)
        {
            valorDesconto = evento.PrecoPadrao * cupom.PorcentagemDesconto / 100m;
        }
    }

    valorFinal = valorBruto - valorDesconto; // TaxaServico não incluída no ValorFinal

    var insertSql = @"
        INSERT INTO Ingressos (ReservaId, TipoIngressoId, CodigoUnico, Status, ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao)
        OUTPUT INSERTED.Id, INSERTED.DataCriacao
        VALUES (@ReservaId, NULL, @CodigoUnico, 'Confirmada', @ValorBruto, @ValorDesconto, @TaxaServico, @ValorFinal, GETDATE())";

    var result = await db.QuerySingleAsync(insertSql, new
    {
        ReservaId = id,
        CodigoUnico = codigoUnico,
        ValorBruto = valorBruto,
        ValorDesconto = valorDesconto,
        TaxaServico = taxaServico,
        ValorFinal = valorFinal
    });

    int ingressoId = (int)result.Id;
    DateTime dataCriacao = (DateTime)result.DataCriacao;

    var response = new IngressoResponse
    {
        Id = ingressoId,
        ReservaId = id,
        TipoIngressoId = null,
        CodigoUnico = codigoUnico,
        Status = "Confirmada",
        ValorBruto = valorBruto,
        ValorDesconto = valorDesconto,
        TaxaServico = taxaServico,
        ValorFinal = valorFinal,
        DataCriacao = dataCriacao
    };

    return Results.Created($"/api/ingressos/{codigoUnico}", response);
});

// 2.2. Consultar ingresso por código único (8 caracteres) ou por ID da reserva (numérico)
app.MapGet("/api/ingressos/{param}", async (IDbConnection db, string param) =>
{
    // Se o parâmetro for numérico, consulta por ReservaId
    if (int.TryParse(param, out int reservaId))
    {
        // Verificar se a reserva existe
        var reserva = await db.QuerySingleOrDefaultAsync<Reserva>(
            "SELECT Id, UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago FROM Reservas WHERE Id = @Id",
            new { Id = reservaId });

        if (reserva is null)
            return Results.NotFound(new { erro = "Reserva não encontrada." });

        // Buscar ingresso vinculado à reserva com dados do evento
        var sql = @"
            SELECT i.Id, i.ReservaId, i.CodigoUnico AS CodigoIngresso, i.Status,
                   i.ValorFinal, i.DataCriacao,
                   e.Nome AS NomeEvento, e.DataEvento,
                   r.UsuarioCpf
            FROM Ingressos i
            INNER JOIN Reservas r ON r.Id = i.ReservaId
            INNER JOIN Eventos e ON e.Id = r.EventoId
            WHERE i.ReservaId = @ReservaId";

        var ingresso = await db.QuerySingleOrDefaultAsync<IngressoPorReservaResponse>(
            sql,
            new { ReservaId = reservaId });

        if (ingresso is null)
            return Results.NotFound(new { erro = "Nenhum ingresso gerado para esta reserva." });

        return Results.Ok(ingresso);
    }

    // Caso contrário, consulta pelo código único de 8 caracteres
    if (param.Length != 8)
        return Results.BadRequest(new { erro = "Código deve ter 8 caracteres." });

    var sqlDetalhado = @"
        SELECT i.Id, i.ReservaId, i.TipoIngressoId, i.CodigoUnico, i.Status,
               i.ValorBruto, i.ValorDesconto, i.TaxaServico, i.ValorFinal, i.DataCriacao,
               ti.Id, ti.Nome, ti.Preco,
               e.Id, e.Nome, e.DataEvento,
               u.Cpf, u.Nome
        FROM Ingressos i
        LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
        INNER JOIN Reservas r ON r.Id = i.ReservaId
        INNER JOIN Eventos e ON e.Id = r.EventoId
        INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
        WHERE i.CodigoUnico = @Codigo";

    var resultado = await db.QueryAsync<IngressoDetalhadoResponse, TipoIngressoResumo, EventoResumo, UsuarioResumo, IngressoDetalhadoResponse>(
        sqlDetalhado,
        (ingresso, tipoIngresso, evento, usuario) =>
        {
            ingresso.TipoIngresso = tipoIngresso?.Id > 0 ? tipoIngresso : null;
            ingresso.Evento = evento;
            ingresso.Usuario = usuario;
            return ingresso;
        },
        new { Codigo = param },
        splitOn: "Id,Id,Cpf");

    var ingressoDetalhado = resultado.FirstOrDefault();

    if (ingressoDetalhado is null)
        return Results.NotFound(new { erro = "Ingresso não encontrado." });

    return Results.Ok(ingressoDetalhado);
});

// 2.3. Consultar ingresso por reserva
app.MapGet("/api/reservas/{id}/ingresso", async (IDbConnection db, int id) =>
{
    var reserva = await db.QuerySingleOrDefaultAsync<Reserva>(
        "SELECT Id FROM Reservas WHERE Id = @Id",
        new { Id = id });

    if (reserva is null)
        return Results.NotFound(new { erro = "Reserva não encontrada." });

    var ingresso = await db.QuerySingleOrDefaultAsync<IngressoResponse>(
        @"SELECT Id, ReservaId, TipoIngressoId, CodigoUnico, Status,
                  ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao
           FROM Ingressos WHERE ReservaId = @ReservaId",
        new { ReservaId = id });

    if (ingresso is null)
        return Results.NotFound(new { erro = "Nenhum ingresso gerado para esta reserva." });

    return Results.Ok(ingresso);
});

// ==========================================================
// RF02 — CHECK-IN (3 endpoints)
// ==========================================================

// 3.1. Realizar check-in
app.MapPost("/api/ingressos/{codigo}/checkin", async (IDbConnection db, string codigo) =>
{
    if (codigo.Length != 8)
        return Results.BadRequest(new { erro = "Código deve ter 8 caracteres." });

    var ingresso = await db.QuerySingleOrDefaultAsync<Ingresso>(
        "SELECT Id, Status FROM Ingressos WHERE CodigoUnico = @Codigo",
        new { Codigo = codigo });

    if (ingresso is null)
        return Results.NotFound(new { erro = "Ingresso não encontrado." });

    if (ingresso.Status != "Confirmada")
        return Results.Conflict(new { erro = $"Ingresso não está confirmado para check-in. Status atual: {ingresso.Status}" });

    // Verificar se check-in já foi realizado
    var checkinExistente = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM CheckIns WHERE IngressoId = @IngressoId",
        new { IngressoId = ingresso.Id });

    if (checkinExistente > 0)
        return Results.Conflict(new { erro = "Check-in já realizado para este ingresso." });

    // Registrar check-in
    var insertSql = @"INSERT INTO CheckIns (IngressoId, DataCheckIn)
                      OUTPUT INSERTED.Id, INSERTED.DataCheckIn
                      VALUES (@IngressoId, GETDATE())";

    var result = await db.QuerySingleAsync(insertSql, new { IngressoId = ingresso.Id });
    int checkinId = (int)result.Id;
    DateTime dataCheckIn = (DateTime)result.DataCheckIn;

    // Atualizar status do ingresso para Utilizada
    await db.ExecuteAsync(
        "UPDATE Ingressos SET Status = 'Utilizada' WHERE Id = @Id",
        new { Id = ingresso.Id });

    var response = new CheckInResponse
    {
        Id = checkinId,
        IngressoId = ingresso.Id,
        CodigoUnico = codigo,
        DataCheckIn = dataCheckIn,
        Mensagem = "Check-in realizado com sucesso. Bem-vindo ao evento!"
    };

    return Results.Created($"/api/checkins/{checkinId}", response);
});

// 3.2. Listar check-ins de um evento
app.MapGet("/api/eventos/{eventoId}/checkins", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT ci.Id, ci.IngressoId, i.CodigoUnico, u.Nome AS NomeUsuario,
               u.Cpf AS UsuarioCpf, ti.Nome AS TipoIngresso, ci.DataCheckIn
        FROM CheckIns ci
        INNER JOIN Ingressos i ON i.Id = ci.IngressoId
        INNER JOIN Reservas r ON r.Id = i.ReservaId
        INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
        LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
        WHERE r.EventoId = @EventoId
        ORDER BY ci.DataCheckIn DESC";

    var checkins = (await db.QueryAsync<CheckInItemResponse>(sql, new { EventoId = eventoId })).AsList();

    var response = new CheckInListResponse
    {
        EventoId = eventoId,
        NomeEvento = evento.Nome,
        TotalCheckIns = checkins.Count,
        CheckIns = checkins
    };

    return Results.Ok(response);
});

// 3.3. Estatísticas de check-in do evento
app.MapGet("/api/eventos/{eventoId}/checkins/stats", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var stats = await db.QuerySingleAsync(@"
        SELECT
            ISNULL(SUM(CASE WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0 END), 0) AS TotalIngressosVendidos,
            ISNULL(COUNT(DISTINCT ci.Id), 0) AS TotalCheckIns,
            ISNULL(SUM(CASE WHEN ig.Status = 'Confirmada' THEN 1 ELSE 0 END), 0) AS Pendentes
        FROM Eventos e
        LEFT JOIN TiposIngresso ti ON ti.EventoId = e.Id
        LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
        LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
        WHERE e.Id = @EventoId",
        new { EventoId = eventoId });

    int totalVendidos = (int)stats.TotalIngressosVendidos;
    int totalCheckIns = (int)stats.TotalCheckIns;
    int pendentes = (int)stats.Pendentes;
    decimal percentual = totalVendidos > 0
        ? Math.Round((decimal)totalCheckIns / totalVendidos * 100, 2)
        : 0;

    var response = new CheckInStatsResponse
    {
        EventoId = eventoId,
        NomeEvento = evento.Nome,
        TotalIngressosVendidos = totalVendidos,
        TotalCheckIns = totalCheckIns,
        Pendentes = pendentes,
        PercentualPresenca = percentual
    };

    return Results.Ok(response);
});

// ==========================================================
// NOVO ENDPOINT: Check-in via CodigoIngresso no corpo
// ==========================================================

app.MapPost("/api/checkin", async (IDbConnection db, [FromBody] CheckInRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.CodigoIngresso))
        return Results.BadRequest(new { erro = "Código do ingresso é obrigatório." });

    // Validar se o ingresso existe
    var ingresso = await db.QuerySingleOrDefaultAsync<Ingresso>(
        "SELECT Id, Status FROM Ingressos WHERE CodigoUnico = @Codigo",
        new { Codigo = request.CodigoIngresso });

    if (ingresso is null)
        return Results.NotFound(new { erro = "Ingresso não encontrado." });

    // Validar se o ingresso está Ativo (Confirmada)
    if (ingresso.Status != "Confirmada")
        return Results.BadRequest(new { erro = $"Ingresso já utilizado. Status atual: {ingresso.Status}" });

    // Bloquear check-in duplicado
    var checkinExistente = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM CheckIns WHERE IngressoId = @IngressoId",
        new { IngressoId = ingresso.Id });

    if (checkinExistente > 0)
        return Results.BadRequest(new { erro = "Check-in já realizado para este ingresso." });

    // Registrar DataHoraCheckin e obter dados inseridos
    var insertSql = @"INSERT INTO CheckIns (IngressoId, DataCheckIn)
                      OUTPUT INSERTED.Id, INSERTED.DataCheckIn
                      VALUES (@IngressoId, GETDATE())";

    var result = await db.QuerySingleAsync(insertSql, new { IngressoId = ingresso.Id });
    int checkinId = (int)result.Id;
    DateTime dataCheckIn = (DateTime)result.DataCheckIn;

    // Alterar status do ingresso para Utilizada
    await db.ExecuteAsync(
        "UPDATE Ingressos SET Status = 'Utilizada' WHERE Id = @Id",
        new { Id = ingresso.Id });

    var response = new CheckInResponse
    {
        Id = checkinId,
        IngressoId = ingresso.Id,
        CodigoUnico = request.CodigoIngresso,
        DataCheckIn = dataCheckIn,
        Mensagem = "Check-in realizado com sucesso. Bem-vindo ao evento!"
    };

    return Results.Created($"/api/checkin/{checkinId}", response);
});

// ==========================================================
// RF03 — LOTES/TIPOS DE INGRESSO (5 endpoints)
// ==========================================================

// 4.1. Criar lote
app.MapPost("/api/eventos/{eventoId}/lotes", async (IDbConnection db, int eventoId, [FromBody] CriarLoteRequest request) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome do lote é obrigatório." });

    if (request.Nome.Length > 100)
        return Results.BadRequest(new { erro = "Nome não pode exceder 100 caracteres." });

    if (request.Preco <= 0)
        return Results.BadRequest(new { erro = "Preço deve ser maior que zero." });

    if (request.Capacidade <= 0)
        return Results.BadRequest(new { erro = "Capacidade deve ser maior que zero." });

    if (request.TaxaServico < 0)
        return Results.BadRequest(new { erro = "Taxa de serviço não pode ser negativa." });

    if (request.DataInicioVenda == default)
        return Results.BadRequest(new { erro = "Data de início da venda é obrigatória." });

    if (request.DataFimVenda == default)
        return Results.BadRequest(new { erro = "Data de fim da venda é obrigatória." });

    if (request.DataFimVenda <= request.DataInicioVenda)
        return Results.BadRequest(new { erro = "Data de fim da venda deve ser posterior à data de início." });

    var insertSql = @"
        INSERT INTO TiposIngresso (EventoId, Nome, Preco, Capacidade, TaxaServico, DataInicioVenda, DataFimVenda)
        OUTPUT INSERTED.Id
        VALUES (@EventoId, @Nome, @Preco, @Capacidade, @TaxaServico, @DataInicioVenda, @DataFimVenda)";

    var loteId = await db.QuerySingleAsync<int>(insertSql, new
    {
        EventoId = eventoId,
        request.Nome,
        request.Preco,
        request.Capacidade,
        request.TaxaServico,
        request.DataInicioVenda,
        request.DataFimVenda
    });

    // Registrar preço inicial no histórico
    await db.ExecuteAsync(@"
        INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
        VALUES (@EventoId, @TipoIngressoId, NULL, @PrecoNovo, 'Preço inicial do lote')",
        new { EventoId = eventoId, TipoIngressoId = loteId, PrecoNovo = request.Preco });

    var response = new LoteResponse
    {
        Id = loteId,
        EventoId = eventoId,
        Nome = request.Nome,
        Preco = request.Preco,
        Capacidade = request.Capacidade,
        TaxaServico = request.TaxaServico,
        DataInicioVenda = request.DataInicioVenda,
        DataFimVenda = request.DataFimVenda
    };

    return Results.Created($"/api/lotes/{loteId}", response);
});

// 4.2. Listar lotes de um evento
app.MapGet("/api/eventos/{eventoId}/lotes", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT ti.Id, ti.EventoId, ti.Nome, ti.Preco, ti.Capacidade, ti.TaxaServico,
               ti.DataInicioVenda, ti.DataFimVenda,
               ISNULL(SUM(CASE WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0 END), 0) AS IngressosVendidos,
               ti.Capacidade - ISNULL(SUM(CASE WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0 END), 0) AS CapacidadeRestante
        FROM TiposIngresso ti
        LEFT JOIN Ingressos ig ON ig.TipoIngressoId = ti.Id
        WHERE ti.EventoId = @EventoId
        GROUP BY ti.Id, ti.EventoId, ti.Nome, ti.Preco, ti.Capacidade, ti.TaxaServico,
                 ti.DataInicioVenda, ti.DataFimVenda
        ORDER BY ti.Id";

    var lotes = await db.QueryAsync<LoteListaResponse>(sql, new { EventoId = eventoId });

    return Results.Ok(lotes);
});

// 4.3. Obter lote específico
app.MapGet("/api/lotes/{loteId}", async (IDbConnection db, int loteId) =>
{
    var lote = await db.QuerySingleOrDefaultAsync<LoteResponse>(
        @"SELECT Id, EventoId, Nome, Preco, Capacidade, TaxaServico, DataInicioVenda, DataFimVenda
          FROM TiposIngresso WHERE Id = @Id",
        new { Id = loteId });

    if (lote is null)
        return Results.NotFound(new { erro = "Lote não encontrado." });

    return Results.Ok(lote);
});

// 4.4. Atualizar lote
app.MapPut("/api/lotes/{loteId}", async (IDbConnection db, int loteId, [FromBody] CriarLoteRequest request) =>
{
    var lote = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
        "SELECT Id, EventoId, Nome, Preco, Capacidade FROM TiposIngresso WHERE Id = @Id",
        new { Id = loteId });

    if (lote is null)
        return Results.NotFound(new { erro = "Lote não encontrado." });

    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome do lote é obrigatório." });

    if (request.Nome.Length > 100)
        return Results.BadRequest(new { erro = "Nome não pode exceder 100 caracteres." });

    if (request.Preco <= 0)
        return Results.BadRequest(new { erro = "Preço deve ser maior que zero." });

    if (request.TaxaServico < 0)
        return Results.BadRequest(new { erro = "Taxa de serviço não pode ser negativa." });

    // Verificar se capacidade não é menor que ingressos vendidos
    var ingressosVendidos = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Ingressos WHERE TipoIngressoId = @TipoIngressoId AND Status IN ('Confirmada', 'Utilizada')",
        new { TipoIngressoId = loteId });

    if (request.Capacidade < ingressosVendidos)
        return Results.BadRequest(new { erro = "Capacidade não pode ser menor que a quantidade de ingressos já vendidos para este lote." });

    if (request.DataInicioVenda == default)
        return Results.BadRequest(new { erro = "Data de início da venda é obrigatória." });

    if (request.DataFimVenda == default)
        return Results.BadRequest(new { erro = "Data de fim da venda é obrigatória." });

    if (request.DataFimVenda <= request.DataInicioVenda)
        return Results.BadRequest(new { erro = "Data de fim da venda deve ser posterior à data de início." });

    // Registrar alteração de preço no histórico se mudou
    if (lote.Preco != request.Preco)
    {
        await db.ExecuteAsync(@"
            INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
            VALUES (@EventoId, @TipoIngressoId, @PrecoAnterior, @PrecoNovo, 'Alteração de preço do lote')",
            new
            {
                EventoId = lote.EventoId,
                TipoIngressoId = loteId,
                PrecoAnterior = lote.Preco,
                PrecoNovo = request.Preco
            });
    }

    await db.ExecuteAsync(@"
        UPDATE TiposIngresso
        SET Nome = @Nome, Preco = @Preco, Capacidade = @Capacidade,
            TaxaServico = @TaxaServico, DataInicioVenda = @DataInicioVenda,
            DataFimVenda = @DataFimVenda
        WHERE Id = @Id",
        new
        {
            Id = loteId,
            request.Nome,
            request.Preco,
            request.Capacidade,
            request.TaxaServico,
            request.DataInicioVenda,
            request.DataFimVenda
        });

    var response = new LoteResponse
    {
        Id = loteId,
        EventoId = lote.EventoId,
        Nome = request.Nome,
        Preco = request.Preco,
        Capacidade = request.Capacidade,
        TaxaServico = request.TaxaServico,
        DataInicioVenda = request.DataInicioVenda,
        DataFimVenda = request.DataFimVenda
    };

    return Results.Ok(response);
});

// 4.5. Remover lote
app.MapDelete("/api/lotes/{loteId}", async (IDbConnection db, int loteId) =>
{
    var lote = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
        "SELECT Id FROM TiposIngresso WHERE Id = @Id",
        new { Id = loteId });

    if (lote is null)
        return Results.NotFound(new { erro = "Lote não encontrado." });

    // Verificar se lote possui ingressos vendidos
    var ingressosVendidos = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Ingressos WHERE TipoIngressoId = @TipoIngressoId AND Status IN ('Confirmada', 'Utilizada')",
        new { TipoIngressoId = loteId });

    if (ingressosVendidos > 0)
        return Results.Conflict(new { erro = "Não é possível remover um lote com ingressos vendidos." });

    // Remove registros de histórico associados
    await db.ExecuteAsync("DELETE FROM HistoricoPrecos WHERE TipoIngressoId = @Id", new { Id = loteId });
    await db.ExecuteAsync("DELETE FROM TiposIngresso WHERE Id = @Id", new { Id = loteId });

    return Results.NoContent();
});

// ==========================================================
// NOVOS ENDPOINTS: Tipos de Ingresso (via /api/tipos-ingresso)
// ==========================================================

// Criar tipo de ingresso
app.MapPost("/api/tipos-ingresso", async (IDbConnection db, [FromBody] CriarTipoIngressoRequest request) =>
{
    // Valida EventoId existente
    if (request.EventoId <= 0)
        return Results.BadRequest(new { erro = "EventoId é obrigatório e deve ser maior que zero." });

    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = request.EventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    // Valida Nome
    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });

    if (request.Nome.Length > 100)
        return Results.BadRequest(new { erro = "Nome não pode exceder 100 caracteres." });

    // Valida QuantidadeDisponivel
    if (request.QuantidadeDisponivel <= 0)
        return Results.BadRequest(new { erro = "QuantidadeDisponivel deve ser maior que zero." });

    // Valida Preco
    if (request.Preco < 0)
        return Results.BadRequest(new { erro = "Preco não pode ser negativo." });

    // Valida Lote
    if (string.IsNullOrWhiteSpace(request.Lote))
        return Results.BadRequest(new { erro = "Lote é obrigatório." });

    if (request.Lote.Length > 100)
        return Results.BadRequest(new { erro = "Lote não pode exceder 100 caracteres." });

    var insertSql = @"
        INSERT INTO TiposIngresso (EventoId, Nome, Preco, Capacidade, TaxaServico, DataInicioVenda, DataFimVenda, Lote)
        OUTPUT INSERTED.Id
        VALUES (@EventoId, @Nome, @Preco, @Capacidade, 0.00, GETDATE(), '9999-12-31', @Lote)";

    var tipoId = await db.QuerySingleAsync<int>(insertSql, new
    {
        EventoId = request.EventoId,
        Nome = request.Nome,
        Preco = request.Preco,
        Capacidade = request.QuantidadeDisponivel,
        Lote = request.Lote
    });

    // Registra preço inicial no histórico
    await db.ExecuteAsync(@"
        INSERT INTO HistoricoPrecos (EventoId, TipoIngressoId, PrecoAnterior, PrecoNovo, Motivo)
        VALUES (@EventoId, @TipoIngressoId, NULL, @PrecoNovo, 'Preço inicial do tipo de ingresso')",
        new
        {
            EventoId = request.EventoId,
            TipoIngressoId = tipoId,
            PrecoNovo = request.Preco
        });

    var response = new TipoIngressoResponse
    {
        Id = tipoId,
        EventoId = request.EventoId,
        Nome = request.Nome,
        QuantidadeDisponivel = request.QuantidadeDisponivel,
        Preco = request.Preco,
        Lote = request.Lote
    };

    return Results.Created($"/api/tipos-ingresso/{tipoId}", response);
});

// Listar tipos de ingresso de um evento
app.MapGet("/api/eventos/{eventoId}/tipos-ingresso", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT Id, EventoId, Nome, Capacidade AS QuantidadeDisponivel, Preco, Lote
        FROM TiposIngresso
        WHERE EventoId = @EventoId
        ORDER BY Id";

    var tipos = await db.QueryAsync<TipoIngressoResponse>(sql, new { EventoId = eventoId });

    return Results.Ok(tipos);
});

// ==========================================================
// RF04 — CARRINHO (4 endpoints)
// ==========================================================

// Função auxiliar para gerar carrinho response
static async Task<CarrinhoResponse> ConstruirCarrinhoResponseAsync(IDbConnection db, int carrinhoId)
{
    var carrinho = await db.QuerySingleAsync<Carrinho>(
        "SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE Id = @Id",
        new { Id = carrinhoId });

    var itens = await db.QueryAsync<CarrinhoItemResponse>(@"
        SELECT ci.Id, ci.EventoId, e.Nome AS NomeEvento, ci.TipoIngressoId,
               ti.Nome AS NomeLote, ci.Quantidade, ci.PrecoUnitario,
               (ci.Quantidade * ci.PrecoUnitario) AS Subtotal
        FROM CarrinhoItens ci
        INNER JOIN Eventos e ON e.Id = ci.EventoId
        LEFT JOIN TiposIngresso ti ON ti.Id = ci.TipoIngressoId
        WHERE ci.CarrinhoId = @CarrinhoId",
        new { CarrinhoId = carrinhoId });

    var itensList = itens.AsList();
    var total = itensList.Sum(i => i.Subtotal);

    var minutosRestantes = carrinho.Status == "Ativo"
        ? Math.Max(0, (int)(carrinho.DataExpiracao - DateTime.Now).TotalMinutes)
        : 0;

    return new CarrinhoResponse
    {
        CarrinhoId = carrinho.Id,
        UsuarioCpf = carrinho.UsuarioCpf,
        Status = carrinho.Status,
        DataCriacao = carrinho.DataCriacao,
        DataExpiracao = carrinho.DataExpiracao,
        MinutosRestantes = minutosRestantes,
        Itens = itensList,
        Total = total
    };
}

// 5.1. Criar carrinho vazio
app.MapPost("/api/carrinho", async (IDbConnection db, [FromBody] CriarCarrinhoRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.UsuarioCpf))
        return Results.BadRequest(new { erro = "CPF do usuário é obrigatório." });

    if (request.UsuarioCpf.Length != 11 || !request.UsuarioCpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    var usuario = await db.QuerySingleOrDefaultAsync<Usuario>(
        "SELECT Cpf FROM Usuarios WHERE Cpf = @Cpf",
        new { Cpf = request.UsuarioCpf });

    if (usuario is null)
        return Results.BadRequest(new { erro = "Usuário não encontrado para o CPF informado." });

    // Verificar carrinho ativo existente
    var carrinhoAtivo = await db.QuerySingleOrDefaultAsync<Carrinho>(@"
        SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao
        FROM Carrinhos
        WHERE UsuarioCpf = @UsuarioCpf AND Status = 'Ativo'",
        new { UsuarioCpf = request.UsuarioCpf });

    if (carrinhoAtivo is not null)
    {
        // Se expirou, marcar como expirado e permitir criar novo
        if (carrinhoAtivo.DataExpiracao <= DateTime.Now)
        {
            await db.ExecuteAsync(
                "UPDATE Carrinhos SET Status = 'Expirado' WHERE Id = @Id",
                new { Id = carrinhoAtivo.Id });
        }
        else
        {
            return Results.BadRequest(new { erro = "Já existe um carrinho ativo para este CPF." });
        }
    }

    // Criar novo carrinho com validade de 15 minutos
    var carrinhoId = await db.QuerySingleAsync<int>(@"
        INSERT INTO Carrinhos (UsuarioCpf, Status, DataCriacao, DataExpiracao)
        OUTPUT INSERTED.Id
        VALUES (@UsuarioCpf, 'Ativo', GETDATE(), DATEADD(MINUTE, 15, GETDATE()))",
        new { UsuarioCpf = request.UsuarioCpf });

    var response = await ConstruirCarrinhoResponseAsync(db, carrinhoId);

    return Results.Created($"/api/carrinho/{carrinhoId}", response);
});

// 5.2. Adicionar itens ao carrinho
app.MapPost("/api/carrinho/{id}/itens", async (IDbConnection db, int id, [FromBody] AdicionarItensRequest request) =>
{
    var carrinho = await db.QuerySingleOrDefaultAsync<Carrinho>(
        "SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao FROM Carrinhos WHERE Id = @Id",
        new { Id = id });

    if (carrinho is null)
        return Results.NotFound(new { erro = "Carrinho não encontrado." });

    if (carrinho.Status != "Ativo")
        return Results.BadRequest(new { erro = "Carrinho não está ativo." });

    if (carrinho.DataExpiracao <= DateTime.Now)
    {
        await db.ExecuteAsync(
            "UPDATE Carrinhos SET Status = 'Expirado' WHERE Id = @Id",
            new { Id = carrinho.Id });
        return Results.BadRequest(new { erro = "Carrinho expirado. Crie um novo carrinho." });
    }

    if (request.Itens is null || request.Itens.Count == 0)
        return Results.BadRequest(new { erro = "Carrinho deve conter ao menos um item." });

    for (int i = 0; i < request.Itens.Count; i++)
    {
        var item = request.Itens[i];

        if (item.EventoId <= 0)
            return Results.BadRequest(new { erro = "EventoId é obrigatório para cada item." });

        var evento = await db.QuerySingleOrDefaultAsync<Evento>(
            "SELECT Id, Nome, PrecoPadrao FROM Eventos WHERE Id = @Id",
            new { Id = item.EventoId });

        if (evento is null)
            return Results.BadRequest(new { erro = "Evento não encontrado para o Id informado." });

        if (item.Quantidade <= 0)
            return Results.BadRequest(new { erro = "Quantidade deve ser maior que zero." });

        decimal precoUnitario = evento.PrecoPadrao;

        if (item.TipoIngressoId.HasValue)
        {
            var tipoIngresso = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
                "SELECT Id, EventoId, Nome, Preco, Capacidade FROM TiposIngresso WHERE Id = @Id",
                new { Id = item.TipoIngressoId.Value });

            if (tipoIngresso is null)
                return Results.BadRequest(new { erro = "Tipo de ingresso não encontrado." });

            if (tipoIngresso.EventoId != item.EventoId)
                return Results.BadRequest(new { erro = "Tipo de ingresso não pertence ao evento informado." });

            // Verificar disponibilidade do lote
            var vendidos = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Ingressos WHERE TipoIngressoId = @TipoIngressoId AND Status IN ('Confirmada', 'Utilizada')",
                new { TipoIngressoId = item.TipoIngressoId.Value });

            var reservadosCarrinho = await db.ExecuteScalarAsync<int>(@"
                SELECT ISNULL(SUM(ci.Quantidade), 0)
                FROM CarrinhoItens ci
                INNER JOIN Carrinhos c ON c.Id = ci.CarrinhoId
                WHERE ci.TipoIngressoId = @TipoIngressoId AND c.Status = 'Ativo' AND c.Id != @CarrinhoId",
                new { TipoIngressoId = item.TipoIngressoId.Value, CarrinhoId = carrinho.Id });

            if (vendidos + reservadosCarrinho + item.Quantidade > tipoIngresso.Capacidade)
                return Results.BadRequest(new { erro = "Capacidade insuficiente no lote informado." });

            precoUnitario = tipoIngresso.Preco;
        }

        // Verificar limite de 2 reservas por CPF por evento (R1)
        var reservasCpfEvento = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId",
            new { UsuarioCpf = carrinho.UsuarioCpf, EventoId = item.EventoId });

        if (reservasCpfEvento >= 2)
            return Results.BadRequest(new { erro = $"CPF já possui o limite máximo de 2 reservas para o evento {evento.Id}." });

        // Adicionar item ao carrinho
        await db.ExecuteAsync(@"
            INSERT INTO CarrinhoItens (CarrinhoId, EventoId, TipoIngressoId, Quantidade, PrecoUnitario)
            VALUES (@CarrinhoId, @EventoId, @TipoIngressoId, @Quantidade, @PrecoUnitario)",
            new
            {
                CarrinhoId = carrinho.Id,
                EventoId = item.EventoId,
                TipoIngressoId = item.TipoIngressoId,
                Quantidade = item.Quantidade,
                PrecoUnitario = precoUnitario
            });
    }

    var response = await ConstruirCarrinhoResponseAsync(db, carrinho.Id);

    return Results.Ok(response);
});

// 5.3. Visualizar carrinho ativo
app.MapGet("/api/carrinho/{cpf}", async (IDbConnection db, string cpf) =>
{
    if (cpf.Length != 11 || !cpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    var carrinho = await db.QuerySingleOrDefaultAsync<Carrinho>(@"
        SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao
        FROM Carrinhos
        WHERE UsuarioCpf = @Cpf AND Status IN ('Ativo', 'Expirado')
        ORDER BY Id DESC",
        new { Cpf = cpf });

    if (carrinho is null)
        return Results.NotFound(new { erro = "Nenhum carrinho ativo encontrado para este CPF." });

    // Se expirou, atualizar status
    if (carrinho.Status == "Ativo" && carrinho.DataExpiracao <= DateTime.Now)
    {
        await db.ExecuteAsync(
            "UPDATE Carrinhos SET Status = 'Expirado' WHERE Id = @Id",
            new { Id = carrinho.Id });
        carrinho.Status = "Expirado";
    }

    var response = await ConstruirCarrinhoResponseAsync(db, carrinho.Id);

    if (carrinho.Status == "Expirado")
    {
        response.Mensagem = "Carrinho expirado. Crie um novo carrinho para continuar.";
        response.MinutosRestantes = 0;
    }

    return Results.Ok(response);
});

// 5.3. Limpar carrinho
app.MapDelete("/api/carrinho/{cpf}", async (IDbConnection db, string cpf) =>
{
    if (cpf.Length != 11 || !cpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    var carrinho = await db.QuerySingleOrDefaultAsync<Carrinho>(@"
        SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao
        FROM Carrinhos
        WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'",
        new { Cpf = cpf });

    if (carrinho is null)
        return Results.NotFound(new { erro = "Nenhum carrinho ativo encontrado para este CPF." });

    if (carrinho.DataExpiracao <= DateTime.Now)
    {
        await db.ExecuteAsync(
            "UPDATE Carrinhos SET Status = 'Expirado' WHERE Id = @Id",
            new { Id = carrinho.Id });
        return Results.BadRequest(new { erro = "Carrinho já expirou." });
    }

    // Remover itens e marcar carrinho como expirado
    await db.ExecuteAsync("DELETE FROM CarrinhoItens WHERE CarrinhoId = @Id", new { Id = carrinho.Id });
    await db.ExecuteAsync("UPDATE Carrinhos SET Status = 'Expirado' WHERE Id = @Id", new { Id = carrinho.Id });

    return Results.NoContent();
});

// 5.4. Confirmar carrinho
app.MapPost("/api/carrinho/{cpf}/confirmar", async (IDbConnection db, string cpf, [FromBody] ConfirmarCarrinhoRequest? request) =>
{
    if (cpf.Length != 11 || !cpf.All(char.IsDigit))
        return Results.BadRequest(new { erro = "CPF deve conter 11 dígitos numéricos." });

    var cupomUtilizado = request?.CupomUtilizado;

    var carrinho = await db.QuerySingleOrDefaultAsync<Carrinho>(@"
        SELECT Id, UsuarioCpf, Status, DataCriacao, DataExpiracao
        FROM Carrinhos
        WHERE UsuarioCpf = @Cpf AND Status = 'Ativo'",
        new { Cpf = cpf });

    if (carrinho is null)
        return Results.NotFound(new { erro = "Nenhum carrinho ativo encontrado para este CPF." });

    if (carrinho.DataExpiracao <= DateTime.Now)
        return Results.BadRequest(new { erro = "Carrinho expirado. Crie um novo carrinho." });

    // Verificar se o carrinho possui itens antes de iniciar a transação
    var totalItens = await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM CarrinhoItens WHERE CarrinhoId = @CarrinhoId",
        new { CarrinhoId = carrinho.Id });

    if (totalItens == 0)
        return Results.BadRequest(new { erro = "Carrinho vazio. Adicione itens antes de confirmar." });

    // Iniciar transação
    if (db.State != ConnectionState.Open)
        db.Open();
    using var transaction = db.BeginTransaction();
    try
    {
        // Validar cupom se informado
        Cupom? cupom = null;
        if (!string.IsNullOrWhiteSpace(cupomUtilizado))
        {
            cupom = await db.QuerySingleOrDefaultAsync<Cupom>(
                "SELECT Codigo, PorcentagemDesconto, ValorMinimoRegra FROM Cupons WHERE Codigo = @Codigo",
                new { Codigo = cupomUtilizado },
                transaction: transaction, commandTimeout: 30);

            if (cupom is null)
                throw new ValidationException("Cupom não encontrado.");
        }

        // Obter itens do carrinho
        var itensCarrinho = await db.QueryAsync<CarrinhoItem>(@"
            SELECT ci.Id, ci.CarrinhoId, ci.EventoId, ci.TipoIngressoId, ci.Quantidade, ci.PrecoUnitario
            FROM CarrinhoItens ci
            WHERE ci.CarrinhoId = @CarrinhoId",
            new { CarrinhoId = carrinho.Id },
            transaction: transaction, commandTimeout: 30);

        var reservasCriadas = new List<ReservaConfirmadaResponse>();
        decimal totalPago = 0;

        foreach (var item in itensCarrinho)
        {
            var evento = await db.QuerySingleOrDefaultAsync<Evento>(
                "SELECT Id, Nome, PrecoPadrao FROM Eventos WHERE Id = @Id",
                new { Id = item.EventoId },
                transaction: transaction, commandTimeout: 30);

            if (evento is null)
                throw new ValidationException($"Evento {item.EventoId} não encontrado.");

            string nomeLote = "";
            if (item.TipoIngressoId.HasValue)
            {
                var lote = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
                    "SELECT Id, Nome FROM TiposIngresso WHERE Id = @Id",
                    new { Id = item.TipoIngressoId.Value },
                    transaction: transaction, commandTimeout: 30);
                nomeLote = lote?.Nome ?? "";
            }

            for (int q = 0; q < item.Quantidade; q++)
            {
                // Verificar limite de 2 reservas por CPF por evento
                var reservasCpfEvento = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Reservas WHERE UsuarioCpf = @UsuarioCpf AND EventoId = @EventoId",
                    new { UsuarioCpf = carrinho.UsuarioCpf, EventoId = item.EventoId },
                    transaction: transaction, commandTimeout: 30);

                if (reservasCpfEvento >= 2)
                    throw new ValidationException($"CPF já possui o limite máximo de 2 reservas para o evento {evento.Id}.");

                // Verificar capacidade do lote
                if (item.TipoIngressoId.HasValue)
                {
                    var lote = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
                        "SELECT Id, Capacidade FROM TiposIngresso WHERE Id = @Id",
                        new { Id = item.TipoIngressoId.Value },
                        transaction: transaction, commandTimeout: 30);

                    if (lote is not null)
                    {
                        var vendidos = await db.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Ingressos WHERE TipoIngressoId = @TipoIngressoId AND Status IN ('Confirmada', 'Utilizada')",
                            new { TipoIngressoId = item.TipoIngressoId.Value },
                            transaction: transaction, commandTimeout: 30);

                        if (vendidos >= lote.Capacidade)
                            throw new ValidationException($"Capacidade insuficiente no lote {item.TipoIngressoId}.");
                    }
                }

                // Calcular valor
                decimal valorBruto = item.PrecoUnitario;
                decimal valorDesconto = 0;
                decimal taxaServico = 0;
                decimal valorFinal = valorBruto;

                if (cupom is not null && evento.PrecoPadrao >= cupom.ValorMinimoRegra)
                {
                    valorDesconto = valorBruto * cupom.PorcentagemDesconto / 100m;
                    valorFinal = valorBruto - valorDesconto;
                }

                // Criar reserva
                var reservaId = await db.QuerySingleAsync<int>(@"
                    INSERT INTO Reservas (UsuarioCpf, EventoId, CupomUtilizado, ValorFinalPago)
                    OUTPUT INSERTED.Id
                    VALUES (@UsuarioCpf, @EventoId, @CupomUtilizado, @ValorFinalPago)",
                    new
                    {
                        UsuarioCpf = carrinho.UsuarioCpf,
                        EventoId = item.EventoId,
                        CupomUtilizado = cupom?.Codigo,
                        ValorFinalPago = valorFinal
                    },
                    transaction: transaction, commandTimeout: 30);

                // Gerar código único para o ingresso
                var codigoUnico = await GerarCodigoUnicoAsync(db, transaction, 30);

                // Criar ingresso
                var ingressoId = await db.QuerySingleAsync<int>(@"
                    INSERT INTO Ingressos (ReservaId, TipoIngressoId, CodigoUnico, Status, ValorBruto, ValorDesconto, TaxaServico, ValorFinal, DataCriacao)
                    OUTPUT INSERTED.Id
                    VALUES (@ReservaId, @TipoIngressoId, @CodigoUnico, 'Confirmada', @ValorBruto, @ValorDesconto, @TaxaServico, @ValorFinal, GETDATE())",
                    new
                    {
                        ReservaId = reservaId,
                        TipoIngressoId = item.TipoIngressoId,
                        CodigoUnico = codigoUnico,
                        ValorBruto = valorBruto,
                        ValorDesconto = valorDesconto,
                        TaxaServico = taxaServico,
                        ValorFinal = valorFinal
                    },
                    transaction: transaction, commandTimeout: 30);

                reservasCriadas.Add(new ReservaConfirmadaResponse
                {
                    ReservaId = reservaId,
                    IngressoId = ingressoId,
                    CodigoUnico = codigoUnico,
                    EventoId = item.EventoId,
                    NomeEvento = evento.Nome,
                    TipoIngresso = nomeLote,
                    ValorFinal = valorFinal,
                    Status = "Confirmada"
                });

                totalPago += valorFinal;
            }
        }

        // Marcar carrinho como confirmado
        await db.ExecuteAsync(
            "UPDATE Carrinhos SET Status = 'Confirmado' WHERE Id = @Id",
            new { Id = carrinho.Id },
            transaction: transaction, commandTimeout: 30);

        // Limpar itens do carrinho
        await db.ExecuteAsync(
            "DELETE FROM CarrinhoItens WHERE CarrinhoId = @Id",
            new { Id = carrinho.Id },
            transaction: transaction, commandTimeout: 30);

        transaction.Commit();

        var response = new CarrinhoConfirmacaoResponse
        {
            Mensagem = "Carrinho confirmado com sucesso.",
            CarrinhoId = carrinho.Id,
            ReservasCriadas = reservasCriadas,
            TotalPago = totalPago
        };

        return Results.Created($"/api/carrinho/{cpf}/confirmar", response);
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
});

// ==========================================================
// RF05 — HISTÓRICO DE PREÇOS (2 endpoints)
// ==========================================================

// 6.1. Histórico de preços do evento
app.MapGet("/api/eventos/{eventoId}/historico-precos", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT hp.Id, hp.PrecoAnterior, hp.PrecoNovo, hp.DataAlteracao, hp.Motivo,
               hp.TipoIngressoId, ti.Nome AS NomeLote
        FROM HistoricoPrecos hp
        LEFT JOIN TiposIngresso ti ON ti.Id = hp.TipoIngressoId
        WHERE hp.EventoId = @EventoId
        ORDER BY hp.DataAlteracao DESC";

    var historico = await db.QueryAsync<HistoricoPrecoResponse>(sql, new { EventoId = eventoId });

    var response = new EventoHistoricoPrecosResponse
    {
        EventoId = eventoId,
        NomeEvento = evento.Nome,
        Historico = historico.AsList()
    };

    return Results.Ok(response);
});

// 6.2. Histórico de preços do lote
app.MapGet("/api/lotes/{loteId}/historico-precos", async (IDbConnection db, int loteId) =>
{
    var lote = await db.QuerySingleOrDefaultAsync<TipoIngresso>(
        "SELECT Id, Nome, EventoId FROM TiposIngresso WHERE Id = @Id",
        new { Id = loteId });

    if (lote is null)
        return Results.NotFound(new { erro = "Lote não encontrado." });

    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = lote.EventoId });

    var sql = @"
        SELECT Id, PrecoAnterior, PrecoNovo, DataAlteracao, Motivo
        FROM HistoricoPrecos
        WHERE TipoIngressoId = @TipoIngressoId
        ORDER BY DataAlteracao DESC";

    var historico = await db.QueryAsync<HistoricoPrecoResponse>(sql, new { TipoIngressoId = loteId });

    var response = new LoteHistoricoPrecosResponse
    {
        LoteId = loteId,
        NomeLote = lote.Nome,
        EventoId = lote.EventoId,
        NomeEvento = evento?.Nome ?? "",
        Historico = historico.AsList()
    };

    return Results.Ok(response);
});

// ==========================================================
// RF06 — DASHBOARD/ADMIN (4 endpoints)
// ==========================================================

// 7.1. Listar eventos com métricas
app.MapGet("/api/admin/eventos", async (IDbConnection db) =>
{
    var eventos = await db.QueryAsync<DashboardEventoListaResponse>(
        "SELECT * FROM vw_DashboardEventos ORDER BY DataEvento");

    return Results.Ok(eventos);
}).RequireAuthorization();

// 7.2. Dashboard detalhado de um evento
app.MapGet("/api/admin/eventos/{eventoId}", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var eventoDashboard = await db.QuerySingleOrDefaultAsync<DashboardEventoDetalhadoResponse>(
        "SELECT * FROM vw_DashboardEventos WHERE EventoId = @EventoId",
        new { EventoId = eventoId });

    if (eventoDashboard is null)
    {
        // Se não há dados na view (sem ingressos via TiposIngresso), buscar dados básicos
        var eventoBase = await db.QuerySingleAsync<Evento>(
            "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
            new { Id = eventoId });

        eventoDashboard = new DashboardEventoDetalhadoResponse
        {
            EventoId = eventoBase.Id,
            NomeEvento = eventoBase.Nome,
            DataEvento = eventoBase.DataEvento,
            CapacidadeTotal = eventoBase.CapacidadeTotal,
            PrecoPadrao = eventoBase.PrecoPadrao,
            TotalIngressosVendidos = 0,
            ReceitaTotal = 0,
            PercentualOcupacao = 0,
            TotalCheckIns = 0,
            PendentesCheckIn = 0,
            TotalCancelados = 0
        };
    }

    // Buscar métricas por lote
    var lotes = await db.QueryAsync<DashboardLoteResponse>(
        "SELECT * FROM vw_DashboardLotes WHERE EventoId = @EventoId",
        new { EventoId = eventoId });

    eventoDashboard.Lotes = lotes.AsList();

    return Results.Ok(eventoDashboard);
}).RequireAuthorization();

// 7.3. Métricas por lote do evento
app.MapGet("/api/admin/eventos/{eventoId}/lotes", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var lotes = await db.QueryAsync<DashboardLoteResponse>(
        "SELECT * FROM vw_DashboardLotes WHERE EventoId = @EventoId",
        new { EventoId = eventoId });

    return Results.Ok(lotes);
}).RequireAuthorization();

// 7.4. Listar todas as reservas (admin) — Item 5.2: SQL fixo com parâmetros opcionais
app.MapGet("/api/admin/reservas", async (IDbConnection db,
    [FromQuery] int? eventoId,
    [FromQuery] string? status,
    [FromQuery] string? cpf) =>
{
    var parameters = new { EventoId = eventoId, Status = status, Cpf = cpf };

    var sql = @"
        SELECT
            r.Id                     AS ReservaId,
            r.UsuarioCpf,
            u.Nome                   AS NomeUsuario,
            r.EventoId,
            e.Nome                   AS NomeEvento,
            e.DataEvento,
            i.Id                     AS IngressoId,
            i.CodigoUnico,
            i.Status                 AS StatusIngresso,
            ti.Nome                  AS TipoIngresso,
            i.ValorBruto,
            i.ValorDesconto,
            i.TaxaServico,
            i.ValorFinal,
            r.CupomUtilizado,
            CASE WHEN ci.Id IS NOT NULL THEN 1 ELSE 0 END AS CheckInRealizado
        FROM Reservas r
        INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
        INNER JOIN Eventos e ON e.Id = r.EventoId
        LEFT JOIN Ingressos i ON i.ReservaId = r.Id
        LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
        LEFT JOIN CheckIns ci ON ci.IngressoId = i.Id
        WHERE (@EventoId IS NULL OR r.EventoId = @EventoId)
          AND (@Status IS NULL OR i.Status = @Status)
          AND (@Cpf IS NULL OR r.UsuarioCpf = @Cpf)
        ORDER BY r.Id DESC";

    var reservas = await db.QueryAsync<AdminReservaResponse>(sql, parameters);

    return Results.Ok(reservas);
}).RequireAuthorization();

// ==========================================================
// ENDPOINTS ADMINISTRATIVOS DE CONSULTA
// ==========================================================

// 8.1. Resumo do evento
app.MapGet("/api/admin/eventos/{eventoId}/resumo", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT
            e.Id              AS EventoId,
            e.Nome            AS NomeEvento,
            e.DataEvento,
            e.CapacidadeTotal,
            ISNULL(COUNT(DISTINCT r.Id), 0)                                                AS TotalReservas,
            e.CapacidadeTotal - ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN 1 ELSE 0
            END), 0)                                                                        AS IngressosDisponiveis,
            ISNULL(SUM(CASE
                WHEN ig.Status IN ('Confirmada', 'Utilizada') THEN ig.ValorFinal ELSE 0
            END), 0.00)                                                                     AS ReceitaTotal,
            ISNULL(COUNT(DISTINCT ci.Id), 0)                                                AS TotalCheckIns
        FROM Eventos e
        LEFT JOIN Reservas r ON r.EventoId = e.Id
        LEFT JOIN Ingressos ig ON ig.ReservaId = r.Id
        LEFT JOIN CheckIns ci ON ci.IngressoId = ig.Id
        WHERE e.Id = @EventoId
        GROUP BY e.Id, e.Nome, e.DataEvento, e.CapacidadeTotal";

    var resumo = await db.QuerySingleAsync<EventoResumoResponse>(sql, new { EventoId = eventoId });

    return Results.Ok(resumo);
}).RequireAuthorization();

// 8.2. Listar check-ins de um evento (admin)
app.MapGet("/api/admin/eventos/{eventoId}/checkins", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id, Nome FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT ci.Id, ci.IngressoId, i.CodigoUnico, u.Nome AS NomeUsuario,
               u.Cpf AS UsuarioCpf, ti.Nome AS TipoIngresso, ci.DataCheckIn
        FROM CheckIns ci
        INNER JOIN Ingressos i ON i.Id = ci.IngressoId
        INNER JOIN Reservas r ON r.Id = i.ReservaId
        INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
        LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
        WHERE r.EventoId = @EventoId
        ORDER BY ci.DataCheckIn DESC";

    var checkins = (await db.QueryAsync<CheckInItemResponse>(sql, new { EventoId = eventoId })).AsList();

    var response = new CheckInListResponse
    {
        EventoId = eventoId,
        NomeEvento = evento.Nome,
        TotalCheckIns = checkins.Count,
        CheckIns = checkins
    };

    return Results.Ok(response);
}).RequireAuthorization();

// 8.3. Listar reservas de um evento (admin)
app.MapGet("/api/admin/eventos/{eventoId}/reservas", async (IDbConnection db, int eventoId) =>
{
    var evento = await db.QuerySingleOrDefaultAsync<Evento>(
        "SELECT Id FROM Eventos WHERE Id = @Id",
        new { Id = eventoId });

    if (evento is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });

    var sql = @"
        SELECT
            r.Id                     AS ReservaId,
            r.UsuarioCpf,
            u.Nome                   AS NomeUsuario,
            r.EventoId,
            e.Nome                   AS NomeEvento,
            e.DataEvento,
            i.Id                     AS IngressoId,
            i.CodigoUnico,
            i.Status                 AS StatusIngresso,
            ti.Nome                  AS TipoIngresso,
            i.ValorBruto,
            i.ValorDesconto,
            i.TaxaServico,
            i.ValorFinal,
            r.CupomUtilizado,
            CASE WHEN ci.Id IS NOT NULL THEN 1 ELSE 0 END AS CheckInRealizado
        FROM Reservas r
        INNER JOIN Usuarios u ON u.Cpf = r.UsuarioCpf
        INNER JOIN Eventos e ON e.Id = r.EventoId
        LEFT JOIN Ingressos i ON i.ReservaId = r.Id
        LEFT JOIN TiposIngresso ti ON ti.Id = i.TipoIngressoId
        LEFT JOIN CheckIns ci ON ci.IngressoId = i.Id
        WHERE r.EventoId = @EventoId
        ORDER BY r.Id DESC";

    var reservas = await db.QueryAsync<AdminReservaResponse>(sql, new { EventoId = eventoId });

    return Results.Ok(reservas);
}).RequireAuthorization();

// ==========================================================
// MÉTODOS AUXILIARES
// ==========================================================

static async Task<string> GerarCodigoUnicoAsync(IDbConnection db, IDbTransaction? transaction = null, int? commandTimeout = 30)
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var random = Random.Shared;
    string codigo;

    do
    {
        codigo = new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
    while (await db.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Ingressos WHERE CodigoUnico = @Codigo",
        new { Codigo = codigo },
        transaction: transaction, commandTimeout: commandTimeout) > 0);

    return codigo;
}

app.Run();

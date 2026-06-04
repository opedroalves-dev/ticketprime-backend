using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TicketPrime.Api.Authentication;
using TicketPrime.Api.Middleware;
using TicketPrime.Api.Models;
using TicketPrime.Api.Repositories;
using TicketPrime.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(connectionString));

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();
builder.Services.AddScoped<CupomService>();

builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<IHistoricoPrecoRepository, HistoricoPrecoRepository>();
builder.Services.AddScoped<ITipoIngressoRepository, TipoIngressoRepository>();
builder.Services.AddScoped<EventoService>();
builder.Services.AddScoped<HistoricoPrecoService>();
builder.Services.AddScoped<IIngressoRepository, IngressoRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<ICheckInRepository, CheckInRepository>();
builder.Services.AddScoped<TipoIngressoService>();
builder.Services.AddScoped<IngressoService>();
builder.Services.AddScoped<CheckInService>();
builder.Services.AddScoped<ReservaService>();
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
builder.Services.AddScoped<CarrinhoService>();

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

app.MapPost("/api/usuarios", async (UsuarioService service, [FromBody] UsuarioRequest request) =>
{
    var resultado = await service.CriarAsync(request);
    return resultado.Sucesso
        ? Results.Created($"/api/usuarios/{resultado.Cpf}", resultado.Usuario)
        : Results.BadRequest(new { erro = resultado.Erro });
});

app.MapPost("/api/eventos", async (EventoService service, [FromBody] EventoRequest request) =>
{
    var resultado = await service.CriarAsync(request);
    return resultado.Sucesso
        ? Results.Created($"/api/eventos/{resultado.Id}", resultado.Evento)
        : Results.BadRequest(new { erro = resultado.Erro });
});

app.MapPost("/api/reservas", async (ReservaService service, [FromBody] ReservaRequest request) =>
{
    var (reserva, erro) = await service.CriarReservaAsync(request);
    return erro is not null
        ? Results.BadRequest(new { erro })
        : Results.Created($"/api/reservas/{reserva!.Id}", reserva);
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

app.MapPost("/api/reservas/simular-preco", async (ReservaService service, [FromBody] SimulacaoPrecoRequest request) =>
{
    var (simulacao, erro, statusCode) = await service.SimularPrecoAsync(request);
    if (erro is not null)
        return Results.Json(new { erro }, statusCode: statusCode);
    return Results.Ok(simulacao);
});

app.MapGet("/api/eventos", async (EventoService service) =>
{
    var eventos = await service.ListarTodosAsync();
    return Results.Ok(eventos);
});

app.MapPost("/api/cupons", async (CupomService service, [FromBody] CupomRequest request) =>
{
    var resultado = await service.CriarAsync(request);
    return resultado.Sucesso
        ? Results.Created($"/api/cupons/{resultado.Codigo}", resultado.Cupom)
        : Results.BadRequest(new { erro = resultado.Erro });
});

app.MapGet("/api/reservas/{cpf}", async (ReservaService service, string cpf) =>
{
    var (reservas, erro) = await service.ObterReservasPorCpfAsync(cpf);
    if (erro is not null)
        return Results.NotFound(new { erro });
    return Results.Ok(reservas);
});

// ==========================================================
// RF01 — INGRESSO DIGITAL (3 endpoints)
// ==========================================================

// 2.1. Gerar ingresso para reserva existente
app.MapPost("/api/reservas/{id}/ingresso", async (IngressoService service, int id) =>
{
    var (response, erro, statusCode) = await service.GerarIngressoAsync(id);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Created($"/api/ingressos/{response.CodigoUnico}", response);
});

// 2.2. Consultar ingresso por código único (8 caracteres) ou por ID da reserva (numérico)
app.MapGet("/api/ingressos/{param}", async (IngressoService service, string param) =>
{
    var (response, erro, statusCode) = await service.ConsultarIngressoAsync(param);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Ok(response);
});

// 2.3. Consultar ingresso por reserva
app.MapGet("/api/reservas/{id}/ingresso", async (IngressoService service, int id) =>
{
    var (response, erro, statusCode) = await service.ObterPorReservaAsync(id);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Ok(response);
});

// ==========================================================
// RF02 — CHECK-IN (3 endpoints)
// ==========================================================

// 3.1. Realizar check-in via código na URL
app.MapPost("/api/ingressos/{codigo}/checkin", async (CheckInService service, string codigo) =>
{
    var (response, erro, statusCode) = await service.RealizarCheckInPorCodigoAsync(codigo);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Created($"/api/checkins/{response.Id}", response);
});

// 3.2. Listar check-ins de um evento
app.MapGet("/api/eventos/{eventoId}/checkins", async (CheckInService service, int eventoId) =>
{
    var (response, erro, statusCode) = await service.ListarCheckInsAsync(eventoId);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Ok(response);
});

// 3.3. Estatísticas de check-in do evento
app.MapGet("/api/eventos/{eventoId}/checkins/stats", async (CheckInService service, int eventoId) =>
{
    var (response, erro, statusCode) = await service.ObterStatsAsync(eventoId);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Ok(response);
});

// 3.4. Check-in via CodigoIngresso no corpo
app.MapPost("/api/checkin", async (CheckInService service, [FromBody] CheckInRequest request) =>
{
    var (response, erro, statusCode) = await service.RealizarCheckInPorRequestAsync(request);

    if (response is null)
        return Results.Json(new { erro }, statusCode: statusCode);

    return Results.Created($"/api/checkin/{response.Id}", response);
});

// ==========================================================
// RF03 — LOTES/TIPOS DE INGRESSO (7 endpoints)
// ==========================================================

// 1. POST /api/eventos/{eventoId}/lotes — Criar lote
app.MapPost("/api/eventos/{eventoId}/lotes", async (TipoIngressoService service, int eventoId, [FromBody] CriarLoteRequest request) =>
{
    var resultado = await service.CriarLoteAsync(eventoId, request);

    if (!resultado.Sucesso)
    {
        if (resultado.StatusCode == 404)
            return Results.NotFound(new { erro = resultado.Erro });
        return Results.BadRequest(new { erro = resultado.Erro });
    }

    return Results.Created($"/api/lotes/{resultado.Id}", resultado.Lote);
});

// 2. GET /api/eventos/{eventoId}/lotes — Listar lotes
app.MapGet("/api/eventos/{eventoId}/lotes", async (TipoIngressoService service, int eventoId) =>
{
    var resultado = await service.ListarLotesAsync(eventoId);

    if (!resultado.Sucesso)
        return Results.NotFound(new { erro = resultado.Erro });

    return Results.Ok(resultado.Lotes);
});

// 3. GET /api/lotes/{loteId} — Obter lote específico
app.MapGet("/api/lotes/{loteId}", async (TipoIngressoService service, int loteId) =>
{
    var resultado = await service.ObterLoteAsync(loteId);

    if (!resultado.Sucesso)
        return Results.NotFound(new { erro = resultado.Erro });

    return Results.Ok(resultado.Lote);
});

// 4. PUT /api/lotes/{loteId} — Atualizar lote
app.MapPut("/api/lotes/{loteId}", async (TipoIngressoService service, int loteId, [FromBody] CriarLoteRequest request) =>
{
    var resultado = await service.AtualizarLoteAsync(loteId, request);

    if (!resultado.Sucesso)
    {
        if (resultado.StatusCode == 404)
            return Results.NotFound(new { erro = resultado.Erro });
        return Results.BadRequest(new { erro = resultado.Erro });
    }

    return Results.Ok(resultado.Lote);
});

// 5. DELETE /api/lotes/{loteId} — Remover lote
app.MapDelete("/api/lotes/{loteId}", async (TipoIngressoService service, int loteId) =>
{
    var resultado = await service.RemoverLoteAsync(loteId);

    if (!resultado.Sucesso)
    {
        if (resultado.StatusCode == 404)
            return Results.NotFound(new { erro = resultado.Erro });
        if (resultado.StatusCode == 409)
            return Results.Conflict(new { erro = resultado.Erro });
        return Results.BadRequest(new { erro = resultado.Erro });
    }

    return Results.NoContent();
});

// 6. POST /api/tipos-ingresso — Criar tipo de ingresso
app.MapPost("/api/tipos-ingresso", async (TipoIngressoService service, [FromBody] CriarTipoIngressoRequest request) =>
{
    var resultado = await service.CriarTipoIngressoAsync(request);

    if (!resultado.Sucesso)
    {
        if (resultado.StatusCode == 404)
            return Results.NotFound(new { erro = resultado.Erro });
        return Results.BadRequest(new { erro = resultado.Erro });
    }

    return Results.Created($"/api/tipos-ingresso/{resultado.Id}", resultado.TipoIngresso);
});

// 7. GET /api/eventos/{eventoId}/tipos-ingresso — Listar tipos de ingresso
app.MapGet("/api/eventos/{eventoId}/tipos-ingresso", async (TipoIngressoService service, int eventoId) =>
{
    var resultado = await service.ListarTiposIngressoAsync(eventoId);

    if (!resultado.Sucesso)
        return Results.NotFound(new { erro = resultado.Erro });

    return Results.Ok(resultado.Tipos);
});

// ==========================================================
// RF04 — CARRINHO (4 endpoints CRUD — Etapa 11a)
// ==========================================================

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

// 5.4. Confirmar carrinho
app.MapPost("/api/carrinho/{cpf}/confirmar", async (IDbConnection db, IIngressoRepository ingressoRepository, string cpf, [FromBody] ConfirmarCarrinhoRequest? request) =>
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
                var codigoUnico = await ingressoRepository.GerarCodigoUnicoAsync(transaction, 30);

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
app.MapGet("/api/eventos/{eventoId}/historico-precos",
    async (HistoricoPrecoService service, int eventoId) =>
{
    var resultado = await service.ObterPorEventoIdAsync(eventoId);
    return resultado is null
        ? Results.NotFound(new { erro = "Evento não encontrado." })
        : Results.Ok(resultado);
});

// 6.2. Histórico de preços do lote
app.MapGet("/api/lotes/{loteId}/historico-precos",
    async (HistoricoPrecoService service, int loteId) =>
{
    var resultado = await service.ObterPorLoteIdAsync(loteId);
    return resultado is null
        ? Results.NotFound(new { erro = "Lote não encontrado." })
        : Results.Ok(resultado);
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

app.Run();

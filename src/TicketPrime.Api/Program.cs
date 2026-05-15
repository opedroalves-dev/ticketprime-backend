using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
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

var app = builder.Build();

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

    Console.WriteLine("Tabelas verificadas/criadas com sucesso.");
}

app.MapGet("/", () => "TicketPrime API");

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
    const string sql = @"
        SELECT r.Id, r.UsuarioCpf, r.EventoId, e.Nome AS NomeEvento, r.CupomUtilizado, r.ValorFinalPago
        FROM Reservas r
        INNER JOIN Eventos e ON r.EventoId = e.Id
        WHERE r.UsuarioCpf = @Cpf";

    var reservas = await db.QueryAsync<ReservaResponse>(sql, new { Cpf = cpf });

    var lista = reservas.AsList();

    if (lista.Count == 0)
        return Results.NotFound(new { mensagem = "Nenhuma reserva encontrada para o CPF informado." });

    return Results.Ok(lista);
});

app.Run();

public class UsuarioRequest
{
    public string Cpf { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class EventoRequest
{
    public string Nome { get; set; } = string.Empty;
    public int CapacidadeTotal { get; set; }
    public DateTime DataEvento { get; set; }
    public decimal PrecoPadrao { get; set; }
}

public class CupomRequest
{
    public string Codigo { get; set; } = string.Empty;
    public decimal PorcentagemDesconto { get; set; }
    public decimal ValorMinimoRegra { get; set; }
}

public class ReservaRequest
{
    public string UsuarioCpf { get; set; } = string.Empty;
    public int EventoId { get; set; }
    public string? CupomUtilizado { get; set; }
}

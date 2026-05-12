using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using TicketPrime.Api.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(connectionString));

var app = builder.Build();

app.MapGet("/", () => "TicketPrime API");

app.MapPost("/api/usuarios", async (IDbConnection db, UsuarioRequest request) =>
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

app.MapPost("/api/eventos", async (IDbConnection db, EventoRequest request) =>
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

app.MapGet("/api/eventos", async (IDbConnection db) =>
{
    const string sql = "SELECT Id, Nome, CapacidadeTotal, DataEvento, PrecoPadrao FROM Eventos";

    var eventos = await db.QueryAsync<Evento>(sql);

    return Results.Ok(eventos);
});

app.MapPost("/api/cupons", async (IDbConnection db, CupomRequest request) =>
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

app.Run();

public record UsuarioRequest(string Cpf, string Nome, string Email);
public record EventoRequest(string Nome, int CapacidadeTotal, DateTime DataEvento, decimal PrecoPadrao);
public record CupomRequest(string Codigo, decimal PorcentagemDesconto, decimal ValorMinimoRegra);

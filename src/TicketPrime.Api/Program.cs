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

    if (string.IsNullOrWhiteSpace(request.Nome))
        return Results.BadRequest(new { erro = "Nome é obrigatório." });

    if (string.IsNullOrWhiteSpace(request.Email))
        return Results.BadRequest(new { erro = "Email é obrigatório." });

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

app.Run();

public record UsuarioRequest(string Cpf, string Nome, string Email);

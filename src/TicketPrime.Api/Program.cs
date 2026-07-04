using Microsoft.AspNetCore.Mvc;
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
    new Microsoft.Data.SqlClient.SqlConnection(connectionString));

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
builder.Services.AddScoped<DashboardService>();

builder.Services.AddControllers();


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

// serve index.html automaticamente
app.UseDefaultFiles();

// libera arquivos estáticos (CSS, JS, HTML)
app.UseStaticFiles();

app.UseHttpsRedirection();

// Middleware global de exception handling (Item 3)
app.UseMiddleware<TicketPrime.Api.Middleware.ExceptionHandlingMiddleware>();

// Middleware de CORS (Item 6)
app.UseCors("AllowFrontend");

// Middleware de autenticação e autorização (Item 2)
app.UseAuthentication();
app.UseAuthorization();

// Inicialização do banco de dados
Console.WriteLine("ALERTA: Execute db/ticketprime.sql e db/ticketprime_incrementos.sql antes de usar a API.");

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

app.MapPost("/api/login", async (IUsuarioRepository repo, UsuarioRequest request) =>
{
    var usuario = await repo.ObterPorCpfAsync(request.Cpf);

    if (usuario is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        token = Guid.NewGuid().ToString(),
        usuario
    });
})
.AllowAnonymous();

app.MapPost("/api/usuarios", async (UsuarioService service, [FromBody] UsuarioRequest request) =>
{
    var resultado = await service.CriarAsync(request);
    return resultado.Sucesso
        ? Results.Created($"/api/usuarios/{resultado.Cpf}", resultado.Usuario)
        : Results.BadRequest(new { erro = resultado.Erro });
})
.AllowAnonymous();

app.MapPost("/api/eventos", async (EventoService service, [FromBody] EventoRequest request) =>
{
    var resultado = await service.CriarAsync(request);
    return resultado.Sucesso
        ? Results.Created($"/api/eventos/{resultado.Id}", resultado.Evento)
        : Results.BadRequest(new { erro = resultado.Erro });
})
.RequireAuthorization();

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

// 5.5. Confirmar carrinho (migrado para CarrinhoService.ConfirmarAsync — Etapa 11b)
app.MapPost("/api/carrinho/{cpf}/confirmar", async (CarrinhoService service, string cpf, [FromBody] ConfirmarCarrinhoRequest? request) =>
{
    var (response, erro, statusCode) = await service.ConfirmarAsync(cpf, request);
    if (erro is not null)
        return Results.Json(new { erro }, statusCode: statusCode);
    return Results.Created($"/api/carrinho/{cpf}/confirmar", response);
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
// RF06 — DASHBOARD/ADMIN (7 endpoints delegados ao DashboardService)
// ==========================================================

// 7.1. Listar eventos com métricas
app.MapGet("/api/admin/eventos", async (DashboardService dashboard) =>
{
    var eventos = await dashboard.ListarEventosAsync();
    return Results.Ok(eventos);
}).RequireAuthorization();

// 7.2. Dashboard detalhado de um evento
app.MapGet("/api/admin/eventos/{eventoId}", async (DashboardService dashboard, int eventoId) =>
{
    var eventoDashboard = await dashboard.ObterDashboardEventoAsync(eventoId);
    if (eventoDashboard is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });
    return Results.Ok(eventoDashboard);
}).RequireAuthorization();

// 7.3. Métricas por lote do evento
app.MapGet("/api/admin/eventos/{eventoId}/lotes", async (DashboardService dashboard, int eventoId) =>
{
    var lotes = await dashboard.ListarLotesEventoAsync(eventoId);
    if (lotes is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });
    return Results.Ok(lotes);
}).RequireAuthorization();

// 7.4. Listar todas as reservas (admin)
app.MapGet("/api/admin/reservas", async (DashboardService dashboard,
    [FromQuery] int? eventoId,
    [FromQuery] string? status,
    [FromQuery] string? cpf) =>
{
    var reservas = await dashboard.ListarReservasAdminAsync(eventoId, status, cpf);
    return Results.Ok(reservas);
}).RequireAuthorization();

// 8.1. Resumo do evento
app.MapGet("/api/admin/eventos/{eventoId}/resumo", async (DashboardService dashboard, int eventoId) =>
{
    var resumo = await dashboard.ObterResumoEventoAsync(eventoId);
    if (resumo is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });
    return Results.Ok(resumo);
}).RequireAuthorization();

// 8.2. Listar check-ins de um evento (admin)
app.MapGet("/api/admin/eventos/{eventoId}/checkins", async (DashboardService dashboard, int eventoId) =>
{
    var response = await dashboard.ListarCheckInsAdminAsync(eventoId);
    if (response is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });
    return Results.Ok(response);
}).RequireAuthorization();

// 8.3. Listar reservas de um evento (admin)
app.MapGet("/api/admin/eventos/{eventoId}/reservas", async (DashboardService dashboard, int eventoId) =>
{
    var reservas = await dashboard.ListarReservasEventoAsync(eventoId);
    if (reservas is null)
        return Results.NotFound(new { erro = "Evento não encontrado." });
    return Results.Ok(reservas);
}).RequireAuthorization();

app.Run();

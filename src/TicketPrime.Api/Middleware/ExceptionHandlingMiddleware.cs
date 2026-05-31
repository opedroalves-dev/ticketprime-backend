using System.Net;
using System.Text.Json;

namespace TicketPrime.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex)
        {
            // A2/F3: BadHttpRequestException → 400, não 500
            _logger.LogWarning(ex, "Request malformado: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { erro = ex.Message });
        }
        catch (ValidationException ex)
        {
            // C1/F2: Exceção customizada de validação → 400
            _logger.LogWarning(ex, "Erro de validação: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { erro = ex.Message });
        }
        catch (Exception ex)
        {
            // TD-001: ex.Message pode conter informações sensíveis em SqlException
            // (registrada como dívida técnica — revisar sanitização em fase futura)
            _logger.LogError(ex, "Exceção não tratada: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            object error;

            if (_env.IsDevelopment())
            {
                error = new { erro = ex.Message, detalhes = ex.StackTrace };
            }
            else
            {
                error = new { erro = "Ocorreu um erro interno no servidor." };
            }

            await context.Response.WriteAsJsonAsync(error);
        }
    }
}

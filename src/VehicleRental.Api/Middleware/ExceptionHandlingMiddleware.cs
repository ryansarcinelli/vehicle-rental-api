using System.Net.Mime;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using VehicleRental.Application.Common;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Api.Middleware;

/// <summary>
/// Traduz exceções em respostas ProblemDetails (RFC 7807). Concentrar isso aqui é o que
/// permite aos services simplesmente lançarem exceções de domínio sem conhecer HTTP.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Exceção após a resposta ter começado; não é possível reescrever.");
            throw exception;
        }

        var problem = BuildProblem(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erro não tratado em {Method} {Path}.", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Requisição rejeitada ({Status}): {Message}", problem.Status, exception.Message);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = MediaTypeNames.Application.ProblemJson;

        // Serializa pelo tipo em tempo de execução: com o tipo estático ProblemDetails,
        // System.Text.Json descartaria o campo `errors` do ValidationProblemDetails.
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, problem.GetType(), JsonOptions));
    }

    private ProblemDetails BuildProblem(Exception exception) => exception switch
    {
        ValidationException validation => new ValidationProblemDetails(
            validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dados inválidos."
        },

        NotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Recurso não encontrado.",
            Detail = exception.Message
        },

        ForbiddenException => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acesso negado.",
            Detail = exception.Message
        },

        InvalidCredentialsException => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autenticado.",
            Detail = exception.Message
        },

        DomainException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Regra de negócio violada.",
            Detail = exception.Message
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno.",
            // Fora de Development a mensagem original não vaza para o cliente.
            Detail = environment.IsDevelopment() ? exception.ToString() : "Ocorreu um erro inesperado."
        }
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

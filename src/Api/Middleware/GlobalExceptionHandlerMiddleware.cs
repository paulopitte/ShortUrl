using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShortUrl.Domain.Exceptions;

namespace ShortUrl.Api.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções que traduz exceções de validação e de domínio
/// em respostas HTTP padronizadas no formato ProblemDetails (RFC 9457 / RFC 7807) em português.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(exception, "A resposta HTTP já foi iniciada; não é possível escrever ProblemDetails.");
            throw exception;
        }

        ProblemDetails problemDetails;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = StatusCodes.Status400BadRequest;
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

                problemDetails = new HttpValidationProblemDetails(errors)
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Title = "Erro de validação na requisição.",
                    Status = statusCode,
                    Detail = "Um ou mais erros de validação ocorreram."
                };

                logger.LogInformation(
                    "Falha de validação na requisição {Method} {Path}: {@Errors}",
                    context.Request.Method,
                    context.Request.Path,
                    errors);
                break;

            case ShortLinkNotFoundException notFoundException:
                statusCode = StatusCodes.Status404NotFound;
                problemDetails = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    Title = "Link curto não encontrado.",
                    Status = statusCode,
                    Detail = notFoundException.Message
                };

                logger.LogInformation(
                    "Link curto não encontrado ({Code}) na requisição {Method} {Path}.",
                    notFoundException.Code,
                    context.Request.Method,
                    context.Request.Path);
                break;

            case ShortLinkExpiredException expiredException:
                statusCode = StatusCodes.Status410Gone;
                problemDetails = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.11",
                    Title = "Link curto expirado.",
                    Status = statusCode,
                    Detail = expiredException.Message
                };

                logger.LogInformation(
                    "Tentativa de acesso a link curto expirado ({Code}) na requisição {Method} {Path}.",
                    expiredException.Code,
                    context.Request.Method,
                    context.Request.Path);
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                problemDetails = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "Erro interno no servidor.",
                    Status = statusCode,
                    Detail = "Ocorreu um erro inesperado ao processar a requisição."
                };

                logger.LogError(
                    exception,
                    "Exceção não tratada ao processar a requisição {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path);
                break;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        var json = JsonSerializer.Serialize<object>(problemDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}


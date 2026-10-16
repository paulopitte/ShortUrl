using MediatR;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

namespace ShortUrl.Api.Endpoints;

/// <summary>
/// Endpoint público de redirecionamento imediato na raiz <c>GET /{code}</c>.
/// </summary>
public static class RedirectEndpoints
{
    /// <summary>
    /// Mapeia o endpoint de redirecionamento público na raiz da aplicação web.
    /// </summary>
    /// <param name="app">Aplicação web ASP.NET Core configurada.</param>
    /// <returns>A mesma instância de <see cref="WebApplication"/> para encadeamento fluente.</returns>
    public static WebApplication MapRedirectEndpoints(this WebApplication app)
    {
        app.MapGet("/{code}", RedirectByCodeAsync)
            .WithName("RedirectShortLink")
            .WithTags("redirect")
            .WithSummary("Redireciona imediatamente o visitante para a URL original associada ao código curto.")
            .WithDescription("Valida o código de 7 caracteres Base62, verifica a vigência do link, incrementa atomicamente o contador de acessos e redireciona com HTTP 302 Found sem páginas intermediárias.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        return app;
    }

    /// <summary>
    /// Processa a requisição de redirecionamento temporário imediato via código curto.
    /// </summary>
    /// <param name="code">Código alfanumérico Base62 de 7 caracteres correspondente ao link curto.</param>
    /// <param name="sender">Instância do MediatR para despacho do comando de resolução.</param>
    /// <param name="httpContext">Contexto HTTP para configuração de cabeçalhos de controle de cache.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Redirecionamento HTTP 302 Found para a URL original de destino.</returns>
    /// <response code="302">Redirecionamento temporário bem-sucedido para a URL de destino.</response>
    /// <response code="404">Código informado não foi localizado.</response>
    /// <response code="410">Link curto expirado e não mais disponível para redirecionamento.</response>
    private static async Task<IResult> RedirectByCodeAsync(
        string code,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var targetUrl = await sender.Send(new ResolveShortLinkCommand(code), cancellationToken);

        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

        return Results.Redirect(targetUrl, permanent: false);
    }
}


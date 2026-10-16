using MediatR;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

namespace ShortUrl.Api.Endpoints;

/// <summary>
/// Endpoint público de redirecionamento imediato na raiz <c>GET /{code}</c>.
/// </summary>
public static class RedirectEndpoints
{
    public static WebApplication MapRedirectEndpoints(this WebApplication app)
    {
        app.MapGet("/{code}", RedirectByCodeAsync)
            .WithName("RedirectShortLink")
            .WithTags("redirect")
            .WithSummary("Redireciona imediatamente o visitante para a URL original associada ao código curto.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status410Gone);

        return app;
    }

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


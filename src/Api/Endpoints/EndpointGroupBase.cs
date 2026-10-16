using System.Reflection;

namespace ShortUrl.Api.Endpoints;

/// <summary>
/// Classe base abstrata para agrupamento modular de endpoints Minimal API sob o prefixo /shorturl-api/{GroupName}.
/// </summary>
public abstract class EndpointGroupBase
{
    /// <summary>
    /// Nome opcional do grupo de rotas. Caso omitido, utiliza o nome da classe sem o sufixo "Endpoints" em letras minúsculas.
    /// </summary>
    public virtual string? GroupName => null;

    /// <summary>
    /// Configura os endpoints pertencentes ao grupo sob <c>/shorturl-api/{GroupName}</c>.
    /// </summary>
    /// <param name="group">Construtor do grupo de rotas.</param>
    public abstract void Map(RouteGroupBuilder group);
}

/// <summary>
/// Métodos de extensão para descoberta e registro automático de grupos de endpoints baseados em <see cref="EndpointGroupBase"/>.
/// </summary>
public static class EndpointGroupExtensions
{
    public const string ApiBasePrefix = "/shorturl-api";

    public static WebApplication MapEndpointGroups(this WebApplication app)
    {
        var endpointGroupType = typeof(EndpointGroupBase);
        var assembly = Assembly.GetExecutingAssembly();

        var endpointGroupTypes = assembly.GetExportedTypes()
            .Where(t => t.IsSubclassOf(endpointGroupType) && !t.IsAbstract);

        foreach (var type in endpointGroupTypes)
        {
            if (Activator.CreateInstance(type) is EndpointGroupBase instance)
            {
                var groupName = instance.GroupName ?? GetDefaultGroupName(type.Name);
                var routeGroup = app.MapGroup($"{ApiBasePrefix}/{groupName}")
                    .WithTags(groupName);

                instance.Map(routeGroup);
            }
        }

        return app;
    }

    private static string GetDefaultGroupName(string typeName)
    {
        const string suffix = "Endpoints";
        var name = typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? typeName[..^suffix.Length]
            : typeName;

        return name.ToLowerInvariant();
    }
}


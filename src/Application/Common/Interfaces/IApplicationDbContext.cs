using Microsoft.EntityFrameworkCore;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Application.Common.Interfaces;

/// <summary>
/// Abstração do contexto de banco de dados da aplicação para persistência de links curtos.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Conjunto de entidades <see cref="ShortLink"/> persistidas no banco de dados.
    /// </summary>
    DbSet<ShortLink> ShortLinks { get; }

    /// <summary>
    /// Persiste de forma assíncrona todas as alterações pendentes no contexto.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Total de registros afetados no banco de dados.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


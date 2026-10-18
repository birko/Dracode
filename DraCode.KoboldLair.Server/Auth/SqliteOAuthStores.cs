using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.SqLite.Stores;
using Birko.Security.OAuth.Server.InMemory.Extensions;
using Birko.Security.OAuth.Server.SqLite.Extensions;
using Birko.Security.OAuth.Server.SqLite.Models;
using Birko.Security.OAuth.Server.SqLite.Stores;
using Birko.Security.OAuth.Server.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// DI registration for the OAuth server stores (TASK-031, adopted from Birko at TASK-109). The stores themselves now ship
/// with Birko (Birko TASK-518): <c>Birko.Security.OAuth.Server.SqLite</c> and <c>.InMemory</c>. They keep this server's
/// table and column names, persist a refresh token's extra claims (Birko TASK-517 added <c>RefreshTokenRecord.Claims</c>,
/// which a plain SQL store refuses to map), and claim codes and refresh tokens atomically.
/// </summary>
public static class OAuthStoreServiceCollectionExtensions
{
    public static IServiceCollection AddOAuthServerStores(
        this IServiceCollection services, bool useSqlite, string? dbPath)
    {
        if (!useSqlite || string.IsNullOrEmpty(dbPath))
            return services.AddOAuthServerInMemoryStores();

        var full = Path.GetFullPath(dbPath);
        var directory = Path.GetDirectoryName(full) ?? ".";
        Directory.CreateDirectory(directory);
        var settings = new SqLiteSettings(directory, Path.GetFileName(full));
        services.AddOAuthServerSqLiteStores(settings);

        // A database created before the adoption has oauth_refresh_tokens without ClaimsJson; table creation never alters
        // an existing table, so add the column once at startup, as SqlUsageRepository does for its own drift.
        services.Replace(ServiceDescriptor.Singleton<IRefreshTokenStore>(_ =>
        {
            var store = new SqLiteRefreshTokenStore(settings);
            store.InitAsync().GetAwaiter().GetResult();
            Birko.Data.SQL.DataBase.GetConnector<SqLiteConnector>(settings).EnsureColumns(typeof(RefreshTokenEntity));
            return store;
        }));
        return services;
    }
}

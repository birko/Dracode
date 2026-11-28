using Birko.Data.Stores;
using Birko.Security.OAuth.Server.Extensions;
using Birko.Security.OAuth.Server.Models.Persistence;
using Birko.Security.OAuth.Server.Stores;
using Birko.Security.OAuth.Server.Stores.Factory;
using DraCode.KoboldLair.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// DI registration for the OAuth server stores (TASK-031, adopted from Birko at TASK-109): since TASK-230 Birko's stores over
/// the configured database's store factory (Birko TASK-1013), on any backend that claims codes and refresh tokens atomically.
/// They keep the table and column names the SQLite stores used, so an existing database is read and written as it is.
/// </summary>
public static class OAuthStoreServiceCollectionExtensions
{
    /// <summary>The five OAuth stores and <see cref="OAuthClientNames"/> on <paramref name="factory"/>'s database.</summary>
    public static IServiceCollection AddKoboldLairOAuthStores(this IServiceCollection services, IStoreFactory factory)
    {
        services.AddOAuthServerStores(factory);

        // A SQLite database created before TASK-109 has oauth_refresh_tokens without ClaimsJson; creating a table never
        // alters an existing one, so add the column once, when the store is first used. Only SQL has fixed columns
        if (factory.SqlConnector() is { } connector)
            services.Replace(ServiceDescriptor.Singleton<IRefreshTokenStore>(_ =>
            {
                factory.CreateSchemaForAsync<RefreshTokenEntity>().GetAwaiter().GetResult();
                connector.EnsureColumns(typeof(RefreshTokenEntity));
                return new FactoryRefreshTokenStore(factory);
            }));

        services.Replace(ServiceDescriptor.Singleton(new OAuthClientNames(factory)));
        return services;
    }
}

using Birko.Data.SQL.Repositories;
using Birko.Security.OAuth.Server.SqLite.Models;
using Birko.Security.OAuth.Server.SqLite.Stores;
using Birko.Security.OAuth.Server.Stores;
using DraCode.KoboldLair.Data;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Whether any registered OAuth client already has a name (TASK-194). A service-account key acts as
/// <c>service:&lt;name&gt;</c> (FEATURE-019 D14), so a new key must not take the name of any client — including ones
/// registered through <c>/register</c> before key owners were recorded (TASK-162), which <see cref="ServiceKeyRegistry"/>
/// does not know. The SQLite client store refuses predicate queries, so its <c>oauth_clients</c> table is read here
/// through the store's own public entity, from the database file the store uses; the in-memory store answers the query
/// itself.
/// </summary>
public sealed class OAuthClientNames
{
    private readonly IOAuthClientStore _store;
    private readonly AsyncSqLiteModelRepository<OAuthClientEntity>? _table;

    /// <param name="sqlitePath">The database the OAuth stores were built with, when they are SQLite-backed.</param>
    public OAuthClientNames(IOAuthClientStore store, string? sqlitePath)
    {
        _store = store;
        if (store is SqLiteOAuthClientStore && !string.IsNullOrEmpty(sqlitePath))
        {
            _table = new AsyncSqLiteModelRepository<OAuthClientEntity>();
            _table.SetSettings(SqliteDatabaseSettings.For(sqlitePath));
        }
    }

    /// <summary>True when a client with this name (any case) exists. Throws when it cannot tell, so a caller fails closed.</summary>
    public async Task<bool> ExistsAsync(string name)
    {
        if (_table != null)
        {
            var rows = await _table.ReadAsync(filter: null, orderBy: null, limit: null, offset: null, ct: CancellationToken.None);
            return rows.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        return await _store.ReadAsync(c => c.Name != null && c.Name.ToLower() == name.ToLower()) != null;
    }
}

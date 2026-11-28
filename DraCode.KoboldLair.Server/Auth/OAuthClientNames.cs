using Birko.Data.Stores;
using Birko.Security.OAuth.Server.Models.Persistence;
using DraCode.KoboldLair.Data;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Whether any registered OAuth client already has a name, in any case (TASK-194). A service-account key acts as
/// <c>service:&lt;name&gt;</c> (FEATURE-019 D14), so a new key must not take the name of any client — including ones
/// registered through <c>/register</c> before key owners were recorded (TASK-162), which <see cref="ServiceKeyRegistry"/>
/// does not know. The client store's own name lookup is exact, so the clients are read here, from the same database the
/// OAuth stores use (TASK-230: any backend, through the store factory).
/// </summary>
public sealed class OAuthClientNames
{
    private readonly StoreRepository<OAuthClientEntity> _clients;
    private readonly SemaphoreSlim _schema = new(1, 1);
    private bool _schemaReady;

    public OAuthClientNames(IStoreFactory factory) => _clients = new StoreRepository<OAuthClientEntity>(factory);

    /// <summary>True when a client with this name (any case) exists. Throws when it cannot tell, so a caller fails closed.</summary>
    public async Task<bool> ExistsAsync(string name)
    {
        if (!_schemaReady)
        {
            await _schema.WaitAsync();
            try
            {
                if (!_schemaReady)
                {
                    await _clients.CreateSchemaAsync();
                    _schemaReady = true;
                }
            }
            finally { _schema.Release(); }
        }
        var rows = await _clients.ReadAsync(filter: null, orderBy: null, limit: null, offset: null, ct: CancellationToken.None);
        return rows.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

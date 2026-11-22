using System.Collections.Concurrent;
using Birko.Data.SQL.Repositories;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Entities;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>A service-account key's owner record (TASK-162).</summary>
public sealed record ServiceKey(string ClientId, string OwnerId, string Name, DateTime CreatedAt);

/// <summary>
/// The owners of service-account keys (TASK-162), kept beside the OAuth client store, which cannot be queried. On the
/// SQLite backend it is the <c>service_keys</c> table in the server's database, as the OAuth stores are; otherwise in
/// memory, as they are too.
/// </summary>
public sealed class ServiceKeyRegistry
{
    private readonly AsyncSqLiteModelRepository<ServiceKeyEntity>? _repository;
    private readonly ConcurrentDictionary<string, ServiceKey> _memory = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _schema = new(1, 1);
    private bool _schemaReady;

    public ServiceKeyRegistry(string? sqlitePath)
    {
        if (string.IsNullOrEmpty(sqlitePath)) return;
        _repository = new AsyncSqLiteModelRepository<ServiceKeyEntity>();
        _repository.SetSettings(SqliteDatabaseSettings.For(sqlitePath));
    }

    private async Task EnsureSchemaAsync()
    {
        if (_repository == null || _schemaReady) return;
        await _schema.WaitAsync();
        try
        {
            if (!_schemaReady)
            {
                await _repository.CreateSchemaAsync();
                _schemaReady = true;
            }
        }
        finally { _schema.Release(); }
    }

    public async Task AddAsync(ServiceKey key)
    {
        if (_repository == null)
        {
            _memory[key.ClientId] = key;
            return;
        }
        await EnsureSchemaAsync();
        await _repository.CreateAsync(new ServiceKeyEntity
        {
            ClientId = key.ClientId, OwnerId = key.OwnerId, Name = key.Name, CreatedAt = key.CreatedAt, UpdatedAt = key.CreatedAt
        });
    }

    /// <summary>The keys of <paramref name="ownerId"/>, or every key when it is null; newest first.</summary>
    public async Task<List<ServiceKey>> ListAsync(string? ownerId)
    {
        IEnumerable<ServiceKey> keys;
        if (_repository == null)
        {
            keys = _memory.Values.Where(k => ownerId == null || k.OwnerId == ownerId);
        }
        else
        {
            await EnsureSchemaAsync();
            var rows = ownerId == null
                ? await _repository.ReadAsync(filter: null, orderBy: null, limit: null, offset: null, ct: CancellationToken.None)
                : await _repository.ReadAsync(filter: e => e.OwnerId == ownerId, orderBy: null, limit: null, offset: null, ct: CancellationToken.None);
            keys = rows.Select(Map);
        }
        return keys.OrderByDescending(k => k.CreatedAt).ToList();
    }

    public async Task<ServiceKey?> GetAsync(string clientId)
    {
        if (_repository == null)
            return _memory.TryGetValue(clientId, out var k) ? k : null;
        await EnsureSchemaAsync();
        var row = await _repository.ReadAsync(e => e.ClientId == clientId, CancellationToken.None);
        return row == null ? null : Map(row);
    }

    /// <summary>
    /// Whether a key already has this name. A key acts as <c>service:&lt;name&gt;</c> (FEATURE-019 D14), so two keys of
    /// one name would share an identity and its projects.
    /// </summary>
    public async Task<bool> NameTakenAsync(string name) =>
        (await ListAsync(null)).Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

    private static ServiceKey Map(ServiceKeyEntity e) => new(e.ClientId, e.OwnerId, e.Name, e.CreatedAt);
}

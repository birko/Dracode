using System.Linq.Expressions;
using System.Text.Json;
using Birko.Configuration;
using Birko.Data.Models;
using Birko.Data.SQL;
using Birko.Data.SQL.Attributes;
using Birko.Data.SQL.SqLite.Stores;
using Birko.Data.Stores;
using Birko.Security.OAuth.Server;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace DraCode.KoboldLair.Server.Auth;

// ---------------------------------------------------------------------------------------------
// SQLite-backed OAuth authorization-server stores (TASK-031).
//
// Four of the five OAuth models (AuthorizationCode, RefreshTokenRecord, DeviceCodeRecord,
// ConsentRecord) are all-scalar AbstractModel POCOs, so they persist directly via
// AsyncSQLiteStore<T>: the Birko SQL field-mapper auto-maps public scalar properties by CLR
// type (no [Field] attributes needed). The POCOs carry no [Table] attribute, so we supply the
// table name through the documented fluent hook DataBase.RegisterTableName(...).
//
// OAuthClient is the exception: its RedirectUris / AllowedGrantTypes / AllowedScopes are
// List<string>, which the column mapper cannot represent (it would silently drop them). That
// one model gets an entity (OAuthClientEntity) with a JSON column for the collections, wrapped
// by SqlOAuthClientStore which maps model <-> entity.
// ---------------------------------------------------------------------------------------------

/// <summary>SQLite store for one-shot authorization codes — scalar model, persists directly.</summary>
public sealed class SqlAuthorizationCodeStore : AsyncSQLiteStore<AuthorizationCode>, IAuthorizationCodeStore { }

/// <summary>SQLite store for refresh-token records — scalar model, persists directly.</summary>
public sealed class SqlRefreshTokenStore : AsyncSQLiteStore<RefreshTokenRecord>, IRefreshTokenStore { }

/// <summary>SQLite store for in-flight device-authorization requests — scalar model, persists directly.</summary>
public sealed class SqlDeviceCodeStore : AsyncSQLiteStore<DeviceCodeRecord>, IDeviceCodeStore { }

/// <summary>SQLite store for prior-consent records — scalar model, persists directly.</summary>
public sealed class SqlConsentStore : AsyncSQLiteStore<ConsentRecord>, IConsentStore { }

/// <summary>
/// Database entity for <see cref="OAuthClient"/>. Scalar fields map to columns; the three
/// <c>List&lt;string&gt;</c> properties are stored together in <see cref="CollectionsJson"/>.
/// </summary>
[Table("oauth_clients")]
public sealed class OAuthClientEntity : AbstractDatabaseLogModel
{
    [RequiredField]
    [MaxLengthField(200)]
    [IndexedField("ix_oauth_clients_client_id")]
    public string ClientId { get; set; } = "";

    [MaxLengthField(128)]
    public string? ClientSecretHash { get; set; }

    /// <summary>Mirror of <see cref="OAuthClientType"/> (0=Confidential, 1=Public).</summary>
    public int ClientType { get; set; }

    [MaxLengthField(200)]
    public string Name { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    public DateTime ClientCreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>JSON of { redirectUris, allowedGrantTypes, allowedScopes }.</summary>
    public string CollectionsJson { get; set; } = "{}";

    public override AbstractModel CopyTo(AbstractModel? clone = null)
    {
        var target = clone as OAuthClientEntity ?? new OAuthClientEntity();
        base.CopyTo(target);
        target.ClientId = ClientId;
        target.ClientSecretHash = ClientSecretHash;
        target.ClientType = ClientType;
        target.Name = Name;
        target.IsEnabled = IsEnabled;
        target.ClientCreatedAt = ClientCreatedAt;
        target.CollectionsJson = CollectionsJson;
        return target;
    }
}

/// <summary>
/// <see cref="IOAuthClientStore"/> backed by SQLite via an <see cref="OAuthClientEntity"/> mapping
/// layer (required because <see cref="OAuthClient"/> has collection properties). Rows are keyed by
/// the indexed <see cref="OAuthClient.ClientId"/>, which the server treats as the stable identifier.
/// </summary>
public sealed class SqlOAuthClientStore : IOAuthClientStore
{
    private sealed record Collections(
        List<string> RedirectUris,
        List<string> AllowedGrantTypes,
        List<string> AllowedScopes);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AsyncSQLiteStore<OAuthClientEntity> _entities = new();

    public SqlOAuthClientStore(PasswordSettings settings)
    {
        _entities.SetSettings(settings);
    }

    /// <summary>Creates the backing table. Call once at startup.</summary>
    public Task InitializeAsync(CancellationToken ct = default) => _entities.CreateSchemaAsync(ct);

    // ---- mapping -----------------------------------------------------------------------------

    private static OAuthClientEntity ToEntity(OAuthClient c, OAuthClientEntity? existing = null)
    {
        var e = existing ?? new OAuthClientEntity { Guid = c.Guid ?? Guid.NewGuid() };
        e.ClientId = c.ClientId;
        e.ClientSecretHash = c.ClientSecretHash;
        e.ClientType = (int)c.ClientType;
        e.Name = c.Name;
        e.IsEnabled = c.IsEnabled;
        e.ClientCreatedAt = c.CreatedAt;
        e.CollectionsJson = JsonSerializer.Serialize(
            new Collections(c.RedirectUris, c.AllowedGrantTypes, c.AllowedScopes), JsonOptions);
        return e;
    }

    private static OAuthClient ToModel(OAuthClientEntity e)
    {
        var cols = JsonSerializer.Deserialize<Collections>(e.CollectionsJson, JsonOptions)
                   ?? new Collections(new(), new(), new());
        return new OAuthClient
        {
            Guid = e.Guid,
            ClientId = e.ClientId,
            ClientSecretHash = e.ClientSecretHash,
            ClientType = (OAuthClientType)e.ClientType,
            Name = e.Name,
            IsEnabled = e.IsEnabled,
            CreatedAt = e.ClientCreatedAt,
            RedirectUris = cols.RedirectUris,
            AllowedGrantTypes = cols.AllowedGrantTypes,
            AllowedScopes = cols.AllowedScopes
        };
    }

    private Task<OAuthClientEntity?> FindEntityAsync(string clientId, CancellationToken ct)
        => _entities.ReadAsync(e => e.ClientId == clientId, ct);

    // ---- IOAuthClientStore named lookup ------------------------------------------------------

    public async Task<OAuthClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        var entity = await FindEntityAsync(clientId, ct).ConfigureAwait(false);
        return entity == null ? null : ToModel(entity);
    }

    // ---- IAsyncStore<OAuthClient> ------------------------------------------------------------

    public Task InitAsync(CancellationToken ct = default) => _entities.CreateSchemaAsync(ct);

    public async Task DestroyAsync(CancellationToken ct = default) => await _entities.DropAsync(ct).ConfigureAwait(false);

    public Task<long> CountAsync(Expression<Func<OAuthClient, bool>>? filter = null, CancellationToken ct = default)
        => _entities.CountAsync(null, ct);

    public async Task<OAuthClient?> ReadAsync(Guid guid, CancellationToken ct = default)
    {
        var entity = await _entities.ReadAsync(guid, ct).ConfigureAwait(false);
        return entity == null ? null : ToModel(entity);
    }

    public Task<OAuthClient?> ReadAsync(Expression<Func<OAuthClient, bool>>? filter = null, CancellationToken ct = default)
        => throw new NotSupportedException(
            "SqlOAuthClientStore exposes lookups through GetByClientIdAsync; arbitrary OAuthClient predicates cannot be translated to the entity table.");

    public async Task<Guid> CreateAsync(OAuthClient data, StoreDataDelegate<OAuthClient>? processDelegate = null, CancellationToken ct = default)
    {
        var entity = ToEntity(data);
        await _entities.CreateAsync(entity, ct: ct).ConfigureAwait(false);
        data.Guid = entity.Guid;
        processDelegate?.Invoke(data);
        return entity.Guid!.Value;
    }

    public async Task UpdateAsync(OAuthClient data, StoreDataDelegate<OAuthClient>? processDelegate = null, CancellationToken ct = default)
    {
        var existing = await FindEntityAsync(data.ClientId, ct).ConfigureAwait(false);
        if (existing != null)
        {
            ToEntity(data, existing);
            await _entities.UpdateAsync(existing, ct: ct).ConfigureAwait(false);
            data.Guid = existing.Guid;
        }
        else
        {
            await CreateAsync(data, ct: ct).ConfigureAwait(false);
        }
        processDelegate?.Invoke(data);
    }

    public async Task DeleteAsync(OAuthClient data, CancellationToken ct = default)
    {
        var existing = await FindEntityAsync(data.ClientId, ct).ConfigureAwait(false);
        if (existing != null)
            await _entities.DeleteAsync(existing, ct).ConfigureAwait(false);
    }

    public async Task<Guid> SaveAsync(OAuthClient data, StoreDataDelegate<OAuthClient>? processDelegate = null, CancellationToken ct = default)
    {
        if (data.Guid == null || data.Guid == Guid.Empty)
            return await CreateAsync(data, processDelegate, ct).ConfigureAwait(false);
        await UpdateAsync(data, processDelegate, ct).ConfigureAwait(false);
        return data.Guid!.Value;
    }

    public OAuthClient CreateInstance() => new();
}

/// <summary>
/// DI registration for the OAuth-server stores. SQLite-backed when <paramref name="useSqlite"/> is
/// set (tables created eagerly here, at startup); otherwise the in-memory stores are registered.
/// </summary>
public static class OAuthStoreServiceCollectionExtensions
{
    public static IServiceCollection AddOAuthServerStores(
        this IServiceCollection services, bool useSqlite, string? dbPath)
    {
        if (!useSqlite || string.IsNullOrEmpty(dbPath))
        {
            services.AddSingleton<IOAuthClientStore, InMemoryOAuthClientStore>();
            services.AddSingleton<IAuthorizationCodeStore, InMemoryAuthorizationCodeStore>();
            services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
            services.AddSingleton<IDeviceCodeStore, InMemoryDeviceCodeStore>();
            services.AddSingleton<IConsentStore, InMemoryConsentStore>();
            return services;
        }

        var dir = Path.GetDirectoryName(dbPath);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var settings = new PasswordSettings(dir, Path.GetFileName(dbPath));

        // The upstream OAuth POCOs carry no [Table] attribute — register table names so the
        // field-mapper (which auto-maps their scalar properties) can build a table for each.
        DataBase.RegisterTableName(typeof(AuthorizationCode), "oauth_auth_codes");
        DataBase.RegisterTableName(typeof(RefreshTokenRecord), "oauth_refresh_tokens");
        DataBase.RegisterTableName(typeof(DeviceCodeRecord), "oauth_device_codes");
        DataBase.RegisterTableName(typeof(ConsentRecord), "oauth_consents");

        services.AddSingleton<IAuthorizationCodeStore>(_ => BuildScalar<AuthorizationCode, SqlAuthorizationCodeStore>(settings));
        services.AddSingleton<IRefreshTokenStore>(_ => BuildScalar<RefreshTokenRecord, SqlRefreshTokenStore>(settings));
        services.AddSingleton<IDeviceCodeStore>(_ => BuildScalar<DeviceCodeRecord, SqlDeviceCodeStore>(settings));
        services.AddSingleton<IConsentStore>(_ => BuildScalar<ConsentRecord, SqlConsentStore>(settings));
        services.AddSingleton<IOAuthClientStore>(_ =>
        {
            var store = new SqlOAuthClientStore(settings);
            store.InitializeAsync().GetAwaiter().GetResult();
            return store;
        });

        return services;
    }

    private static TStore BuildScalar<TModel, TStore>(PasswordSettings settings)
        where TModel : AbstractModel
        where TStore : AsyncSQLiteStore<TModel>, new()
    {
        var store = new TStore();
        store.SetSettings(settings);
        store.CreateSchemaAsync().GetAwaiter().GetResult();
        return store;
    }
}

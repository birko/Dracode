using System.Collections.Concurrent;
using System.Linq.Expressions;
using Birko.Data.Models;
using Birko.Data.Stores;
using Birko.Security.OAuth.Server.Models;
using Birko.Security.OAuth.Server.Stores;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// Thread-safe in-memory <see cref="IAsyncStore{T}"/> for the OAuth authorization server.
/// <para>
/// Used while OAuth runs against in-memory state. TASK-031 replaces the registrations with
/// SQLite-backed stores (<c>AsyncSQLiteStore&lt;T&gt;</c>). Registered as <b>singletons</b> — the
/// device-code flow creates a record in one request and reads/mutates it across later requests, so
/// a single shared instance is required (a scoped/transient store would lose all state per request).
/// </para>
/// Mirrors the pattern in <c>Birko.Security.OAuth.Server.Tests/InMemoryStore.cs</c>; the named
/// lookups (GetByClientIdAsync, GetByCodeAsync, …) come from the store interfaces' default methods.
/// </summary>
public class InMemoryOAuthStore<T> : IAsyncStore<T>
    where T : AbstractModel, new()
{
    private readonly ConcurrentDictionary<Guid, T> _data = new();

    public Task InitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DestroyAsync(CancellationToken ct = default) { _data.Clear(); return Task.CompletedTask; }

    public Task<long> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default)
    {
        var query = _data.Values.AsQueryable();
        if (filter != null) query = query.Where(filter);
        return Task.FromResult((long)query.Count());
    }

    public Task<T?> ReadAsync(Guid guid, CancellationToken ct = default)
    {
        _data.TryGetValue(guid, out var value);
        return Task.FromResult(value);
    }

    public Task<T?> ReadAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default)
    {
        var query = _data.Values.AsQueryable();
        if (filter != null) query = query.Where(filter);
        return Task.FromResult(query.FirstOrDefault());
    }

    public Task<Guid> CreateAsync(T data, StoreDataDelegate<T>? processDelegate = null, CancellationToken ct = default)
    {
        if (data.Guid == null || data.Guid == Guid.Empty) data.Guid = Guid.NewGuid();
        _data[data.Guid!.Value] = data;
        processDelegate?.Invoke(data);
        return Task.FromResult(data.Guid!.Value);
    }

    public Task UpdateAsync(T data, StoreDataDelegate<T>? processDelegate = null, CancellationToken ct = default)
    {
        if (data.Guid != null) _data[data.Guid.Value] = data;
        processDelegate?.Invoke(data);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T data, CancellationToken ct = default)
    {
        if (data.Guid != null) _data.TryRemove(data.Guid.Value, out _);
        return Task.CompletedTask;
    }

    public Task<Guid> SaveAsync(T data, StoreDataDelegate<T>? processDelegate = null, CancellationToken ct = default)
    {
        return data.Guid == null || data.Guid == Guid.Empty
            ? CreateAsync(data, processDelegate, ct)
            : UpdateAsync(data, processDelegate, ct).ContinueWith(_ => data.Guid!.Value, TaskScheduler.Default);
    }

    public T CreateInstance() => new();
}

public class InMemoryOAuthClientStore : InMemoryOAuthStore<OAuthClient>, IOAuthClientStore { }
public class InMemoryAuthorizationCodeStore : InMemoryOAuthStore<AuthorizationCode>, IAuthorizationCodeStore { }
public class InMemoryRefreshTokenStore : InMemoryOAuthStore<RefreshTokenRecord>, IRefreshTokenStore { }
public class InMemoryDeviceCodeStore : InMemoryOAuthStore<DeviceCodeRecord>, IDeviceCodeStore { }
public class InMemoryConsentStore : InMemoryOAuthStore<ConsentRecord>, IConsentStore { }

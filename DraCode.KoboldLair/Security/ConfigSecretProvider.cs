using System.Collections.Concurrent;
using Birko.Security;

namespace DraCode.KoboldLair.Security
{
    /// <summary>
    /// Phase-1 <see cref="ISecretProvider"/> backed by in-memory values seeded from configuration
    /// (TASK-073). It holds the provider-config master key supplied via appsettings / user-secrets /
    /// environment, so <see cref="ProviderKeyCipher"/> can resolve it the same way it will later resolve
    /// it from Azure Key Vault / HashiCorp Vault — swapping to those is a DI registration change only,
    /// because everything depends on this interface, not on config directly.
    /// </summary>
    public sealed class ConfigSecretProvider : ISecretProvider
    {
        private readonly ConcurrentDictionary<string, string> _secrets;

        public ConfigSecretProvider(IDictionary<string, string> secrets)
            => _secrets = new ConcurrentDictionary<string, string>(secrets);

        public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_secrets.TryGetValue(key, out var v) ? v : null);

        public Task<SecretResult?> GetSecretWithMetadataAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_secrets.TryGetValue(key, out var v)
                ? new SecretResult { Key = key, Value = v }
                : null);

        public Task SetSecretAsync(string key, string value, CancellationToken ct = default)
        {
            _secrets[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteSecretAsync(string key, CancellationToken ct = default)
        {
            _secrets.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListSecretsAsync(string? path = null, CancellationToken ct = default)
        {
            IReadOnlyList<string> keys = _secrets.Keys
                .Where(k => string.IsNullOrEmpty(path) || k.StartsWith(path, StringComparison.Ordinal))
                .ToList();
            return Task.FromResult(keys);
        }
    }
}

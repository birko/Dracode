using System.Security.Cryptography;
using System.Text;
using Birko.Security;
using Birko.Security.Encryption;

namespace DraCode.KoboldLair.Security
{
    /// <summary>
    /// Encrypts/decrypts LLM provider API keys for at-rest storage (TASK-073). The master key is resolved
    /// through Birko's <see cref="ISecretProvider"/> — Phase 1 a config-backed provider, Phase 2 a Vault/
    /// Key Vault provider (a DI swap, no code change) — then stretched to a 256-bit AES-GCM key. Resolve the
    /// master key once via <see cref="InitializeAsync"/>; <see cref="Encrypt"/>/<see cref="Decrypt"/> are then
    /// synchronous so they don't complicate the hot path. The master key never leaves this process.
    /// </summary>
    public sealed class ProviderKeyCipher
    {
        /// <summary>The <see cref="ISecretProvider"/> key under which the master key is stored.</summary>
        public const string MasterKeySecretName = "provider-config-master-key";

        private readonly ISecretProvider _secrets;
        private readonly AesEncryptionProvider _aes = new();
        private byte[]? _key;

        public ProviderKeyCipher(ISecretProvider secrets) => _secrets = secrets;

        /// <summary>Fetches the master key via <see cref="ISecretProvider"/> and derives the AES-256 key. Idempotent.</summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_key != null) return;
            var master = await _secrets.GetSecretAsync(MasterKeySecretName, ct);
            if (string.IsNullOrEmpty(master))
                throw new InvalidOperationException(
                    $"provider-config master key ('{MasterKeySecretName}') is not configured; set it in config (Phase 1) or a secret store (Phase 2)");
            // Stretch the (arbitrary-length) master string to a fixed 256-bit AES-GCM key.
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(master));
        }

        /// <summary>Encrypts a plaintext key → base64 ciphertext. Null/empty in → null out.</summary>
        public string? Encrypt(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return null;
            return _aes.EncryptString(plaintext, Key());
        }

        /// <summary>Decrypts base64 ciphertext → plaintext key. Null/empty in → null out.</summary>
        public string? Decrypt(string? ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return null;
            return _aes.DecryptString(ciphertext, Key());
        }

        private byte[] Key() => _key
            ?? throw new InvalidOperationException("ProviderKeyCipher.InitializeAsync must be called before Encrypt/Decrypt");
    }
}

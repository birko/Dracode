using System.Security.Cryptography;
using DraCode.KoboldLair.Security;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Security;

/// <summary>
/// Unit coverage for <see cref="ProviderKeyCipher"/> (TASK-073): round-trips a key, never returns plaintext
/// for ciphertext, refuses to operate uninitialized, fails closed on a wrong master key, and surfaces a
/// missing master key clearly. Master key comes via the Phase-1 <see cref="ConfigSecretProvider"/>.
/// </summary>
public class ProviderKeyCipherTests
{
    private static ProviderKeyCipher Cipher(string masterKey) =>
        new(new ConfigSecretProvider(new Dictionary<string, string> { [ProviderKeyCipher.MasterKeySecretName] = masterKey }));

    [Fact]
    public async Task Round_trips_a_key_and_ciphertext_is_not_plaintext()
    {
        var cipher = Cipher("master-key-123");
        await cipher.InitializeAsync();

        var ct = cipher.Encrypt("sk-ant-secret");
        ct.Should().NotBeNullOrEmpty();
        ct.Should().NotBe("sk-ant-secret");
        cipher.Decrypt(ct).Should().Be("sk-ant-secret");
    }

    [Fact]
    public async Task Null_or_empty_passes_through_as_null()
    {
        var cipher = Cipher("m");
        await cipher.InitializeAsync();
        cipher.Encrypt(null).Should().BeNull();
        cipher.Encrypt("").Should().BeNull();
        cipher.Decrypt(null).Should().BeNull();
        cipher.Decrypt("").Should().BeNull();
    }

    [Fact]
    public async Task A_different_master_key_cannot_decrypt()
    {
        var a = Cipher("master-A");
        var b = Cipher("master-B");
        await a.InitializeAsync();
        await b.InitializeAsync();

        var ct = a.Encrypt("secret");
        var act = () => b.Decrypt(ct);
        act.Should().Throw<CryptographicException>("AES-GCM authentication must fail under the wrong key");
    }

    [Fact]
    public void Operating_before_initialize_throws()
    {
        var cipher = Cipher("m");
        var act = () => cipher.Encrypt("x");
        act.Should().Throw<InvalidOperationException>().WithMessage("*InitializeAsync*");
    }

    [Fact]
    public async Task Missing_master_key_is_a_clear_error()
    {
        var cipher = new ProviderKeyCipher(new ConfigSecretProvider(new Dictionary<string, string>()));
        var act = async () => await cipher.InitializeAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*master key*");
    }
}

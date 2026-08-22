using FluentAssertions;
using SSHTunnelProxy.Core.Security;
using Xunit;

namespace SSHTunnelProxy.Tests.Unit;

public class DpapiProtectorTests
{
    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        var protector = new DpapiProtector();
        var secret = "my-secret-password-123";

        var encrypted = protector.Encrypt(secret);
        var decrypted = protector.Decrypt(encrypted);

        decrypted.Should().Be(secret);
        encrypted.Should().NotBe(secret); // 不应为明文
    }

    [Fact]
    public void Encrypt_Empty_ReturnsEmpty()
    {
        var protector = new DpapiProtector();

        protector.Encrypt(string.Empty).Should().Be(string.Empty);
        protector.Decrypt(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
    {
        // 使用熵，确保相同明文得到不同密文（防止外部推定）。
        var protector = new DpapiProtector();

        var c1 = protector.Encrypt("same");
        var c2 = protector.Encrypt("same");

        c1.Should().NotBe(c2);
    }
}

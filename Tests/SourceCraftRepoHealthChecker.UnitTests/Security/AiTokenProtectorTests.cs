using System.Security.Cryptography;
using AutoFixture;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.Security;

namespace SourceCraftRepoHealthChecker.UnitTests.Security;

public sealed class AiTokenProtectorTests
{
    private static readonly string TestKey = Convert.ToBase64String(new byte[32]);

    private readonly Fixture _fixture = new();
    private readonly AiTokenProtector systemUnderTests;

    public AiTokenProtectorTests()
    {
        var options = A.Fake<IOptions<AiTokenEncryptionOptions>>();
        A.CallTo(() => options.Value).Returns(new AiTokenEncryptionOptions { Key = TestKey });

        systemUnderTests = new AiTokenProtector(options);
    }

    [Fact]
    public void Protect_ThenUnprotect_ReturnsOriginalToken()
    {
        // Arrange
        var token = _fixture.Create<string>();

        // Act
        var protectedToken = systemUnderTests.Protect(token);
        var actual = systemUnderTests.Unprotect(protectedToken);

        // Assert
        actual.Should().Be(token);
    }

    [Fact]
    public void Protect_WithSameTokenTwice_ReturnsDifferentValues()
    {
        // Arrange
        var token = _fixture.Create<string>();

        // Act
        var first = systemUnderTests.Protect(token);
        var second = systemUnderTests.Protect(token);

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void Protect_EmptyToken_ThenUnprotect_ReturnsEmpty()
    {
        // Act
        var protectedToken = systemUnderTests.Protect(string.Empty);
        var actual = systemUnderTests.Unprotect(protectedToken);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Unprotect_WhenCiphertextTampered_ThrowsCryptographicException()
    {
        // Arrange
        var protectedToken = systemUnderTests.Protect(_fixture.Create<string>());
        var payload = Convert.FromBase64String(protectedToken);
        payload[^1] ^= 0xFF;

        // Act
        var act = () => systemUnderTests.Unprotect(Convert.ToBase64String(payload));

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WhenPayloadTooShort_ThrowsCryptographicException()
    {
        // Arrange
        var payload = Convert.ToBase64String(new byte[4]);

        // Act
        var act = () => systemUnderTests.Unprotect(payload);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WhenValueIsNotBase64_ThrowsFormatException()
    {
        // Act
        var act = () => systemUnderTests.Unprotect("не-base64!!!");

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Unprotect_WhenProtectedWithAnotherKey_ThrowsCryptographicException()
    {
        // Arrange
        var otherOptions = A.Fake<IOptions<AiTokenEncryptionOptions>>();
        A.CallTo(() => otherOptions.Value).Returns(new AiTokenEncryptionOptions
        {
            Key = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray())
        });
        var otherProtector = new AiTokenProtector(otherOptions);
        var protectedToken = otherProtector.Protect(_fixture.Create<string>());

        // Act
        var act = () => systemUnderTests.Unprotect(protectedToken);

        // Assert
        act.Should().Throw<CryptographicException>();
    }
}

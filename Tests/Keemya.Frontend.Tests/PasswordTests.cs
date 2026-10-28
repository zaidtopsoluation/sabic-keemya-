using Keemya.Frontend.Services;
using Xunit;

namespace Keemya.Frontend.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashThenVerify_RoundTrips()
    {
        string hash = PasswordHasher.HashPassword("Secret@123");
        Assert.True(PasswordHasher.VerifyPassword("Secret@123", hash));
        Assert.False(PasswordHasher.VerifyPassword("Secret@124", hash));
    }

    [Fact]
    public void Hash_IsSaltedPerCall()
    {
        Assert.NotEqual(PasswordHasher.HashPassword("Secret@123"), PasswordHasher.HashPassword("Secret@123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Hash_RejectsEmpty(string? password)
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.HashPassword(password!));
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("AAAA")] // valid base64, wrong length
    [InlineData("")]
    public void Verify_ReturnsFalseOnGarbageHash(string hash)
    {
        Assert.False(PasswordHasher.VerifyPassword("Secret@123", hash));
    }
}

public class PasswordValidatorTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Ab@1", false)]        // too short
    [InlineData("12345678@", false)]   // no letter
    [InlineData("abcdefgh1", false)]   // no special char
    [InlineData("abcd efgh", false)]   // whitespace is not a special char
    [InlineData("abcdefg@", true)]
    [InlineData("Secret@123", true)]
    public void IsValid(string password, bool expected)
    {
        Assert.Equal(expected, PasswordValidator.IsValid(password, out string error));
        Assert.Equal(expected, error == string.Empty);
    }

    [Fact]
    public void TempPassword_PassesValidation()
    {
        for (int i = 0; i < 50; i++)
            Assert.True(PasswordValidator.IsValid(PasswordValidator.GenerateTempPassword(), out _));
    }
}

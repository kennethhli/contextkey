using ContextKey.Core;

namespace ContextKey.Tests;

public sealed class SecureFieldTests
{
    [Fact]
    public void SecureTextFieldRole_IsSecure()
    {
        Assert.True(SecureField.IsSecure("AXTextField", "AXSecureTextField"));
        Assert.True(SecureField.IsSecure("AXSecureTextField", null));
        Assert.True(SecureField.IsSecure("AXSecureTextArea", null));
    }

    [Fact]
    public void PasswordDescription_IsSecure()
    {
        Assert.True(SecureField.IsSecure("AXTextField", null, "Password"));
        Assert.True(SecureField.IsSecure("AXTextField", null, "secure text field"));
    }

    [Fact]
    public void NormalField_IsNotSecure()
    {
        Assert.False(SecureField.IsSecure("AXTextField", null, "email"));
        Assert.False(SecureField.IsSecure("AXTextArea", "AXStandard", "notes"));
        Assert.False(SecureField.IsSecure(null, null, null));
    }
}

using CleanWipe.Core.Services;

namespace CleanWipe.Tests;

/// <summary>
/// Pruebas CRÍTICAS de seguridad. Si alguna falla, la app podría dañar el sistema:
/// estas pruebas son la red de seguridad de CleanWipe.
/// </summary>
public class SafetyValidatorTests
{
    // ---------------- Rutas prohibidas ----------------

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"C:\Windows\SysWOW64")]
    [InlineData(@"C:\Windows\WinSxS")]
    [InlineData(@"C:\Windows\WinSxS\some.dll")]
    [InlineData(@"C:\Boot")]
    [InlineData(@"C:\$Recycle.Bin")]
    [InlineData(@"C:\ProgramData\Microsoft")]
    [InlineData(@"C:\ProgramData\Microsoft\Crypto")]
    [InlineData(@"C:\ProgramData\Windows")]
    public void IsPathSafe_SystemTrees_ReturnsFalse(string path)
    {
        Assert.False(SafetyValidator.IsPathSafe(path), $"DEBÍA bloquear: {path}");
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    [InlineData(@"C:\ProgramData")]
    public void IsPathSafe_ProtectedRoots_ReturnsFalse(string path)
    {
        Assert.False(SafetyValidator.IsPathSafe(path), $"DEBÍA bloquear la raíz: {path}");
    }

    [Fact]
    public void IsPathSafe_UserProfileRoots_ReturnsFalse()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        Assert.False(SafetyValidator.IsPathSafe(profile));
        Assert.False(SafetyValidator.IsPathSafe(appdata));
        Assert.False(SafetyValidator.IsPathSafe(local));
        Assert.False(SafetyValidator.IsPathSafe(docs));
        Assert.False(SafetyValidator.IsPathSafe(desktop));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsPathSafe_EmptyOrNull_ReturnsFalse(string? path)
    {
        Assert.False(SafetyValidator.IsPathSafe(path!));
    }

    [Fact]
    public void IsPathSafe_TraversalTrick_IsResolvedAndBlocked()
    {
        // Un intento de escapar a C:\Windows mediante "..".
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string tricky = Path.Combine(appdata, @"..\..\..\..\..\Windows\System32");
        Assert.False(SafetyValidator.IsPathSafe(tricky), "El traversal debe resolverse y bloquearse.");
    }

    // ---------------- Rutas permitidas (rastros legítimos de apps) ----------------

    [Fact]
    public void IsPathSafe_AppSubfoldersUnderProtectedRoots_ReturnsTrue()
    {
        string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.True(SafetyValidator.IsPathSafe(Path.Combine(appdata, "Spotify")));
        Assert.True(SafetyValidator.IsPathSafe(Path.Combine(local, "Discord")));
        Assert.True(SafetyValidator.IsPathSafe(@"C:\Program Files\SomeVendor\SomeApp"));
        Assert.True(SafetyValidator.IsPathSafe(@"C:\Program Files (x86)\SomeVendor\SomeApp"));
        Assert.True(SafetyValidator.IsPathSafe(@"C:\ProgramData\SomeVendorApp"));
    }

    // ---------------- Registro prohibido ----------------

    [Theory]
    [InlineData(@"HKLM\SYSTEM")]
    [InlineData(@"HKLM\SYSTEM\CurrentControlSet\Services")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet")]
    [InlineData(@"HKLM\HARDWARE")]
    [InlineData(@"HKLM\SAM")]
    [InlineData(@"HKLM\SECURITY")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows NT")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\.NETFramework")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\NET Framework Setup")]
    [InlineData(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders")]
    public void IsRegistryKeySafe_SystemKeys_ReturnsFalse(string key)
    {
        Assert.False(SafetyValidator.IsRegistryKeySafe(key), $"DEBÍA bloquear: {key}");
    }

    [Theory]
    [InlineData(@"HKLM")]
    [InlineData(@"HKCU")]
    [InlineData(@"HKLM\SOFTWARE")]
    [InlineData(@"HKCU\SOFTWARE")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")]
    [InlineData(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")]
    public void IsRegistryKeySafe_ProtectedRootsAndUninstallParent_ReturnsFalse(string key)
    {
        Assert.False(SafetyValidator.IsRegistryKeySafe(key), $"DEBÍA bloquear el contenedor: {key}");
    }

    // ---------------- Registro permitido (claves concretas de apps) ----------------

    [Theory]
    [InlineData(@"HKCU\SOFTWARE\SomeVendorApp")]
    [InlineData(@"HKLM\SOFTWARE\SomeVendor\SomeApp")]
    [InlineData(@"HKLM\SOFTWARE\WOW6432Node\SomeVendor\SomeApp")]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{GUID-APP}")]
    [InlineData(@"HKEY_CURRENT_USER\Software\SomeVendorApp")]
    public void IsRegistryKeySafe_AppKeys_ReturnsTrue(string key)
    {
        Assert.True(SafetyValidator.IsRegistryKeySafe(key), $"DEBÍA permitir: {key}");
    }

    [Fact]
    public void NormalizeRegistry_NormalizesHiveAbbreviations()
    {
        Assert.Equal(@"HKLM\SOFTWARE\App",
            SafetyValidator.NormalizeRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\App"));
        Assert.Equal(@"HKCU\Software\App",
            SafetyValidator.NormalizeRegistry(@"hkcu\Software\App\"));
    }
}

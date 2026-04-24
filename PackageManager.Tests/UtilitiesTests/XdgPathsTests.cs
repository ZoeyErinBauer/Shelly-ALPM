using PackageManager.Utilities;

namespace PackageManager.Tests.UtilitiesTests;

[TestFixture]
[NonParallelizable]
public class XdgPathsTests
{
    private string? _origCache;
    private string? _origData;
    private string? _origConfig;
    private string? _origSudoUser;
    private string? _origHome;

    [SetUp]
    public void SaveEnv()
    {
        _origCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        _origData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _origConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        _origSudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
        _origHome = Environment.GetEnvironmentVariable("HOME");
        Environment.SetEnvironmentVariable("SUDO_USER", null);
    }

    [TearDown]
    public void RestoreEnv()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", _origCache);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _origData);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _origConfig);
        Environment.SetEnvironmentVariable("SUDO_USER", _origSudoUser);
        Environment.SetEnvironmentVariable("HOME", _origHome);
    }

    [Test]
    public void CacheHome_Unset_FallsBackToHomeDotCache()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", null);
        Environment.SetEnvironmentVariable("HOME", "/tmp/fakehome");

        Assert.That(XdgPaths.CacheHome(), Is.EqualTo("/tmp/fakehome/.cache"));
    }

    [Test]
    public void CacheHome_Empty_FallsBackToDefault()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "");
        Environment.SetEnvironmentVariable("HOME", "/tmp/fakehome");

        Assert.That(XdgPaths.CacheHome(), Is.EqualTo("/tmp/fakehome/.cache"));
    }

    [Test]
    public void CacheHome_RelativePath_IsIgnored()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "relative/path");
        Environment.SetEnvironmentVariable("HOME", "/tmp/fakehome");

        Assert.That(XdgPaths.CacheHome(), Is.EqualTo("/tmp/fakehome/.cache"));
    }

    [Test]
    public void CacheHome_AbsolutePath_IsHonored()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "/tmp/xcache");

        Assert.That(XdgPaths.CacheHome(), Is.EqualTo("/tmp/xcache"));
    }

    [Test]
    public void UnderSudo_XdgVarsAreIgnored()
    {
        Environment.SetEnvironmentVariable("SUDO_USER", "someone");
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "/tmp/x");

        Assert.That(XdgPaths.CacheHome(), Is.EqualTo("/home/someone/.cache"));
    }

    [Test]
    public void ShellyCache_WithParts_ComposesCorrectly()
    {
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "/tmp/xcache");

        Assert.That(XdgPaths.ShellyCache("pkg", "sub"),
            Is.EqualTo("/tmp/xcache/Shelly/pkg/sub"));
    }

    [Test]
    public void ShellyData_ComposesCorrectly()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", "/tmp/xdata");

        Assert.That(XdgPaths.ShellyData("vcs.json"),
            Is.EqualTo("/tmp/xdata/Shelly/vcs.json"));
    }

    [Test]
    public void ShellyConfig_UsesLowercaseShelly()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/tmp/xconfig");

        Assert.That(XdgPaths.ShellyConfig("settings.toml"),
            Is.EqualTo("/tmp/xconfig/shelly/settings.toml"));
    }

    [Test]
    public void InvokingUserHome_WithoutSudoUser_FallsBackToHome()
    {
        Environment.SetEnvironmentVariable("SUDO_USER", null);
        Environment.SetEnvironmentVariable("HOME", "/tmp/fakehome");

        Assert.That(XdgPaths.InvokingUserHome(), Is.EqualTo("/tmp/fakehome"));
    }

    [Test]
    public void InvokingUserHome_WithSudoUser_UsesHomeUserPath()
    {
        Environment.SetEnvironmentVariable("SUDO_USER", "alice");

        Assert.That(XdgPaths.InvokingUserHome(), Is.EqualTo("/home/alice"));
    }
}

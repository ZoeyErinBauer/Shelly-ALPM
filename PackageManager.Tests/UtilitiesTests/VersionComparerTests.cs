using NUnit.Framework;
using PackageManager.Utilities;

namespace PackageManager.Tests.UtilitiesTests;

[TestFixture]
public class VersionComparerTests
{
    [TestCase("1.0.0", "1.0.0", 0)]
    [TestCase("1.0.0", "1.0.1", -1)]
    [TestCase("1.0.1", "1.0.0", 1)]
    [TestCase("1.2.3", "1.2.3", 0)]
    [TestCase("2.0.0", "1.9.9", 1)]
    [TestCase("1.10.0", "1.9.0", 1)]
    public void Compare_SemanticVersions(string v1, string v2, int expected)
    {
        Assert.That(VersionComparer.Compare(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1.0.0-1", "1.0.0-1", 0)]
    [TestCase("1.0.0-1", "1.0.0-2", -1)]
    [TestCase("1.0.0-2", "1.0.0-1", 1)]
    [TestCase("1.0.0-10", "1.0.0-9", 1)]
    public void Compare_WithPkgrel(string v1, string v2, int expected)
    {
        Assert.That(VersionComparer.Compare(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1:1.0.0", "1.0.0", 1)]
    [TestCase("1.0.0", "1:1.0.0", -1)]
    [TestCase("2:1.0.0", "1:2.0.0", 1)]
    [TestCase("1:1.0.0-1", "1:1.0.0-2", -1)]
    public void Compare_WithEpoch(string v1, string v2, int expected)
    {
        Assert.That(VersionComparer.Compare(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1.0.0alpha", "1.0.0beta", -1)]
    [TestCase("1.0.0beta", "1.0.0rc", -1)]
    [TestCase("1.0.0rc", "1.0.0", -1)]
    [TestCase("1.0.0alpha", "1.0.0", -1)]
    public void Compare_PreReleaseVersions(string v1, string v2, int expected)
    {
        Assert.That(VersionComparer.Compare(v1, v2), Is.EqualTo(expected));
    }

    [TestCase(null, null, 0)]
    [TestCase(null, "1.0.0", -1)]
    [TestCase("1.0.0", null, 1)]
    [TestCase("", "", 0)]
    [TestCase("", "1.0.0", -1)]
    public void Compare_NullAndEmpty(string? v1, string? v2, int expected)
    {
        Assert.That(VersionComparer.Compare(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1.0.0-2", "1.0.0-1", true)]
    [TestCase("2.0.0", "1.0.0", true)]
    [TestCase("1.0.0", "1.0.0", false)]
    public void IsNewer_ReturnsCorrectResult(string v1, string v2, bool expected)
    {
        Assert.That(VersionComparer.IsNewer(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1.0.0-1", "1.0.0-2", true)]
    [TestCase("1.0.0", "2.0.0", true)]
    [TestCase("1.0.0", "1.0.0", false)]
    public void IsOlder_ReturnsCorrectResult(string v1, string v2, bool expected)
    {
        Assert.That(VersionComparer.IsOlder(v1, v2), Is.EqualTo(expected));
    }

    [TestCase("1.0.0", "1.0.0", true)]
    [TestCase("1.0.0-1", "1.0.0-1", true)]
    [TestCase("1.0.0", "1.0.1", false)]
    public void AreEqual_ReturnsCorrectResult(string v1, string v2, bool expected)
    {
        Assert.That(VersionComparer.AreEqual(v1, v2), Is.EqualTo(expected));
    }
}

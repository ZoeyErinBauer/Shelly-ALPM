using Shelly.Gtk.Helpers;

namespace Shelly.Gtk.Tests;

[TestFixture]
public class PackageSearchTests
{
    // ---- MatchesNameOrDescription -------------------------------------------------

    [Test]
    public void MatchesNameOrDescription_NullDescription_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            PackageSearch.MatchesNameOrDescription("foo", null, "bar"));
    }

    [Test]
    public void MatchesNameOrDescription_NullName_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            PackageSearch.MatchesNameOrDescription(null, "foo", "bar"));
    }

    [Test]
    public void MatchesNameOrDescription_BothNull_EmptySearch_ReturnsTrue()
    {
        Assert.That(PackageSearch.MatchesNameOrDescription(null, null, null), Is.True);
        Assert.That(PackageSearch.MatchesNameOrDescription(null, null, ""), Is.True);
        Assert.That(PackageSearch.MatchesNameOrDescription(null, null, "   "), Is.True);
    }

    [Test]
    public void MatchesNameOrDescription_BothNull_NonEmptySearch_ReturnsFalse()
    {
        Assert.That(PackageSearch.MatchesNameOrDescription(null, null, "anything"), Is.False);
    }

    [Test]
    public void MatchesNameOrDescription_DescriptionOnlyMatch_ReturnsTrue()
    {
        Assert.That(
            PackageSearch.MatchesNameOrDescription("emby-pac-beta", "AppStream catalog and icons", "appstream"),
            Is.True);
    }

    [Test]
    public void MatchesNameOrDescription_NameOnlyMatch_ReturnsTrue()
    {
        Assert.That(
            PackageSearch.MatchesNameOrDescription("media.emby.client.beta", null, "emby"),
            Is.True);
    }

    [Test]
    public void MatchesNameOrDescription_CaseInsensitive_ReturnsTrue()
    {
        Assert.That(
            PackageSearch.MatchesNameOrDescription("FOO", null, "foo"),
            Is.True);
        Assert.That(
            PackageSearch.MatchesNameOrDescription(null, "FoO BaR", "bar"),
            Is.True);
    }

    [Test]
    public void MatchesNameOrDescription_NoMatch_ReturnsFalse()
    {
        Assert.That(
            PackageSearch.MatchesNameOrDescription("foo", "bar", "baz"),
            Is.False);
    }

    // ---- MatchesName --------------------------------------------------------------

    [Test]
    public void MatchesName_NullName_DoesNotThrow_NonEmptySearch_ReturnsFalse()
    {
        Assert.DoesNotThrow(() => PackageSearch.MatchesName(null, "foo"));
        Assert.That(PackageSearch.MatchesName(null, "foo"), Is.False);
    }

    [Test]
    public void MatchesName_EmptySearch_ReturnsTrue()
    {
        Assert.That(PackageSearch.MatchesName(null, null), Is.True);
        Assert.That(PackageSearch.MatchesName(null, ""), Is.True);
        Assert.That(PackageSearch.MatchesName("anything", "  "), Is.True);
    }

    [Test]
    public void MatchesName_CaseInsensitive_ReturnsTrue()
    {
        Assert.That(PackageSearch.MatchesName("EmBy", "emby"), Is.True);
    }

    // ---- MatchesGroup -------------------------------------------------------------

    [Test]
    public void MatchesGroup_AnyOrEmpty_ReturnsTrue()
    {
        Assert.That(PackageSearch.MatchesGroup(null, "Any"), Is.True);
        Assert.That(PackageSearch.MatchesGroup(null, null), Is.True);
        Assert.That(PackageSearch.MatchesGroup(null, ""), Is.True);
        Assert.That(PackageSearch.MatchesGroup(new[] { "g1" }, "Any"), Is.True);
    }

    [Test]
    public void MatchesGroup_NullGroups_NonAnySelection_ReturnsFalse()
    {
        // emby-pac-beta has no %GROUPS% — must not throw and must filter the row out.
        Assert.DoesNotThrow(() => PackageSearch.MatchesGroup(null, "default"));
        Assert.That(PackageSearch.MatchesGroup(null, "default"), Is.False);
    }

    [Test]
    public void MatchesGroup_GroupPresent_ReturnsTrue()
    {
        Assert.That(PackageSearch.MatchesGroup(new[] { "default", "extra" }, "default"), Is.True);
    }

    [Test]
    public void MatchesGroup_GroupAbsent_ReturnsFalse()
    {
        Assert.That(PackageSearch.MatchesGroup(new[] { "extra" }, "default"), Is.False);
    }

    // ---- Regression: the actual EndeavourOS crashing rows --------------------------

    [Test]
    public void Regression_MediaEmbyClientBeta_NullDescription_NoCrashOnSearch()
    {
        // %DESC% is missing → Description is null on the in-memory model.
        const string? name = "media.emby.client.beta";
        const string? description = null;

        Assert.DoesNotThrow(() => PackageSearch.MatchesNameOrDescription(name, description, "emby"));
        Assert.That(PackageSearch.MatchesNameOrDescription(name, description, "emby"), Is.True);
        Assert.That(PackageSearch.MatchesNameOrDescription(name, description, "zzz"), Is.False);
    }

    [Test]
    public void Regression_EmbyPacBeta_NullGroups_NoCrashOnGroupFilter()
    {
        // %GROUPS% missing → Groups is null on the in-memory model.
        Assert.DoesNotThrow(() => PackageSearch.MatchesGroup(null, "default"));
        Assert.That(PackageSearch.MatchesGroup(null, "default"), Is.False);
        Assert.That(PackageSearch.MatchesGroup(null, "Any"), Is.True);
    }
}

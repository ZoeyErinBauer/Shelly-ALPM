using PackageManager.Aur;

namespace PackageManager.Tests.Aur;

public class VcsSourceParserTests
{
    [Test]
    public void ParseSource_GitPlusHttps_ReturnsEntry()
    {
        var result = VcsSourceParser.ParseSource("git+https://github.com/user/repo.git");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("https://github.com/user/repo.git"));
        Assert.That(result.Branch, Is.EqualTo("HEAD"));
        Assert.That(result.Protocols, Does.Contain("https"));
        Assert.That(result.Protocols, Does.Not.Contain("git"));
    }

    [Test]
    public void ParseSource_WithNamePrefix_StripsName()
    {
        var result = VcsSourceParser.ParseSource("myrepo::git+https://github.com/user/repo.git");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("https://github.com/user/repo.git"));
    }

    [Test]
    public void ParseSource_WithBranchFragment_ParsesBranch()
    {
        var result = VcsSourceParser.ParseSource("git+https://github.com/user/repo.git#branch=develop");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("https://github.com/user/repo.git"));
        Assert.That(result.Branch, Is.EqualTo("develop"));
    }

    [Test]
    public void ParseSource_WithCommitFragment_ReturnsNull()
    {
        var result = VcsSourceParser.ParseSource("git+https://github.com/user/repo.git#commit=abc123");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseSource_WithTagFragment_ReturnsNull()
    {
        var result = VcsSourceParser.ParseSource("git+https://github.com/user/repo.git#tag=v1.0");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseSource_NonGitSource_ReturnsNull()
    {
        var result = VcsSourceParser.ParseSource("https://example.com/archive.tar.gz");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseSource_WithQueryParams_StripsQuery()
    {
        var result = VcsSourceParser.ParseSource("git+https://github.com/user/repo.git?signed");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("https://github.com/user/repo.git"));
    }

    [Test]
    public void ParseSource_EmptyString_ReturnsNull()
    {
        Assert.That(VcsSourceParser.ParseSource(""), Is.Null);
        Assert.That(VcsSourceParser.ParseSource("   "), Is.Null);
    }

    [Test]
    public void ParseSources_MixedSources_ReturnsOnlyGit()
    {
        var sources = new[]
        {
            "git+https://github.com/user/repo.git",
            "https://example.com/archive.tar.gz",
            "git+https://github.com/user/repo2.git#commit=abc",
            "myname::git+https://github.com/user/repo3.git#branch=main"
        };

        var results = VcsSourceParser.ParseSources(sources);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Url, Is.EqualTo("https://github.com/user/repo.git"));
        Assert.That(results[1].Url, Is.EqualTo("https://github.com/user/repo3.git"));
        Assert.That(results[1].Branch, Is.EqualTo("main"));
    }

    [Test]
    public void ParseSource_NamedWithColonColon_HandlesCorrectly()
    {
        var result = VcsSourceParser.ParseSource("pkg-name::git+https://gitlab.com/org/project.git");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("https://gitlab.com/org/project.git"));
    }
}

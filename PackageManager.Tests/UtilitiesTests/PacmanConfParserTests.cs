using NUnit.Framework;
using PackageManager.Utilities;
using System.IO;
using System.Linq;
using PackageManager.Alpm;

namespace PackageManager.Tests.UtilitiesTests;

[TestFixture]
internal class PacmanConfParserTests
{
    private string _testConfPath;

    [SetUp]
    public void Setup()
    {
        _testConfPath = Path.GetTempFileName();
        File.WriteAllText(_testConfPath, @"
[options]
RootDir = /mnt/target
DBPath = /mnt/target/var/lib/pacman
HoldPkg = pacman glibc linux
Architecture = x86_64
CheckSpace

[core]
Server = https://mirror.example.com/core/os/$arch

[extra]
Server = https://mirror.example.com/extra/os/$arch
Server = https://another-mirror.example.com/extra/os/$arch
");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_testConfPath))
        {
            File.Delete(_testConfPath);
        }
    }

    [Test]
    public void Parse_ValidConfig_ReturnsPopulatedRecord()
    {
        var conf = PacmanConfParser.Parse(_testConfPath);

        Assert.Multiple(() =>
        {
            Assert.That(conf.RootDirectory, Is.EqualTo("/mnt/target"));
            Assert.That(conf.DbPath, Is.EqualTo("/mnt/target/var/lib/pacman"));
            Assert.That(conf.HoldPkg, Is.EquivalentTo(new[] { "pacman", "glibc", "linux" }));
            Assert.That(conf.Architecture, Is.EqualTo("x86_64"));
            Assert.That(conf.CheckSpace, Is.True);
            Assert.That(conf.Repos, Has.Count.EqualTo(2));
            
            var core = conf.Repos.FirstOrDefault(r => r.Name == "core");
            Assert.That(core, Is.Not.Null);
            Assert.That(core.Servers, Has.Count.EqualTo(1));
            Assert.That(core.Servers[0], Is.EqualTo("https://mirror.example.com/core/os/$arch"));

            var extra = conf.Repos.FirstOrDefault(r => r.Name == "extra");
            Assert.That(extra, Is.Not.Null);
            Assert.That(extra.Servers, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Parse_WithInclude_ReturnsPopulatedRecord()
    {
        var includePath = Path.GetTempFileName();
        File.WriteAllText(includePath, "Architecture = arm64\nCheckSpace");
        
        var mainPath = Path.GetTempFileName();
        File.WriteAllText(mainPath, $"[options]\nInclude = {includePath}");

        try
        {
            var conf = PacmanConfParser.Parse(mainPath);
            Assert.That(conf.Architecture, Is.EqualTo("arm64"));
            Assert.That(conf.CheckSpace, Is.True);
        }
        finally
        {
            File.Delete(includePath);
            File.Delete(mainPath);
        }
    }

    [Test]
    public void Parse_SystemConfig_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => PacmanConfParser.Parse());
    }

    [Test]
    [TestCase("Required DatabaseOptional", AlpmSigLevel.Package | AlpmSigLevel.DatabaseOptional)]
    [TestCase("Never", AlpmSigLevel.None)]
    [TestCase("Optional TrustAll", AlpmSigLevel.PackageOptional | AlpmSigLevel.PackageUnknownOk | AlpmSigLevel.PackageMarginalOk | AlpmSigLevel.DatabaseUnknownOk | AlpmSigLevel.DatabaseMarginalOk)]
    [TestCase("PackageRequired DatabaseRequired", AlpmSigLevel.Package | AlpmSigLevel.Database)]
    [TestCase("UseDefault", AlpmSigLevel.UseDefault)]
    public void Parse_SigLevel_CorrectlyParsed(string sigLevelValue, AlpmSigLevel expected)
    {
        var mainPath = Path.GetTempFileName();
        File.WriteAllText(mainPath, $"[options]\nSigLevel = {sigLevelValue}");

        try
        {
            var conf = PacmanConfParser.Parse(mainPath);
            Assert.That(conf.SigLevel, Is.EqualTo(expected));
        }
        finally
        {
            if (File.Exists(mainPath)) File.Delete(mainPath);
        }
    }
}

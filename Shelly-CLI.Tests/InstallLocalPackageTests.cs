using System.Formats.Tar;
using System.IO.Compression;
using Shelly_CLI.Commands.Standard;
using Spectre.Console.Cli;

namespace Shelly_CLI.Tests;

[TestFixture]
public class InstallLocalPackageTests
{
    private string _tempDir = null!;
    private InstallLocalPackage _command = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "shelly-cli-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _command = new InstallLocalPackage();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static CommandContext CreateCommandContext()
    {
        return new CommandContext([], new EmptyRemainingArguments(), "install-local", null);
    }

    private class EmptyRemainingArguments : IRemainingArguments
    {
        public ILookup<string, string?> Parsed => Array.Empty<string>().ToLookup(_ => "", _ => (string?)null);
        public IReadOnlyList<string> Raw => Array.Empty<string>();
    }

    #region ExecuteAsync - Validation Tests

    [Test]
    public async Task ExecuteAsync_NullPackageLocation_Returns1()
    {
        var settings = new InstallLocalPackageSettings { PackageLocation = null };
        var result = await _command.ExecuteAsync(CreateCommandContext(), settings);
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_NonExistentFile_Returns1()
    {
        var settings = new InstallLocalPackageSettings
        {
            PackageLocation = Path.Combine(_tempDir, "nonexistent.pkg.tar.gz")
        };
        var result = await _command.ExecuteAsync(CreateCommandContext(), settings);
        Assert.That(result, Is.EqualTo(1));
    }

    #endregion

    #region IsArchPackage Tests

    [Test]
    public async Task IsArchPackage_GzWithPkginfo_ReturnsTrue()
    {
        var filePath = CreateTarGzWithEntries(_tempDir, "test.tar.gz", [".PKGINFO"]);
        var result = await _command.IsArchPackage(filePath);
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsArchPackage_GzWithoutPkginfo_ReturnsFalse()
    {
        var filePath = CreateTarGzWithEntries(_tempDir, "test.tar.gz", ["readme.txt"]);
        var result = await _command.IsArchPackage(filePath);
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsArchPackage_UnsupportedExtension_ReturnsFalse()
    {
        var filePath = Path.Combine(_tempDir, "test.tar.bz2");
        File.WriteAllBytes(filePath, [0x00]);
        var result = await _command.IsArchPackage(filePath);
        Assert.That(result, Is.False);
    }

    #endregion

    #region HasBinaries Tests

    [Test]
    public async Task HasBinaries_TarGzWithElfBinary_ReturnsTrue()
    {
        var filePath = CreateTarGzWithBinaryEntry(_tempDir, "test.tar.gz", "mybin", [0x7F, 0x45, 0x4C, 0x46, 0x00]);
        var result = await InstallLocalPackage.HasBinaries(filePath);
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task HasBinaries_TarGzWithoutElfBinary_ReturnsFalse()
    {
        var filePath = CreateTarGzWithBinaryEntry(_tempDir, "test.tar.gz", "readme.txt", "hello world"u8.ToArray());
        var result = await InstallLocalPackage.HasBinaries(filePath);
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasBinaries_UnsupportedExtension_Throws()
    {
        var filePath = Path.Combine(_tempDir, "test.tar.bz2");
        File.WriteAllBytes(filePath, [0x00]);
        Assert.ThrowsAsync<NotSupportedException>(async () =>
            await InstallLocalPackage.HasBinaries(filePath));
    }

    [Test]
    public async Task HasBinaries_TarGzWithSmallFile_ReturnsFalse()
    {
        var filePath = CreateTarGzWithBinaryEntry(_tempDir, "test.tar.gz", "tiny", [0x7F, 0x45]);
        var result = await InstallLocalPackage.HasBinaries(filePath);
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task HasBinaries_TarGzWithDirectoryOnly_ReturnsFalse()
    {
        var filePath = CreateTarGzWithDirectoryOnly(_tempDir, "test.tar.gz", "somedir/");
        var result = await InstallLocalPackage.HasBinaries(filePath);
        Assert.That(result, Is.False);
    }

    #endregion

    #region Helper Methods

    private static string CreateTarGzWithEntries(string dir, string fileName, string[] entryNames)
    {
        var filePath = Path.Combine(dir, fileName);
        using var fileStream = File.Create(filePath);
        using var gzStream = new GZipStream(fileStream, CompressionLevel.Fastest);
        using var tarWriter = new TarWriter(gzStream);

        foreach (var name in entryNames)
        {
            var content = "dummy content"u8.ToArray();
            var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
            {
                DataStream = new MemoryStream(content)
            };
            tarWriter.WriteEntry(entry);
        }

        return filePath;
    }

    private static string CreateTarGzWithBinaryEntry(string dir, string fileName, string entryName, byte[] content)
    {
        var filePath = Path.Combine(dir, fileName);
        using var fileStream = File.Create(filePath);
        using var gzStream = new GZipStream(fileStream, CompressionLevel.Fastest);
        using var tarWriter = new TarWriter(gzStream);

        var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = new MemoryStream(content)
        };
        tarWriter.WriteEntry(entry);

        return filePath;
    }

    private static string CreateTarGzWithDirectoryOnly(string dir, string fileName, string dirEntryName)
    {
        var filePath = Path.Combine(dir, fileName);
        using var fileStream = File.Create(filePath);
        using var gzStream = new GZipStream(fileStream, CompressionLevel.Fastest);
        using var tarWriter = new TarWriter(gzStream);

        var entry = new PaxTarEntry(TarEntryType.Directory, dirEntryName);
        tarWriter.WriteEntry(entry);

        return filePath;
    }

    #endregion
}

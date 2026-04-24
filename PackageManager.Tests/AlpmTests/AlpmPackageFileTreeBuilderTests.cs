using System.Runtime.InteropServices;
using PackageManager.Alpm.Package;

namespace PackageManager.Tests.AlpmTests;

[TestFixture]
public class AlpmPackageFileTreeBuilderTests
{
    private readonly List<IntPtr> _allocated = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var p in _allocated)
            Marshal.FreeCoTaskMem(p);
        _allocated.Clear();
    }

    private AlpmFile MakeFile(string? name, uint mode = 0, long size = 0)
    {
        IntPtr ptr = IntPtr.Zero;
        if (name is not null)
        {
            ptr = Marshal.StringToCoTaskMemUTF8(name);
            _allocated.Add(ptr);
        }
        return new AlpmFile { Name = ptr, Mode = mode, Size = size };
    }

    [Test]
    public void BuildTree_EmptyInput_ReturnsEmptyRoot()
    {
        var root = AlpmPackageFileTreeBuilder.BuildTree([]);

        Assert.That(root.Name, Is.EqualTo(""));
        Assert.That(root.Files, Is.Empty);
    }

    [Test]
    public void BuildTree_SingleFileAtRoot_AddedToRoot()
    {
        var files = new[] { MakeFile("readme.txt") };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        Assert.That(root.Files[0].Name, Is.EqualTo("readme.txt"));
        Assert.That(root.Files[0].Files, Is.Empty);
    }

    [Test]
    public void BuildTree_SkipsEntriesWithNullName()
    {
        var files = new[]
        {
            MakeFile(null),
            MakeFile("a.txt"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        Assert.That(root.Files[0].Name, Is.EqualTo("a.txt"));
    }

    [Test]
    public void BuildTree_SkipsEntriesWithEmptyName()
    {
        var files = new[]
        {
            MakeFile(""),
            MakeFile("/"),
            MakeFile("a.txt"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        Assert.That(root.Files[0].Name, Is.EqualTo("a.txt"));
    }

    [Test]
    public void BuildTree_DirectoryWithTrailingSlash_RegisteredAsDirectory()
    {
        var files = new[]
        {
            MakeFile("usr/"),
            MakeFile("usr/bin/"),
            MakeFile("usr/bin/ls"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        var usr = root.Files[0];
        Assert.That(usr.Name, Is.EqualTo("usr"));
        Assert.That(usr.Files, Has.Count.EqualTo(1));

        var bin = usr.Files[0];
        Assert.That(bin.Name, Is.EqualTo("bin"));
        Assert.That(bin.Files, Has.Count.EqualTo(1));
        Assert.That(bin.Files[0].Name, Is.EqualTo("ls"));
    }

    [Test]
    public void BuildTree_DirectoryByMode_RegisteredAsDirectory()
    {
        var files = new[]
        {
            MakeFile("etc", mode: (uint)AlpmFileMode.Directory),
            MakeFile("etc/hosts"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        var etc = root.Files[0];
        Assert.That(etc.Name, Is.EqualTo("etc"));
        Assert.That(etc.Files, Has.Count.EqualTo(1));
        Assert.That(etc.Files[0].Name, Is.EqualTo("hosts"));
    }

    [Test]
    public void BuildTree_MissingAncestorDirectories_AutoCreated()
    {
        // Only a leaf file is given; all intermediate directories must be created.
        var files = new[] { MakeFile("usr/share/doc/readme.md") };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        var usr = root.Files[0];
        Assert.That(usr.Name, Is.EqualTo("usr"));

        Assert.That(usr.Files, Has.Count.EqualTo(1));
        var share = usr.Files[0];
        Assert.That(share.Name, Is.EqualTo("share"));

        Assert.That(share.Files, Has.Count.EqualTo(1));
        var doc = share.Files[0];
        Assert.That(doc.Name, Is.EqualTo("doc"));

        Assert.That(doc.Files, Has.Count.EqualTo(1));
        Assert.That(doc.Files[0].Name, Is.EqualTo("readme.md"));
    }

    [Test]
    public void BuildTree_MultipleSiblings_AllAddedUnderSameParent()
    {
        var files = new[]
        {
            MakeFile("usr/"),
            MakeFile("usr/bin/"),
            MakeFile("usr/bin/ls"),
            MakeFile("usr/bin/cat"),
            MakeFile("usr/bin/cp"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        var bin = root.Files[0].Files[0];
        Assert.That(bin.Name, Is.EqualTo("bin"));
        Assert.That(bin.Files.Select(f => f.Name),
            Is.EquivalentTo(new[] { "ls", "cat", "cp" }));
    }

    [Test]
    public void BuildTree_ExplicitDirectoryBeforeFile_NotDuplicated()
    {
        var files = new[]
        {
            MakeFile("usr/"),
            MakeFile("usr/bin/"),
            MakeFile("usr/bin/ls"),
            MakeFile("usr/lib/"),
            MakeFile("usr/lib/libc.so"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        var usr = root.Files[0];
        Assert.That(usr.Files.Select(f => f.Name),
            Is.EquivalentTo(new[] { "bin", "lib" }));
    }

    [Test]
    public void BuildTree_RootLevelEmptyDirectoryEntry_Skipped()
    {
        // "/" trims to "" and must be ignored.
        var files = new[]
        {
            MakeFile("/"),
            MakeFile("file"),
        };

        var root = AlpmPackageFileTreeBuilder.BuildTree(files);

        Assert.That(root.Files, Has.Count.EqualTo(1));
        Assert.That(root.Files[0].Name, Is.EqualTo("file"));
    }
}

using System.Runtime.InteropServices;
using PackageManager.Alpm.Package;

namespace PackageManager.Tests.AlpmTests;

[TestFixture]
public class AlpmFileListMarshallerTests
{
    private readonly List<IntPtr> _hGlobal = [];
    private readonly List<IntPtr> _coTask = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var p in _hGlobal) Marshal.FreeHGlobal(p);
        _hGlobal.Clear();
        foreach (var p in _coTask) Marshal.FreeCoTaskMem(p);
        _coTask.Clear();
    }

    private IntPtr AllocName(string name)
    {
        var p = Marshal.StringToCoTaskMemUTF8(name);
        _coTask.Add(p);
        return p;
    }

    private IntPtr AllocFileList(AlpmFile[] files)
    {
        var fileSize = Marshal.SizeOf<AlpmFile>();
        IntPtr filesPtr = IntPtr.Zero;
        if (files.Length > 0)
        {
            filesPtr = Marshal.AllocHGlobal(fileSize * files.Length);
            _hGlobal.Add(filesPtr);
            for (int i = 0; i < files.Length; i++)
            {
                Marshal.StructureToPtr(files[i], IntPtr.Add(filesPtr, i * fileSize), false);
            }
        }

        var listPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AlpmFileList>());
        _hGlobal.Add(listPtr);
        var list = new AlpmFileList { Count = (nuint)files.Length, Files = filesPtr };
        Marshal.StructureToPtr(list, listPtr, false);
        return listPtr;
    }

    [Test]
    public void Enumerate_NullPtr_ReturnsEmpty()
    {
        Assert.That(AlpmFileListMarshaller.Enumerate(IntPtr.Zero), Is.Empty);
    }

    [Test]
    public void Enumerate_ZeroCount_ReturnsEmpty()
    {
        var listPtr = AllocFileList([]);
        Assert.That(AlpmFileListMarshaller.Enumerate(listPtr), Is.Empty);
    }

    [Test]
    public void Enumerate_Single_ReturnsOneFile()
    {
        var name = AllocName("usr/bin/ls");
        var listPtr = AllocFileList([
            new AlpmFile { Name = name, Size = 42, Mode = 0 },
        ]);

        var result = AlpmFileListMarshaller.Enumerate(listPtr).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(Marshal.PtrToStringUTF8(result[0].Name), Is.EqualTo("usr/bin/ls"));
        Assert.That(result[0].Size, Is.EqualTo(42));
    }

    [Test]
    public void Enumerate_Multiple_ReturnsAllInOrder()
    {
        var n1 = AllocName("a");
        var n2 = AllocName("b");
        var n3 = AllocName("c");
        var listPtr = AllocFileList([
            new AlpmFile { Name = n1 },
            new AlpmFile { Name = n2 },
            new AlpmFile { Name = n3 },
        ]);

        var names = AlpmFileListMarshaller.Enumerate(listPtr)
            .Select(f => Marshal.PtrToStringUTF8(f.Name))
            .ToList();

        Assert.That(names, Is.EqualTo(new[] { "a", "b", "c" }));
    }
}

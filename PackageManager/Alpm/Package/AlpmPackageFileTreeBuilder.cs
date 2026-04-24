using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace PackageManager.Alpm.Package;

public static class AlpmPackageFileTreeBuilder
{
    public static AlpmPackageTreeDto BuildTree(IEnumerable<AlpmFile> files)
    {
        var root = new AlpmPackageTreeDto("");
        var dirs = new Dictionary<string, AlpmPackageTreeDto>(StringComparer.Ordinal)
        {
            [""] = root,
        };

        foreach (var f in files)
        {
            var raw = f.Name != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(f.Name) ?? ""
                : "";

            if (raw.Length == 0) continue;
            var isDir = raw.EndsWith('/')
                        || (f.Mode & 0xF000u) == (uint)AlpmFileMode.Directory;
            var path = raw.TrimEnd('/');
            if (path.Length == 0) continue;

            // Ensure every ancestor directory exists in the tree.
            var parent = EnsureDirectory(GetParent(path), dirs, root);

            var leaf = GetLeaf(path);
            var node = new AlpmPackageTreeDto(leaf);
            parent.Files.Add(node);

            if (isDir)
                dirs[path] = node;
        }

        return root;
    }

    private static AlpmPackageTreeDto EnsureDirectory(
        string path,
        Dictionary<string, AlpmPackageTreeDto> dirs,
        AlpmPackageTreeDto root)
    {
        if (path.Length == 0) return root;
        if (dirs.TryGetValue(path, out var existing)) return existing;

        var parent = EnsureDirectory(GetParent(path), dirs, root);
        var node = new AlpmPackageTreeDto(GetLeaf(path));
        parent.Files.Add(node);
        dirs[path] = node;
        return node;
    }

    private static string GetParent(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? "" : path[..i];
    }

    private static string GetLeaf(string path)
    {
        var i = path.LastIndexOf('/');
        return i < 0 ? path : path[(i + 1)..];
    }
}
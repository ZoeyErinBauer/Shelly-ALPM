using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

public class VcsInfoStore
{
    private readonly string _storePath;
    private Dictionary<string, List<VcsSourceEntry>> _entries = new();

    public VcsInfoStore()
    {
        var user = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        var home = $"/home/{user}";
        _storePath = Path.Combine(home, ".local", "share", "Shelly", "vcs.json");
    }

    public async Task Load()
    {
        if (!File.Exists(_storePath))
        {
            _entries = new Dictionary<string, List<VcsSourceEntry>>();
            return;
        }

        await using var stream = File.OpenRead(_storePath);
        _entries = await JsonSerializer.DeserializeAsync(
            stream,
            AurJsonContext.Default.DictionaryStringListVcsSourceEntry
        ) ?? new Dictionary<string, List<VcsSourceEntry>>();
    }

    public async Task Save()
    {
        var dir = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(dir);

        await using var stream = File.Create(_storePath);
        await JsonSerializer.SerializeAsync(
            stream,
            _entries,
            AurJsonContext.Default.DictionaryStringListVcsSourceEntry
        );
    }

    public List<VcsSourceEntry>? GetEntries(string packageName)
        => _entries.TryGetValue(packageName, out var entries) ? entries : null;

    public void SetEntries(string packageName, List<VcsSourceEntry> entries)
        => _entries[packageName] = entries;

    public void RemovePackage(string packageName)
        => _entries.Remove(packageName);

    public void Clean(IEnumerable<string> installedPackageNames)
    {
        var installed = new HashSet<string>(installedPackageNames);
        var orphaned = _entries.Keys.Where(k => !installed.Contains(k)).ToList();
        foreach (var key in orphaned)
            _entries.Remove(key);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PackageManager.Aur.Models;

namespace PackageManager.Aur;

public static class VcsSourceParser
{
    public static VcsSourceEntry? ParseSource(string sourceEntry)
    {
        if (string.IsNullOrWhiteSpace(sourceEntry))
            return null;

        var entry = sourceEntry.Trim();

        var colonColonIndex = entry.IndexOf("::", StringComparison.Ordinal);
        if (colonColonIndex >= 0)
        {
            entry = entry[(colonColonIndex + 2)..];
        }

        var protocols = new List<string>();
        var url = entry;

        var schemeEnd = entry.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return null;

        var schemePart = entry[..schemeEnd];
        url = entry;

        protocols.AddRange(schemePart.Split('+', StringSplitOptions.RemoveEmptyEntries));

        if (!protocols.Contains("git", StringComparer.OrdinalIgnoreCase))
            return null;

        if (url.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
        {
            url = url[4..];
        }

        string branch = "HEAD";
        var fragmentIndex = url.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            var fragment = url[(fragmentIndex + 1)..];
            url = url[..fragmentIndex];

            if (fragment.StartsWith("commit=", StringComparison.OrdinalIgnoreCase) ||
                fragment.StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (fragment.StartsWith("branch=", StringComparison.OrdinalIgnoreCase))
            {
                branch = fragment.Split('=', 2)[1];
            }
        }

        var queryIndex = url.IndexOf('?');
        if (queryIndex >= 0)
        {
            url = url[..queryIndex];
        }

        return new VcsSourceEntry
        {
            Url = url,
            Branch = branch,
            Protocols = protocols.Where(p => !p.Equals("git", StringComparison.OrdinalIgnoreCase)).ToList(),
            CommitSha = string.Empty
        };
    }

    public static List<VcsSourceEntry> ParseSources(IEnumerable<string> sources)
    {
        var results = new List<VcsSourceEntry>();
        foreach (var source in sources)
        {
            var parsed = ParseSource(source);
            if (parsed != null)
                results.Add(parsed);
        }
        return results;
    }
}

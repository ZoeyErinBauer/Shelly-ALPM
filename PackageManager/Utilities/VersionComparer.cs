using System;
using System.Text.RegularExpressions;

namespace PackageManager.Utilities;

/// <summary>
/// Compares package versions following common versioning schemes including:
/// - Semantic versioning (1.2.3)
/// - Epoch prefixes (1:2.3.4)
/// - Release suffixes (-1, -2)
/// - Pre-release tags (alpha, beta, rc)
/// </summary>
public static class VersionComparer
{
    /// <summary>
    /// Compares two version strings.
    /// </summary>
    /// <param name="version1">First version string</param>
    /// <param name="version2">Second version string</param>
    /// <returns>
    /// -1 if version1 &lt; version2,
    /// 0 if version1 == version2,
    /// 1 if version1 &gt; version2
    /// </returns>
    public static int Compare(string? version1, string? version2)
    {
        if (string.IsNullOrEmpty(version1) && string.IsNullOrEmpty(version2))
            return 0;
        if (string.IsNullOrEmpty(version1))
            return -1;
        if (string.IsNullOrEmpty(version2))
            return 1;

        // Parse epoch (e.g., "1:2.3.4" -> epoch=1, rest="2.3.4")
        var (epoch1, rest1) = ParseEpoch(version1);
        var (epoch2, rest2) = ParseEpoch(version2);

        var epochCompare = epoch1.CompareTo(epoch2);
        if (epochCompare != 0)
            return epochCompare;

        // Parse pkgrel (e.g., "2.3.4-1" -> version="2.3.4", pkgrel="1")
        var (ver1, pkgrel1) = ParsePkgrel(rest1);
        var (ver2, pkgrel2) = ParsePkgrel(rest2);

        var versionCompare = CompareVersionParts(ver1, ver2);
        if (versionCompare != 0)
            return versionCompare;

        return CompareVersionParts(pkgrel1, pkgrel2);
    }

    /// <summary>
    /// Checks if version1 is newer than version2.
    /// </summary>
    public static bool IsNewer(string? version1, string? version2) => Compare(version1, version2) > 0;

    /// <summary>
    /// Checks if version1 is older than version2.
    /// </summary>
    public static bool IsOlder(string? version1, string? version2) => Compare(version1, version2) < 0;

    /// <summary>
    /// Checks if two versions are equal.
    /// </summary>
    public static bool AreEqual(string? version1, string? version2) => Compare(version1, version2) == 0;

    private static (int epoch, string rest) ParseEpoch(string version)
    {
        var colonIndex = version.IndexOf(':');
        if (colonIndex > 0 && int.TryParse(version[..colonIndex], out var epoch))
        {
            return (epoch, version[(colonIndex + 1)..]);
        }
        return (0, version);
    }

    private static (string version, string pkgrel) ParsePkgrel(string version)
    {
        // Find the last hyphen that separates version from pkgrel
        var lastHyphen = version.LastIndexOf('-');
        if (lastHyphen > 0)
        {
            return (version[..lastHyphen], version[(lastHyphen + 1)..]);
        }
        return (version, "0");
    }

    private static int CompareVersionParts(string v1, string v2)
    {
        var parts1 = SplitVersion(v1);
        var parts2 = SplitVersion(v2);

        var maxLen = Math.Max(parts1.Length, parts2.Length);
        for (var i = 0; i < maxLen; i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : "";
            var p2 = i < parts2.Length ? parts2[i] : "";

            var cmp = ComparePart(p1, p2);
            if (cmp != 0)
                return cmp;
        }
        return 0;
    }

    private static string[] SplitVersion(string version)
    {
        // Split on dots, separators, and transitions between digits and letters
        var parts = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        bool? wasDigit = null;

        foreach (var c in version)
        {
            if (c == '.' || c == '-' || c == '_' || c == '+')
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                wasDigit = null;
                continue;
            }

            var isDigit = char.IsDigit(c);
            if (wasDigit.HasValue && wasDigit.Value != isDigit && current.Length > 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            current.Append(c);
            wasDigit = isDigit;
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts.ToArray();
    }

    private static int ComparePart(string p1, string p2)
    {
        if (string.IsNullOrEmpty(p1) && string.IsNullOrEmpty(p2))
            return 0;
        
        // Empty vs pre-release: empty (release) is greater than pre-release
        if (string.IsNullOrEmpty(p1))
        {
            return GetPreReleaseOrder(p2) >= 0 ? 1 : -1;
        }
        if (string.IsNullOrEmpty(p2))
        {
            return GetPreReleaseOrder(p1) >= 0 ? -1 : 1;
        }

        // Try numeric comparison first
        var isNum1 = long.TryParse(p1, out var num1);
        var isNum2 = long.TryParse(p2, out var num2);

        if (isNum1 && isNum2)
            return num1.CompareTo(num2);

        // If one is numeric and one isn't, numeric is greater (except for pre-release tags)
        if (isNum1 != isNum2)
        {
            // Pre-release tags (alpha, beta, rc) are less than numeric
            var preRelease1 = GetPreReleaseOrder(p1);
            var preRelease2 = GetPreReleaseOrder(p2);

            if (preRelease1 >= 0 || preRelease2 >= 0)
            {
                if (preRelease1 >= 0 && preRelease2 >= 0)
                    return preRelease1.CompareTo(preRelease2);
                // Pre-release is less than release
                return preRelease1 >= 0 ? -1 : 1;
            }

            return isNum1 ? 1 : -1;
        }

        // Both are strings - check for pre-release tags
        var pre1 = GetPreReleaseOrder(p1);
        var pre2 = GetPreReleaseOrder(p2);

        if (pre1 >= 0 && pre2 >= 0)
            return pre1.CompareTo(pre2);
        if (pre1 >= 0)
            return -1;
        if (pre2 >= 0)
            return 1;

        // Fall back to string comparison
        return string.Compare(p1, p2, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPreReleaseOrder(string part)
    {
        var lower = part.ToLowerInvariant();
        
        // Extract the base tag (e.g., "alpha1" -> "alpha", "rc2" -> "rc")
        var match = Regex.Match(lower, @"^(alpha|beta|rc|pre|dev|snapshot|git|svn|cvs|hg)(\d*)$");
        if (!match.Success)
            return -1;

        var tag = match.Groups[1].Value;
        return tag switch
        {
            "dev" => 0,
            "snapshot" => 1,
            "git" or "svn" or "cvs" or "hg" => 2,
            "alpha" => 3,
            "beta" => 4,
            "pre" => 5,
            "rc" => 6,
            _ => -1
        };
    }
}

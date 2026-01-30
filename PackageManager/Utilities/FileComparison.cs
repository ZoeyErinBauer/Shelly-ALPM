using System;
using System.IO;
using System.Security.Cryptography;

namespace PackageManager.Utilities;

/// <summary>
/// File comparison utilities for determining when files need replacement during sync operations.
/// </summary>
public static class FileComparison
{
    /// <summary>
    /// Checks if the new file differs from the current file and replacement is needed.
    /// Performs a fast size comparison first, then falls back to MD5 hash comparison if sizes match.
    /// </summary>
    /// <param name="currentFilePath">Path to the existing local file.</param>
    /// <param name="newFilePath">Path to the newly downloaded file.</param>
    /// <returns><c>true</c> if files differ and replacement is needed; <c>false</c> if files are identical.</returns>
    public static bool DoFileReplace(string currentFilePath, string newFilePath)
    {
        // If current file doesn't exist, replacement is always needed
        if (!File.Exists(currentFilePath))
            return true;
    
        var currentFile = new FileInfo(currentFilePath);
        var newFile = new FileInfo(newFilePath);

        if (IsFilesSameSize(currentFile, newFile))
        {
            // Same size - compare hashes to detect content differences
            return ComputeFileHash(currentFilePath) != ComputeFileHash(newFilePath);
        }

        // Different sizes - files are definitely different
        return true;
    }

    /// <summary>
    /// Checks if two files have the same size.
    /// </summary>
    private static bool IsFilesSameSize(FileInfo currentFile, FileInfo newFile)
    {
        return currentFile.Length == newFile.Length;
    }

    /// <summary>
    /// Computes MD5 hash of a file. Used for change detection, not security.
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = md5.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}
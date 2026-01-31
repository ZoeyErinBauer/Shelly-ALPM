using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using PackageManager.Utilities;

namespace PackageManager.Alpm;

public partial class AlpmManager
{
    private int DownloadFile(IntPtr ctx, IntPtr urlPtr, IntPtr localpathPtr, int force)
    {
        try
        {
            string? url = Marshal.PtrToStringUTF8(urlPtr);
            string? localpathDir = null;

            if (localpathPtr != IntPtr.Zero)
            {
                try
                {
                    localpathDir = Marshal.PtrToStringUTF8(localpathPtr);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine("[DEBUG_LOG] localpathPtr points to invalid memory");
                }
            }

            Console.Error.WriteLine(
                $"[DEBUG_LOG] DownloadFile called with url='{url}', localpath='{localpathDir}', force={force}");

            if (string.IsNullOrEmpty(url)) return -1;

            // Extract filename from URL
            string fileName;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                fileName = Path.GetFileName(uri.LocalPath);
            }
            else
            {
                fileName = Path.GetFileName(url);
            }

            // Construct full destination path
            string localpath;
            if (!string.IsNullOrEmpty(localpathDir))
            {
                // localpath from fetchcb is a DIRECTORY, combine with filename
                localpath = Path.Combine(localpathDir, fileName);
            }
            else
            {
                // Fallback: determine directory based on file type
                if (url.EndsWith(".db") || url.EndsWith(".db.sig"))
                {
                    localpath = Path.Combine(_config.DbPath, "sync", fileName);
                }
                else
                {
                    localpath = Path.Combine(_config.CacheDir, fileName);
                }
            }

            Console.Error.WriteLine($"[DEBUG_LOG] Full destination path: {localpath}");

            if (string.IsNullOrEmpty(localpath)) return -1;

            var directory = Path.GetDirectoryName(localpath);
            if (directory != null) Directory.CreateDirectory(directory);

            // URL should already be absolute from fetchcb
            return PerformDownload(url, localpath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DEBUG_LOG] Download failed: {ex.Message}");
            Console.Error.WriteLine($"[DEBUG_LOG] Stack trace: {ex.StackTrace}");
            return -1;
        }
    }


    private int PerformDownload(string fullUrl, string localpath)
    {
        // Use a temporary file for atomic writes - prevents corruption if download is interrupted
        string tempPath = localpath + ".part";
        Console.Error.WriteLine($"[DEBUG_LOG] Using temp file {tempPath}");

        try
        {
            Console.Error.WriteLine($"[Shelly][DEBUG_LOG] Downloading {fullUrl} to {localpath}");

            using var response = HttpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseContentRead)
                .GetAwaiter()
                .GetResult();


            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"[DEBUG_LOG] Failed to download {fullUrl}: {response.StatusCode}");
                return -1;
            }

            var totalBytes = response.Content.Headers.ContentLength;
            string fileName = Path.GetFileName(localpath);

            // Write to temporary file first
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = response.Content.ReadAsStream())
            {
                Console.Error.WriteLine($"[DEBUG_LOG] Reading content stream");
                byte[] buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;
                int lastPercent = -1;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int percent = (int)((totalRead * 100) / totalBytes.Value);
                        if (percent != lastPercent)
                        {
                            Console.Error.WriteLine($"[DEBUG_LOG] Download progress: {percent}%");
                            Console.Error.WriteLine(
                                $"[DEBUG_LOG] Download progress: {totalRead} / {totalBytes.Value} bytes");
                            lastPercent = percent;
                            Progress?.Invoke(this, new AlpmProgressEventArgs(
                                AlpmProgressType.PackageDownload,
                                fileName,
                                percent,
                                (ulong)totalBytes.Value,
                                (ulong)totalRead
                            ));
                        }
                    }
                }

                // Ensure 100% is sent
                if (lastPercent != 100)
                {
                    Console.Error.WriteLine($"[DEBUG_LOG] Download progress: 100% (.)(.)");
                    Progress?.Invoke(this, new AlpmProgressEventArgs(
                        AlpmProgressType.PackageDownload,
                        fileName,
                        100,
                        (ulong)(totalBytes ?? (long)totalRead),
                        (ulong)totalRead
                    ));
                }
            }

            //Compares files to determine if a replacement is needed
            if (!FileComparison.DoFileReplace(localpath, tempPath))
            {
                // Files are identical, clean up temp file
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    Console.Error.WriteLine($"[DEBUG_LOG] Failed to delete temp file: {tempPath}");
                }
            
                return 0;
            }

            // Atomic rename: move temp file to final destination only after successful download
            try
            {
                File.Move(tempPath, localpath, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DEBUG_LOG] Failed to move temp file: {ex.Message}");
                Console.Error.WriteLine($"[DEBUG_LOG] Source: {tempPath}, Exists: {File.Exists(tempPath)}");
                Console.Error.WriteLine($"[DEBUG_LOG] Destination: {localpath}");
                Console.Error.WriteLine(
                    $"[DEBUG_LOG] Dest dir exists: {Directory.Exists(Path.GetDirectoryName(localpath))}");
                return -1;
            }

            // If we just downloaded a .db file, also download the corresponding .db.sig file
            // This ensures database and signature files stay in sync, preventing "signature invalid" errors
            Console.Error.WriteLine($"[DEBUG_LOG] Downloading corresponding signature file: {fullUrl}.sig");
            Console.Error.WriteLine($"[DEBUG_LOG] Destination: {localpath}.sig");
            if (fullUrl.EndsWith(".db") && !fullUrl.EndsWith(".db.sig"))
            {
                var sigUrl = fullUrl + ".sig";
                var sigLocalPath = localpath + ".sig";
                Console.Error.WriteLine($"[DEBUG_LOG] Downloading corresponding signature file: {sigUrl}");
                DownloadSignatureFile(sigUrl, sigLocalPath);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DEBUG_LOG] Download failed for {fullUrl}: {ex.Message}");
            // Clean up temp file on failure to prevent leaving partial files
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                /* Ignore cleanup errors */
            }

            return -1;
        }
    }

    /// <summary>
    /// Downloads a signature file (.sig) for a database file.
    /// This is called automatically when a .db file is downloaded to ensure
    /// the signature file stays in sync with the database file.
    /// Failures are logged but don't cause the main download to fail.
    /// </summary>
    private void DownloadSignatureFile(string sigUrl, string sigLocalPath)
    {
        string tempPath = sigLocalPath + ".part";
        try
        {
            Console.Error.WriteLine($"[Shelly][DEBUG_LOG] Downloading signature {sigUrl}");

            using var response = HttpClient.GetAsync(sigUrl, HttpCompletionOption.ResponseContentRead)
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode)
            {
                // Signature file may not exist on the server (optional), just log and continue
                Console.Error.WriteLine($"[DEBUG_LOG] Signature file not available: {sigUrl} ({response.StatusCode})");
                // Delete any existing stale signature file to prevent mismatch
                try
                {
                    File.Delete(sigLocalPath);
                }
                catch
                {
                    /* ignore */
                }

                return;
            }

            // Write to temporary file first
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = response.Content.ReadAsStream())
            {
                stream.CopyTo(fs);
            }

            // Move temp file to final destination
            File.Move(tempPath, sigLocalPath, overwrite: true);
            Console.Error.WriteLine($"[DEBUG_LOG] Signature file downloaded successfully: {sigLocalPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DEBUG_LOG] Failed to download signature file {sigUrl}: {ex.Message}");
            // Clean up temp file on failure
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                /* ignore */
            }

            // Delete any existing stale signature file to prevent mismatch
            try
            {
                File.Delete(sigLocalPath);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PackageManager.Alpm;
using SharpCompress.Compressors.Xz;
using Spectre.Console;
using Spectre.Console.Cli;
using ZstdSharp;

namespace Shelly_CLI.Commands.Standard;

public class InstallLocalPackageCommand : AsyncCommand<InstallLocalPackageSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InstallLocalPackageSettings settings)
    {
        //Validate the file location and that a file is actually passed in
        if (settings.PackageLocation == null)
        {
            AnsiConsole.MarkupLine("[red]Error: No package specified[/]");
            return 1;
        }

        if (!File.Exists(settings.PackageLocation))
        {
            AnsiConsole.MarkupLine("[red]Error: Specified file does not exist.[/]");
            return 1;
        }

        if (await IsArchPackage(settings.PackageLocation))
        {
            InitializeAndInstallLocalAlpmPackage(settings);
            return 0;
        }

        //check if binary and install to /opt/ and create symlink in /bin
        if (await HasBinaries(settings.PackageLocation))
        {
            //install local files to /opt/{folder here} and then create symlink to /usr/bin
            return await InstallLocalBinaries(settings);
        }

        return 0;
    }

    private static async Task<int> InstallLocalBinaries(InstallLocalPackageSettings settings)
    {
        var filePath = settings.PackageLocation!;
        var extension = Path.GetExtension(filePath);

        // Create install directory under /opt/
        var packageName = Path.GetFileName(filePath)
            .Replace(".pkg.tar" + extension, "")
            .Replace(".tar" + extension, "");
        var installDir = Path.Combine("/opt", packageName);
        Directory.CreateDirectory(installDir);

        var installedBinaries = new List<string>();
        var foundIcons = new Dictionary<string, string>();

        await using var fileStream = File.OpenRead(filePath);
        Stream decompressedStream = extension switch
        {
            ".gz" => new GZipStream(fileStream, CompressionMode.Decompress),
            ".xz" => new XZStream(fileStream),
            ".zst" => new ZstdStream(fileStream, ZstdStreamMode.Decompress),
            _ => throw new NotSupportedException($"Unsupported compression: {extension}")
        };

        await using (decompressedStream)
        await using (var tarReader = new TarReader(decompressedStream))
        {
            while (await tarReader.GetNextEntryAsync() is { } entry)
            {
                var destPath = Path.Combine(installDir, entry.Name);

                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(destPath);
                        break;

                    case TarEntryType.RegularFile:
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        await entry.ExtractToFileAsync(destPath, overwrite: true);

                        var ext = Path.GetExtension(destPath).ToLower();

                        if (ext is ".png" or ".svg")
                        {
                            var iconFileName = Path.GetFileNameWithoutExtension(destPath).ToLower();
                            foundIcons[iconFileName] = destPath;
                        }

                        // Check if it's an ELF binary and create symlink in /usr/bin
                        if (entry.DataStream is not null)
                        {
                            await using var fs = File.OpenRead(destPath);
                            var magic = new byte[4];
                            var bytesRead = await fs.ReadAsync(magic);
                            if (bytesRead >= 4 &&
                                magic[0] == 0x7F && magic[1] == 0x45 &&
                                magic[2] == 0x4C && magic[3] == 0x46)
                            {
                                var binaryName = Path.GetFileName(destPath);
                                var linkPath = Path.Combine("/usr/bin", binaryName);
                                if (File.Exists(linkPath)) File.Delete(linkPath);
                                File.CreateSymbolicLink(linkPath, destPath);

                                installedBinaries.Add(binaryName);
                            }
                        }

                        break;

                    case TarEntryType.SymbolicLink:
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        if (File.Exists(destPath)) File.Delete(destPath);
                        File.CreateSymbolicLink(destPath, entry.LinkName);
                        break;
                }
            }
        }

        AnsiConsole.MarkupLine($"[green]Extracted to {installDir}[/]");

        foreach (var binaryName in installedBinaries)
        {
            var iconName = "application-x-executable";
            
            if (packageName.Contains(binaryName))
            {
                if (foundIcons.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[cyan]Found icon for {binaryName}: {foundIcons.FirstOrDefault().Key}[/]");
                    var installedIconName = InstallIcon(foundIcons.FirstOrDefault().Value, binaryName);
                    if (installedIconName != null)
                    {
                        iconName = installedIconName;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]No icon found for {binaryName}, using default[/]");
                }
                Console.WriteLine("Creating desktop entry...");
                CreateDesktopEntry(
                    appName: binaryName,
                    executablePath: binaryName,
                    comment: $"{binaryName} - Installed from {packageName}",
                    icon: iconName,
                    terminal: false,
                    categories: "Utility;"
                );
            }
        }

        AnsiConsole.MarkupLine($"[green]Desktop Entries Created[/]");

        return 0;
    }

    internal static async Task<bool> HasBinaries(string filePath)
    {
        var fileStream = File.OpenRead(filePath);
        var extension = Path.GetExtension(filePath);
        Stream decompressedStream = extension switch
        {
            ".gz" => new GZipStream(fileStream, CompressionMode.Decompress),
            ".xz" => new XZStream(fileStream),
            ".zst" => new ZstdStream(fileStream, ZstdStreamMode.Decompress),
            _ => throw new NotSupportedException($"Unsupported file extension")
        };
        var tarReader = new TarReader(decompressedStream);
        while (await tarReader.GetNextEntryAsync() is { } entry)
        {
            if (entry.EntryType != TarEntryType.RegularFile)
            {
                continue;
            }

            if (entry.DataStream is not null)
            {
                var magic = new byte[4];
                var bytesRead = await entry.DataStream.ReadAsync(magic);

                if (bytesRead >= 4 &&
                    magic[0] == 0x7F && magic[1] == 0x45 &&
                    magic[2] == 0x4C && magic[3] == 0x46)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void InitializeAndInstallLocalAlpmPackage(InstallLocalPackageSettings settings)
    {
        object renderLock = new();
        var isPaused = false;
        var manager = new AlpmManager();
        var progressTable = new Table().AddColumns("Package", "Progress", "Status", "Stage");
        AnsiConsole.Live(progressTable).AutoClear(false)
            .Start(ctx =>
            {
                var rowIndex = new Dictionary<string, int>();
                manager.Progress += (sender, args) =>
                {
                    lock (renderLock)
                    {
                        var name = args.PackageName ?? "unknown";
                        var pct = args.Percent ?? 0;
                        var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
                        var actionType = args.ProgressType;

                        if (!rowIndex.TryGetValue(name, out var idx))
                        {
                            progressTable.AddRow(
                                $"[blue]{Markup.Escape(name)}[/]",
                                $"[green]{bar}[/]",
                                $"{pct}%",
                                $"{actionType}"
                            );
                            rowIndex[name] = rowIndex.Count;
                        }
                        else
                        {
                            progressTable.UpdateCell(idx, 1, $"[green]{bar}[/]");
                            progressTable.UpdateCell(idx, 2, $"{pct}%");
                        }
                    }

                    ctx.Refresh();
                };
            });
        manager.Question += (sender, args) =>
        {
            lock (renderLock)
            {
                // Handle SelectProvider and ConflictPkg differently - they need a selection, not yes/no
                if ((args.QuestionType == AlpmQuestionType.SelectProvider ||
                     args.QuestionType == AlpmQuestionType.ConflictPkg) 
                    && args.ProviderOptions?.Count > 0)
                {
                    if (settings.NoConfirm)
                    {
                        if (Program.IsUiMode)
                        {
                            if (args.QuestionType == AlpmQuestionType.ConflictPkg)
                            {
                                // Dedicated conflict protocol for UI integration
                                Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT]{args.QuestionText}");
                                for (var i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[Shelly][ALPM_CONFLICT_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[Shelly][ALPM_CONFLICT_END]");
                            }
                            else
                            {
                                // Machine-readable format for UI integration
                                Console.Error.WriteLine($"[Shelly][ALPM_SELECT_PROVIDER]{args.DependencyName}");
                                for (var i = 0; i < args.ProviderOptions.Count; i++)
                                {
                                    Console.Error.WriteLine($"[Shelly][ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                                }

                                Console.Error.WriteLine("[Shelly][ALPM_PROVIDER_END]");
                            }
                            Console.Error.Flush();
                            var input = Console.ReadLine();
                            args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
                        }
                        else
                        {
                            // Non-interactive CLI mode: default to the first provider
                            args.Response = 0;
                        }
                    }
                    else
                    {
                        var selection = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title($"[yellow]{args.QuestionText}[/]")
                                .AddChoices(args.ProviderOptions));
                        args.Response = args.ProviderOptions.IndexOf(selection);
                    }
                }
                else if (settings.NoConfirm)
                {
                    if (Program.IsUiMode)
                    {
                        // Machine-readable format for UI integration
                        Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                        Console.Error.Flush();
                        var input = Console.ReadLine();
                        args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true
                            ? 1
                            : 0;
                    }
                    else
                    {
                        // Non-interactive CLI mode: automatically confirm
                        args.Response = 1;
                    }
                }
                else
                {
                    var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                    args.Response = response ? 1 : 0;
                }
            }
        };

        AnsiConsole.MarkupLine("[yellow]Initializing ALPM...[/]");
        manager.Initialize();
        manager.InstallLocalPackage(Path.GetFullPath(settings.PackageLocation!));
        manager.Dispose();
    }

    internal async Task<bool> IsArchPackage(string filePath)
    {
        var isArch = false;
        var fileStream = File.OpenRead(filePath);
        var extenstion = Path.GetExtension(filePath);
        switch (extenstion)
        {
            case ".zst":
                var zStdStream = new ZstdStream(fileStream, ZstdStreamMode.Decompress);
                var zstTarReader = new TarReader(zStdStream);
                while (await zstTarReader.GetNextEntryAsync() is { } entry)
                {
                    if (entry.Name.Contains("PKGINFO", StringComparison.InvariantCultureIgnoreCase))
                    {
                        isArch = true;
                        break;
                    }
                }

                await zstTarReader.DisposeAsync();
                await zStdStream.DisposeAsync();
                await fileStream.DisposeAsync();
                break;
            case ".xz":
                var xzStream = new XZStream(fileStream);
                var xzTarReader = new TarReader(xzStream);
                while (await xzTarReader.GetNextEntryAsync() is { } entry)
                {
                    if (entry.Name.Contains("PKGINFO", StringComparison.InvariantCultureIgnoreCase))
                    {
                        isArch = true;
                        break;
                    }
                }

                await xzTarReader.DisposeAsync();
                await xzTarReader.DisposeAsync();
                await fileStream.DisposeAsync();
                break;
            case ".gz":
                var gzStream = new GZipStream(fileStream, CompressionMode.Decompress);
                var gzTarReader = new TarReader(gzStream);
                while (await gzTarReader.GetNextEntryAsync() is { } entry)
                {
                    if (entry.Name.Contains("PKGINFO", StringComparison.InvariantCultureIgnoreCase))
                    {
                        isArch = true;
                        break;
                    }
                }

                await gzTarReader.DisposeAsync();
                await gzStream.DisposeAsync();
                await fileStream.DisposeAsync();
                break;
        }


        return isArch;
    }

    private static void CreateDesktopEntry(
        string appName,
        string executablePath,
        string? comment = null,
        string icon = "application-x-executable",
        bool terminal = false,
        string categories = "Utility;")
    {
        const string desktopDir = "/usr/share/applications";
        var cleanName = CleanInvalidNames(appName);
        var desktopFilePath = Path.Combine(desktopDir, $"{cleanName}.desktop");

        var content = new StringBuilder();
        content.AppendLine("[Desktop Entry]");
        content.AppendLine("Version=1.0");
        content.AppendLine("Type=Application");
        content.AppendLine($"Name={appName}");
        content.AppendLine($"Comment={comment ?? $"{appName} application"}");
        content.AppendLine($"Exec={executablePath}");
        content.AppendLine($"Icon={icon}");
        content.AppendLine($"Terminal={terminal.ToString().ToLower()}");
        content.AppendLine($"Categories={categories}");
        content.AppendLine("StartupNotify=true");

        try
        {
            Directory.CreateDirectory(desktopDir);
            File.WriteAllText(desktopFilePath, content.ToString());
            SetFilePermissions(desktopFilePath, "644");
            UpdateDesktopDatabase(desktopDir);

            AnsiConsole.MarkupLine($"[green]Desktop entry created: {desktopFilePath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not create desktop entry: {ex.Message}[/]");
        }
    }

    private static string CleanInvalidNames(string name)
    {
        return name.ToLower()
            .Replace(" ", "-")
            .Replace("/", "-")
            .Replace("\\", "-");
    }

    private static void SetFilePermissions(string filePath, string permissions)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{permissions} \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not set file permissions: {ex.Message}[/]");
        }
    }

    private static void UpdateDesktopDatabase(string desktopDir)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "update-desktop-database",
                Arguments = $"\"{desktopDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not set desktop database: {ex.Message}[/]");
        }
    }

    private static string? InstallIcon(string iconPath, string appName)
    {
        try
        {
            var extension = Path.GetExtension(iconPath);
            var iconName = $"{appName.ToLower()}{extension}";
            string destDir;
            if (extension == ".svg")
            {
                destDir = "/usr/share/icons/hicolor/scalable/apps";
            }
            else
            {
                var sizeMatch = Regex.Match(Path.GetFileName(iconPath), @"(\d+)x?\d*");
                var size = sizeMatch.Success && int.TryParse(sizeMatch.Groups[1].Value, out var s)
                    ? s
                    : 256;
                destDir = $"/usr/share/icons/hicolor/{size}x{size}/apps";
            }

            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, iconName);

            File.Copy(iconPath, destPath, overwrite: true);

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "gtk-update-icon-cache",
                    Arguments = "-f -t /usr/share/icons/hicolor",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to update icon cache: {ex.Message}[/]");
            }

            return appName.ToLower();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not install icon: {ex.Message}[/]");
            return null;
        }
    }
}
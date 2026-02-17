using System.Formats.Tar;
using System.IO.Compression;
using PackageManager.Alpm;
using SharpCompress.Compressors.Xz;
using Shelly_CLI.Utility;
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
                                var linkPath = Path.Combine("/usr/bin", Path.GetFileName(destPath));
                                if (File.Exists(linkPath)) File.Delete(linkPath);
                                File.CreateSymbolicLink(linkPath, destPath);
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
                QuestionHandler.HandleQuestion(args, Program.IsUiMode, settings.NoConfirm);
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
}
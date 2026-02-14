using System.Formats.Tar;
using System.IO.Compression;
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
        var manager = new AlpmManager();
        manager.Progress += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[blue]{args.PackageName}[/]: {args.Percent}%");
        };

        manager.Question += (sender, args) =>
        {
            // Handle SelectProvider differently - it needs a selection, not yes/no
            if (args.QuestionType == AlpmQuestionType.SelectProvider && args.ProviderOptions?.Count > 0)
            {
                if (settings.NoConfirm)
                {
                    // Machine-readable format for UI integration
                    Console.Error.WriteLine($"[ALPM_SELECT_PROVIDER]{args.DependencyName}");
                    for (var i = 0; i < args.ProviderOptions.Count; i++)
                    {
                        Console.Error.WriteLine($"[ALPM_PROVIDER_OPTION]{i}:{args.ProviderOptions[i]}");
                    }

                    Console.Error.WriteLine("[ALPM_PROVIDER_OPTION_END]");
                    Console.Error.Flush();
                    var input = Console.ReadLine();
                    args.Response = int.TryParse(input?.Trim(), out var idx) ? idx : 0;
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
                // Machine-readable format for UI integration
                Console.Error.WriteLine($"[Shelly][ALPM_QUESTION]{args.QuestionText}");
                Console.Error.Flush();
                var input = Console.ReadLine();
                args.Response = input?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
            }
            else
            {
                var response = AnsiConsole.Confirm($"[yellow]{args.QuestionText}[/]", defaultValue: true);
                args.Response = response ? 1 : 0;
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
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Config;

public class SetParallelDownloadsSettings : CommandSettings
{
    [CommandArgument(0, "<downloadCount>")]
    public int DownloadCount { get; set; } = 1;
}
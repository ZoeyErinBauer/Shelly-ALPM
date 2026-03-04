namespace Shelly_CLI.Configuration;

public class ShellyConfig
{
    public string FileSizeDisplay { get; set; } = nameof(SizeDisplay.Bytes);

    public string DefaultExecution { get; set; } = nameof(DefaultCommand.UpgradeAll);
}
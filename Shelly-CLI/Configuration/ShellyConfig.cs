namespace Shelly_CLI;

public class ShellyConfig
{
    public SizeDisplay FileSizeDisplay { get; set; } = SizeDisplay.Bytes;

    public DefaultCommand DefaultExecution { get; set; } = DefaultCommand.UpgradeAll;
}
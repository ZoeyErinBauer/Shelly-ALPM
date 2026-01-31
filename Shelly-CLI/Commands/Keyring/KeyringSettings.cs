using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringSettings : CommandSettings
{
    [CommandArgument(0, "[keys]")]
    [Description("Key IDs or fingerprints to operate on")]
    public string[]? Keys { get; set; }

    [CommandOption("--keyserver <server>")]
    [Description("Keyserver to use for receiving keys")]
    public string? Keyserver { get; set; }
}

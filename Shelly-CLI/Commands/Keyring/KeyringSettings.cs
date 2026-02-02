using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Keyring;

public class KeyringSettings : CommandSettings
{
    [CommandArgument(0, "[keys]")]
    [Description("GPG key IDs or fingerprints to operate on (e.g., 0x12345678)")]
    public string[]? Keys { get; set; }

    [CommandOption("--keyserver <server>")]
    [Description("URL of the keyserver to fetch keys from (e.g., keyserver.ubuntu.com)")]
    public string? Keyserver { get; set; }
}

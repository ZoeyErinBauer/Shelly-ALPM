using System.ComponentModel;
using Spectre.Console.Cli;

namespace Shelly_CLI.Commands.Standard;

public class InstallPackageSettings : PackageSettings
{
        
    [CommandOption("-o | --build-deps")]
    [Description("Install build dependencies only for the specified packages")]
    public bool BuildDepsOn { get; set; }
    
    [CommandOption("-m | --make-deps")]
    [Description("Install make dependencies only for the specified packages")]
    public bool MakeDepsOn { get; set; } 
}
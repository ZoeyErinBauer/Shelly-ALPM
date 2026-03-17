using System.Diagnostics.CodeAnalysis;
using ConsoleAppFramework;
using PackageManager.Alpm;
using Shelly.Commands.StandardCommands;

// ReSharper disable InvalidXmlDocComment

namespace Shelly.Commands;

[RegisterCommands]
[SuppressMessage("GenerateConsoleAppFramework", "CAF007:Command name is duplicated.")]
internal class Standard
{
    /// <summary>
    /// Synchronize package databases
    /// </summary>
    /// <param name="force">-f,force the sync of the package databases </param>
    /// <returns></returns>
    public int Sync(ConsoleAppContext context, bool force = false)
    {
        var globals = (GlobalOptions)context.GlobalOptions!;
        return globals.UiMode ? SyncCommands.SyncUiMode(globals.Verbose, force) : SyncCommands.SyncConsoleMode(globals.Verbose, force);
    }

    /// <summary>
    /// Upgrades all system packages
    /// </summary>
    /// <param name="force">-f,force the sync of the package databases </param>
    /// <returns></returns>
    public int Upgrade(ConsoleAppContext context, bool force = false)
    {
        var globals = (GlobalOptions)context.GlobalOptions!;
        return globals.UiMode
            ? UpgradeCommands.UpgradeUiMode(globals.Verbose, force)
            : UpgradeCommands.UpgradeConsole(globals.Verbose, force);
    }

   
}
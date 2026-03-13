using ConsoleAppFramework;
using Shelly;


var app = ConsoleApp.Create();
app.ConfigureGlobalOptions((ref builder) =>
{
    var verbose = builder.AddGlobalOption<bool>("-v|--verbose");
    var uiMode = builder.AddGlobalOption<bool>("--ui-mode");
    var sync = builder.AddGlobalOption<bool>("-y|--sync");
    return new GlobalOptions(verbose, uiMode, sync);
});
await app.RunAsync(args);
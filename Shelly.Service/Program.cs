using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackageManager.Alpm;
using Shelly.Service;

var builder = Host.CreateApplicationBuilder(args);

// Add systemd integration for proper service lifecycle management
builder.Services.AddSystemd();

// Register AlpmManager as singleton (it manages native resources)
builder.Services.AddSingleton<IAlpmManager, AlpmManager>();

// Register the D-Bus service
builder.Services.AddSingleton<ShellyDbusService>();

// Add the D-Bus hosted service
builder.Services.AddHostedService<ShellyDbusHostedService>();

var host = builder.Build();
await host.RunAsync();

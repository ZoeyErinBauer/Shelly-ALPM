using ConsoleAppFramework;
using Shelly.Commands;

Console.WriteLine("Hello, World!");

var app = ConsoleApp.Create();
await app.RunAsync(args);

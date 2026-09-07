using SAVI.Desktop;

Console.Title = "SAVI — Shatru's Adaptive Virtual Intelligence";
var host = new DesktopHost();
await host.StartAsync();

Console.WriteLine("\nSAVI Desktop Companion initialized. Press 'Q' to quit.\n");
var key = Console.ReadKey(true);
while (key.Key != ConsoleKey.Q)
{
    key = Console.ReadKey(true);
}

await host.StopAsync();

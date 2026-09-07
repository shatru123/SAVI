using Microsoft.AspNetCore.SignalR.Client;
using SAVI.Application.DTOs;
using SAVI.Core.Constants;
using SAVI.Core.Enums;

namespace SAVI.Desktop;

public class DesktopHost
{
    private HubConnection? _hubConnection;
    private readonly string _apiBaseUrl;

    public DesktopHost(string apiBaseUrl = "http://localhost:5000")
    {
        _apiBaseUrl = apiBaseUrl;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=================================================");
        Console.WriteLine("        SAVI DESKTOP COMPANION HOST v1.0         ");
        Console.WriteLine("=================================================");
        Console.ResetColor();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hub/savi")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<StreamTokenDto>("ReceiveStreamToken", token =>
        {
            Console.Write(token.Token);
        });

        _hubConnection.On<ChatResponseDto>("ReceiveAgentResponse", resp =>
        {
            Console.WriteLine();
            if (resp.Sources.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"\n[Verified via {resp.Sources.Count} sources: {string.Join(", ", resp.Sources.Select(s => s.SourceName))}]");
                Console.ResetColor();
            }
        });

        _hubConnection.On<VoiceState>("ReceiveVoiceState", state =>
        {
            // Desktop state updates
        });

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("● SYSTEM ONLINE — Connected to SAVI Hub.");
            Console.ResetColor();
        }
        catch (Exception)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("○ Running in Local Offline Mode (API Hub not connected).");
            Console.ResetColor();
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

using Microsoft.AspNetCore.SignalR.Client;
using SAVI.Application.DTOs;
using SAVI.Core.Enums;

namespace SAVI.Mobile;

public class MobileCompanionClient
{
    private HubConnection? _hubConnection;
    private readonly string _serverUrl;

    public event Action<string>? OnTokenReceived;
    public event Action<ChatResponseDto>? OnResponseReceived;
    public event Action<VoiceState>? OnVoiceStateChanged;

    public MobileCompanionClient(string serverUrl = "http://localhost:5000")
    {
        _serverUrl = serverUrl;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_serverUrl}/hub/savi")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<StreamTokenDto>("ReceiveStreamToken", token =>
        {
            OnTokenReceived?.Invoke(token.Token);
        });

        _hubConnection.On<ChatResponseDto>("ReceiveAgentResponse", resp =>
        {
            OnResponseReceived?.Invoke(resp);
        });

        _hubConnection.On<VoiceState>("ReceiveVoiceState", state =>
        {
            OnVoiceStateChanged?.Invoke(state);
        });

        await _hubConnection.StartAsync(cancellationToken);
    }

    public async Task SendMessageAsync(string message, string conversationId, bool voiceActive = false)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.SendAsync("SendMessage", new SendChatMessageRequest
            {
                Message = message,
                ConversationId = conversationId,
                VoiceActive = voiceActive
            });
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

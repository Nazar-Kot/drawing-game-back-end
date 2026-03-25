using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DrawingApp.Api.Hubs;

public record UserInfo(string Name, string Role);

public record DrawingSegment(
    string Type,       // "draw" | "clear"
    double X0, double Y0,
    double X1, double Y1,
    string Color,
    double Size
);

public class DrawingHub : Hub
{
    // Shared state (single-room game)
    private static string? _drawerConnectionId = null;
    private static string? _secretWord = null;
    private static bool _gameOver = false;
    private static readonly ConcurrentDictionary<string, UserInfo> _users = new();

    // ── Client → Server ──────────────────────────────────────────────────────

    public async Task JoinAsDrawer(string name)
    {
        if (_drawerConnectionId != null && _users.ContainsKey(_drawerConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "A drawer is already in the game. Refresh to try again.");
            return;
        }

        _drawerConnectionId = Context.ConnectionId;
        _secretWord = null;
        _gameOver = false;
        _users[Context.ConnectionId] = new UserInfo(name, "drawer");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, "game");
        await Clients.Caller.SendAsync("JoinedAsDrawer", name);
        await Clients.All.SendAsync("UserJoined", name, "drawer");
    }

    public async Task JoinAsGuesser(string name)
    {
        _users[Context.ConnectionId] = new UserInfo(name, "guesser");

        await Groups.AddToGroupAsync(Context.ConnectionId, "game");
        await Clients.Caller.SendAsync("JoinedAsGuesser", name);
        await Clients.All.SendAsync("UserJoined", name, "guesser");
    }

    public async Task SendDrawing(DrawingSegment segment)
    {
        if (Context.ConnectionId != _drawerConnectionId) return;
        await Clients.Others.SendAsync("DrawingReceived", segment);
    }

    public async Task SetWord(string word)
    {
        if (Context.ConnectionId != _drawerConnectionId) return;
        _secretWord = word.Trim();
        _gameOver = false;
        await Clients.Caller.SendAsync("WordConfirmed");
        // Tell everyone (except drawer) the word has been set without revealing it
        await Clients.All.SendAsync("WordWasSet");
    }

    public async Task SendGuess(string message)
    {
        if (!_users.TryGetValue(Context.ConnectionId, out var user)) return;

        await Clients.All.SendAsync("GuessReceived", user.Name, message, user.Role);

        if (!_gameOver
            && !string.IsNullOrWhiteSpace(_secretWord)
            && message.Trim().Equals(_secretWord, StringComparison.OrdinalIgnoreCase))
        {
            _gameOver = true;
            await Clients.All.SendAsync("WordGuessed", user.Name, _secretWord);
        }
    }

    public async Task ClearCanvas()
    {
        if (Context.ConnectionId != _drawerConnectionId) return;
        await Clients.All.SendAsync("CanvasCleared");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_users.TryRemove(Context.ConnectionId, out var user))
        {
            if (Context.ConnectionId == _drawerConnectionId)
            {
                // Drawer left — reset everything and kick everyone
                _drawerConnectionId = null;
                _secretWord = null;
                _gameOver = false;
                _users.Clear();
                await Clients.All.SendAsync("AdminDisconnected", user.Name);
            }
            else
            {
                await Clients.All.SendAsync("UserLeft", user.Name);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}

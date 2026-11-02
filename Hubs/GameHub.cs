using System.Collections.Concurrent;
using GeoPolitics_Game.Data.Repositories;
using GeoPolitics_Game.Security;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;

namespace GeoPolitics_Game.Hubs
{
    // The real-time channel for each game: watcher count, cheers, and "the game changed" pings.
    // Everyone watching game X is in the SignalR group "game-X".
    public class GameHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> Watching = new();     // connection -> game id
        private static readonly ConcurrentDictionary<string, DateTime> LastCheer = new();  // connection -> last cheer time
        private static readonly string[] AllowedCheers = { "go", "retreat" };              // no free text = no abuse

        private readonly IGameRepository _games;

        public GameHub(IGameRepository games) => _games = games;

        public static string GroupName(string gameId) => $"game-{gameId}";

        // Called by the browser when a game page opens
        public async Task JoinGame(string gameId)
        {
            if (!ObjectId.TryParse(gameId, out _)) return;
            var game = await _games.GetByIdAsync(gameId);
            if (game == null) return;

            // Same rule as the page itself: spectators only if the owner allowed them
            if (!game.Settings.AllowSpectators && Context.User?.IsOwnerOf(game) != true) return;

            if (Watching.TryGetValue(Context.ConnectionId, out var previous))
            {
                if (previous == gameId) return;
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(previous));
            }

            Watching[Context.ConnectionId] = gameId;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
            await BroadcastWatcherCount(gameId);
        }

        // "Go Lethu!" / "Retreat!" - shown as a floating bubble on every screen watching the game
        public async Task Cheer(string gameId, string kind)
        {
            if (!AllowedCheers.Contains(kind)) return;
            if (!Watching.TryGetValue(Context.ConnectionId, out var watched) || watched != gameId) return;

            var now = DateTime.UtcNow;
            if (LastCheer.TryGetValue(Context.ConnectionId, out var last) && now - last < TimeSpan.FromSeconds(2))
                return;   // simple rate limit: one cheer every 2 seconds
            LastCheer[Context.ConnectionId] = now;

            await Clients.Group(GroupName(gameId)).SendAsync("Cheer", kind);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            LastCheer.TryRemove(Context.ConnectionId, out _);
            if (Watching.TryRemove(Context.ConnectionId, out var gameId))
                await BroadcastWatcherCount(gameId);
            await base.OnDisconnectedAsync(exception);
        }

        private Task BroadcastWatcherCount(string gameId) =>
            Clients.Group(GroupName(gameId)).SendAsync("Watchers", Watching.Values.Count(g => g == gameId));
    }
}
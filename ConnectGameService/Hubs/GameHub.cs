using ConnectGameService.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace ConnectGameService.Hubs
{
    public class GameHub : Hub
    {
        private readonly IDictionary<string, UserConnection> _connections;

        public GameHub(IDictionary<string, UserConnection> connections)
        {
            _connections = connections;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                _connections.Remove(Context.ConnectionId);

                SendConnectedUsers(userConnection.Room);
            }

            return base.OnDisconnectedAsync(exception);
        }

        public async Task JoinRoom(UserConnection userConnection)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userConnection.Room);

            _connections[Context.ConnectionId] = userConnection;

            await SendConnectedUsers(userConnection.Room);
        }

        public Task SendConnectedUsers(string room)
        {
            var users = _connections.Values
                .Where(t => t.Room == room)
                .Select(t => t.User);

            return Clients.Group(room).SendAsync("UsersInRoom", users);
        }

        public async Task SendGameUsers(string room)
        {
            var users = _connections.Values
                .Where(t => t.Room == room)
                .Select(t => t);

            await Clients.Group(room).SendAsync("UsersInGame", users);
        }

        public async Task SendInvite(string user, string fromUser)
        {
            var connectionId = _connections.FirstOrDefault(x => x.Value.User == user).Key;
            if (_connections.TryGetValue(connectionId, out UserConnection userConnection))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveInvitation", fromUser);
            }
        }

        public async Task AcceptInvitation(string opponentUser)
        {
            Guid id = Guid.NewGuid();
            var newRoomName = id.ToString();

            var opponentItem = _connections.FirstOrDefault(x => x.Value.User == opponentUser);
            var opponentConnectionId = opponentItem.Key;
            var opponent = opponentItem.Value;
            opponent.Room = newRoomName;
            opponent.Turn = false;
            opponent.Color = "red";

            var currentUser = _connections[Context.ConnectionId];
            currentUser.Room = newRoomName;
            currentUser.Turn = true;
            currentUser.Color = "yellow";

            await Groups.RemoveFromGroupAsync(opponentConnectionId, "Lobby");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");

            await Groups.AddToGroupAsync(opponentConnectionId, newRoomName);
            await Groups.AddToGroupAsync(Context.ConnectionId, newRoomName);


            _connections.Remove(opponentConnectionId);
            _connections.Remove(Context.ConnectionId);
            _connections[opponentConnectionId] = opponent;

            _connections[Context.ConnectionId] = currentUser;

            await Clients.Group(newRoomName)
                    .SendAsync("StartGame");

            await SendConnectedUsers("Lobby");
            await SendGameUsers(newRoomName);
        }

        public async Task Play(string coord)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                var currentUser = _connections[Context.ConnectionId];
                currentUser.Turn = !currentUser.Turn;

                var gameMove = new GameMove()
                {
                    User = currentUser.User,
                    Color = currentUser.Color,
                    Coord = coord
                };

                await Clients.Group(currentUser.Room).SendAsync("ReturnPlay", gameMove);
            }
        }

        public async Task LeaveGame()
        {
            if (_connections.TryGetValue(Context.ConnectionId, out UserConnection userConnection))
            {
                var currentUser = _connections[Context.ConnectionId];
                var gameRoom = currentUser.Room;
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameRoom);
                await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");

                currentUser.Turn = false;
                currentUser.Room = "Lobby";

                _connections.Remove(Context.ConnectionId);
                _connections[Context.ConnectionId] = currentUser;

                await SendGameUsers(gameRoom);
                await SendConnectedUsers(currentUser.Room);
            }
        }
    }
}

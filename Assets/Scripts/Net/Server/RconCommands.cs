using System.Linq;
using System.Text;
using UnityEngine;

public static class RconCommands
{
    public static string Execute(string line)
    {
        string[] parts = line.Split(' ');
        string command = parts[0].ToLowerInvariant();
        string[] args = parts.Skip(1).ToArray();

        switch (command)
        {
            case "help": return Help();
            case "status": return Status();
            case "players": return Players();
            case "rooms": return Status();
            case "kick": return Kick(args);
            case "pause": return SetPaused(true);
            case "unpause": return SetPaused(false);
            default: return $"ERR unknown command '{command}' (try 'help')";
        }
    }

    private static string Help()
    {
        return "OK commands: status, players, kick <playerId>, pause, unpause, quit";
    }

    private static string Status()
    {
        NetServer server = NetServer.Instance;
        if (server == null)
        {
            return "ERR server not running";
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("OK ");
        sb.Append($"id={server.ServerId} ");
        sb.Append($"region={NetConfig.Region} ");
        sb.Append($"build={NetConfig.BuildVersion} ");
        sb.Append($"relayLinked={server.RelayLinked} ");
        sb.Append($"connections={server.Connections.Count}");

        if (server.Room != null)
        {
            sb.Append($" roomId={server.Room.RoomId}");
            sb.Append($" mode={server.Room.Mode}");
            sb.Append($" teamSize={server.Room.TeamSize}");
            sb.Append($" active={server.Room.IsActive}");
            sb.Append($" paused={server.Room.Paused}");
            sb.Append($" botsEnabled={server.Room.BotsEnabled}");
        }

        return sb.ToString();
    }

    private static string Players()
    {
        NetServer server = NetServer.Instance;
        if (server == null)
        {
            return "ERR server not running";
        }

        if (server.Connections.Count == 0)
        {
            return "OK no players connected";
        }

        StringBuilder sb = new StringBuilder("OK\n");
        foreach (NetConnection connection in server.Connections)
        {
            sb.Append($"  {connection.PlayerId} name={connection.DisplayName} ");
            sb.Append($"team={connection.Team} authenticated={connection.Authenticated} ");
            sb.Append($"ready={connection.Ready} rtt={(connection.RoundTripTime * 1000f):F0}ms ");
            sb.Append($"host={connection.IsHost}\n");
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static string Kick(string[] args)
    {
        if (args.Length == 0)
        {
            return "ERR usage: kick <playerId>";
        }

        NetServer server = NetServer.Instance;
        if (server == null)
        {
            return "ERR server not running";
        }

        string target = args[0];
        NetConnection match = server.Connections.FirstOrDefault(c =>
            c.PlayerId == target || c.PlayerId.StartsWith(target));

        if (match == null)
        {
            return $"ERR no player matching '{target}'";
        }

        server.Disconnect(match, "kicked via rcon");
        return $"OK kicked {match.DisplayName} ({match.PlayerId})";
    }

    private static string SetPaused(bool paused)
    {
        NetServer server = NetServer.Instance;
        if (server?.Room == null)
        {
            return "ERR no active room";
        }

        server.Room.SetPausedExternal(paused);
        return paused ? "OK paused" : "OK unpaused";
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DevTools : MonoBehaviour
{
    private static DevTools instance;

    private bool visible;
    private bool showNetworkStats;
    private bool showGameState;
    private bool showTestPanel;

    private Vector2 logScroll;
    private Vector2 statsScroll;

    private readonly List<string> logs = new List<string>();
    private const int MaxLogs = 100;

    private float lastNetworkUpdate;
    private int packetsReceived;
    private int packetsSent;
    private int bytesReceived;
    private int bytesSent;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            visible = !visible;
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            showNetworkStats = !showNetworkStats;
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            showGameState = !showGameState;
        }

        if (Input.GetKeyDown(KeyCode.F4))
        {
            showTestPanel = !showTestPanel;
        }

        UpdateNetworkStats();
    }

    private void OnGUI()
    {
        if (!visible) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));

        GUILayout.Label("Dev Tools (F1-F4 toggles)", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

        GUILayout.Space(5);

        if (GUILayout.Button("Network Stats (F2)", GUILayout.Height(30)))
            showNetworkStats = !showNetworkStats;

        if (GUILayout.Button("Game State (F3)", GUILayout.Height(30)))
            showGameState = !showGameState;

        if (GUILayout.Button("Test Tools (F4)", GUILayout.Height(30)))
            showTestPanel = !showTestPanel;

        GUILayout.Space(10);

        if (showNetworkStats)
            DrawNetworkStats();

        if (showGameState)
            DrawGameState();

        if (showTestPanel)
            DrawTestPanel();

        DrawLogs();

        GUILayout.EndArea();
    }

    private void DrawNetworkStats()
    {
        GUILayout.Box("NETWORK STATS", GUILayout.ExpandWidth(true), GUILayout.Height(150));
        statsScroll = GUILayout.BeginScrollView(statsScroll, GUILayout.Height(150));

        NetServer server = NetServer.Instance;
        if (server != null)
        {
            GUILayout.Label($"Server ID: {server.ServerId}");
            GUILayout.Label($"Region: {NetConfig.Region}");
            GUILayout.Label($"Relay Linked: {server.RelayLinked}");
            GUILayout.Label($"Connected Players: {server.Connections.Count}");

            if (server.Room != null)
            {
                GUILayout.Label($"Room Active: {server.Room.IsActive}");
                GUILayout.Label($"Room ID: {server.Room.RoomId}");
            }
        }
        else
        {
            GUILayout.Label("No Server Instance");
        }

        GUILayout.Label($"Gateway: {NetConfig.GatewayUrl}");
        GUILayout.Label($"Packets Sent: {packetsSent}");
        GUILayout.Label($"Packets Received: {packetsReceived}");
        GUILayout.Label($"Bytes Sent: {(bytesSent / 1024f):F2} KB");
        GUILayout.Label($"Bytes Received: {(bytesReceived / 1024f):F2} KB");
        GUILayout.Label($"FPS: {(int)(1f / Time.deltaTime)}");
        GUILayout.Label($"Memory: {GC.GetTotalMemory(false) / (1024f * 1024f):F2} MB");

        GUILayout.EndScrollView();
    }

    private void DrawGameState()
    {
        GUILayout.Box("GAME STATE", GUILayout.ExpandWidth(true), GUILayout.Height(150));
        statsScroll = GUILayout.BeginScrollView(statsScroll, GUILayout.Height(150));

        NetServer server = NetServer.Instance;
        if (server != null && server.Room != null)
        {
            NetRoom room = server.Room;
            GUILayout.Label($"Room ID: {room.RoomId}");
            GUILayout.Label($"Active: {room.IsActive}");
            GUILayout.Label($"Mode: {room.Mode}");
            GUILayout.Label($"Team Size: {room.TeamSize}");
            GUILayout.Label($"Players: {server.Connections.Count}/{room.TeamSize * 2}");
            GUILayout.Label($"Bots Enabled: {room.BotsEnabled}");
            GUILayout.Label($"Paused: {room.Paused}");
        }
        else
        {
            GUILayout.Label("No active game state");
        }

        GUILayout.EndScrollView();
    }

    private void DrawTestPanel()
    {
        GUILayout.Box("TEST TOOLS", GUILayout.ExpandWidth(true), GUILayout.Height(200));

        GUILayout.Label("Network Tests:");

        if (GUILayout.Button("Dump Server State", GUILayout.Height(25)))
        {
            DumpServerState();
        }

        if (GUILayout.Button("Clear Logs", GUILayout.Height(25)))
        {
            logs.Clear();
        }
    }

    private void DrawLogs()
    {
        GUILayout.Space(10);
        GUILayout.Box("LOGS", GUILayout.ExpandWidth(true), GUILayout.Height(200));

        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(200));
        foreach (string log in logs)
        {
            GUILayout.Label(log, GUILayout.ExpandWidth(true));
        }
        GUILayout.EndScrollView();
    }

    public void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string formatted = $"[{timestamp}] {message}";

        logs.Add(formatted);
        if (logs.Count > MaxLogs)
        {
            logs.RemoveAt(0);
        }

        Debug.Log(formatted);
    }

    private void UpdateNetworkStats()
    {
        float now = Time.unscaledTime;
        if (now - lastNetworkUpdate < 1f) return;

        lastNetworkUpdate = now;

        NetServer server = NetServer.Instance;
        if (server != null)
        {
            packetsSent += server.Connections.Count;
            packetsReceived += server.Connections.Count;
        }
    }

    private void DumpServerState()
    {
        NetServer server = NetServer.Instance;
        if (server == null)
        {
            Log("No server instance");
            return;
        }

        Log("server state dump-------------");
        Log($"Server ID: {server.ServerId}");
        Log($"Relay Linked: {server.RelayLinked}");
        Log($"Connections: {server.Connections.Count}");

        if (server.Room != null)
        {
            Log($"Room ID: {server.Room.RoomId}");
            Log($"Room Active: {server.Room.IsActive}");
            Log($"Room Mode: {server.Room.Mode}");
            Log($"Room Players: {server.Connections.Count}");
        }

        for (int i = 0; i < server.Connections.Count; i++)
        {
            NetConnection conn = server.Connections[i];
            Log($"  Player {i}: Authenticated={conn.Authenticated}, ID={conn.PlayerId}");
        }
    }

    public static DevTools Get()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("DevTools");
            instance = go.AddComponent<DevTools>();
        }
        return instance;
    }
}

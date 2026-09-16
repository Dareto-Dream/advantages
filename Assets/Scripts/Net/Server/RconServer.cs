using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class RconServer : MonoBehaviour
{
    private TcpListener listener;
    private Thread acceptThread;
    private volatile bool running;

    private readonly ConcurrentQueue<PendingCommand> pending = new ConcurrentQueue<PendingCommand>();

    private struct PendingCommand
    {
        public NetworkStream stream;
        public string command;
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(NetConfig.RconPassword))
        {
            Debug.LogWarning("[RCON] RCON_PASSWORD is not set - RCON stays disabled (it is a plaintext "
                + "TCP port exposed publicly by Edgegap, so it must not fall back to INTERNAL_SECRET).");
            return;
        }

        try
        {
            listener = new TcpListener(IPAddress.Any, NetConfig.RconPort);
            listener.Start();
            running = true;

            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "RconAccept" };
            acceptThread.Start();

            Debug.Log($"[RCON] listening on port {NetConfig.RconPort}");
        }
        catch (Exception err)
        {
            Debug.LogError($"[RCON] failed to start on port {NetConfig.RconPort}: {err.Message}");
        }
    }

    private void Update()
    {
        while (pending.TryDequeue(out PendingCommand item))
        {
            string response;
            try
            {
                response = RconCommands.Execute(item.command);
            }
            catch (Exception err)
            {
                response = $"ERR {err.Message}";
            }

            WriteLine(item.stream, response);
        }
    }

    private void AcceptLoop()
    {
        while (running)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch
            {
                return;
            }

            Thread clientThread = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "RconClient" };
            clientThread.Start();
        }
    }

    private void HandleClient(TcpClient client)
    {
        client.ReceiveTimeout = 60_000;

        try
        {
            using NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];

            string auth = ReadLine(stream, buffer);
            string expected = NetConfig.RconPassword;

            if (string.IsNullOrEmpty(expected) || auth != expected)
            {
                WriteLine(stream, "ERR unauthorized");
                return;
            }

            WriteLine(stream, $"OK connected to {NetConfig.ServerId}");

            while (running)
            {
                string line = ReadLine(stream, buffer);
                if (line == null)
                {
                    break;
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Equals("quit", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                pending.Enqueue(new PendingCommand { stream = stream, command = line });
            }
        }
        catch
        {

        }
        finally
        {
            client.Close();
        }
    }

    private static string ReadLine(NetworkStream stream, byte[] buffer)
    {
        StringBuilder line = new StringBuilder();

        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                return line.Length > 0 ? line.ToString() : null;
            }

            if (b == '\n')
            {
                if (line.Length > 0 && line[line.Length - 1] == '\r')
                {
                    line.Length--;
                }
                return line.ToString();
            }

            line.Append((char)b);

            if (line.Length > 4096)
            {
                return line.ToString();
            }
        }
    }

    private static void WriteLine(NetworkStream stream, string text)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
        catch
        {

        }
    }

    private void OnDestroy()
    {
        running = false;

        try
        {
            listener?.Stop();
        }
        catch
        {

        }
    }
}

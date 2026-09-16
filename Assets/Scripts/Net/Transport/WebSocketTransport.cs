using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class WebSocketTransport : ITransport
{
    private const int ReceiveChunkSize = 8192;
    private const int MaxMessageBytes = 1 << 20;

    private ClientWebSocket socket;
    private CancellationTokenSource cancellation;

    private readonly ConcurrentQueue<TransportMessage> inbound = new ConcurrentQueue<TransportMessage>();
    private readonly ConcurrentQueue<TransportMessage> outbound = new ConcurrentQueue<TransportMessage>();
    private readonly SemaphoreSlim outboundSignal = new SemaphoreSlim(0);

    private int state = (int)TransportState.Idle;

    public TransportState State => (TransportState)Volatile.Read(ref state);

    public string LastError { get; private set; } = string.Empty;

    public float RoundTripTime { get; set; }

    private void SetState(TransportState value)
    {
        Volatile.Write(ref state, (int)value);
    }

    public void Connect(string url)
    {
        if (State == TransportState.Connecting || State == TransportState.Open)
        {
            return;
        }

        SetState(TransportState.Connecting);
        LastError = string.Empty;

        cancellation = new CancellationTokenSource();
        socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

        Task.Run(() => RunAsync(url, cancellation.Token));
    }

    private async Task RunAsync(string url, CancellationToken token)
    {
        try
        {
            await socket.ConnectAsync(new Uri(url), token).ConfigureAwait(false);
            SetState(TransportState.Open);

            Task send = SendLoopAsync(token);
            Task receive = ReceiveLoopAsync(token);
            await Task.WhenAny(send, receive).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception err)
        {
            LastError = err.Message;
            SetState(TransportState.Failed);
            return;
        }

        if (State != TransportState.Failed)
        {
            SetState(TransportState.Closed);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        byte[] chunk = new byte[ReceiveChunkSize];
        byte[] assembled = new byte[ReceiveChunkSize];
        int assembledLength = 0;

        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;

            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(chunk), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception err)
            {
                LastError = err.Message;
                SetState(TransportState.Failed);
                return;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                LastError = $"{(int)(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure)}: {result.CloseStatusDescription}";
                SetState(TransportState.Closing);
                return;
            }

            if (assembledLength + result.Count > assembled.Length)
            {
                int size = assembled.Length * 2;
                while (size < assembledLength + result.Count)
                {
                    size *= 2;
                }

                if (size > MaxMessageBytes)
                {
                    LastError = "message exceeded the size limit";
                    SetState(TransportState.Failed);
                    return;
                }

                Array.Resize(ref assembled, size);
            }

            Buffer.BlockCopy(chunk, 0, assembled, assembledLength, result.Count);
            assembledLength += result.Count;

            if (!result.EndOfMessage)
            {
                continue;
            }

            byte[] payload = new byte[assembledLength];
            Buffer.BlockCopy(assembled, 0, payload, 0, assembledLength);
            assembledLength = 0;

            inbound.Enqueue(new TransportMessage
            {
                data = payload,
                length = payload.Length,
                isText = result.MessageType == WebSocketMessageType.Text,
            });
        }
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await outboundSignal.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (outbound.TryDequeue(out TransportMessage message))
            {
                if (socket.State != WebSocketState.Open)
                {
                    return;
                }

                try
                {
                    WebSocketMessageType type = message.isText
                        ? WebSocketMessageType.Text
                        : WebSocketMessageType.Binary;

                    await socket
                        .SendAsync(new ArraySegment<byte>(message.data, 0, message.length), type, true, token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception err)
                {
                    LastError = err.Message;
                    SetState(TransportState.Failed);
                    return;
                }
            }
        }
    }

    public void SendBinary(ArraySegment<byte> payload)
    {
        if (State != TransportState.Open)
        {
            return;
        }

        byte[] copy = new byte[payload.Count];
        Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);

        outbound.Enqueue(new TransportMessage { data = copy, length = copy.Length, isText = false });
        outboundSignal.Release();
    }

    public void SendText(string payload)
    {
        if (State != TransportState.Open)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        outbound.Enqueue(new TransportMessage { data = bytes, length = bytes.Length, isText = true });
        outboundSignal.Release();
    }

    public bool TryReceive(out TransportMessage message)
    {
        return inbound.TryDequeue(out message);
    }

    public void Close(string reason)
    {
        if (State == TransportState.Closed || State == TransportState.Idle)
        {
            return;
        }

        SetState(TransportState.Closing);

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {

        }

        ClientWebSocket closing = socket;
        if (closing != null && closing.State == WebSocketState.Open)
        {

            _ = Task.Run(async () =>
            {
                try
                {
                    using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await closing
                        .CloseAsync(WebSocketCloseStatus.NormalClosure, reason ?? string.Empty, timeout.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {

                }
            });
        }

        SetState(TransportState.Closed);
    }

    public void Dispose()
    {
        Close("disposed");

        try
        {
            socket?.Dispose();
            cancellation?.Dispose();
            outboundSignal.Dispose();
        }
        catch (Exception err)
        {
            Debug.LogWarning($"[Net] transport dispose: {err.Message}");
        }

        socket = null;
        cancellation = null;
    }
}

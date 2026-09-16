using System;

public struct TransportMessage
{
    public byte[] data;
    public int length;
    public bool isText;

    public string AsText()
    {
        return isText ? System.Text.Encoding.UTF8.GetString(data, 0, length) : string.Empty;
    }
}

public enum TransportState
{
    Idle,
    Connecting,
    Open,
    Closing,
    Closed,
    Failed
}

public interface ITransport : IDisposable
{
    TransportState State { get; }

    string LastError { get; }

    float RoundTripTime { get; set; }

    void Connect(string url);

    void SendBinary(ArraySegment<byte> payload);

    void SendText(string payload);

    bool TryReceive(out TransportMessage message);

    void Close(string reason);
}

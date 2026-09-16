using System;
using System.Text;
using UnityEngine;

public sealed class NetWriter
{
    private byte[] buffer;
    private int position;

    public NetWriter(int capacity = 1024)
    {
        buffer = new byte[Mathf.Max(64, capacity)];
    }

    public int Length => position;

    public void Reset()
    {
        position = 0;
    }

    public void Truncate(int length)
    {
        position = Mathf.Clamp(length, 0, position);
    }

    public ArraySegment<byte> Segment => new ArraySegment<byte>(buffer, 0, position);

    public byte[] ToArray()
    {
        byte[] copy = new byte[position];
        Buffer.BlockCopy(buffer, 0, copy, 0, position);
        return copy;
    }

    private void Need(int bytes)
    {
        if (position + bytes <= buffer.Length)
        {
            return;
        }

        int size = buffer.Length * 2;
        while (size < position + bytes)
        {
            size *= 2;
        }

        Array.Resize(ref buffer, size);
    }

    public void WriteByte(byte value)
    {
        Need(1);
        buffer[position++] = value;
    }

    public void WriteBool(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteSByte(sbyte value)
    {
        WriteByte(unchecked((byte)value));
    }

    public void WriteUShort(ushort value)
    {
        Need(2);
        buffer[position++] = (byte)value;
        buffer[position++] = (byte)(value >> 8);
    }

    public void WriteShort(short value)
    {
        WriteUShort(unchecked((ushort)value));
    }

    public void WriteUInt(uint value)
    {
        Need(4);
        buffer[position++] = (byte)value;
        buffer[position++] = (byte)(value >> 8);
        buffer[position++] = (byte)(value >> 16);
        buffer[position++] = (byte)(value >> 24);
    }

    public void WriteInt(int value)
    {
        WriteUInt(unchecked((uint)value));
    }

    public void WriteLong(long value)
    {
        WriteUInt(unchecked((uint)value));
        WriteUInt(unchecked((uint)(value >> 32)));
    }

    public void WriteFloat(float value)
    {
        WriteUInt(BitConverterHelper.SingleToUInt(value));
    }

    public void WriteVector3(Vector3 value)
    {
        WriteFloat(value.x);
        WriteFloat(value.y);
        WriteFloat(value.z);
    }

    public void WriteAngle(float degrees)
    {
        float wrapped = Mathf.Repeat(degrees, 360f);
        WriteUShort((ushort)Mathf.RoundToInt(wrapped * (65535f / 360f)));
    }

    public void WriteNormalized(float value)
    {
        WriteSByte((sbyte)Mathf.Clamp(Mathf.RoundToInt(value * 127f), -127, 127));
    }

    public void WritePool(float value)
    {
        WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(value * 4f), 0, 65535));
    }

    public void WriteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteUShort(0);
            return;
        }

        int count = Encoding.UTF8.GetByteCount(value);
        if (count > ushort.MaxValue)
        {
            throw new ArgumentException("string too long for the wire", nameof(value));
        }

        WriteUShort((ushort)count);
        Need(count);
        Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, position);
        position += count;
    }
}

internal static class BitConverterHelper
{
    public static uint SingleToUInt(float value)
    {
        return unchecked((uint)BitConverter.SingleToInt32Bits(value));
    }

    public static float UIntToSingle(uint value)
    {
        return BitConverter.Int32BitsToSingle(unchecked((int)value));
    }
}

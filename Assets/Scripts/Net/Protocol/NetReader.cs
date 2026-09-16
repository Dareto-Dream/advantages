using System;
using System.Text;
using UnityEngine;

public sealed class NetReader
{
    private byte[] buffer;
    private int position;
    private int length;

    public NetReader()
    {
        buffer = Array.Empty<byte>();
    }

    public NetReader(byte[] data, int offset, int count)
    {
        Reset(data, offset, count);
    }

    public bool Ok { get; private set; } = true;

    public int Remaining => Mathf.Max(0, length - position);

    public void Reset(byte[] data, int offset, int count)
    {
        buffer = data;
        position = offset;
        length = offset + count;
        Ok = true;
    }

    private bool Take(int bytes)
    {
        if (position + bytes > length)
        {
            Ok = false;
            return false;
        }

        return true;
    }

    public byte ReadByte()
    {
        if (!Take(1))
        {
            return 0;
        }

        return buffer[position++];
    }

    public bool ReadBool()
    {
        return ReadByte() != 0;
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public ushort ReadUShort()
    {
        if (!Take(2))
        {
            return 0;
        }

        ushort value = (ushort)(buffer[position] | (buffer[position + 1] << 8));
        position += 2;
        return value;
    }

    public short ReadShort()
    {
        return unchecked((short)ReadUShort());
    }

    public uint ReadUInt()
    {
        if (!Take(4))
        {
            return 0;
        }

        uint value = (uint)(buffer[position]
            | (buffer[position + 1] << 8)
            | (buffer[position + 2] << 16)
            | (buffer[position + 3] << 24));
        position += 4;
        return value;
    }

    public int ReadInt()
    {
        return unchecked((int)ReadUInt());
    }

    public long ReadLong()
    {
        uint low = ReadUInt();
        uint high = ReadUInt();
        return unchecked((long)((ulong)high << 32 | low));
    }

    public float ReadFloat()
    {
        return BitConverterHelper.UIntToSingle(ReadUInt());
    }

    public Vector3 ReadVector3()
    {
        float x = ReadFloat();
        float y = ReadFloat();
        float z = ReadFloat();
        return new Vector3(x, y, z);
    }

    public float ReadAngle()
    {
        return ReadUShort() * (360f / 65535f);
    }

    public float ReadNormalized()
    {
        return Mathf.Clamp(ReadSByte() / 127f, -1f, 1f);
    }

    public float ReadPool()
    {
        return ReadUShort() * 0.25f;
    }

    public string ReadString()
    {
        int count = ReadUShort();
        if (count == 0)
        {
            return string.Empty;
        }

        if (!Take(count))
        {
            return string.Empty;
        }

        string value = Encoding.UTF8.GetString(buffer, position, count);
        position += count;
        return value;
    }
}

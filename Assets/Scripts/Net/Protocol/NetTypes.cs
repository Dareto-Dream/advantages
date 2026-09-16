using UnityEngine;

public struct NetInput
{

    public uint tick;

    public float yaw;
    public float pitch;

    public float moveX;
    public float moveY;

    public byte buttons;

    public byte abilities;

    public byte weaponSlot;

    public Vector3 predictedPosition;

    public bool Held(byte button)
    {
        return (buttons & button) != 0;
    }

    public bool Ability(byte slot)
    {
        return (abilities & slot) != 0;
    }

    public void Write(NetWriter writer)
    {
        writer.WriteUInt(tick);
        writer.WriteAngle(yaw);
        writer.WriteAngle(pitch + 180f);
        writer.WriteNormalized(moveX);
        writer.WriteNormalized(moveY);
        writer.WriteByte(buttons);
        writer.WriteByte(abilities);
        writer.WriteByte(weaponSlot);
        writer.WriteVector3(predictedPosition);
    }

    public static NetInput Read(NetReader reader)
    {
        NetInput input = default;
        input.tick = reader.ReadUInt();
        input.yaw = reader.ReadAngle();
        input.pitch = reader.ReadAngle() - 180f;
        input.moveX = reader.ReadNormalized();
        input.moveY = reader.ReadNormalized();
        input.buttons = reader.ReadByte();
        input.abilities = reader.ReadByte();
        input.weaponSlot = reader.ReadByte();
        input.predictedPosition = reader.ReadVector3();
        return input;
    }
}

public struct EntityState
{
    public ushort id;
    public byte flags;
    public byte team;
    public byte operative;
    public Vector3 position;
    public Vector3 velocity;
    public float yaw;
    public float pitch;
    public float health;
    public float armor;

    public bool Has(byte flag)
    {
        return (flags & flag) != 0;
    }

    public void Write(NetWriter writer)
    {
        writer.WriteUShort(id);
        writer.WriteByte(flags);
        writer.WriteByte(team);
        writer.WriteByte(operative);
        writer.WriteVector3(position);
        writer.WriteVector3(velocity);
        writer.WriteAngle(yaw);
        writer.WriteAngle(pitch + 180f);
        writer.WritePool(health);
        writer.WritePool(armor);
    }

    public static EntityState Read(NetReader reader)
    {
        EntityState state = default;
        state.id = reader.ReadUShort();
        state.flags = reader.ReadByte();
        state.team = reader.ReadByte();
        state.operative = reader.ReadByte();
        state.position = reader.ReadVector3();
        state.velocity = reader.ReadVector3();
        state.yaw = reader.ReadAngle();
        state.pitch = reader.ReadAngle() - 180f;
        state.health = reader.ReadPool();
        state.armor = reader.ReadPool();
        return state;
    }
}

public struct RosterEntry
{
    public ushort entityId;
    public string playerId;
    public string name;
    public byte team;
    public byte operative;
    public bool bot;

    public void Write(NetWriter writer)
    {
        writer.WriteUShort(entityId);
        writer.WriteString(playerId);
        writer.WriteString(name);
        writer.WriteByte(team);
        writer.WriteByte(operative);
        writer.WriteBool(bot);
    }

    public static RosterEntry Read(NetReader reader)
    {
        RosterEntry entry = default;
        entry.entityId = reader.ReadUShort();
        entry.playerId = reader.ReadString();
        entry.name = reader.ReadString();
        entry.team = reader.ReadByte();
        entry.operative = reader.ReadByte();
        entry.bot = reader.ReadBool();
        return entry;
    }
}

public struct MatchStateMessage
{
    public byte phase;
    public bool phaseIsTimed;
    public float phaseSecondsRemaining;
    public byte roundNumber;
    public byte attackerScore;
    public byte defenderScore;
    public byte mode;
    public byte teamSize;
    public bool botsEnabled;
    public bool paused;
    public float objectiveA;
    public float objectiveB;
    public float objectiveC;

    public void Write(NetWriter writer)
    {
        writer.WriteByte(phase);
        writer.WriteBool(phaseIsTimed);
        writer.WriteFloat(phaseIsTimed ? phaseSecondsRemaining : 0f);
        writer.WriteByte(roundNumber);
        writer.WriteByte(attackerScore);
        writer.WriteByte(defenderScore);
        writer.WriteByte(mode);
        writer.WriteByte(teamSize);
        writer.WriteBool(botsEnabled);
        writer.WriteBool(paused);
        writer.WriteFloat(objectiveA);
        writer.WriteFloat(objectiveB);
        writer.WriteFloat(objectiveC);
    }

    public static MatchStateMessage Read(NetReader reader)
    {
        MatchStateMessage message = default;
        message.phase = reader.ReadByte();
        message.phaseIsTimed = reader.ReadBool();
        message.phaseSecondsRemaining = reader.ReadFloat();
        message.roundNumber = reader.ReadByte();
        message.attackerScore = reader.ReadByte();
        message.defenderScore = reader.ReadByte();
        message.mode = reader.ReadByte();
        message.teamSize = reader.ReadByte();
        message.botsEnabled = reader.ReadBool();
        message.paused = reader.ReadBool();
        message.objectiveA = reader.ReadFloat();
        message.objectiveB = reader.ReadFloat();
        message.objectiveC = reader.ReadFloat();
        return message;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NetConnection
{

    private const int MaxBufferedInputs = 8;

    private readonly List<NetInput> buffered = new List<NetInput>(MaxBufferedInputs);

    public NetConnection(ITransport transport, string sessionId)
    {
        Transport = transport;
        SessionId = sessionId;
        ConnectedAt = Time.realtimeSinceStartup;
    }

    public ITransport Transport { get; }

    public string SessionId { get; }

    public float ConnectedAt { get; }

    public bool Authenticated { get; private set; }

    public string PlayerId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = "Player";

    public Team Team { get; set; } = Team.Attackers;

    public bool IsHost { get; private set; }

    public PlayerController Avatar { get; set; }

    public ushort EntityId { get; set; }

    public int HeroIndex { get; set; } = -1;

    public bool Ready { get; set; }

    public uint LastAppliedTick { get; private set; }

    public NetInput LastInput { get; private set; }

    public float RoundTripTime { get; set; }

    public float LastHeardFrom { get; set; }

    public float PredictionError { get; private set; }

    public bool ShouldDrop => Transport.State == TransportState.Closed
        || Transport.State == TransportState.Failed
        || Transport.State == TransportState.Closing;

    public void MarkAuthenticated(string playerId, string displayName, Team team, bool host)
    {
        Authenticated = true;
        PlayerId = playerId;
        DisplayName = string.IsNullOrEmpty(displayName) ? "Player" : displayName;
        Team = team;
        IsHost = host;
    }

    public void ReceiveInput(NetInput input)
    {

        if (input.tick <= LastAppliedTick)
        {
            return;
        }

        for (int i = 0; i < buffered.Count; i++)
        {
            if (buffered[i].tick == input.tick)
            {
                return;
            }
        }

        buffered.Add(input);

        for (int i = buffered.Count - 1; i > 0 && buffered[i - 1].tick > buffered[i].tick; i--)
        {
            NetInput swap = buffered[i - 1];
            buffered[i - 1] = buffered[i];
            buffered[i] = swap;
        }

        while (buffered.Count > MaxBufferedInputs)
        {
            buffered.RemoveAt(0);
        }
    }

    public bool Drain(List<NetInput> into)
    {
        if (buffered.Count == 0)
        {
            return false;
        }

        foreach (NetInput input in buffered)
        {
            into.Add(input);
        }

        NetInput newest = buffered[buffered.Count - 1];
        buffered.Clear();

        LastAppliedTick = newest.tick;
        LastInput = newest;

        PredictionError = Avatar != null
            ? Vector3.Distance(newest.predictedPosition, Avatar.transform.position)
            : 0f;

        return true;
    }

    public bool Coast(out NetInput input)
    {
        input = LastInput;

        if (LastAppliedTick == 0)
        {
            return false;
        }

        input.buttons &= unchecked((byte)~(InputButton.Jump | InputButton.Reload | InputButton.Interact));
        input.abilities = 0;
        input.weaponSlot = 0;
        return true;
    }

    private bool previousJumpHeld;
    private bool previousFireHeld;

    public void ResolveEdges(NetInput input, out bool jumpPressed, out bool firePressed)
    {
        bool jumpHeld = input.Held(InputButton.Jump);
        bool fireHeld = input.Held(InputButton.Fire);

        jumpPressed = jumpHeld && !previousJumpHeld;
        firePressed = fireHeld && !previousFireHeld;

        previousJumpHeld = jumpHeld;
        previousFireHeld = fireHeld;
    }

    public void Close(string reason)
    {
        Transport.Close(reason);
        Transport.Dispose();
    }
}

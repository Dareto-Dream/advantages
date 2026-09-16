using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class NetToken
{
#pragma warning disable 0649
    [Serializable]
    private class Claims
    {
        public string sub;
        public string room;
        public string name;
        public string handle;
        public int team;
        public bool host;
        public long exp;
    }
#pragma warning restore 0649

    public struct JoinClaims
    {
        public string playerId;
        public string roomId;
        public string name;
        public string handle;
        public Team team;
        public bool host;
        public bool valid;
    }

    public static JoinClaims Verify(string token, string secret)
    {
        JoinClaims result = default;

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(secret))
        {
            return result;
        }

        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            return result;
        }

        try
        {
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            byte[] signed = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            byte[] presented = FromBase64Url(parts[2]);

            if (!FixedTimeEquals(signed, presented))
            {
                return result;
            }

            string json = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
            Claims claims = JsonUtility.FromJson<Claims>(json);

            if (claims == null || string.IsNullOrEmpty(claims.sub) || string.IsNullOrEmpty(claims.room))
            {
                return result;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (claims.exp > 0 && claims.exp < now)
            {
                return result;
            }

            result.playerId = claims.sub;
            result.roomId = claims.room;
            result.name = claims.name;
            result.handle = claims.handle;
            result.team = claims.team == 1 ? Team.Defenders : Team.Attackers;
            result.host = claims.host;
            result.valid = true;
            return result;
        }
        catch (Exception err)
        {
            Debug.LogWarning($"[Net] join token rejected: {err.Message}");
            return result;
        }
    }

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        int difference = 0;
        for (int i = 0; i < a.Length; i++)
        {
            difference |= a[i] ^ b[i];
        }

        return difference == 0;
    }
}

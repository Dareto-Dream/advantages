using System;
using UnityEngine;

public static class DeviceIdentity
{
    private const string DeviceKey = "advantage.device.id";
    private const string TokenKey = "advantage.session.token";
    private const string NameKey = "advantage.player.name";

    public static string DeviceId
    {
        get
        {
            string existing = PlayerPrefs.GetString(DeviceKey, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            string created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(DeviceKey, created);
            PlayerPrefs.Save();
            return created;
        }
    }

    public static string SessionToken
    {
        get => PlayerPrefs.GetString(TokenKey, string.Empty);
        set
        {
            PlayerPrefs.SetString(TokenKey, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static string PreferredName
    {
        get => PlayerPrefs.GetString(NameKey, string.Empty);
        set
        {
            PlayerPrefs.SetString(NameKey, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.Save();
    }

    public static void ResetIdentity()
    {
        PlayerPrefs.DeleteKey(DeviceKey);
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(NameKey);
        PlayerPrefs.Save();
    }
}

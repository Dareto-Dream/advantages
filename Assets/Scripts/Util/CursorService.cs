using UnityEngine;

public static class CursorService
{
    public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

    public static void Lock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void Unlock()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Set(bool locked)
    {
        if (locked)
        {
            Lock();
        }
        else
        {
            Unlock();
        }
    }
}

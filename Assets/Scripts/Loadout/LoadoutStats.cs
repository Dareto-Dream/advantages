using System;
using UnityEngine;

[Serializable]
public struct LoadoutStats
{
    public float moveSpeed;
    public float groundAcceleration;
    public float airAcceleration;
    public float jumpForce;
    public float maxHealth;
    public float maxArmor;
    public Vector3 bodyScale;

    public static LoadoutStats For(LoadoutRole role)
    {
        switch (role)
        {
            case LoadoutRole.Light:
                return new LoadoutStats
                {
                    moveSpeed = 7.5f,
                    groundAcceleration = 34f,
                    airAcceleration = 13f,
                    jumpForce = 6.6f,
                    maxHealth = 80f,
                    maxArmor = 0f,
                    bodyScale = new Vector3(0.85f, 0.90f, 0.85f)
                };

            case LoadoutRole.Heavy:
                return new LoadoutStats
                {
                    moveSpeed = 5f,
                    groundAcceleration = 22f,
                    airAcceleration = 8f,
                    jumpForce = 5.4f,
                    maxHealth = 125f,
                    maxArmor = 25f,
                    bodyScale = new Vector3(1.20f, 1.05f, 1.20f)
                };

            default:
                return new LoadoutStats
                {
                    moveSpeed = 6f,
                    groundAcceleration = 28f,
                    airAcceleration = 10f,
                    jumpForce = 6f,
                    maxHealth = 100f,
                    maxArmor = 10f,
                    bodyScale = new Vector3(1.00f, 0.95f, 1.00f)
                };
        }
    }

    public static string Blurb(LoadoutRole role)
    {
        switch (role)
        {
            case LoadoutRole.Light:
                return "Fastest on foot and the best strafe-jumper. Thin margins - trade fights you already won.";
            case LoadoutRole.Heavy:
                return "Slow, armoured, hits hard up close. Holds a doorway on its own.";
            default:
                return "Middle of everything. Good default for a first round.";
        }
    }
}

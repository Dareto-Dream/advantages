using UnityEngine;

[System.Serializable]
public struct HeroDefinition
{
    public string name;
    public HeroRole role;
    public string archetype;
    public string weapon;
    public string identity;
    public string ultimate;
    public string gameplay;
    public Color color;

    public HeroDefinition(
        string name,
        HeroRole role,
        string archetype,
        string weapon,
        string identity,
        string ultimate,
        string gameplay,
        Color color)
    {
        this.name = name;
        this.role = role;
        this.archetype = archetype;
        this.weapon = weapon;
        this.identity = identity;
        this.ultimate = ultimate;
        this.gameplay = gameplay;
        this.color = color;
    }
}

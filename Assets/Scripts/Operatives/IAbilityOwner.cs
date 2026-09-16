using System;

public interface IAbilityOwner
{

    void ReportHealingDone(float amount);

    Ability GetAbility(Ability.Slot slot);

    event Action<Ability.Slot> AbilityCast;
}

using System.Collections.Generic;

public sealed class WeaponExecutorRegistry
{
    private readonly Dictionary<WeaponType, IWeaponExecutor> executors = new Dictionary<WeaponType, IWeaponExecutor>();
    private readonly IWeaponExecutor fallbackExecutor;

    public WeaponExecutorRegistry()
    {
        fallbackExecutor = new PrimaryOnlyWeaponExecutor();
        Register(WeaponType.Knife, new KnifeWeaponExecutor());
        Register(WeaponType.Sword, new SwordWeaponExecutor());
        Register(WeaponType.Spear, new SpearWeaponExecutor());
        Register(WeaponType.Spell, new SpellWeaponExecutor());
    }

    public void Register(WeaponType weaponType, IWeaponExecutor executor)
    {
        if (executor != null)
        {
            executors[weaponType] = executor;
        }
    }

    public IWeaponExecutor Resolve(WeaponType weaponType)
    {
        return executors.TryGetValue(weaponType, out IWeaponExecutor executor) ? executor : fallbackExecutor;
    }
}

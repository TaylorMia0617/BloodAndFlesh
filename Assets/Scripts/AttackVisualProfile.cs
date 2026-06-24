public readonly struct AttackVisualProfile
{
    public readonly WeaponType visualType;
    public readonly float range;
    public readonly float width;
    public readonly float shape;
    public readonly float thickness;
    public readonly bool isSpearSweep;

    public AttackVisualProfile(WeaponType visualType, float range, float width, float shape, float thickness, bool isSpearSweep)
    {
        this.visualType = visualType;
        this.range = range;
        this.width = width;
        this.shape = shape;
        this.thickness = thickness;
        this.isSpearSweep = isSpearSweep;
    }
}

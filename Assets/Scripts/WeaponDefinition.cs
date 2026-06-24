using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "TopDownRogue/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    public WeaponType weaponType = WeaponType.Knife;
    public string displayName = "Knife";
    public Sprite weaponSprite;
    public float damage = 10f;
    public float armorPiercing = 0f;
    public float attackRange = 1.2f;
    public float attackRadius = 0.45f;
    public float cooldown = 0.6f;
    public float windup = 0.15f;
    public float recovery = 0.2f;
    [TextArea(2, 4)] public string description = "Prototype weapon.";
    public Vector2 equippedOffset = new Vector2(0.45f, -0.12f);
    public float equippedScale = 0.55f;
    public float swingAngle = 85f;
    public float thrustDistance = 0.35f;
    public bool useSweepArc;
    public bool sweepLeftToRight = true;
    public Vector2 effectOffset = Vector2.zero;
    public LayerMask targetLayers;

    [Header("Combat Feel")]
    [Range(0f, 0.15f)] public float hitStopDuration = 0.035f;
    [Range(0f, 1f)] public float hitStopTimeScale = 0.05f;
    [Min(0f)] public float knockbackDistance = 1f;
    [Min(0.01f)] public float knockbackDuration = 0.12f;
    [Range(0f, 3f)] public float feedbackIntensity = 1.4f;
    [Range(0f, 2f)] public float cameraShake = 0f;
    public bool blocksMovementDuringAttack = true;
}

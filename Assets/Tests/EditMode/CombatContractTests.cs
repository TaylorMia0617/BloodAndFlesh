using NUnit.Framework;
using UnityEngine;

public class CombatContractTests
{
    [Test]
    public void DamageCalculatorUsesAttackWeaponMultiplierAndArmor()
    {
        GameObject attackerObject = new GameObject("attacker");
        GameObject defenderObject = new GameObject("defender");
        try
        {
            CharacterStats attacker = attackerObject.AddComponent<CharacterStats>();
            CharacterStats defender = defenderObject.AddComponent<CharacterStats>();
            attacker.attack = 5f;
            attacker.damageMultiplier = 2f;
            attacker.critChance = 0f;
            defender.armor = 3f;

            DamageResult result = DamageCalculator.Calculate(attacker, defender, 10f, 1f);

            Assert.AreEqual(30f, result.rawDamage);
            Assert.AreEqual(28f, result.armorReducedDamage);
            Assert.AreEqual(28f, result.finalDamage);
            Assert.IsFalse(result.isCritical);
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(defenderObject);
        }
    }

    [Test]
    public void CombatStateMachineTracksMovementLockAndClears()
    {
        CombatStateMachine stateMachine = new CombatStateMachine();

        stateMachine.Enter(CombatPhase.Windup, true);
        Assert.AreEqual(CombatPhase.Windup, stateMachine.Phase);
        Assert.IsTrue(stateMachine.BlocksMovement);
        Assert.IsFalse(stateMachine.IsReady);

        stateMachine.SetPhase(CombatPhase.Active);
        Assert.AreEqual(CombatPhase.Active, stateMachine.Phase);

        stateMachine.Clear();
        Assert.AreEqual(CombatPhase.Ready, stateMachine.Phase);
        Assert.IsFalse(stateMachine.BlocksMovement);
        Assert.IsTrue(stateMachine.IsReady);
    }

    [Test]
    public void RunLevelManagerAdvancesStageLabel()
    {
        GameObject managerObject = new GameObject("run-level-manager-test");
        try
        {
            RunLevelManager manager = managerObject.AddComponent<RunLevelManager>();
            manager.StartNewRun();
            Assert.AreEqual("1-1", manager.StageLabel);
            Assert.AreEqual(RunLevelManager.RunPhase.RunMap, manager.Phase);
            Assert.AreEqual(6, manager.CurrentInitialEnemySpawnCount);

            manager.AdvanceStage();
            Assert.AreEqual("1-2", manager.StageLabel);
            Assert.AreEqual(7, manager.CurrentInitialEnemySpawnCount);

            manager.EnterSafeRoom(Vector3.one);
            Assert.IsTrue(manager.IsInSafeRoom);
            Assert.AreEqual(Vector3.one, manager.LastMapReturnPosition);

            manager.ExitSafeRoom();
            Assert.IsFalse(manager.IsInSafeRoom);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void EnemyBehaviorResolverMapsArchetypesToStrategies()
    {
        Assert.AreEqual(EnemyBehaviorKind.Melee, EnemyBehaviorResolver.Resolve(EnemyArchetype.Attacker));
        Assert.AreEqual(EnemyBehaviorKind.Melee, EnemyBehaviorResolver.Resolve(EnemyArchetype.Shield));
        Assert.AreEqual(EnemyBehaviorKind.Ranged, EnemyBehaviorResolver.Resolve(EnemyArchetype.Ranged));
        Assert.AreEqual(EnemyBehaviorKind.Sentinel, EnemyBehaviorResolver.Resolve(EnemyArchetype.Sentinel));
    }

    [Test]
    public void PrototypeWeaponExecutorRejectsMissingInputs()
    {
        PrototypeWeaponExecutor executor = new PrototypeWeaponExecutor();
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        try
        {
            Assert.IsFalse(executor.TryExecute(null, weapon, WeaponActionKind.Primary, Vector2.up));
        }
        finally
        {
            Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void WeaponExecutorRegistryResolvesDedicatedExecutors()
    {
        WeaponExecutorRegistry registry = new WeaponExecutorRegistry();

        Assert.IsInstanceOf<KnifeWeaponExecutor>(registry.Resolve(WeaponType.Knife));
        Assert.IsInstanceOf<SwordWeaponExecutor>(registry.Resolve(WeaponType.Sword));
        Assert.IsInstanceOf<SpearWeaponExecutor>(registry.Resolve(WeaponType.Spear));
        Assert.IsInstanceOf<SpellWeaponExecutor>(registry.Resolve(WeaponType.Spell));
    }

    [Test]
    public void FeedbackPayloadCopiesWeaponFeelFields()
    {
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        try
        {
            weapon.hitStopDuration = 0.07f;
            weapon.hitStopTimeScale = 0.1f;
            weapon.knockbackDistance = 1.4f;
            weapon.knockbackDuration = 0.2f;
            weapon.feedbackIntensity = 2.1f;
            weapon.cameraShake = 0.35f;

            FeedbackPayload payload = FeedbackPayload.FromWeapon(weapon);

            Assert.AreEqual(0.07f, payload.hitStopDuration);
            Assert.AreEqual(0.1f, payload.hitStopTimeScale);
            Assert.AreEqual(1.4f, payload.knockbackDistance);
            Assert.AreEqual(0.2f, payload.knockbackDuration);
            Assert.AreEqual(2.1f, payload.intensity);
            Assert.AreEqual(0.35f, payload.cameraShake);
        }
        finally
        {
            Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void WeaponTimingKeepsWeaponSpecificWindows()
    {
        WeaponDefinition sword = ScriptableObject.CreateInstance<WeaponDefinition>();
        WeaponDefinition spearSweep = ScriptableObject.CreateInstance<WeaponDefinition>();
        try
        {
            sword.weaponType = WeaponType.Sword;
            spearSweep.weaponType = WeaponType.Spear;
            spearSweep.useSweepArc = true;

            Assert.AreEqual(0.035f, WeaponTiming.GetWindup(sword));
            Assert.AreEqual(0.055f, WeaponTiming.GetActive(sword));
            Assert.AreEqual(0.035f, WeaponTiming.GetRecovery(sword));
            Assert.AreEqual(0.42f, WeaponTiming.GetActive(spearSweep));
        }
        finally
        {
            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(spearSweep);
        }
    }

    [Test]
    public void AttackVisualProfileResolvesWeaponSpecificShapeAndScale()
    {
        WeaponDefinition sword = ScriptableObject.CreateInstance<WeaponDefinition>();
        WeaponDefinition spearSweep = ScriptableObject.CreateInstance<WeaponDefinition>();
        try
        {
            sword.weaponType = WeaponType.Sword;
            sword.attackRange = 1.6f;
            sword.attackRadius = 0.55f;

            spearSweep.weaponType = WeaponType.Spear;
            spearSweep.useSweepArc = true;
            spearSweep.attackRange = 2.1f;
            spearSweep.attackRadius = 0.35f;

            AttackVisualProfile swordProfile = AttackVisualProfileResolver.Resolve(sword, 1f);
            AttackVisualProfile spearSweepProfile = AttackVisualProfileResolver.Resolve(spearSweep, 1f);

            Assert.AreEqual(WeaponType.Sword, swordProfile.visualType);
            Assert.AreEqual(1f, swordProfile.shape);
            Assert.AreEqual(2.16f, swordProfile.range, 0.001f);
            Assert.AreEqual(1.5125f, swordProfile.width, 0.001f);

            Assert.AreEqual(WeaponType.Knife, spearSweepProfile.visualType);
            Assert.IsTrue(spearSweepProfile.isSpearSweep);
            Assert.AreEqual(0f, spearSweepProfile.shape);
            Assert.AreEqual(2.205f, spearSweepProfile.range, 0.001f);
            Assert.AreEqual(1.7f, spearSweepProfile.width, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(spearSweep);
        }
    }

    [Test]
    public void StageConfigDatabaseProvidesFirstStageBudgetAndMap()
    {
        StageConfig config = StageConfigDatabase.Get(1, 1);

        Assert.AreEqual("1-1", config.label);
        Assert.AreEqual(100, config.mapWidth);
        Assert.AreEqual(80, config.mapHeight);
        Assert.AreEqual(6, config.initialEnemySpawnCount);
        Assert.AreEqual(20, config.enemyBudget);
        Assert.AreEqual(3.5f, config.hostilitySpawnInterval, 0.001f);
    }

    [Test]
    public void EnemySenseConfigSeparatesVisionAndHostility()
    {
        EnemySenseConfig sentinel = EnemySenseConfigDatabase.Get(EnemyArchetype.Sentinel);
        EnemySenseConfig shield = EnemySenseConfigDatabase.Get(EnemyArchetype.Shield);

        Assert.AreEqual(20f, sentinel.visual.range);
        Assert.AreEqual(12f, sentinel.hostility.range);
        Assert.IsTrue(sentinel.hostility.ignoresLineOfSight);
        Assert.AreEqual(5f, shield.visual.range);
        Assert.AreEqual(4f, shield.hostility.range);
    }

    [Test]
    public void WeaponConfigDatabaseReadsWeaponNumbers()
    {
        WeaponConfig sword = WeaponConfigDatabase.Get(WeaponType.Sword);

        Assert.AreEqual("Sword", sword.displayName);
        Assert.AreEqual(16f, sword.damage);
        Assert.AreEqual(2.5f, sword.armorPiercing);
        Assert.AreEqual(1.6f, sword.attackRange);
        Assert.AreEqual(0.85f, sword.cooldown);
    }

    [Test]
    public void WeaponConfigCanApplyToRuntimeDefinition()
    {
        WeaponDefinition weapon = WeaponConfigDatabase.CreateRuntimeDefinition(WeaponType.Knife);
        try
        {
            Assert.AreEqual(WeaponType.Knife, weapon.weaponType);
            Assert.AreEqual("Knife", weapon.displayName);
            Assert.AreEqual(10f, weapon.damage);
            Assert.AreEqual(1.2f, weapon.attackRange);
        }
        finally
        {
            Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void WeaponSpecialConfigReadsRightClickActions()
    {
        WeaponSpecialConfig sword = WeaponSpecialConfigDatabase.Get(WeaponType.Sword);
        WeaponSpecialConfig spear = WeaponSpecialConfigDatabase.Get(WeaponType.Spear);
        WeaponSpecialConfig spell = WeaponSpecialConfigDatabase.Get(WeaponType.Spell);

        Assert.AreEqual("LightningDash", sword.specialType);
        Assert.AreEqual(4f, sword.dashDistance);
        Assert.AreEqual(0.22f, sword.dashDuration);
        Assert.AreEqual("SpinSweep", spear.specialType);
        Assert.AreEqual(1.5f, spear.movementSpeedMultiplier);
        Assert.AreEqual(0.5f, spell.manaCrystalCost);
        Assert.AreEqual(3f, spell.shieldDuration);
    }
}

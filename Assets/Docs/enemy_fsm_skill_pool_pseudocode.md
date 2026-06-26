# Enemy FSM and Skill Pool Pseudocode

## Goal
Use a finite state machine for enemy AI, with a separate skill pool responsible for unlocks, cooldowns, and skill selection. Designers can describe enemy behavior in plain language; implementation converts it into states, blackboard facts, and skill configs.

## Runtime Shape

```csharp
EnemyBrain
{
    EnemyBlackboard blackboard;
    EnemySkillPool skillPool;
    EnemyStateMachine fsm;

    void Tick()
    {
        blackboard.Refresh(self, target, worldHostility);
        skillPool.RefreshUnlocks(enemyConfig.skills, blackboard);
        fsm.Tick(blackboard, skillPool);
    }
}
```

## Blackboard

```csharp
EnemyBlackboard
{
    bool isDead;
    bool isStunned;
    bool isHitStopped;
    bool hasTarget;
    bool canSenseTarget;
    bool hasLineOfSight;
    bool isInAttackRange;
    bool isTooClose;
    float targetDistance;
    float healthPercent;
    int hostilityLevel;
    int bossPhase;
    Vector2 targetDirection;
    Vector2 lastKnownTargetPosition;
}
```

## State Machine

```csharp
EnemyStateMachine
{
    EnemyState current;

    void Tick(EnemyBlackboard bb, EnemySkillPool skills)
    {
        if (bb.isDead) ChangeState(Dead);
        else if (bb.isStunned || bb.isHitStopped) ChangeState(Disabled);
        else if (skills.TryPickSkill(bb, out skill)) ChangeState(CastSkill, skill);
        else
        {
            current.Tick(bb);
            if (current.IsFinished)
                ChangeState(ChooseDefaultState(bb));
        }
    }

    EnemyState ChooseDefaultState(EnemyBlackboard bb)
    {
        if (!bb.hasTarget) return Patrol;
        if (!bb.canSenseTarget) return Search;
        if (bb.isTooClose && bb.archetype == Ranged) return Reposition;
        if (bb.isInAttackRange) return BasicAttack;
        return Chase;
    }
}
```

## Skill Pool

```csharp
EnemySkillPool
{
    List<EnemySkillInstance> unlockedSkills;

    void RefreshUnlocks(SkillConfig[] configs, EnemyBlackboard bb)
    {
        foreach (config in configs)
        {
            if (config.minHostilityLevel > bb.hostilityLevel) continue;
            if (config.minBossPhase > bb.bossPhase) continue;
            UnlockIfNeeded(config);
        }
    }

    bool TryPickSkill(EnemyBlackboard bb, out EnemySkillInstance selected)
    {
        candidates.Clear();
        foreach (skill in unlockedSkills)
        {
            if (!skill.CooldownReady) continue;
            if (!skill.TriggerMatches(bb)) continue;
            if (!skill.ConditionMatches(bb)) continue;
            candidates.Add(skill);
        }

        selected = PickByPriorityThenWeight(candidates);
        return selected != null;
    }
}
```

## Cast Skill State

```csharp
CastSkillState
{
    EnemySkillInstance skill;

    void Enter()
    {
        skill.StartCast();
        if (skill.LocksMovement) self.StopMoving();
        if (skill.FacesTarget) self.FaceTarget();
    }

    void Tick()
    {
        if (skill.IsInWindup) skill.ShowTelegraph();
        if (skill.ShouldExecuteNow) skill.Execute();
        if (skill.IsFinished) MarkFinished();
    }

    void Exit()
    {
        skill.StartCooldown();
        skill.ClearTelegraph();
    }
}
```

## Skill Config Example

```json
{
  "id": "shield_bash",
  "trigger": "targetInRange",
  "priority": 80,
  "weight": 1.0,
  "cooldown": 5.0,
  "minHostilityLevel": 3,
  "minBossPhase": 0,
  "range": 1.2,
  "windup": 0.35,
  "active": 0.12,
  "recovery": 0.45,
  "movementLock": true,
  "effect": {
    "type": "meleeHit",
    "damageMultiplier": 1.4,
    "knockbackDistance": 1.4,
    "stunDuration": 0.25
  }
}
```

## Authoring Rule
When a new enemy behavior is described, convert it into:

- default FSM states it can use
- blackboard conditions it needs
- skill configs it unlocks
- boss phase or world hostility requirements
- interrupt rules: immediate, queued, or never interrupt current cast

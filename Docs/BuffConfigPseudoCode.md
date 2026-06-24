# Buff 配置与伪代码说明

这份文档用于设计安全屋祈愿神像触发的 Buff 系统。当前推荐用 JSON，不用 CSV，因为 Buff 往往有条件、触发事件、持续时间、叠加规则和多个效果，结构比武器/敌人数值复杂。

配置文件建议放在：

```text
Assets/Resources/Configs/buff_config.json
```

## Buff 类型

第一版 Buff 分三类：

1. 直接数值增益  
   例如最大生命 +10、护甲 +2、最大法力 +0.5。

2. 条件型数值变化  
   例如生命低于 35% 时伤害 +20%、护盾存在时移速 +10%、使用法术武器时冷却 -15%。

3. 事件触发型增益  
   例如击杀敌人后获得 3 秒移速、受到伤害后获得短暂无敌、消耗法力后获得护盾。

## JSON 字段

```json
{
  "id": "low_hp_damage_boost",
  "name": "濒死锋芒",
  "description": "生命值低于 35% 时，造成伤害 +20%。",
  "rarity": "rare",
  "maxStacks": 1,
  "conditions": [
    { "type": "healthPercentBelow", "value": 0.35 }
  ],
  "effects": [
    { "target": "outgoingDamage", "stat": "damageMultiplier", "mode": "multiply", "value": 1.2, "duration": 0.0 }
  ]
}
```

字段说明：

- `id`：唯一 ID，代码、存档、任务奖励都用它。
- `name`：显示名称。
- `description`：UI 描述。
- `rarity`：稀有度，建议 `common / rare / epic / cursed`。
- `maxStacks`：最大叠加层数。
- `conditions`：触发或生效条件。
- `effects`：真正修改的效果。

## Condition 类型建议

```text
always
healthPercentBelow
healthPercentAbove
manaPercentBelow
weaponTypeIs
enemyTypeIs
onEnemyKilled
onPlayerDamaged
onManaSpent
onWeaponAttackHit
onEnterSafeHouse
onEnterStage
```

示例：

```json
{ "type": "weaponTypeIs", "stringValue": "Sword" }
{ "type": "healthPercentBelow", "value": 0.35 }
{ "type": "onEnemyKilled", "value": 1.0 }
```

## Effect 类型建议

`target` 建议：

```text
player
weapon
outgoingDamage
incomingDamage
enemy
stage
```

`stat` 建议：

```text
maxHealth
armor
maxMana
moveSpeedMultiplier
damageMultiplier
attackDamage
weaponDamageMultiplier
cooldownMultiplier
critChance
critDamage
shield
invulnerable
```

`mode` 建议：

```text
add
multiply
set
grantStatus
```

`duration`：

- `0` 表示永久或条件满足期间持续生效。
- 大于 `0` 表示临时状态。

## 运行时数据结构伪代码

```csharp
class BuffConfig
{
    string id;
    string name;
    string description;
    string rarity;
    int maxStacks;
    BuffCondition[] conditions;
    BuffEffect[] effects;
}

class BuffCondition
{
    string type;
    float value;
    string stringValue;
}

class BuffEffect
{
    string target;
    string stat;
    string mode;
    float value;
    float duration;
}

class ActiveBuff
{
    BuffConfig config;
    int stacks;
    float expiresAt;
}
```

## 数据库读取伪代码

```csharp
class BuffDatabase
{
    Dictionary<string, BuffConfig> byId;
    List<BuffConfig> allBuffs;

    void Load()
    {
        TextAsset asset = Resources.Load<TextAsset>("Configs/buff_config");
        BuffConfigList list = JsonUtility.FromJson<BuffConfigList>(asset.text);

        foreach (BuffConfig buff in list.buffs)
        {
            byId[buff.id] = buff;
            allBuffs.Add(buff);
        }
    }

    List<BuffConfig> RollWishChoices(int count)
    {
        List<BuffConfig> candidates = allBuffs
            .Where(buff => PlayerCanReceive(buff))
            .Where(buff => SafeHouseCanOffer(buff))
            .ToList();

        return WeightedRandomPick(candidates, count);
    }
}
```

## 安全屋祈愿流程伪代码

```csharp
void InteractWithWishStatue()
{
    if (safeHouseState.wishStatueUsed)
    {
        ShowMessage("神像已经沉寂。");
        return;
    }

    List<BuffConfig> choices = BuffDatabase.RollWishChoices(3);

    ShowBuffChoicePanel(choices, selected =>
    {
        playerBuffController.AddBuff(selected.id);
        safeHouseState.wishStatueUsed = true;
        safeHouseExit.Unlock();
    });
}
```

## 添加 Buff 伪代码

```csharp
class PlayerBuffController
{
    List<ActiveBuff> activeBuffs;

    void AddBuff(string buffId)
    {
        BuffConfig config = BuffDatabase.Get(buffId);
        ActiveBuff existing = FindActiveBuff(buffId);

        if (existing != null)
        {
            existing.stacks = Min(existing.stacks + 1, config.maxStacks);
            RefreshDurationIfNeeded(existing);
            RecalculateStats();
            return;
        }

        activeBuffs.Add(new ActiveBuff
        {
            config = config,
            stacks = 1,
            expiresAt = CalculateExpireTime(config)
        });

        ApplyImmediateEffects(config);
        RecalculateStats();
    }
}
```

## 直接数值增益伪代码

```csharp
void RecalculateStats()
{
    RuntimeStats runtime = baseStats.Clone();

    foreach (ActiveBuff buff in activeBuffs)
    {
        foreach (BuffEffect effect in buff.config.effects)
        {
            if (!ConditionsAreMet(buff.config.conditions))
                continue;

            if (effect.duration > 0)
                continue; // 临时状态由状态系统管理

            ApplyStatEffect(runtime, effect, buff.stacks);
        }
    }

    stats.ApplyRuntimeStats(runtime);
}
```

## 条件型伤害增益伪代码

伤害公式可以在 `DamageCalculator` 里接 Buff：

```csharp
float CalculateDamage(DamageContext context)
{
    float baseDamage = context.attacker.attack + context.weapon.damage;
    float multiplier = 1.0f;

    foreach (ActiveBuff buff in attackerBuffs)
    {
        if (!ConditionsAreMet(buff.config.conditions, context))
            continue;

        foreach (BuffEffect effect in buff.config.effects)
        {
            if (effect.target == "outgoingDamage" && effect.stat == "damageMultiplier")
                multiplier *= Pow(effect.value, buff.stacks);
        }
    }

    float damageBeforeArmor = baseDamage * multiplier;
    float damageAfterArmor = Max(1, damageBeforeArmor - context.target.armor);

    if (RollCrit(context))
        damageAfterArmor *= 1 + context.attacker.critDamage;

    return damageAfterArmor;
}
```

## 事件触发型 Buff 伪代码

```csharp
void OnEnemyKilled(Enemy enemy)
{
    BuffEvent evt = new BuffEvent
    {
        type = "onEnemyKilled",
        enemyType = enemy.archetype,
        position = enemy.position
    };

    playerBuffController.HandleEvent(evt);
}

void HandleEvent(BuffEvent evt)
{
    foreach (ActiveBuff buff in activeBuffs)
    {
        if (!ConditionsAreMet(buff.config.conditions, evt))
            continue;

        foreach (BuffEffect effect in buff.config.effects)
        {
            if (effect.duration > 0)
                GrantTemporaryStatus(effect, buff.stacks);
        }
    }
}
```

## 临时状态伪代码

```csharp
void GrantTemporaryStatus(BuffEffect effect, int stacks)
{
    TemporaryStatus status = new TemporaryStatus
    {
        stat = effect.stat,
        mode = effect.mode,
        value = StackValue(effect.value, stacks),
        expiresAt = Time.time + effect.duration
    };

    temporaryStatuses.Add(status);
    RecalculateStats();
}

void Update()
{
    RemoveExpiredTemporaryStatuses();
    RecalculateStatsIfDirty();
}
```

## 推荐第一批 Buff

```text
坚韧祈愿：最大生命 +10
残响魔晶：最大法力 +0.5
锋刃祈愿：武器伤害 +12%
轻身祈愿：移动速度 +8%
冷却祈愿：武器冷却 -10%
濒死锋芒：生命低于 35% 时，造成伤害 +20%
猎杀余势：击杀敌人后，3 秒移速 +25%
秘法余辉：消耗法力后，获得 1.5 秒护盾
破甲印记：攻击命中盾兵时，破甲 +30%
血偿：受到伤害后，下一次攻击伤害 +25%
```

## 和安全屋的关系

安全屋出口不再弹 Buff。流程应为：

```text
进入安全屋
-> 商店 / 任务栏 / 黑市 / NPC 可交互
-> 玩家使用祈愿神像
-> 弹出 Buff 三选一
-> 玩家选 Buff
-> 神像变为已使用
-> 出口解锁
-> 玩家走到出口
-> 进入下一关并重新生成地牢
```


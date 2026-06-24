# 任务与商店配置伪代码

这份文档描述安全屋任务栏和商店的配置方式。任务和商品都建议使用 JSON，而不是 CSV，因为它们都需要嵌套的条件、奖励、惩罚和效果。

配置文件：

```text
Assets/Resources/Configs/task_config.json
Assets/Resources/Configs/shop_goods_config.json
```

## 任务配置

任务字段：

```json
{
  "id": "task_kill_attacker_01",
  "name": "清理战士",
  "description": "进入下一关后击杀 6 个战士。",
  "rarity": "common",
  "rewards": [
    { "type": "currency", "id": "coin", "amount": 80 }
  ],
  "failurePenalties": [
    { "type": "worldHostility", "mode": "add", "value": 0.1 }
  ],
  "completionConditions": [
    { "type": "killEnemy", "enemyArchetype": "Attacker", "count": 6 }
  ],
  "failureConditions": [
    { "type": "stageEndedWithoutCompletion" }
  ]
}
```

字段说明：

- `id`：唯一任务 ID。
- `name`：任务名称。
- `description`：任务描述。
- `rarity`：任务稀有度，建议 `common / rare / epic / cursed`。
- `rewards`：完成奖励。
- `failurePenalties`：失败惩罚，可为空数组。
- `completionConditions`：完成条件。
- `failureConditions`：失败条件。

## 任务条件类型

建议第一版支持：

```text
killEnemy
collectItem
enterRoom
clearStage
interactObject
surviveSeconds
sentinelAlarmCount
playerDeath
stageEndedWithoutCompletion
```

示例：

```json
{ "type": "killEnemy", "enemyArchetype": "Sentinel", "count": 2 }
{ "type": "collectItem", "itemId": "unstable_crystal", "count": 3 }
{ "type": "sentinelAlarmCount", "count": 3 }
```

## 任务运行时结构伪代码

```csharp
class TaskConfig
{
    string id;
    string name;
    string description;
    string rarity;
    RewardConfig[] rewards;
    PenaltyConfig[] failurePenalties;
    TaskCondition[] completionConditions;
    TaskCondition[] failureConditions;
}

class ActiveTask
{
    TaskConfig config;
    Dictionary<string, int> counters;
    bool completed;
    bool failed;
}
```

## 任务栏交互伪代码

```csharp
void InteractTaskBoard()
{
    List<TaskConfig> choices = TaskDatabase.RollTasksForStage(currentStage, count: 3);
    ShowTaskPanel(choices, selectedTask =>
    {
        PlayerTaskLog.Accept(selectedTask.id);
    });
}
```

## 任务事件推进伪代码

```csharp
void OnEnemyKilled(Enemy enemy)
{
    foreach (ActiveTask task in activeTasks)
    {
        task.UpdateProgress(new TaskEvent
        {
            type = "killEnemy",
            enemyArchetype = enemy.archetype
        });
    }
}

void ActiveTask.UpdateProgress(TaskEvent evt)
{
    foreach (TaskCondition condition in config.completionConditions)
    {
        if (ConditionMatchesEvent(condition, evt))
            counters[condition.Key] += 1;
    }

    if (AllCompletionConditionsMet())
        Complete();

    if (AnyFailureConditionMet(evt))
        Fail();
}
```

## 任务完成/失败伪代码

```csharp
void Complete()
{
    completed = true;
    foreach (RewardConfig reward in config.rewards)
        RewardSystem.Apply(reward);
}

void Fail()
{
    failed = true;
    foreach (PenaltyConfig penalty in config.failurePenalties)
        PenaltySystem.Apply(penalty);
}
```

## 商品配置

商品字段：

```json
{
  "id": "item_small_heal",
  "name": "小型治疗剂",
  "rarity": "common",
  "category": "item",
  "price": 40,
  "description": "立即恢复 20 点生命值。",
  "appearanceChance": 0.85,
  "shopTags": ["normalShop"],
  "effects": [
    { "type": "restoreHealth", "target": "player", "value": 20.0 }
  ]
}
```

字段说明：

- `id`：唯一商品 ID。
- `name`：商品名称。
- `rarity`：稀有度。
- `category`：类目，建议 `item / skill / material / buff / weaponUpgrade`。
- `price`：价格。
- `description`：描述。
- `appearanceChance`：出现概率，0 到 1。
- `shopTags`：可选，例如 `normalShop / blackMarket / rareOnly`。
- `effects`：购买后执行的效果。

## 商品效果类型

建议第一版支持：

```text
restoreHealth
restoreMana
addMaterial
addItem
unlockSkill
addBuff
addDebuff
upgradeWeapon
shopDiscount
```

示例：

```json
{ "type": "restoreHealth", "target": "player", "value": 20.0 }
{ "type": "unlockSkill", "skillId": "dash_spark", "slot": "Q" }
{ "type": "addBuff", "buffId": "cursed_weapon_damage", "stacks": 1 }
```

## 商店生成伪代码

```csharp
void InteractShop(string shopTag)
{
    List<ShopGoodConfig> goods = ShopDatabase.AllGoods
        .Where(good => good.MatchesShopTag(shopTag))
        .Where(good => Random.value <= good.appearanceChance)
        .Take(6)
        .ToList();

    ShowShopPanel(goods, selectedGood =>
    {
        TryBuy(selectedGood);
    });
}
```

## 购买伪代码

```csharp
bool TryBuy(ShopGoodConfig good)
{
    if (playerWallet.coins < good.price)
    {
        ShowMessage("钱不够。");
        return false;
    }

    playerWallet.coins -= good.price;

    foreach (ShopEffect effect in good.effects)
    {
        ShopEffectResolver.Apply(effect);
    }

    return true;
}
```

## 效果分发伪代码

```csharp
void Apply(ShopEffect effect)
{
    switch (effect.type)
    {
        case "restoreHealth":
            playerStats.RestoreHealth(effect.value);
            break;
        case "restoreMana":
            playerStats.RestoreMana(effect.value);
            break;
        case "addMaterial":
            inventory.AddMaterial(effect.materialId, effect.amount);
            break;
        case "unlockSkill":
            skillLoadout.SetSkill(effect.slot, effect.skillId);
            break;
        case "addBuff":
            playerBuffPool.AddBuff(effect.buffId, effect.buffId, BuffDatabase.GetIcon(effect.buffId));
            break;
        case "addDebuff":
            playerBuffPool.AddDebuff(effect.debuffId, effect.debuffId, DebuffDatabase.GetIcon(effect.debuffId), effect.duration);
            break;
    }
}
```

## 推荐实现顺序

1. 先实现 `TaskDatabase` 和 `ShopGoodDatabase` 读取 JSON。
2. 任务栏先展示 3 个任务，支持接受 1 个任务。
3. 商店先展示 4-6 个商品，支持购买恢复类和材料类。
4. 再把任务事件接入击杀、收集、通关。
5. 最后把技能解锁、武器升级、黑市诅咒效果接入正式系统。


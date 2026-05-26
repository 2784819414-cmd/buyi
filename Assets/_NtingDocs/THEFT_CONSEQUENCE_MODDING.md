# 《不义校园》盗窃后果 Modding 说明

盗窃后果系统的正式 owner 是：

`CampusTheftConsequenceService`

它只处理已经发生的客观事件，不创建 NPC 意图，不移动 NPC，不让 NPC 说话，也不决定 NPC 要追谁。

## 主链

```text
ProtectedTransfer / illegal item move
-> Stolen evidence
-> NPC observes through CampusNpcInventoryAwareness
-> CampusItemTheftObservedEvent or CampusContrabandFoundEvent
-> CampusTheftConsequenceService
-> CampusTheftConsequenceEvaluator
-> CampusSanctionService / confiscation / game-state consequences
```

## 配置表

盗窃后果配置在：

`Assets/NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets/TheftConsequencePresets.json`

第一版配置只负责通用规则：

- `WitnessWeights`：目击者身份权重。
- `RoomSensitivity`：房间类型或房间 ID 的敏感度。
- `ItemValues`：物品定义 ID 对应的价值。
- `SeverityBands`：轻度、中度、重度阈值。
- `ConsequenceRules`：各严重度产生的嫌疑、证据、前科、风声、严打、处分、赔偿和没收规则。

## Mod 作者应该改哪里

新增地点差异时，优先改 `RoomSensitivity`。

新增物品价值时，优先改 `ItemValues`。

调整学校严厉程度时，优先改 `SeverityBands` 和 `ConsequenceRules`。

不要在 `CampusSanctionService` 里写盗窃分支。处分服务只执行已经评估好的处分结果。

不要在 `CampusNpcInventoryAwareness` 里写后果规则。NPC 感知只负责“是否看到、是否上报、个人关系如何变化”。

不要在全局服务里移动 NPC、生成追赶意图、堵出口、让 NPC 告密或说话。后续如果要做追赶、告密、谣言，应该先把它们表达成事实、案件状态或 NPC 可读的机会，再由每个 NPC 自己的控制器决定。

## 第一版已覆盖

- 目击盗窃生成案件。
- 搜出赃物生成案件。
- 按配置计算严重度。
- 写入玩家嫌疑、证据、前科。
- 写入校园风声、严打、秩序和混乱。
- 按配置没收赃物。
- 按配置扣赔偿。
- 按配置请求处分。
- 玩家可见日志走本地化文本目录。

## 第一版故意不做

- 全局追捕。
- 出口封锁。
- 新增巡逻路线。
- 谣言传播网络。
- 告密者 AI。
- 替罪羊事件。
- 装傻、解释、甩锅、找关系等反制玩法。

这些内容需要在案件模型稳定后分阶段扩展，不能作为局部补丁插入现有服务。

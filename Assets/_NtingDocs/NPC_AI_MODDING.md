# 不义校园 NPC AI Modding 说明

这套 NPC AI 不是行为树，也不是全局规划器。它属于 `Schedule-driven Smart Object / Opportunity AI`，中文可以理解为“配置驱动的日程机会型 NPC AI”。

NPC 日常决策主链是：

`CampusConfigDrivenNpcAiController`
-> `CampusNpcEcologyPresetCatalog`
-> `ScheduleTemplate`
-> `ActionDefinition`
-> `CampusNpcActionOpportunity`
-> `CampusCharacterActionExecutor`

你如果想给 NPC 加新的日程行为，主要改这里：

`Assets/NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets/NpcEcologyPresets.json`

## 三个核心配置概念

- `FacilityGroups`
  把一组设施类型起一个稳定 ID，供后面的行为复用。
- `ActionDefinitions`
  定义“NPC 想去什么目标、到了以后用什么动作模式执行”。
- `ScheduleTemplates`
  定义“什么角色、在什么时间窗、选择什么 ActionDefinition”。

## 最小例子

先定义一个动作：

```json
{
  "Id": "roam_corridor",
  "TargetKind": "RoomType",
  "RoomType": "Corridor",
  "ActionMode": "NoOp",
  "StopDistance": 0.22
}
```

再在日程里引用它：

```json
{
  "Id": "student_break",
  "ScheduleWindows": [ "Break" ],
  "IntentKind": "Roam",
  "IntentLabel": "Break",
  "ActionId": "roam_corridor",
  "Score": 30
}
```

## Score 说明

现在的 `Score` 只是静态优先级，谁高谁先选。

代码里已经预留了 `CampusNpcScoreCalculator` 接入点，后续可以扩展成动态评分，但当前系统还不是复杂的 Utility AI。写 Mod 时，先把它理解成“简单优先级”就够了。

## 不要这样做

- 不要新建学生 / 老师 / 员工专用 AI Controller。
- 不要绕过统一的 `CampusCharacterActionExecutor` 直接改 NPC 状态。
- 不要把 NPC 行为写死在场景物体脚本里。
- 不要在 JSON 里发明复杂表达式语言。

## 建议的扩展方式

如果你要扩一个新日常行为，优先顺序通常是：

1. 先确认已有 `TargetKind` 和 `ActionMode` 能不能表达。
2. 不够时，优先补最小的 `ActionDefinition` / 目标解析路径。
3. 最后再让 `ScheduleTemplate` 在合适时间窗引用它。

这样改出来的行为最容易读，也最不容易破坏现有 NPC 生态路径。

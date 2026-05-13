# 《不义校园》V0.1 Step01 Gameplay Core 报告

## 本轮范围

- 新增玩法核心目录：`Assets/_NtingCampus/Scripts/Gameplay/Core/`
- 新增玩法总入口：`CampusGameBootstrap`
- 新增基础状态：`CampusGameState`
- 新增资源状态：`CampusResourceState`
- 新增事件日志：`CampusEventLog`
- 新增 Play Mode 调试面板：`CampusGameplayDebugPanel`
- 在 `Assets/Scenes/CampusMap.unity` 新增根物体 `NtingCampus_GameplayBootstrap`，并挂载 `CampusGameBootstrap` 与 `CampusGameplayDebugPanel`

## 初始状态

- Day = 1
- Money = 500
- DivinePower = 0
- 初始化时写入事件日志：`Gameplay Core 初始化完成：Day=1, Money=500, DivinePower=0`

## 资源接口

- `CampusResourceState.AddMoney(int amount)`
- `CampusResourceState.SpendMoney(int amount)`
- `CampusResourceState.AddDivinePower(int amount)`
- `CampusResourceState.SpendDivinePower(int amount)`

扣除接口在余额不足时返回 `false`，非正数消耗视为无需扣除并返回 `true`。

## 事件日志

- `CampusEventLog.AddLog(string message)` 会追加事件文本。
- 最多保留最近 50 条，超过后自动删除最早日志。
- Debug 面板显示最近 5 条。

## Debug UI

- 使用 IMGUI。
- Play Mode 左上角显示：
  - Day
  - Money
  - DivinePower
  - 最近 5 条事件日志

## 未接入内容

- 未修改 `CampusRuntimeMapEditor`。
- 未修改 `CampusWallAutoRenderer`。
- 未修改任何 Shader。
- 未接入 NPC、恶作剧、经济、招募。
- 未修改地图编辑器、墙体系统、光照系统、储物系统代码。

## 验证

- `dotnet build Assembly-CSharp.csproj -v:minimal`：成功，0 警告，0 错误。
- Unity Editor 6000.3.14f1 自动导入新增脚本与 `CampusMap.unity` 后完成脚本编译，Editor.log 记录 `Tundra build success`。


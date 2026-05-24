# 《不义校园》V0.1 M0 项目体检与可运行性验收报告

生成日期：2026-05-14  
检查场景：`Assets/Scenes/CampusMap.unity`  
本轮范围：只做 M0 体检和最小稳定性修复，不新增正式玩法系统。

## Unity 版本

- ProjectSettings：`6000.3.14f1 (d68c3f99a318)`
- Unity Editor：`6000.3.14f1`
- 当前项目已被一个 Unity Editor 实例打开，因此独立 `-batchmode` 二次打开被 Unity 拒绝：`Multiple Unity instances cannot open the same project.`

## 编译结果

- `dotnet build Assembly-CSharp.csproj -v:minimal`：通过，0 警告，0 错误。
- `dotnet build Assembly-CSharp-Editor.csproj -v:minimal`：通过，0 警告，0 错误。
- 当前 `Editor.log` 未发现 `error CS`、`Scripts have compiler errors`、`Tundra build failed` 等脚本编译红错。

结论：脚本编译通过，Console 未见红色编译错误。

## Play Mode 结果

- 当前 `Editor.log` 记录了 Play Mode 域重载：`Reloading assemblies for play mode`。
- Play Mode 中运行时地图编辑器成功执行恢复：`[NtingCampusRuntimeMapEditor] Restored runtime content from project folder`。
- 未发现 Play Mode 启动阶段的脚本编译阻断。

本轮发现过一个非编译异常：编辑器灯光维护菜单在 Play Mode 中尝试 `EditorSceneManager.MarkSceneDirty`。已做最小修复：Play Mode 或即将切换 Play Mode 时直接跳过该编辑器维护入口，避免 Console 出红色 `InvalidOperationException`。该修复不改运行时地图编辑器建造/恢复逻辑。

## 自动 Bootstrap 检查

- `CampusGameBootstrap`：通过代码路径检查。`CampusGameBootstrap.EnsureGameplayBootstrapAfterSceneLoad()` 会在 `CampusMap` 场景加载后自动创建 `NtingCampus_GameplayBootstrap`。
- `CampusTimeController`：通过代码路径检查。`CampusGameBootstrap.EnsureTimeController()` 会确保同物体上存在时间控制器。
- `CampusDayNightController`：通过场景检查。`CampusMap.unity` 已序列化 `Campus Day Night Controller`。
- `Campus Runtime Map Editor`：通过 Play Mode 日志检查。运行时自动安装并恢复 ProjectSync 内容。
- `CampusMapRoot / CampusFloorRoot`：通过运行时恢复路径和 ProjectSync 数据检查。当前恢复数据包含 1 层楼层。

## 日夜系统检查

- `CampusDayNightController.DefaultRealSecondsPerGameDay = 1440f`。
- `CampusMap.unity` 中 `realSecondsPerGameDay: 1440`。
- 结论：一天长度保持 1440 秒，即现实 24 分钟等于游戏内 1 天。

## 时间系统检查

- `CampusTimeSegment` 已包含中国高中作息时段：早读、上午课、课间、大课间、午休、下午课、晚饭、晚自习、熄灯、夜间自由、凌晨结算。
- `CampusTimeController` 可推进时段、日期，并同步 `CampusDayNightController.ApplyExternalGameHour()`。
- `CampusGameplayDebugPanel` 会显示日期、当前时间、当前时段、时段范围、Money、DivinePower、TimeScale 和最近事件日志。
- 初始资源来自 `CampusGameBootstrap`：Money = 500，DivinePower = 0。

## 地图恢复检查

ProjectSync 文件：`Assets/NtingCampus/UserGeneratedRuntimeContent/CampusMap_ProjectSync.json`

当前数据摘要：

- 楼层：1
- FloorTiles：2
- WallTiles：0
- Objects：3
- Stairs：0
- Rooms：0

对象摘要：

- `Cafeteria_Counter_4x1` x 2，可交互，非储物容器。
- `测试箱` x 1，可交互，储物容器，StorageSize = 8x4，StorageMaxWeight = 12。

结论：地图可以恢复，但当前内容仍是测试级地图，不是 V0.1 正式校园布局。

## 玩家移动检查

- `CampusMap.unity` 中存在 `测试人物` Prefab 实例。
- Prefab 源 GUID 已解析到 `Assets/NtingCampus/Prefabs/Player/测试人物.prefab`。
- `测试人物` Prefab 具备：
  - `CampusTestPlayerController`
  - `Rigidbody2D`
  - `CapsuleCollider2D`
  - `CampusInteractionSensor`
  - `CampusInteractionController`
- `CampusTestPlayerController.MoveSpeed = 3.5`，`InteractKey = E`。

结论：测试人物具备 Play Mode 移动与交互所需组件；日志中未见玩家移动相关报错。

## 储物 UI 检查

- `_NtingCampus/UI` 脚本编译通过。
- `测试箱.prefab` 具备：
  - `CampusPlacedObject.IsStorageContainer = true`
  - `CampusSimpleInteractable.DefaultActionId = open_storage`
- `CampusSimpleInteractable.TryOpenStorageWindow()` 会创建或查找 `StorageWindowUI`，并打开口袋、书包和目标容器。
- 当前日志未见 Storage UI 相关异常。

结论：测试箱存在时具备 E 键打开 Storage UI 的接入条件；Storage UI 可由交互系统打开并通过 Esc/关闭按钮关闭。

## Missing 引用列表

检查范围：`CampusMap.unity`、`Assets/NtingCampus/Prefabs`、`Assets/_NtingCampus/UI/Storage/Prefabs`、ProjectSync 引用。

- Missing Script：未发现 `m_Script: {fileID: 0}`。
- Missing Prefab：未发现缺失 GUID。`CampusMap.unity` 的测试人物 Prefab GUID 可解析。
- Missing Material：未发现缺失 GUID。部分 `m_Material: {fileID: 0}` 为 Unity 默认空引用，不是 Missing 资产。
- Missing Sprite：未发现缺失 GUID。部分 `m_Sprite: {fileID: 0}` 为可选空引用，不是 Missing 资产。
- Missing Tile：ProjectSync 中 `地面_人字转` 可解析到 `Assets/NtingCampus/Tiles/Floor/Imported/地面_人字转.asset`。
- ProjectSync Object：`Cafeteria_Counter_4x1`、`测试箱` Prefab 均存在。

## 修改过的文件

- `Assets/NtingCampus/Editor/CampusMapEditorUtility.cs`
  - 在 `EnsureMapLightingFromMenu()` 增加 Play Mode guard。
  - 在 `MarkSceneDirty()` 增加 Play Mode guard。
  - 目的：避免编辑器灯光维护逻辑在 Play Mode 中调用 `EditorSceneManager.MarkSceneDirty` 产生红色异常。
- `Assets/_NtingDocs/V0_1_M0_Project_Health_Check_Report.md`
  - 本报告。

未新增 `CampusV01SceneValidator.cs`，因为本轮已通过现有 Editor 日志、脚本编译和资产引用扫描完成体检；避免引入未必要的诊断运行逻辑。

## 未解决问题

- 当前项目已由 Unity Editor 打开，独立 batchmode 无法再次打开同一项目；后续如需全自动 M0，可先关闭 Editor，再运行批处理诊断。
- `CampusMap_ProjectSync.json` 当前仍是测试内容：1 层、2 个地面格、0 墙、0 房间，不满足后续 V0.1 校园布局要求。
- 自动 Bootstrap 的对象创建通过代码路径和 Play Mode 日志侧证完成；本轮没有加入自动化 Play Mode 断言脚本。
- 玩家移动和 Storage UI 是基于 Play Mode 进入、组件配置和无异常日志验证；本轮没有做屏幕级自动输入录制。

## 下一轮 M1 建议

- 在不改日夜 Shader 和地图编辑器核心逻辑的前提下，新增正式 `CampusModeController`。
- 将学生本体模式和校园神游模式写入 `CampusGameState`。
- 给 `CampusTimeController` 增加正式暂停、1x、2x API，不再让 gameplay 直接依赖地图编辑器可调的高倍速。
- 让 `CampusTestPlayerController` 只在学生本体模式接收移动和交互输入。
- 增加最小顶部状态栏，显示日期、当前时钟、当前时段、Money、DivinePower 和当前模式。

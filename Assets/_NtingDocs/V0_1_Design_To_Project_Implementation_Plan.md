# 《不义校园》V0.1 设计文档到当前 Unity 项目实施路线

生成日期：2026-05-14  
输入文档：`不义校园_V0.1_初创闭环设计文档.docx`  
目标项目：`Assets/Scenes/CampusMap.unity`

## 0. 本轮结论

本轮只做路线拆解，不新增玩法代码，不修改现有玩法、场景、美术或 Shader。

当前项目已经具备三块可复用基础：

- 地图/交互/光照基础：`Assets/NtingCampus` 已有运行时地图编辑器、Tilemap 楼层、摆放物、门、交互锚点、测试玩家、日夜光照和阴影系统。
- 储物 UI 基础：`Assets/_Nting/UI/Storage` 已有格子容器、物品定义、拖拽 UI、PlayerPrefs 临时保存、测试箱交互接入。
- 玩法核心起点：`Assets/_NtingCampus/Scripts/Gameplay/Core` 已有 `CampusGameBootstrap`、`CampusTimeController`、`CampusTimeSchedule`、`CampusResourceState`、`CampusEventLog` 和 IMGUI Debug 面板。

当前最大的缺口不是美术，也不是 Shader，而是玩法域对象缺失：房间语义、学生/老师数据、日程 AI、事件卡、恶作剧、制裁、招募、结算、正式 UI 和存档还没有统一的 gameplay 层。

时间标准必须以现实 24 分钟等于游戏内 1 天为准。当前 `CampusDayNightController` 的 `DefaultRealSecondsPerGameDay = 1440f`，`CampusMap.unity` 中 `realSecondsPerGameDay: 1440`，符合 24 分钟/天。设计文档内“8 到 12 分钟等于 1 天”已过期，不再作为实现依据。

## 1. 重点目录体检

### 1.1 `Assets/NtingCampus`

已有基础：

- `Runtime/CampusRuntimeMapEditor.cs`：运行时地图编辑器，支持地面、墙、对象、楼梯、房间标记、房间 Prefab、灯光、ProjectSync JSON。
- `Runtime/CampusMapRoot.cs`、`Runtime/CampusFloorRoot.cs`、`Runtime/CampusMapData.cs`：地图/楼层/Tile/Object/Stair 序列化基础。
- `Runtime/CampusPlacedObject.cs`：摆放物元数据，已有 `ObjectId`、占地、阻挡、视线阻挡、可交互、储物容器、交互锚点。
- `Runtime/CampusInteractionController.cs`、`CampusInteractionSensor.cs`、`CampusInteractionAnchor.cs`、`CampusSimpleInteractable.cs`：E 键交互基础，已能打开储物、开关门、写 Debug log。
- `Runtime/CampusTestPlayerController.cs`：测试学生角色移动与交互基础。
- `Runtime/CampusDayNightController.cs`：24 分钟/天日夜光照基础，并可由 `CampusTimeController` 外部驱动。

当前缺口：

- 地图编辑器中的 `CampusRuntimeRoomMarker` 只有房间名，没有 gameplay 房间类型、核心设施检测、可用地块、建造价格、房间有效性。
- `CampusPlacedObject` 只知道通用摆放物/储物/交互，不知道“课桌、讲台、黑板、床、公告栏、招募台、祭坛”等 V0.1 设施语义。
- 没有学生/老师寻路、座位、讲台、办公室处罚点、宿舍床位、安全区等 gameplay 接入点。
- `CampusMap_ProjectSync.json` 当前只有 1 层、2 个地面格、3 个对象、0 墙、0 房间、11 个灯；`RoomPrefabs/fangjian1.json` 存在 6x5 房间 Prefab，但 `Rooms.txt` 只有注释，没有正式房间定义。

实施原则：

- V0.1 不重写地图编辑器、不改 Shader、不改美术。
- 新增 gameplay 适配层读取 `CampusMapRoot`、`CampusFloorRoot`、`CampusPlacedObject` 和 `CampusRuntimeRoomMarker`，不要把玩法规则塞进 `CampusRuntimeMapEditor.cs` 这个大文件。

### 1.2 `Assets/_Nting/UI/Storage`

已有基础：

- `StorageContainerModel`：容器尺寸、占格、重叠、承重检测。
- `StorageItemDefinition`、`StorageItemRegistry`：物品静态数据和实例创建。
- `StorageMemory`、`StorageMemorySaveData`：运行时容器记忆、JSON、PlayerPrefs 临时保存。
- `StorageWindowUI`、`StorageGridUI`、`StorageItemView`、`StorageDragController`：储物窗口、拖拽、旋转、快速转移、口袋/背包/外部容器。
- `Resources/StorageItems` 已有 `note` 纸条、`phone` 手机、`lunch_box` 饭盒、`textbook` 教材、`workbook` 练习册、`snack` 辣条等，正好可供传纸条、偷看手机、偷外卖、学习压力做第一版数据。
- `CampusSimpleInteractable.TryOpenStorageWindow()` 已把 `CampusPlacedObject.IsStorageContainer` 接到 Storage UI。

当前缺口：

- 这是储物/道具层，不是恶作剧、搜查、证据、学习或外卖事件系统。
- `StorageDemoBootstrap` 只负责测试数据，不应成为正式流程入口。

### 1.3 `Assets/_NtingCampus/Scripts/Gameplay/Core`

已有基础：

- `CampusGameBootstrap`：玩法总入口，初始化 Day/Money/DivinePower/EventLog/TimeController；`CampusMap` 场景加载后可自动确保 bootstrap。
- `CampusGameState`：目前只有 `Day`。
- `CampusResourceState`：已有金钱、神力、加减接口。
- `CampusEventLog`：最多 50 条事件日志。
- `CampusTimeSegment`：已经从简单 5 段扩展到中国寄宿高中作息，包含早读、上午课、课间、大课间、午休、下午课、晚饭、晚自习、熄灯、夜间自由、凌晨结算。
- `CampusTimeSchedule`：每个时段的中文名、起止时间、时长、下一时段、时钟文本、日志文本。
- `CampusTimeController`：推进游戏内分钟、时段切换、日期推进、每日结算事件、同步日夜光照。
- `CampusGameplayDebugPanel`：Play Mode 显示日期、时间、时段、资源、倍速、最近日志。

当前缺口：

- 没有“暂停/1x/2x”的正式玩法倍速 API；当前时间倍速依赖 `CampusDayNightController.DaySpeedMultiplier`，运行时地图编辑器可调到 200x，不适合 gameplay 规则。
- 没有学生本体/校园神游模式枚举和切换控制。
- 每日结算事件有入口，但没有学费、学习值清零、警觉度下降、状态恢复、神兴趣变化等结算处理器。
- `CampusGameState` 还没有全局秩序、混乱度、教师警觉、神兴趣、无聊度、当前模式等字段。

### 1.4 `Assets/Scenes/CampusMap.unity`

当前序列化根对象：

- `Main Camera`
- `二维日光`
- `二维全局光`
- `Campus Day Night Controller`
- Prefab 实例 `测试人物`

关键事实：

- 场景内 `Campus Day Night Controller.realSecondsPerGameDay = 1440`，符合 24 分钟/天。
- 场景文件未序列化 `NtingCampus_GameplayBootstrap`；`CampusGameBootstrap.EnsureGameplayBootstrapAfterSceneLoad()` 会在 `CampusMap` 场景加载后运行时创建。
- 场景文件未序列化 `Campus Runtime Map Editor`；`CampusRuntimeMapEditor.Install()` 会运行时创建并尝试从 `UserGeneratedRuntimeContent/CampusMap_ProjectSync.json` 恢复。
- 场景本体没有正式 classroom/dorm/office/public/hr/shrine gameplay 对象，后续必须通过地图内容 + gameplay marker/registry 接入，而不是假设场景里已经有房间。

## 2. 文档模块落地状态

| 文档模块 | 落地状态 | 当前项目依据 | V0.1 落地方式 |
|---|---|---|---|
| 1. 文档目标与版本边界 | 部分已有 | 有 Core 报告和初始 `CampusGameBootstrap`，但闭环未接通 | 以本报告作为后续分轮任务源，严禁扩展到多种族、大世界、复杂战斗 |
| 2. 游戏定位与核心体验 | 部分已有 | 有学生测试角色、地图编辑器、资源状态 | M1 建立双模式，M4/M5 打通“建学校 -> 捣乱 -> 神力/金钱 -> 扩建/招募” |
| 3. V0.1 核心闭环 | 尚未开始 | Money/DivinePower 只是数值；地图、人口、恶作剧、招募没有联动 | M5 做第一轮闭环验收，不提前做大型系统 |
| 4. 世界观最小版本 | 尚未开始 | 无神谕/剧情/背景事件脚本 | M4 加 `CampusOracleService` 只做任务与日志；异族只做事件文本 |
| 5. 双模式系统 | 部分已有 | 学生移动已有；运行时地图编辑器类似神游工具但不是正式模式 | M1 新增 `CampusModeController`，让学生本体和神游模式成为 gameplay 状态 |
| 6. 时间与日程系统 | 已有基础 | `CampusTimeSegment/Schedule/Controller` 已含中国高中作息，24 分钟/天 | M1 固化暂停/1x/2x；M3 让 NPC 订阅时段变化 |
| 7. 地图与沙盒建筑体系 | 部分已有 | `CampusRuntimeMapEditor`、Tilemap、房间 Prefab、摆放物、ProjectSync | V0.1 不重写编辑器；M3 新增房间语义/有效性，M5 接建造花费与一次地皮扩展 |
| 8. 资源与经济系统 | 部分已有 | `CampusResourceState` 已有金钱/神力接口 | M5 接学费、招募费、建造费、活动收入；M4 接神力奖励 |
| 9. 人物系统 | 尚未开始 | 没有 Student/Teacher 数据脚本 | M2 新增角色数据、生成、注册、状态、记忆、玩家角色标记 |
| 10. AI 与生态行为系统 | 尚未开始 | 只有测试玩家移动；无 NPC AI | M3 做轻量“日程 + 地点行为池 + 属性权重” |
| 11. 学习系统 | 尚未开始 | 储物里有教材/练习册物品，但无学习值 | M3/M4 新增两门课、今日学习值、长期掌握度、书架学习 |
| 12. 玩家恶作剧系统 | 尚未开始 | 交互系统可复用，纸条/手机/饭盒物品可复用 | M4 新增恶作剧定义、条件、风险、奖励、后果 |
| 13. 制裁与逃脱系统 | 尚未开始 | 门/交互/移动基础可复用 | M4 新增警告、训斥、办公室、追捕、安全区 |
| 14. 事件系统 | 部分已有 | `CampusEventLog` 仅是文本日志 | M3/M4 新增事件卡、事件总线、简单导演，日志只作为展示端 |
| 15. 招募系统 | 尚未开始 | 无候选人/人事部逻辑 | M5 新增学生/老师候选、录用、第二天加入日程 |
| 16. 初始配置与第一小时体验 | 部分已有 | 初始资源 500/0 已有；玩家和时间可运行 | M2 生成 6 学生 2 老师；M5 完成第一小时闭环脚本化验收 |
| 17. UI/UX 功能需求 | 部分已有 | Debug 面板、Storage UI；无正式资源栏/人物面板/招募面板 | M1 顶栏雏形，M2 人物面板，M5 招募/结算/日志 UI |
| 18. 数据结构建议 | 部分已有 | 资源、时间、地图、储物 DTO 已有 | M2-M5 按角色、房间、事件、招募、存档分目录补齐 |
| 19. 开发优先级与里程碑 | 部分已有 | 旧文档里程碑存在，但不符合当前 6 阶段要求 | 使用本报告 M0-M5 作为新的分轮开发依据 |
| 20. 完整闭环验收标准 | 尚未开始 | 没有自动或手工闭环场景 | M5 定义一条可重复手工验收路径 |
| 21. 风险控制与设计铁律 | 部分已有 | 地图/储物/时间已经拆开，但玩法联动缺失 | 每阶段只接闭环必要规则，避免做独立小游戏 |
| 22. V0.1 功能清单 | 部分已有 | 基础地图、测试玩家、储物、时间、资源有基础 | 按 M0-M5 拆成可执行脚本清单，不一次性大改 |
| V0.1 暂不制作内容 | 暂不适合 V0.1 实现 | 多种族、大世界、复杂战斗、管线物流、派系政治、毕业传承、大型主线均未开始 | 只保留背景事件/日志，不做真实系统 |

## 3. 时间表实施标准

旧文档中的“现实 8 到 12 分钟等于游戏内 1 天”废弃。后续所有任务以 24 分钟/天为准。

当前应保留并接入的作息粒度：

- 06:30-07:00 起床/洗漱
- 07:00-07:30 早饭/到教室
- 07:30-07:55 早读
- 上午四节课，包含普通课间和 09:30-10:00 大课间/课间操
- 11:30-14:00 午饭/午休
- 下午四节课，包含普通课间
- 17:15-18:40 晚饭/自由活动
- 18:40-22:55 三段晚自习与课间
- 22:55-23:20 回宿舍/查寝
- 23:20-23:40 熄灯
- 23:40-06:00 熄灯后夜间自由状态
- 06:00-06:30 凌晨结算

实现要求：

- `CampusTimeSchedule` 作为时段唯一来源。
- `CampusDayNightController.realSecondsPerGameDay` 保持 1440 秒。
- `CampusTimeController` 需要提供 gameplay 可控的暂停/1x/2x；运行时地图编辑器中的 200x 只能用于编辑工具，不作为 V0.1 玩法倍速。
- NPC 日程不要硬编码“白天/夜晚”，而是订阅 `CampusTimeSegment` 并通过时段分类判断行为。

## 4. 六阶段开发路线

### M0 项目体检与可运行性

目标：

- 确认 `CampusMap` Play Mode 能稳定启动当前三套基础：运行时地图编辑器、玩法 Core、日夜光照。
- 确认当前 ProjectSync 地图能恢复，玩家能移动，测试箱储物能打开。
- 形成后续接入的对象锚点清单，不在本阶段实现新玩法。

需要新增的脚本：

- 可选新增 `Assets/_NtingCampus/Scripts/Gameplay/Diagnostics/CampusV01SceneValidator.cs`：只做 Editor/PlayMode 体检，不驱动玩法。
- 可选新增 `Assets/_NtingCampus/Scripts/Gameplay/Diagnostics/CampusV01BootstrapReport.cs`：运行时打印 bootstrap、time、map、player、storage 状态。

需要修改的现有脚本：

- 原则上无。
- 如果体检发现 `CampusGameBootstrap` 与场景自启动冲突，优先定位 `CampusGameBootstrap.EnsureGameplayBootstrapAfterSceneLoad()` 或 `CampusRuntimeMapEditor.Install()` 的职责边界，删除重写冲突代码，不做叠补丁。

需要接入的场景对象：

- `Assets/Scenes/CampusMap.unity`：`Main Camera`、`Campus Day Night Controller`、`测试人物`。
- 运行时自动对象：`NtingCampus_GameplayBootstrap`、`Campus Runtime Map Editor`、运行时恢复的 `CampusMapRoot`。
- `Assets/NtingCampus/UserGeneratedRuntimeContent/CampusMap_ProjectSync.json`：作为当前地图内容输入。

验收标准：

- Play Mode 后场景中存在一个 `CampusGameBootstrap`、一个 `CampusTimeController`、一个 `CampusDayNightController`。
- Debug 面板显示日期、时钟、时段、Money=500、DivinePower=0。
- `CampusDayNightController.RealMinutesPerGameDay == 24`。
- `测试人物` 可 WASD/方向键移动，E 键可触发可交互物。
- `测试箱` 可打开 Storage UI，窗口关闭后地图编辑器 overlay 状态恢复。
- 不要求地图达到 60x60，不要求生成学生老师，不要求闭环。

不做什么：

- 不新增学生/老师/AI/恶作剧。
- 不改 `CampusMap.unity` 美术摆放。
- 不改 Shader、灯光材质、墙体渲染。
- 不把 `CampusRuntimeMapEditor.cs` 拆分重构。

### M1 时间系统与双模式

目标：

- 把“学生本体模式 / 校园神游模式”做成正式 gameplay 状态。
- 固化 24 分钟/天与中国高中作息。
- 暴露暂停、1x、2x 三档玩法倍速。
- 模式切换不清除玩家追捕、训斥、上课、发呆等状态，为 M4 留接口。

需要新增的脚本：

- `Assets/_NtingCampus/Scripts/Gameplay/Core/CampusGameMode.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Core/CampusTimeSpeedMode.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Modes/CampusModeController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Modes/CampusStudentBodyState.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Modes/CampusGodViewCameraController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusTopBarUI.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusModeSwitchUI.cs`

需要修改的现有脚本：

- `CampusTimeController.cs`：新增 `SetSpeedMode(Paused/Normal/Fast)`，并限制 gameplay 倍速为 0/1/2。
- `CampusGameState.cs`：增加当前模式、全局秩序/混乱度/教师警觉/神兴趣占位字段。
- `CampusGameBootstrap.cs`：初始化 `CampusModeController`、`CampusTopBarUI`。
- `CampusGameplayDebugPanel.cs`：显示 Mode、SpeedMode、CurrentClockText。
- `CampusTestPlayerController.cs`：只在学生本体模式处理移动/交互；神游模式保留角色位置并进入“神游发呆”状态。
- `CampusDayNightController.cs`：只在必要时确认外部时钟驱动，不改光照曲线、不改 Shader。

需要接入的场景对象：

- `测试人物`：作为玩家学生本体。
- `Main Camera`：学生模式跟随/观察；神游模式俯瞰平移缩放。
- `Campus Day Night Controller`：继续由 `CampusTimeController` 外部驱动小时。
- 新增运行时 UI Canvas：资源栏、模式切换、时间倍率按钮。

验收标准：

- Play Mode 后默认学生本体模式，玩家可移动和交互。
- 按键或 UI 可切到校园神游模式，玩家角色留在原地，移动输入改为控制俯瞰相机。
- 再切回学生本体模式，玩家回到原角色当前位置和状态。
- 时间可暂停、1x、2x，Debug 面板和顶栏显示一致。
- 一整天按 24 分钟推进，能进入 `PreWakeSettlement` 并日期 +1。

不做什么：

- 不做建造菜单完整 UI。
- 不做学生/老师生成。
- 不做追捕/训斥的真实后果，只保留状态字段。
- 不使用 200x 倍速作为玩法功能。

### M2 学生/老师数据与生成

目标：

- 按文档生成初始 6 名学生、2 名老师。
- 建立角色数据、运行时实例、玩家学生身份、老师职责。
- 让后续 AI、学习、制裁、关系、记忆都能引用同一套人物数据。

需要新增的脚本：

- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterRole.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusTeacherDuty.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusSubjectType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterState.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterTrait.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterData.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterRuntime.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusPlayerCharacter.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusRosterService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusCharacterSpawner.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Characters/CampusNameGenerator.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusCharacterListUI.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusCharacterDetailUI.cs`

需要修改的现有脚本：

- `CampusGameBootstrap.cs`：初始化 roster、生成初始角色、把 `测试人物` 注册为玩家学生。
- `CampusGameState.cs`：保存学生数、老师数、当天警告次数等基础字段。
- `CampusEventLog.cs`：可新增带分类的 `AddLog(category, message)` 重载，但保留原 `AddLog(string)`。
- `CampusGameplayDebugPanel.cs`：显示学生/老师数量。
- `CampusTestPlayerController.cs`：挂接 `CampusPlayerCharacter` 或能被 `CampusCharacterSpawner` 注册。

需要接入的场景对象：

- `测试人物`：绑定玩家学生数据。
- 新增运行时 NPC 根节点：`CampusCharactersRoot`。
- 新增临时 SpawnPoint：教室入口、宿舍、办公室、公共区、人事部。没有正式房间前可先放在地图根下，M3 再改为房间注册。
- NPC 可先复用测试人物视觉或简单运行时占位物，不新增美术。

验收标准：

- 开局生成 6 名学生：普通学生 2、爱睡觉 1、捣蛋鬼 1、好学生 1、告状精 1。
- 开局生成 2 名老师：世界语言学老师兼班主任、数学老师兼巡查主任。
- 每个角色有 ID、姓名、身份、职责/班级、当前房间、易困值、捣乱值、学习值、状态、最近记忆列表。
- UI 可查看人物列表和单人详情。
- 人物数据只存在一份权威来源：`CampusRosterService`。

不做什么：

- 不做完整外观随机系统；只做颜色/名称/占位显示。
- 不做多种族角色。
- 不做复杂关系矩阵，只做事件发生后的稀疏关系和最近 5 条记忆。
- 不做 AI 行为，只让人物可生成、可注册、可被 UI 查看。

### M3 日程与课堂生态

目标：

- 把 M1 时间表接到 M2 人物。
- 建立 gameplay 房间/设施语义，至少识别教室、宿舍、办公室、公共活动区、人事部。
- 让学生和老师按时段移动、上课、课间活动、午休、晚自习、夜间自由。
- 做第一条课堂生态链：易困学生犯困/睡着 -> 老师注意力转移 -> 日志记录。

需要新增的脚本：

- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusRoomType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusFacilityType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusGameplayRoom.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusGameplayFacilityMarker.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusRoomRegistry.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusRoomValidator.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Rooms/CampusSeatSlot.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Schedule/CampusScheduleService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Schedule/CampusClassSchedule.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Schedule/CampusScheduleAgent.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/AI/CampusLightweightAIController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Study/CampusStudyProgress.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Study/CampusStudyController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Events/CampusEventBus.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Events/CampusEventCard.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Events/CampusEventDirector.cs`

需要修改的现有脚本：

- `CampusTimeController.cs`：不塞 AI，只确保 `SegmentChanged`、`DailySettlementStarted` 对外稳定。
- `CampusGameBootstrap.cs`：启动 RoomRegistry、ScheduleService、EventDirector。
- `CampusGameplayDebugPanel.cs`：显示当前课堂/房间有效性/最近生态事件。
- `CampusPlacedObject.cs`：尽量不改；若必须补字段，优先新增独立 `CampusGameplayFacilityMarker` 挂到对象上。
- `CampusRuntimeMapEditor.cs`：不直接改；房间语义通过独立 gameplay marker/registry 适配。

需要接入的场景对象：

- 地图中的房间或运行时房间 Prefab：至少标为教室、宿舍、办公室、公共活动区、人事部。
- 教室：黑板、讲台、课桌/座位点。
- 宿舍：床位点。
- 办公室：办公桌、处罚点。
- 公共活动区：闲聊点、围观点。
- 人事部：招募台。

验收标准：

- 当前地图能被 `CampusRoomRegistry` 扫描，至少 5 类 P0 房间有可用记录。
- `CampusRoomValidator` 能报告房间缺少核心设施，而不是静默通过。
- 学生在早读、上课、晚自习时进入教室；课间/午休/晚饭/夜间自由进入对应行为池。
- 教书老师在自己的课程进入教室，班主任/巡查主任有目标地点。
- 课堂自然触发至少“学生犯困/睡着”和“老师注意力转移”日志。
- 今日学习值会在上课/拿书学习/自由学习时增长，凌晨结算清零。

不做什么：

- 不做复杂寻路。第一版可用直线移动、传送到座位、或简化 waypoint。
- 不做完整社交关系网络。
- 不做所有 V0.1 事件，只做课堂生态最小链条。
- 不做正式建造收费 UI，房间可先用现有运行时地图工具配置。

### M4 恶作剧、制裁、神力

目标：

- 建立玩家主动恶作剧系统，恶作剧必须有条件、风险、奖励、后果。
- 建立老师发现违规、每日警告、训斥、办公室、追捕、逃脱的第一版。
- 接通神力奖励和神谕任务。
- 至少实现能支撑第一轮闭环的 3 种恶作剧，剩余恶作剧留 M5 或后续。

需要新增的脚本：

- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankDefinition.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankContext.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankFreshnessTracker.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Pranks/CampusPrankInteraction.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusViolationType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusSanctionLevel.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusTeacherDetectionSensor.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusSanctionController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusChaseController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Sanctions/CampusSafeZone.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Divine/CampusDivineRewardCalculator.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Divine/CampusOracleService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Divine/CampusShrineFacility.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusPrankPromptUI.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusSanctionPromptUI.cs`

需要修改的现有脚本：

- `CampusResourceState.cs`：保留接口，可增加神力/金钱变化事件供 UI 订阅。
- `CampusGameState.cs`：增加每日警告次数、班主任关注度、校园混乱度、神兴趣。
- `CampusEventLog.cs`：记录恶作剧、制裁、神谕分类日志。
- `CampusTimeController.cs`：每日结算时触发 `CampusPrankFreshnessTracker` 和警告次数清理。
- `CampusInteractionAnchor.cs` / `CampusSimpleInteractable.cs`：优先不改；通过新增 `CampusPrankInteraction` 或 gameplay action handler 挂在设施上。
- `StorageMemory.cs`：不改核心；偷外卖只读取/写入外卖事件对应容器。

需要接入的场景对象：

- 教室课桌/附近学生：传纸条。
- 教室门：上课逃课、把老师关门外。
- 公共区/宿舍储物或外卖点：偷外卖。
- 公告栏：伪造公告。
- 安全区：宿舍、公共区人群、教室座位、神龛室。
- 办公室处罚点。
- 神龛室/神秘祭坛/涂鸦墙/混乱铃铛可先用现有摆放物加 gameplay marker，不新增美术。

验收标准：

- 玩家能执行至少 3 种恶作剧：传纸条、上课逃课、偷外卖。
- 神力奖励按基础奖励、风险倍率、新鲜度衰减计算。
- 同一种恶作剧当天重复会降低收益。
- 老师发现违规后根据每日警告次数进入口头警告、训斥、办公室或追捕。
- 追捕状态下 10 秒内离开视野并进入安全区可逃脱，否则去办公室。
- 事件日志能看到恶作剧、老师反应、神力变化。
- 初始神谕任务“第一节课中制造一次不被老师成功制裁的恶作剧”可完成并奖励神力 +30，解锁神龛室建造标志。

不做什么：

- 不做复杂战斗。
- 不做真正视锥渲染或高级感知 AI；第一版用距离、房间、朝向/注意力权重。
- 不做全部 5 种恶作剧的完美版本；优先保证 3 种能闭环。
- 不做神力技能树，只做神龛室和少量设施效果。

### M5 招募、结算、第一轮闭环

目标：

- 接通每日学费、公共区活动收入、招募学生/老师、候选人次日出现、新人加入日程。
- 接通建造/设施消耗的第一版规则。
- 完成“第一天上课 -> 恶作剧 -> 神力 -> 神龛室/设施 -> 次日学费 -> 人事部招募 -> 新人加入 -> 事件变多”的 V0.1 第一轮闭环。

需要新增的脚本：

- `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusEconomyConfig.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusTuitionSettlementSystem.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusActivityIncomeSystem.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusActivityIncomePoint.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Build/CampusBuildCostService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Build/CampusLandPlotService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Recruitment/CampusRecruitmentType.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Recruitment/CampusCandidateData.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Recruitment/CampusCandidateGenerator.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Recruitment/CampusRecruitmentService.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Recruitment/CampusRecruitmentDesk.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Save/CampusV01SaveData.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Save/CampusV01SaveSystem.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusRecruitmentPanelUI.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusEventLogUI.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/UI/CampusSettlementSummaryUI.cs`

需要修改的现有脚本：

- `CampusGameBootstrap.cs`：启动经济、招募、保存系统，统一初始化顺序。
- `CampusResourceState.cs`：增加资源变化事件，确保 UI 和日志一致。
- `CampusGameState.cs`：保存地皮扩展状态、神龛室解锁、活动收入冷却、招募队列。
- `CampusEventLog.cs`：支持至少 20 条校园故事片段可读展示。
- `CampusTimeController.cs`：`DailySettlementStarted` 接入学费、学习清零、警觉下降、状态恢复、事件归档、神兴趣变化。
- `StorageMemory.cs`：正式存档时由 `CampusV01SaveSystem` 调用 `ToSaveData/LoadFromSaveData`，不继续依赖 PlayerPrefs。

需要接入的场景对象：

- 人事部招募台：挂 `CampusRecruitmentDesk`。
- 公共活动区活动点：挂 `CampusActivityIncomePoint` 或由 RoomRegistry 暴露。
- 神龛室设施：神秘祭坛、涂鸦墙、混乱铃铛 marker。
- 地皮边界：用 `CampusLandPlotService` 管理一次扩展区域，不改 Tilemap 美术。
- UI Canvas：资源栏、人物列表、招募面板、事件日志、结算摘要。

验收标准：

- 每日学费收入 = 学生人数 x 20 金钱。
- 公共活动区每天最多一次获得 80 金钱，同时提高混乱度和疲劳。
- 招生宣传费 100 金钱，第二天出现 2-4 名学生候选，录取后加入 `CampusRosterService` 并参与日程。
- 老师招聘按普通教书老师 250、班主任 350、巡查主任 400 金钱生成候选并录用。
- 能购买一次地皮扩展或完成一次基础建造消耗验证。
- 玩家能用神力建造至少 1 个神力设施，并影响事件概率或奖励。
- 新增学生能参与上课、睡觉、捣乱或课间移动。
- 事件日志累计至少 20 条故事片段。
- 完整闭环手工验收路径可跑通：进入游戏 -> 上课 -> 学生睡着 -> 玩家恶作剧 -> 获得神力 -> 神游建神龛/设施 -> 次日学费到账 -> 人事部招募 -> 新学生加入 -> 事件密度上升。

不做什么：

- 不做工资、维护费、电力、水管、食物物流。
- 不做多地皮连续扩张，只做一次。
- 不做正式长线剧情，只做神谕提示和日志。
- 不做完整商业版存档迁移，只做 V0.1 存取读档。

## 5. 后续分轮建议

推荐开发顺序：

1. M0 先跑通当前 `CampusMap` Play Mode，确认自动 bootstrap、ProjectSync、Storage UI 没有互相覆盖。
2. M1 完成模式和时间 API，因为后续所有 AI、恶作剧、结算都依赖它。
3. M2 只做人和数据，不急着做 AI。
4. M3 先做房间语义和课堂生态链，不扩展全部事件。
5. M4 做 3 个恶作剧和一条制裁链，优先保证风险/奖励/后果。
6. M5 接经济、招募、存档和第一轮闭环验收。

每轮 Codex 开发都应遵守：

- 不改 Shader。
- 不改美术资源。
- 不重构 `CampusRuntimeMapEditor.cs` 大文件。
- 若现有脚本职责与新 gameplay 冲突，先定位冲突源，删除重写对应 gameplay 代码，不在错误代码上叠补丁。
- 每轮结束必须说明修改脚本、接入场景对象、手工验收路径和未做范围。

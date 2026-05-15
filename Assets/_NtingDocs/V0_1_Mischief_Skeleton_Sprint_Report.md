# V0.1 Mischief Skeleton Sprint / 缺德玩法骨架冲刺报告

## 新增文件

- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefAnchorBootstrap.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefActionController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusSkeletonActor.cs`
- `Assets/_NtingDocs/V0_1_Mischief_Skeleton_Sprint_Report.md`

## 修改文件

- `Assets/_NtingCampus/Scripts/Gameplay/Core/CampusGameplayDebugPanel.cs`

本轮没有修改 Shader、美术资源、`CampusRuntimeMapEditor.cs`、ProjectSync JSON、正式存档、正式 UI 或日夜系统配置。

## 骨架启动方式

- `CampusMischiefAnchorBootstrap` 使用 `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)` 在 `CampusMap` 场景加载后自动运行。
- 启动时复用现有 `CampusGameBootstrap.EnsureSceneBootstrap()`，不改变初始 `Money = 500` 和 `DivinePower = 0`。
- 每次生成前会查找并清理同名根节点 `NtingCampus_V01_MischiefSkeletonRoot`，避免 Play Mode 里重复堆叠。
- 交互接入现有 `CampusInteractionAnchor`，统一 action id 为 `nting.mischief.execute`，具体行为由 payload function id 派发。

## 临时场景锚点说明

根节点下自动生成：

- `ClassroomAnchor`：教室，用于传纸条。
- `CampusShopAnchor`：学校超市，学校内部商业区，和书店在同一层楼。
- `BookstoreAnchor`：学校书店，本轮只做锚点。
- `CanteenAnchor`：食堂，本轮用于偷炸鸡。
- `LibraryAnchor`：行政楼一楼图书馆，本轮用于逗柱子。
- `OutsideDeliveryAnchor`：校外外卖灰区，本轮用于偷外卖事件。
- `SkewerStandAnchor`：校外烤面筋摊，本轮只生成老板和锚点。

锚点用运行时生成的简单贴图图标表示：教室桌子、超市货架、书本、餐盘、书架、外卖袋、烤摊。场景里不再给每个锚点挂常驻 `TextMesh` 顶字；锚点以玩家当前位置为原点做相对布局，NPC 位置也从锚点偏移派生，不写入地图编辑器数据。

## 临时 NPC 说明

- 超市店员：`Role = ShopClerk`，`Alertness = 50`，位于 `CampusShopAnchor`。
- 食堂店员：`Role = CanteenClerk`，`State = Busy`，位于 `CanteenAnchor`。
- 柱子：`Role = LibrarianZhuzi`，`State = SortingBooks`，位于 `LibraryAnchor`。
- 登记女老师：`Role = LibraryTeacher`，`Alertness = 70`，位于 `LibraryAnchor` 外侧偏移点。
- 外卖失主：`Role = DeliveryOwnerStudent`，位于 `OutsideDeliveryAnchor`。
- 烤面筋老板：`Role = SkewerBoss`，位于 `SkewerStandAnchor`。

NPC 用运行时生成的小人贴图表示，并用职业道具区分：店员柜台、厨师帽、书本、登记板、手机、烤串。NPC 图标尺寸已放大到 `0.92` Unity 单位，场景里不再给每个 NPC 挂常驻姓名文字。

## 五个缺德行为说明

- `mischief.pass_note` / 传纸条：绑定 `ClassroomAnchor`。
- `mischief.steal_fried_chicken` / 偷炸鸡：绑定 `CanteenAnchor`。
- `mischief.steal_delivery` / 偷外卖：绑定 `OutsideDeliveryAnchor`。
- `mischief.twist_bottle_caps` / 拧瓶盖：绑定 `CampusShopAnchor`。
- `mischief.confuse_zhuzi` / 逗柱子乱整理书：绑定 `LibraryAnchor`。

所有行为只做抽象结果：加神力、加热度、写日志；没有实现现实可复刻步骤、躲监控技巧、追捕链或完整盗窃系统。

## 神力奖励说明

- 传纸条：`DivinePower +5`。
- 偷炸鸡：`DivinePower +12`；食堂店员 `Busy` 时额外 `+5`。
- 偷外卖：`DivinePower +10`，3 秒后补日志“失主去年级部告状，手机先被收了。”。
- 拧瓶盖：第一次 `+8`，第二次约 `+6`，第三次及以后约 `+3`。
- 逗柱子：`DivinePower +6`，3 秒后补日志“登记老师开始怀疑今天的书架不太对。”。

奖励全部通过现有 `CampusResourceState.AddDivinePower()` 写入。

## 缺德热度说明

- 传纸条：`MischiefHeat +3`。
- 偷炸鸡：`MischiefHeat +8`。
- 偷外卖：`MischiefHeat +7`。
- 拧瓶盖：`MischiefHeat +6`。
- 逗柱子：`MischiefHeat +4`。

`CampusMischiefActionController` 内维护 `MischiefHeat`、`DailyActionCount`、`CurrentAvailableActionName`、`LastActionTime`，并预留 `ResetDailyMischief()`。本轮不接正式每日清零，也不触发正式制裁。

## Debug 面板说明

`CampusGameplayDebugPanel` 最小扩展了：

- `Money`
- `DivinePower`
- `MischiefHeat`
- 当前可触发行为
- 今日各行为触发次数
- 最近 10 条日志
- 交互方式提示：出现交互提示后按 `E`

Debug 面板不再提供行为触发按钮，避免绕过场景交互。所有缺德行为都通过场景里的 `CampusInteractionAnchor` 触发。

## 玩家触发方式说明

- 采用现有玩家对象的 `CampusInteractionController` / `CampusInteractionSensor`。
- 玩家靠近对应 `CampusInteractionAnchor`，屏幕出现交互提示后按 `E`，由统一 action id `nting.mischief.execute` 触发对应 function id。
- 缺德锚点优先级设为 `90`，交互 Collider 半径为 `1.35`，并轻微上移 `0.18`；交互提示锚点上移到 `0.46`。行为触发以现有交互系统命中的 `CampusInteractionAnchor` 为准，不再在行为控制器里追加第二层距离校验。
- 行为冷却时会把对应 `CampusInteractionAnchor` 标记为不可用，并显示“冷却中”，避免按 `E` 时静默失败。
- Debug 面板只显示状态，不再提供备用触发按钮。

## 手工验收步骤

1. 打开 `Assets/Scenes/CampusMap.unity`。
2. 进入 Play Mode。
3. Hierarchy 中确认生成 `NtingCampus_V01_MischiefSkeletonRoot`。
4. 展开根节点，确认 7 个锚点和 6 个 NPC 都存在。
5. 移动测试人物靠近教室、食堂、学校超市、图书馆、校外外卖灰区。
6. 等屏幕出现对应交互提示后按 `E`，确认 `DivinePower`、`MischiefHeat`、行为次数和日志变化。
7. 重复触发拧瓶盖，确认收益从第一次 `+8` 衰减到后续较低奖励。
8. 触发偷外卖后等待 3 秒，确认延迟日志出现。
9. 触发逗柱子后等待 3 秒，确认登记老师怀疑书架的延迟日志出现。
10. 测试储物箱原有 `E` 交互，确认储物 UI 仍可打开。
11. 确认日夜系统仍按原本节奏推进。

## 未做内容

- 未做完整课堂日程。
- 未做完整学生老师 AI。
- 未做完整偷超市、偷书店、偷图书馆系统。
- 未做烤面筋跑单系统。
- 未做老板追责、监控、追捕、办公室处罚。
- 未做现实躲监控技巧或现实盗窃教程。
- 未做招募、建造、神龛室、正式存档、正式 UI。
- 未做美术优化或 Shader 修改。

## 下一轮建议

1. 把超市、书店、食堂、图书馆、外卖点替换为正式房间/区域语义。
2. 增加真实课堂逃课进入食堂的路线。
3. 增加第二批行为：偷笔、偷杂志、偷书、买烤面筋跑单、调黑暗饮料。
4. 增加最小制裁链：店员怀疑、老师登记、年级部追责。
5. 增加神游视角下的区域状态查看。

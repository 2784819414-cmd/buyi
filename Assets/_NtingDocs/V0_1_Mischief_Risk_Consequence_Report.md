# V0.1 Mischief Risk Consequence Sprint / 缺德风险后果骨架报告

## 新增文件

- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefAreaState.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefConsequenceController.cs`
- `Assets/_NtingDocs/V0_1_Mischief_Risk_Consequence_Report.md`

## 修改文件

- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefActionController.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Skeleton/CampusMischiefAnchorBootstrap.cs`
- `Assets/_NtingCampus/Scripts/Gameplay/Core/CampusGameplayDebugPanel.cs`

本轮没有修改 Shader、美术资源、`CampusRuntimeMapEditor.cs`、正式 UI、正式存档、地图编辑器数据或 ProjectSync JSON。

## 区域风险状态说明

新增 `CampusMischiefAreaState`，每个区域维护：

- `AreaName`
- `Suspicion`
- `AlertLevel`
- `LastIncidentName`
- `IsTemporarilyHot`

当前区域包括：

- `CampusShop`
- `Canteen`
- `Library`
- `OutsideDelivery`
- `Classroom`
- `SkewerStand`

每次触发缺德行为都会给对应区域增加 `Suspicion`，并记录最后事件名。`Suspicion >= 20` 后区域进入敏感状态，`AlertLevel` 只用于 Debug 显示和轻量判断，不做正式 AI。

## 后果控制器说明

新增 `CampusMischiefConsequenceController`，挂在 `NtingCampus_V01_MischiefSkeletonRoot` 上。

它负责：

- 维护区域风险状态。
- 写入后果日志。
- 记录最近后果日志。
- 判断 `MischiefHeat` 阈值。
- 处理敏感区域收益折扣。
- 处理拧瓶盖行为的 20 秒临时禁用。

行为触发仍由 `CampusMischiefActionController` 执行；奖励发放后会调用后果控制器进行区域风险和后续日志结算。

## 五个行为的后果说明

- 传纸条：教室 `Suspicion +1`；受当前 `MischiefHeat` 影响，有小概率出现“老师看了你一眼。”，触发时额外 `MischiefHeat +2`、`Suspicion +3`。
- 偷炸鸡：食堂店员 `Busy` 时食堂 `Suspicion +2`；如果店员不是 `Busy`，记录“食堂店员觉得熟食柜少了点什么。”，食堂 `Suspicion +5`、`MischiefHeat +3`。
- 偷外卖：校外外卖灰区 `Suspicion +8`、`MischiefHeat +5`；3 秒后记录“失主去年级部告状，手机先被收了。”，6 秒后记录“年级部要求严查外卖，没人提外卖为什么会丢。”。
- 拧瓶盖：学校超市 `Suspicion +6`；第二次后记录“超市店员开始检查饮料货架。”，第三次后记录“这排饮料暂时被盯上了。”，第四次后临时禁用该行为 20 秒。
- 逗柱子乱整理书：图书馆 `Suspicion +4`；3 秒后记录“登记老师开始怀疑今天的书架不太对。”；重复触发后记录“柱子把你的话当成了新规定。”，并让神力收益继续衰减。

## MischiefHeat 阈值说明

- `MischiefHeat >= 30`：记录“今天校园里不太安生。”
- `MischiefHeat >= 60`：记录“老师们开始觉得有人在故意找事。”

每个阈值本轮只记录一次，不触发追捕、传送办公室或扣钱。

## 敏感区域规则说明

- 区域 `Suspicion >= 20` 后，`IsTemporarilyHot = true`。
- 首次进入敏感状态时记录“区域名暂时变得敏感。”
- 在敏感区域继续触发缺德行为，神力收益按 80% 结算，并写入收益变化日志。
- 在敏感区域继续触发缺德行为，会额外提高 `MischiefHeat +2`。

## Debug 面板说明

`CampusGameplayDebugPanel` 显示：

- `MischiefHeat`
- 当前可触发行为
- 今日各行为触发次数
- 每个区域的 `Suspicion`
- 每个区域的 `AlertLevel`
- 当前区域是否敏感
- 最近后果日志
- 最近事件日志
- 五个 Debug 行为按钮

Debug 行为按钮保留，用于快速压测风险和阈值；按钮调用的仍是 `CampusMischiefActionController` 的统一行为入口，会走冷却、临时禁用、奖励折扣和后果结算。

## 手工验收步骤

1. 打开 `Assets/Scenes/CampusMap.unity`。
2. 进入 Play Mode，确认 `NtingCampus_V01_MischiefSkeletonRoot` 存在，并带有 `CampusMischiefConsequenceController`。
3. 靠近五个已有缺德行为锚点，出现交互提示后按 `E`，确认行为仍可触发。
4. 观察 Debug 面板，确认对应区域 `Suspicion` 增加。
5. 连续触发行为，把 `MischiefHeat` 推到 30，确认出现“今天校园里不太安生。”。
6. 继续触发行为，把 `MischiefHeat` 推到 60，确认出现“老师们开始觉得有人在故意找事。”。
7. 让某一区域 `Suspicion >= 20`，确认该区域变敏感。
8. 在敏感区域继续触发行为，确认神力收益降低，并记录收益变化日志。
9. 触发偷外卖，等待 6 秒，确认两段延迟后果日志都出现。
10. 重复触发拧瓶盖，确认超市店员检查货架日志、饮料被盯上日志和 20 秒临时禁用。
11. 重复触发逗柱子，确认“柱子把你的话当成了新规定。”日志出现。
12. 测试储物箱、日夜系统、地图编辑器入口，确认未被本轮改动破坏。

## 未做内容

- 未做完整追捕。
- 未做办公室处罚。
- 未做完整老师视野。
- 未做完整监控系统。
- 未做现实躲监控技巧。
- 未做正式 UI。
- 未做正式存档。
- 未做招募系统、神龛室、建造系统。
- 未做动物伤害行为。
- 未新增大量新恶作剧。

## 下一轮建议

1. 把区域风险绑定到正式房间/区域语义，而不是临时 Skeleton 锚点。
2. 给每个区域增加最小 NPC 反应动画或状态图标。
3. 增加“怀疑对象”字段，让风险可以落到玩家或其他学生身上。
4. 给年级部、老师、店员做最小制裁链，但仍避免复杂追捕。
5. 把 Debug 面板拆成更清晰的运行时调试页，正式 UI 另起一轮设计。

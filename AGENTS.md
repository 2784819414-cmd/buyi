# AI Handoff Rules

## 最高原则：简单，一般，通用，清晰，严整，面向 mod

- 所有代码必须干净清晰、结构分明、逻辑优先。简单优先，一般优先，通用优先，对 mod 友好，对源码阅读玩家友好。
- 严禁用局部补丁掩盖根因。先找出导致问题出现的拥有路径，删除或重写混乱逻辑，再提交干净清晰的代码。
- 优先配置表驱动，不要把正常玩法规则硬编码进零散 `if/switch`、对象名、位置猜测、prefab 名称或隐藏 fallback。
- 如果现有配置表、overlay 数据、preset 或显式 mod 字段已经能表达需求，必须只改数据；不得为了新增变体、NPC 职责、服务站、窗口、菜单或行为而修改运行时代码。只有在证明现有配置模型无法表达一般规则时，才允许扩展最小拥有的配置模型和校验路径。
- 新功能必须尽量进入可独立维护的清晰层级，不要硬插入旧架构。若旧路径已经混乱，应重写拥有路径，而不是叠加条件。
- 每次改动都要面向开源读者和 mod 开发者：概念要有稳定 ID、显式数据定义、校验、编辑器/文档支持，并且能从顶层规则读懂。

## CheckoutPoint 与服务站配置锁定

`CheckoutPoint` 只表示“柜台/窗口类设施”的物理类型，不拥有收银、登记、借阅、取号、报到等玩法含义。

硬规则：

- 新增任何 `CheckoutPoint` 用途，只允许新增或修改配置表、overlay 数据、preset 或显式 mod 字段，不得新增运行时代码硬编码。
- 收银、登记、借阅、取号、报到等差异必须由 `ServiceStationPresets.json` 的 `StationTypeId`、`InteractionActionId`、`Clearance`、`Availability`、slot 定义，以及对象交互 preset 表达。
- 地图中的具体柜台必须在 gameplay overlay 中绑定稳定 service station，并在对象交互 preset 中显式绑定交互动作和本地化提示。不得依赖 `CheckoutPoint` 默认推断出“结账”或其他玩家可见含义。
- 运行时代码不得写出 `CheckoutPoint -> retail checkout`、`CheckoutPoint -> registration` 之类的一对一硬编码。默认动作、提示、HUD 文案和清算规则都必须从服务站/交互配置解析。
- 如果现有配置模型无法表达新的柜台功能，只能扩展最小拥有的配置模型和校验路径；扩展后必须删除旧硬编码路径，不能让配置路径和硬编码 fallback 并存。
- mod 作者新增柜台变体时，正常入口应是：`ServiceStationPresets.json` 定义服务站类型，`ObjectInteractionPresets.json` 定义对象交互，地图 gameplay overlay 绑定 station/facility/slot，必要时补本地化文本目录。

## 开工前必须做的宏观分析

所有代码变更必须从系统层面开始，不能先治症状。

严禁：

- 同一条规则在多个文件重复出现。
- 一个类混合数据解析、状态变更、决策、UI、持久化。
- mod 作者必须阅读无关运行时代码才能新增物品、设施、NPC 职责、窗口、菜单、恶作剧或行为。
- 正常玩法依赖 fallback、对象名、位置猜测或隐式扫描。
- 修 bug 需要继续给混乱代码加特殊分支。
- 新玩家可见字符串绕过多语言系统。

## 可读源码与 mod 架构锁定

项目是开源且面向 mod 的。代码清晰度是硬要求，不是偏好。

硬规则：

- 一个玩法概念必须只有一个明显拥有者。不要让物理对象、运行时服务、编辑器标记、UI 面板和 NPC 行为各自重新定义同一概念。
- 保持层级分离：数据定义、世界事实、单角色状态、决策、执行、表现、编辑器序列化、校验不能混在一个大类里。
- 优先显式 mod 数据和编辑器字段。名称/位置猜测只能用于迁移或诊断，不能成为正常设计。
- fallback 只能是安全网。任何承载正常玩法的 fallback 都说明模型错误，必须重设数据模型或拥有路径。
- 不要把旧混乱行为留在新清晰路径旁边。清晰路径建立后，删除旧拥有路径。
- 完成改动前，必须能用 3 到 5 句普通话解释受影响系统，并指出 mod 作者在哪里新增变体。
- 文本是 mod 面接口的一部分。新玩法概念必须通过显式本地化字段或子系统文本目录暴露显示文本。

严禁把正常玩家可见含义藏在：

- 对象名、prefab 名、transform 名。
- 生成 ID、文件名、资源路径。
- 枚举 `ToString()`。
- 单语言 legacy 字段。
- 隐式 fallback 文案。

## 玩家可见文本本地化锁定

所有玩家可见文本必须通过现有多语言系统。

硬规则：

- 资产或 mod 数据拥有的文本使用 `CampusLocalizedText`，包括物品名、描述、使用文本、容器名、菜品名、恶作剧名、对象提示覆盖、角色显示名等。
- 运行时代码拥有的文本使用清晰的子系统文本目录，包括提示、状态、事件日志、debug 面板、服务摘要、校验信息、启动流程、商业、食堂、检查、NPC 生态、教室、处罚、恶作剧、交互文本。
- legacy 单语言字段只能作为迁移 fallback。正常玩法不能依赖它们携带唯一有效文本。
- 不得在 `GUILayout`、UI Toolkit、TextMeshPro、事件日志、交互提示、状态标签、气泡文本、物品使用消息、运行时编辑器消息、服务摘要中新增裸字符串。
- debug 面板也是玩家/开发者可见 UI，必须使用多语言目录。
- 新增文本子系统时，在所属子系统附近新增或扩展一个明显目录，不要散落 `if language` 分支。
- 完成前必须做残留文本扫描和编译/构建检查。任何剩余可见字面量必须迁移到目录/本地化字段，或明确说明它只是内部 ID、层级名、异常、诊断、迁移 fallback。

## NPC 主观自治锁定

NPC 是独立主体，不是全局系统指挥的单位。

硬规则：

- 全局系统只能提供客观事实、事件投递、公开查询、校验、以及被明确请求的动作执行。
- 全局系统不得返回或创建 `CampusNpcIntent`。
- 全局系统不得实现 `TickClerk`、`TickCustomer`、`TickTeacher`、`TickStudent` 等角色驱动 tick。
- 全局系统不得遍历所有 NPC 来决定行为、目标、发言、调查、排队、点单、服务或反应。
- 全局系统不得调用 NPC intent、navigation、speech、mind、state、decision API 来推动行为。
- 事件是客观事实。NPC 反应必须由每个 NPC 自己的控制器通过自己的缓存/错峰事实视图读取并决定。
- 如果需要共享行为，应表达为事实查询或动作校验，再由每个 NPC 控制器自愿调用。

当前重写触发器：

- 任何全局玩法服务中形如 `TryBuild...Intent` 或 `Tick...` 且会决定 NPC 角色行为的方法，必须删除并迁移到对应 NPC 控制器或配置驱动路径。
- 任何 NPC 正常决策中出现同帧全局扫描、全 NPC 批量感知、或 `FindObjectsByType` 查目标的路径，必须改成世界事实/索引/每 NPC 缓存视图。

## 配置驱动 NPC 生态锁定

常规 NPC 行为必须通过配置进入系统。

固定路径：

`Assets/NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets/NpcEcologyPresets.json -> CampusNpcEcologyPresetCatalog -> CampusConfigDrivenNpcAiController -> CampusNpcActionOpportunity -> CampusCharacterActionExecutor`

硬规则：

- 新常规 NPC 行为优先新增或扩展 `NpcEcologyPresets.json`。
- 不得重新引入 `CampusStudentAiController`、`CampusTeacherAiController`、`CampusStaffAiController`。
- 不得按学生/老师/职员拆分常规生态控制器。角色差异来自 schedule config、assignments、shared fact queries。
- 不得新增全局 NPC planner、behavior tree framework 或玩法专用 NPC controller 来绕过配置路径。
- 如果现有 `ScheduleTemplates`、`TargetKind`、`ActionMode` 无法表达新行为，只扩展最小拥有的配置/动作路径。
- 配置缺失时应通过校验报告问题。不要让 `Fallback`、`Common`、随机游荡或对象名猜测承载正常设计。

## 角色运行时与动作执行锁定

主角和 NPC 都是角色，必须共享同一底层运行时和动作规则。

硬规则：

- 使用同一个角色基底，以 `CampusCharacterRuntime` 为中心。
- 玩家输入和 NPC AI 只是不同决策来源，不是不同玩法底座。
- 拾取、丢弃、使用、交易、偷窃、检查、食堂服务、移动、交互必须走同一 actor-based gameplay service。
- 核心动作服务必须接收 actor/runtime 和目标数据，不能静默读取 player singleton 作为唯一支持角色。
- `IsPlayer` 分支只能影响输入、摄像机、UI、提示、debug 展示，不能制造不同的合法性、库存、物品、移动或交互规则。
- 不得为同一动作新增玩家专用和 NPC 专用执行路径。已有分叉必须重写成统一 actor path。
- 不得通过直接改 transform、item container、held item 或 character state 来模拟 NPC 动作。
- 共享执行层不得创建或打开玩家专属 UI。表现层应监听执行结果或由玩家输入层请求 UI。

## 世界事实与扫描规则

正常玩法不能依赖即时全场扫描。

硬规则：

- 世界事实由明确服务、注册表、索引或缓存视图提供。
- NPC AI 必须通过每 NPC 缓存/错峰视图读取世界事实。
- `FindObjectsByType`、`FindFirstObjectByType`、场景遍历只能用于 bootstrap、编辑器工具、迁移、诊断或低频安全兜底。
- 如果扫描结果决定正常 NPC 目标、物品选择、服务队列、调查对象或反应，必须重写为显式事实源。
- 掉落物、房间、设施、服务站点、角色 roster 都应有稳定 ID 和公开查询入口。

## 阴影系统锁定

除非我明确要求重设计阴影系统，否则不要修改这些核心文件：

- `Assets/NtingCampus/Runtime/NtingShadowCasterProfile.cs`
- `Assets/NtingCampus/Runtime/NtingCustomShadowSystem.cs`
- `Assets/NtingCampus/Resources/Shaders/NtingSpriteShadowExtrude.shader`

硬规则：

- 不改变自定义阴影投影算法、proxy mesh 生成、shader 采样逻辑。
- 不恢复全局场景扫描来自动补 `NtingShadowCasterProfile`。
- 不把 floors、tilemaps、`FloorRoot` 或 ground objects 注册为物体阴影 caster。
- 不为新导入对象新增 per-object 阴影特殊分支。

当前正确流程：

- 场景对象通过 `CampusPlacedObject.EnsureShadowRegistration()` 注册阴影。
- 运行时对象实例必须位于楼层 `PropsRoot` 下。
- 运行时导入源对象 clone 进场景时必须调用 `CampusSceneInstanceUtility.NormalizeSceneInstance()`，避免继承的 `DontSave*` flag 阻止阴影收集。
- `NtingCustomShadowSystem` 只收集已有 `NtingShadowCasterProfile`，不得扫描 placed objects 并自行添加 profile。

新导入物体阴影，先修资产/导入规范：

- 裁掉 PNG 大透明边。
- Sprite pivot、footprint、视觉比例对齐旧对象。
- 0/90/180/270 方向图使用同一裁剪/contact point 规则。

## 保留工作

- 可能存在未提交改动。不得回退、覆盖或整理与当前任务无关的 dirty files。
- 修改前先看 `git status --short`，只处理任务相关文件。
- 如果必须碰到已有改动，先读懂并在其基础上工作。
- 不得使用 `git reset --hard`、`git checkout --` 等破坏性命令，除非用户明确要求。

## 完成前检查

每次代码变更完成前至少检查：

- 受影响系统能否用 3 到 5 句解释清楚。
- mod 作者新增变体的位置是否明确。
- 是否新增了正常玩法 fallback。
- 是否新增了对象名/枚举名/文件名作为显示文本。
- 是否新增了玩家可见裸字符串。
- 是否新增了 NPC 全局扫描或全局 planner。
- 玩家和 NPC 是否仍共享 `CampusCharacterRuntime` 和 actor-based action path。
- 是否运行了残留文本扫描和可用的编译/构建检查。

如果某项检查无法执行，最终回复必须说明原因。

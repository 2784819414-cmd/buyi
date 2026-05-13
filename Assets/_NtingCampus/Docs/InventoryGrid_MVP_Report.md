# Inventory Grid MVP Report

## 1. 本轮新增文件列表

- `Assets/_NtingCampus/Scripts/InventoryGrid/Data/InventoryContainerType.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Data/ItemDefinition.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Data/InventoryContainerDefinition.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Data/InventoryItemRegistry.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Runtime/ItemInstance.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Runtime/PlacedItem.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Runtime/InventoryContainerRuntime.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Runtime/InventorySaveData.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/UI/InventoryCellView.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/UI/InventoryGridView.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/UI/InventoryItemView.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/UI/InventoryWindowController.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Tests/InventoryGridDemoBootstrap.cs`
- `Assets/_NtingCampus/Scripts/InventoryGrid/Tests/Editor/InventoryGridDemoSceneBuilder.cs`
- `Assets/_NtingCampus/Docs/InventoryGrid_MVP_Report.md`

本轮同时提供测试资产与 Demo 场景：

- `Assets/_NtingCampus/Scenes/InventoryGridDemo.unity`
- `Assets/_NtingCampus/ScriptableObjects/InventoryItems/*.asset`
- `Assets/_NtingCampus/ScriptableObjects/InventoryContainers/*.asset`
- `Assets/_NtingCampus/Prefabs/UI/InventoryGrid/*.prefab`

补充测试容器：

- `Assets/_NtingCampus/ScriptableObjects/InventoryContainers/TestBox_Default.asset`：`测试箱`，4x4，`Custom` 容器，最大承重 12kg。

## 2. 核心类说明

- `ItemDefinition`：物品静态数据，包含二维尺寸、旋转、重量、可疑度、气味、噪音、违禁标签和堆叠字段。
- `ItemInstance`：物品运行时实例，保存 instanceId、旋转状态和堆叠数量，并根据旋转状态计算当前宽高。
- `PlacedItem`：物品在容器中的左上角坐标。
- `InventoryContainerDefinition`：容器静态数据，包含容器类型、尺寸、暴露系数、取出速度、最大承重和是否随身。
- `InventoryContainerRuntime`：核心数据层，负责放置、移动、旋转、移除、查找、承重检查、序列化和反序列化。
- `InventoryGridView`：根据容器定义生成格子和物品 UI，不写死容器尺寸。
- `InventoryItemView`：物品 UI，支持拖拽、右键旋转、双击/Shift 点击快速转移。
- `InventoryWindowController`：管理当前打开的左右两个容器窗口和快速转移。
- `InventoryRoundedBoxGraphic`：纯 UGUI 图形绘制的圆角矩形、描边和格子底板，不依赖外部 UI 图片素材。
- `InventoryRuntimeWindowHost`：运行时从场景交互直接打开储物窗口，供测试箱等世界物体调用。

## 3. 如何打开 Demo 场景测试

打开 `Assets/_NtingCampus/Scenes/InventoryGridDemo.unity`，进入 Play。

如需重新生成测试资产、UI prefab 或 Demo 场景，可在 Unity 菜单执行 `Nting Campus/Inventory Grid/Rebuild Demo Scene`。

场景顶部按钮：

- `打开 口袋 + 书包`
- `打开 书包 + 桌肚`
- `打开 书包 + 储物柜`
- `打开 书包 + 测试箱`
- `添加测试物品到书包`
- `清空所有容器`

操作：

- 拖动物品到格子中摆放。
- 右键点击物品旋转。
- 双击物品快速转移到另一个打开容器。
- Shift + 左键点击物品也会尝试快速转移。
- 靠近地图里的 `测试箱` 按 E，可打开 `书包 + 测试箱` 的运行时储物窗口。

## 4. 已实现功能

- 矩形二维物品尺寸。
- 可旋转物品宽高交换。
- 旋转后越界或重叠会自动回退。
- 口袋、书包、桌肚、储物柜、测试箱五类测试容器。
- 数据层越界、重叠、超重检查。
- 容器内移动和容器之间转移。
- 拖拽时显示绿色/红色预览。
- 非法放置回到原位置，不丢物品。
- 纯图形拼装 UI：圆角窗口、面板阴影、描边格子、风险色条物品卡和按钮状态。
- 双击和 Shift 点击快速转移。
- 容器满或超重时转移失败并保留原物品。
- DTO 存档结构和 `ToSaveData()` / `LoadFromSaveData()`。

## 5. 未实现但已预留的功能

- 正式搜查概率计算。
- 容器套容器。
- L/T 等非矩形物品。
- 正式背包打开入口和剧情绑定。
- 自动整理。当前已通过 `ItemDefinition.HasEvidenceRisk` 预留避开可疑/违禁物品的判断入口。
- 正式图标和美术动效。

## 6. 后续建议

- 搜查系统：使用 `item.suspicion * container.searchExposure * searchIntensity * depthFactor` 作为第一版发现概率基础。
- 桌肚深浅层：可先用格子 y 坐标或额外 depth map 表示靠外/靠里。
- 物品气味扩散：根据 `smell` 和容器封闭程度向外传播风险。
- 书包鼓包：根据占用面积、重量和物品尺寸计算外观异常。
- 可疑物品证据链：用 `forbiddenTags` 和 instanceId 记录物品来源、转移路径和被目击状态。
- 自动整理但避开可疑物品：整理普通物品时跳过 `HasEvidenceRisk` 或带特定 forbiddenTags 的物品，避免破坏搜查风险位置。

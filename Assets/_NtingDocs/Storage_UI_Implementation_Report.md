# Storage UI Implementation Report

## 1. 新增文件

- `Assets/_NtingCampus/UI/Scripts/Storage/StorageContainerModel.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageItemModel.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageItemDefinition.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageItemRegistry.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageMemory.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageMemorySaveData.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageWindowUI.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageGridUI.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageSlotUI.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageItemView.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageDragController.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageDemoBootstrap.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageBoxGraphic.cs`
- `Assets/_NtingCampus/UI/Scripts/Storage/StorageUIUtility.cs`
- `Assets/_NtingCampus/UI/Storage/Prefabs/StorageWindow.prefab`
- `Assets/_NtingCampus/UI/Storage/Prefabs/StorageGrid.prefab`
- `Assets/_NtingCampus/UI/Storage/Prefabs/StorageSlot.prefab`
- `Assets/_NtingCampus/UI/Storage/Prefabs/StorageItemView.prefab`
- `Assets/_NtingCampus/UI/Storage/Resources/StorageItemRegistry.asset`
- `Assets/_NtingCampus/UI/Storage/Resources/StorageItems/*.asset`

## 2. 如何打开测试 UI

进入 Play Mode 后，`StorageDemoBootstrap` 会通过 `RuntimeInitializeOnLoadMethod` 自动创建 `Canvas_Storage` 并打开正式储物窗口。

Demo 会优先加载 `Resources/StorageItemRegistry.asset` 作为物品注册表；如果资源缺失，则创建运行时 fallback 注册表。

当前 Demo 数据：
- 左侧默认显示 4 个 2x3 衣服口袋。
- 背包页签可切换，示例背包尺寸为 5x6。
- 右侧显示 4x4 测试箱。

## 3. 数据结构说明

`StorageContainerModel` 表示一个格子容器：
- `Id`
- `DisplayName`
- `Columns`
- `Rows`
- `MaxWeight`
- `Items`

`StorageItemModel` 表示一个占格物品：
- `Id`
- `DefinitionId`
- `InstanceId`
- `DisplayName`
- `Width`
- `Height`
- `Weight`
- `Description`
- `X`
- `Y`
- `Rotated`
- `ThemeColor`
- `Icon`

放置检测由容器模型执行，UI 层只发起操作和刷新显示。

`StorageItemDefinition` 是物品静态配置，负责尺寸、重量、说明、颜色和图标。`StorageItemRegistry` 按 `definitionId` 查找定义并创建可放入容器的物品实例。

`StorageMemory` 是当前阶段的储物记忆层：
- 按 `containerId` 保存和复用 `StorageContainerModel`。
- UI 关闭再打开不会重置物品位置。
- 提供 `ToSaveData()` / `LoadFromSaveData()`。
- 提供 `ToJson()` / `LoadFromJson()`。
- 提供 `SaveToPlayerPrefs()` / `LoadFromPlayerPrefs()` 作为临时验证入口，后续可替换为正式存档系统。

## 4. 拖拽实现说明

拖拽由 `StorageDragController` 统一管理。

- BeginDrag 时记录鼠标在物品 RectTransform 内的抓手偏移。
- 拖拽时物品会移到 `DragLayer`，保证不被面板遮挡。
- 拖拽坐标通过 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 转换。
- Drop 坐标基于拖拽物品左上角计算最近格子。
- 合法区域显示绿色预览，非法区域显示红色预览。
- 松手合法则提交到目标容器，非法则回滚到原容器原坐标。

## 5. 口袋 / 背包 / 测试箱配置

`StorageDemoBootstrap` 当前创建：
- 左胸袋：2x3
- 右胸袋：2x3
- 左裤袋：2x3
- 右裤袋：2x3
- 学生书包：5x6，最大重量 20kg
- 测试箱：4x4，最大重量 12kg

后续正式接入时，只需要创建新的 `StorageContainerModel` 并传入 `StorageWindowUI.Open(...)`。

物品应通过 `StorageItemRegistry.CreateItem(definitionId, instanceId)` 创建，或通过 `StorageMemory.TryPlaceNewItem(containerId, definitionId, instanceId, x, y)` 直接放入记忆容器。

## 6. 已知问题

- 当前使用 Unity UI Text 作为 TMP 未初始化时的稳定兜底。
- Prefab 是脚本驱动模板，正式美术可在此基础上继续替换面板和物品表现。
- Demo 会自动打开 UI，后续接入正式流程时可移除或禁用 `StorageDemoBootstrap`。
- `PlayerPrefs` 保存只是临时验证接口，不是正式存档系统。

## 7. 下一步建议

- 接入真实装备系统，让衣服和裤子决定口袋数量。
- 接入背包装备状态和不同背包尺寸。
- 接入测试箱 / 桌肚 / 储物柜等交互容器数据源。
- 增加非法放置状态栏闪红和窗口淡入淡出。
- 增加物品图标资源和更细的证据风险提示。
- 将 `StorageMemorySaveData` 接入正式存档系统。

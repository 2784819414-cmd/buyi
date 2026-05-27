# 角色经济与零售定价

角色经济的拥有路径保持简单：

- 每个角色的钱在 `CampusCharacterData -> CampusCharacterEconomyState`。
- 加钱、扣钱、转账入口在 `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusEconomyService.cs`。
- 全局 `CampusResourceState` 只保留精神力，不保存全局金钱。

受保护物品清算的主链路：

`ServiceStationPresets.json`
-> `CampusServiceStationPresetCatalog`
-> `CampusServiceStationRegistry`
-> `CampusProtectedTransferClearanceService`
-> `CampusEconomyService`
-> `CampusProtectedTransferState.ClearPendingTransfer`

零售结账和图书借阅登记不是两套运行时代码。它们都通过服务站配置声明：

- `InteractionActionId`
- `Clearance.Mode`
- `Clearance.PriceMode`
- `Clearance.Scope`：`AllPending` 清算角色身上所有待清算受保护物品；`StationRoom` 只清算来自服务站所在房间的待清算物品。
- `Clearance.CompleteText`
- `Clearance.NoPendingItemsText`
- `Clearance.InsufficientFundsText`

mod 作者主要改三个地方：

1. 商品价格  
`Assets/_NtingCampus/UI/Storage/Resources/StorageItems/*.asset`  
字段：`Price`

2. 角色初始金钱  
- 场景角色：`CampusSceneCharacterDefinition.InitialMoney`
- 运行时 overlay 角色：`CampusRuntimeGameplayActorSnapshot.InitialMoney`
- 运行时地图编辑器预设：`CampusRuntimeGameplayActorPresetCatalog`

3. 结账或登记窗口
- `ServiceStationPresets.json` 定义服务站类型、交互动作、清算规则和本地化文本。
- `ObjectInteractionPresets.json` 定义对象交互锚点和提示。
- 地图 gameplay overlay 绑定 service station、facility、slot、operator。

不要新增 `CheckoutPoint -> 结账` 或 `CheckoutPoint -> 登记` 的运行时硬编码。`CheckoutPoint` 只表示柜台物理设施，玩法含义来自服务站和对象交互配置。

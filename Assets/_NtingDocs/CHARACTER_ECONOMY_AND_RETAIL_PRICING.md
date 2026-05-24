# 角色经济与零售定价

现在的经济 owner 很简单：

- 每个角色自己的钱在 `CampusCharacterData -> CampusCharacterEconomyState`
- 统一加钱、扣钱、转账入口在 `Assets/_NtingCampus/Scripts/Gameplay/Economy/CampusEconomyService.cs`
- 全局 `CampusResourceState` 只保留神力，不再保存全局金钱

零售结账主链：

`CampusRetailService`
-> 统计当前店铺房间里的待结算商品
-> 累加 `StorageItemModel.Price`
-> 用 `CampusEconomyService` 扣当前角色的钱
-> 扣钱成功后清掉 `PendingProtectedTransfer`

Mod 作者主要改两个地方：

1. 商品价格  
`Assets/_Nting/UI/Storage/Resources/StorageItems/*.asset`  
字段：`Price`

2. 角色初始金钱  
- 场景角色：`CampusSceneCharacterDefinition.initialMoney`
- 运行时 overlay 角色：`CampusRuntimeGameplayActorSnapshot.InitialMoney`
- 运行时地图编辑器预设：`CampusRuntimeGameplayActorPresetCatalog`

这套设计故意保持最小化：

- 不做全局钱包
- 不做复杂税费、折扣、商店账户
- 不做玩家和 NPC 两套经济规则

角色自己有钱，商品自己有价，结账时统一扣钱。仅此而已。

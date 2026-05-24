# 《不义校园》受保护转移、赃物与风险说明

这条链现在的正式模型是：

1. 普通物品
2. 待清算的受保护转移
3. 真正的赃物证物

它不是“看见 `Suspicious` 就已经算偷窃”。
当前正式术语是 `StorageItemLegalState.PendingProtectedTransfer`。
`StorageItemLegalState.Suspicious` 只是保留给旧存档、旧 prefab 和旧 Mod 的兼容别名，当前校园玩法里通常表示：

`Pending Protected Transfer / Pending Checkout`

也就是“已经从受保护来源拿出，但还没有离开来源房间，也还没有完成结算”。

## 主链

核心 owner 在：

`Assets/_NtingCampus/Scripts/Gameplay/Inventory/CampusProtectedTransferState.cs`

统一规则是：

- 在 `ProtectedTransfer` 来源中拿走物品时，先进入 pending transfer。
- 只要还在 `SourceRoomId` 内，它仍然是待清算状态。
- 一旦离开 `SourceRoomId`，就升级为真正 `Stolen` evidence。

## 谁负责什么

- `CampusProtectedTransferState`
  只负责 pending transfer 的状态判断和升级。
- `CampusItemLegalityService`
  负责判断当前移动是不是非法，并在非法时写入证物状态。
- `CampusInventoryTransferService`
  负责真正的物品移动与事件发布。
- `CampusContrabandService`
  只扫描角色当前携带的证物结果。
- `CampusNpcInventoryAwareness`
  只感知统一结果，不再自己定义升级规则。

## 风险值

风险值不是“会不会变成赃物”，而是“这件事被看见后有多严重”。

当前来源主要有：

- 来源容器自己的 `SuspicionRisk`
- 来源容器访问策略的额外风险
- 物品自己的 `SuspicionRisk`
- 少量重量修正

见：

`Assets/_NtingCampus/Scripts/Gameplay/Inventory/CampusItemRiskUtility.cs`

NPC 目击后会在这个基础上再按直视、余光、听见等情况加权。

## 玩家能看到的表现

- `Pending Checkout`
  说明这件物品还处于待清算状态。
- `Stolen`
  说明它已经是正式赃物证物。
- `Risk`
  说明被目击或被查获后的风险强度。

相关 UI 在：

`Assets/_NtingCampus/UI/Scripts/Storage/StorageWindowUI.cs`

## 对 Mod 作者最重要的结论

- “待清算”不等于“已经偷窃成功”。
- 让一个场景支持“先拿后结算”，应该走 `ProtectedTransfer` 路径。
- 让一个行为真正进入盗窃/目击/处罚链，关键不是拿起物品，而是离开来源房间后升级为证物。
- 不要在别的系统里重新发明 pending -> stolen 规则，统一走 `CampusProtectedTransferState`。

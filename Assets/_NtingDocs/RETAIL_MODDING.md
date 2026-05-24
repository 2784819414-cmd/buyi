# 《不义校园》零售/超市 Modding 说明

这套超市系统不是独立小游戏，也不是 NPC 专用脚本。
它走的是统一角色运行时和统一物品合法性链，主路径很简单：

`GoodsShelf`
-> 顾客拿货
-> 商品进入“待结算”状态
-> `CheckoutPoint` 结账
-> 未结账离店时升级为真正盗窃标记

## 两种货架

- `Container`
  货架本体就是一个受保护储物容器，玩家或 NPC 直接对货架交互拿货。
- `DirectPickupDisplay`
  货架会在架面上生成可拾取商品，玩家或 NPC 直接拾取展示商品。

## 地图编辑器里改哪里

选中货架对象后，在对象设置里看 `零售货架 / Retail Shelf` 区块。

主要字段：

- `Shelf Mode`
  只读。说明这个货架是容器货架还是直摆货架。
- `Item Definition ID`
  这格货架卖什么商品，例如 `retail_water`。
- `Auto Restock`
  是否自动补货。
- `Container Stock`
  容器货架的默认库存。
- `Display Count`
  直摆货架的展示数量。

这些字段现在会进入地图对象快照，不再只是组件上的隐式状态。

## 一个最小超市

最小闭环只需要三样东西：

1. 一个 `RetailArea` 房间。
2. 至少一个 `GoodsShelf`。
3. 至少一个 `CheckoutPoint`。

推荐最小测试组合：

- 饮料货架：`Item Definition ID = retail_water`
- 零食货架：`Item Definition ID = retail_potato_chips`
- 收银台：`TypeId = CheckoutPoint`

## NPC 想逛超市时改哪里

不要新写角色专用 AI Controller。
继续改这里：

`Assets/NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets/NpcEcologyPresets.json`

让日程模板引用现有零售动作即可，例如：

- `pick_goods_from_shelf`
- `checkout_retail_goods`

## 不要这样做

- 不要绕过统一 `CampusCharacterActionExecutor`。
- 不要把“卖什么商品”写死在场景物体名字里。
- 不要把“未结账”直接当成普通偷窃证物。
- 不要给学生/老师/员工各写一套独立超市 AI。

这套系统的目标不是复杂商业模拟，而是给校园地图提供一个清楚、统一、可扩展的零售生态路径。

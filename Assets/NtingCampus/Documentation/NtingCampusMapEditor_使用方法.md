# Nting Campus Map Editor 使用方法

## 适用环境

- Unity 6000.3.14 或更高的 Unity 6 版本。
- 2D 项目。
- 需要 Unity Tilemap 相关模块。

## 包体内容

- `Assets/NtingCampus`
  - 地图编辑器窗口、运行时组件、ScriptableObject 配置、Debug 资源、墙体自动渲染资源。
- `Assets/瓦片`
  - 统一贴图源目录。
  - `Assets/瓦片/地面`：地面贴图。
  - `Assets/瓦片/墙`：墙体贴图。

## 导入

1. 将包导入 Unity 项目，或直接复制 `Assets/NtingCampus` 和 `Assets/瓦片` 到目标项目。
2. 等待 Unity 编译完成。
3. 打开菜单：`Tools/Nting Campus/Map Editor`。
4. 第一次使用时点击 `Generate Debug Assets` 或 `Refresh Assets/瓦片`。

## 基本绘制

1. `Map Root` 区域创建或选择 `CampusMapRoot`。
2. `Floor` 区域选择当前编辑楼层。
3. `Palette` 区域选择地面、墙、Prefab 或楼梯资源。
4. `Brush Mode` 选择绘制模式：
   - `Paint Floor Tile`：绘制地面。
   - `Paint Wall Tile`：绘制逻辑墙。
   - `Place Prefab`：放置 Prefab。
   - `Place Stair`：放置楼梯。
   - `Erase`：擦除。
   - `Pick`：拾取当前格子的资源。
5. 在 Scene 视图内绘制。

## 墙体视觉

墙体分为两层：

- `Tilemap_WallLogic`：逻辑墙和唯一碰撞层。
- 自动生成的视觉层：
  - `WallCapTilemap`
  - `WallFaceTilemap`
  - `WallSideTilemap`
  - `WallOverlayTilemap`

不同墙逻辑 Tile 已经不再共用同一套视觉。系统通过 `CampusWallVisualCatalog` 的 `LogicTile -> WallRenderProfile` 绑定决定每个墙格使用哪套顶面、墙面和侧面贴图。

## 为某种墙单独设置贴图

1. 在 `Palette` 中选择一个墙 Tile。
2. 在 `Wall Visuals` 区域设置：
   - `Wall Face Texture`：墙面/侧面材质，通常从 `Assets/瓦片/墙` 选择。
   - `Wall Top Texture`：墙顶材质，通常从 `Assets/瓦片/地面` 或自定义顶面贴图选择。
3. 点击 `Apply Wall Textures`。
4. 系统会为当前墙 Tile 生成或更新独立的 `CampusWallRenderProfile`。
5. 点击 `Rebuild All Wall Visuals` 或等待自动 rebuild 后查看效果。

## Debug View

`Wall Debug View` 支持：

- `Show Final Wall Visuals`：只看最终墙体视觉。
- `Show Wall Logic Only`：只看逻辑墙和碰撞层。
- `Show Both`：同时显示逻辑层和视觉层。

## 保存和读取

- `Save Map`：保存当前地图数据到 `CampusMapData`。
- `Load Map`：从 `CampusMapData` 恢复地图。
- 读取时只恢复逻辑层、地面、Prefab、楼梯等数据，墙体视觉会自动重建。

## 资源目录约定

后续所有贴图统一放在：

- `Assets/瓦片/地面`
- `Assets/瓦片/墙`

导入或刷新时，编辑器只从 `Assets/瓦片` 下读取贴图源。

## 注意事项

- 视觉 Tile 的 ColliderType 必须保持 `None`。
- 墙碰撞只应存在于 `Tilemap_WallLogic`。
- 不要手动在视觉层绘制墙体，视觉层由自动渲染器重建。
- 修改墙贴图后，如果场景没有刷新，点击 `Rebuild All Wall Visuals`。

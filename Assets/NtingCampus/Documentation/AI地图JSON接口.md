# AI 地图 JSON 接口

AI 不直接操作编辑器窗口。AI 只输出结构化 JSON，导入器负责校验、解析稳定 ID、生成 `CampusMapData`，再走现有地图加载和墙体重建路径。

菜单入口：

- `Tools/Nting Campus/Generate AI Map ID Catalog`
- `Tools/Nting Campus/Import AI Map JSON`

最小示例：

```json
{
  "MapId": "school_demo",
  "Floors": [
    {
      "FloorIndex": 1,
      "IsUnlocked": true,
      "Rooms": [
        {
          "Id": "classroom_1a",
          "Rect": { "X": 0, "Y": 0, "Width": 12, "Height": 8 },
          "FloorTileId": "floor.concrete.light",
          "WallTileId": "wall.gray.basic"
        }
      ],
      "Objects": [
        {
          "ObjectId": "desk.student",
          "Cell": { "X": 3, "Y": 3 },
          "Rotation90": 0
        }
      ],
      "Stairs": [
        {
          "FromCell": { "X": 10, "Y": 2 },
          "ToFloor": 2,
          "Rotation90": 1
        }
      ]
    }
  ]
}
```

ID 规则：

- `FloorTileId` 必须能在 AI 地图资源目录中解析到地面瓦片。
- `WallTileId` 必须能在 AI 地图资源目录中解析到逻辑墙瓦片。
- 地面和墙体瓦片 ID 由 `Assets/NtingCampus/ScriptableObjects/CampusAiMapAuthoringAssetCatalog.asset` 显式拥有。
- `ObjectId` 必须能在物体资源面板、AI 地图资源目录，或 `Assets/NtingCampus/Prefabs/Props` 下解析到带 `CampusPlacedObject.ObjectId` 的 prefab。
- 导入器允许 GUID 作为迁移或工具链稳定 ID，但正常创作应优先使用显式玩法 ID。

导入约束：

- 校验失败时不会生成地图。
- 导入成功时会用 JSON 内容替换当前 `CampusMapRoot` 下的楼层。
- 缺失瓦片、缺失物体、非法楼层、非法房间尺寸都会报告错误。
- 房间会填充地板，并在矩形边界生成墙体逻辑瓦片；墙体视觉仍由现有墙体自动渲染器重建。
- `FloorTiles` 和 `WallTiles` 可用于在房间规则之外补充单格瓦片。

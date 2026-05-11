using UnityEditor;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class RestroomStallPrefabRepairer
    {
        private const string PrefabPath = CampusMapEditorUtility.PropPrefabsPath + "/" + CampusObjectNames.SquatStall + ".prefab";
        private const string UrinalPrefabPath = CampusMapEditorUtility.PropPrefabsPath + "/" + CampusObjectNames.Urinal + ".prefab";
        private const string BaseSpritePath = "Assets/NtingCampus/Tiles/Source/Props/Restroom_SquatStall_Base_64.png";
        private const string ClosedDoorSpritePath = "Assets/NtingCampus/Tiles/Source/Props/Restroom_SquatStall_DoorClosed_64.png";
        private const string OpenDoorSpritePath = "Assets/NtingCampus/Tiles/Source/Props/Restroom_SquatStall_DoorOpen_64.png";
        private const string DoorPanelSpritePath = "Assets/NtingCampus/Tiles/Source/Props/Restroom_SquatStall_DoorPanel_46.png";
        private const string UrinalSpritePath = "Assets/NtingCampus/Tiles/Source/Props/Restroom_Urinal_32.png";
        private const string PrefabPalettePath = CampusMapEditorUtility.DefaultPrefabPalettePath;
        private const float BaseVisualOffsetY = 0.609375f;
        private const float DoorPivotOffsetY = -0.7265625f;
        private const float DoorPivotOffsetX = -0.71875f;
        private const float DoorVisualOffsetX = 0.71875f;
        private const float DoorWidthWorldUnits = 1.4375f;
        private const float DoorHeightWorldUnits = 1.40625f;

        [MenuItem("Tools/Nting Campus/Repair Restroom Prefabs")]
        private static void RepairFromMenu()
        {
            Repair(true);
        }

        private static void Repair(bool logResult)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Sprite baseSprite = LoadSprite(BaseSpritePath);
            Sprite closedDoorSprite = LoadSprite(ClosedDoorSpritePath);
            Sprite openDoorSprite = LoadSprite(OpenDoorSpritePath);
            Sprite doorPanelSprite = LoadSprite(DoorPanelSpritePath);
            if (baseSprite == null || closedDoorSprite == null || openDoorSprite == null || doorPanelSprite == null)
            {
                if (logResult)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Restroom stall prefab repair skipped because one or more stall sprites are missing.");
                }

                return;
            }

            bool loadedExistingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject prefabRoot = loadedExistingPrefab
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject(CampusObjectNames.SquatStall);

            ConfigurePrefab(prefabRoot, baseSprite, closedDoorSprite, openDoorSprite, doorPanelSprite);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);

            if (loadedExistingPrefab)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            else
            {
                Object.DestroyImmediate(prefabRoot);
            }

            AddPrefabToPalette(savedPrefab != null ? savedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));

            Sprite urinalSprite = LoadSprite(UrinalSpritePath);
            if (urinalSprite != null)
            {
                bool loadedExistingUrinalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UrinalPrefabPath) != null;
                GameObject urinalPrefabRoot = loadedExistingUrinalPrefab
                    ? PrefabUtility.LoadPrefabContents(UrinalPrefabPath)
                    : new GameObject(CampusObjectNames.Urinal);

                ConfigureUrinalPrefab(urinalPrefabRoot, urinalSprite);
                GameObject savedUrinalPrefab = PrefabUtility.SaveAsPrefabAsset(urinalPrefabRoot, UrinalPrefabPath);

                if (loadedExistingUrinalPrefab)
                {
                    PrefabUtility.UnloadPrefabContents(urinalPrefabRoot);
                }
                else
                {
                    Object.DestroyImmediate(urinalPrefabRoot);
                }

                AddPrefabToPalette(savedUrinalPrefab != null ? savedUrinalPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(UrinalPrefabPath));
            }
            else if (logResult)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Restroom urinal prefab repair skipped because the urinal sprite is missing.");
            }

            AssetDatabase.SaveAssets();

            if (logResult)
            {
                Debug.Log("[NtingCampusMapEditor] Repaired restroom prefab sprite bindings.");
            }
        }

        private static Sprite LoadSprite(string path)
        {
            EnsureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureSpriteImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 128f) ||
                           importer.mipmapEnabled ||
                           importer.filterMode != FilterMode.Point ||
                           importer.wrapMode != TextureWrapMode.Clamp ||
                           !importer.alphaIsTransparency ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Center || settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                changed = true;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigurePrefab(GameObject root, Sprite baseSprite, Sprite closedDoorSprite, Sprite openDoorSprite, Sprite doorPanelSprite)
        {
            root.name = CampusObjectNames.SquatStall;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            CampusPlacedObject placed = GetOrAdd<CampusPlacedObject>(root);
            placed.ObjectId = CampusObjectNames.SquatStall;
            placed.FootprintSize = new Vector2Int(2, 3);
            placed.SortingOrderOffset = 8;
            placed.BlocksMovement = true;
            placed.BlocksSight = true;
            placed.IsInteractable = true;

            Transform baseVisual = GetOrCreateChild(root.transform, CampusObjectNames.BaseVisual, CampusObjectNames.LegacyBaseVisual);
            baseVisual.localPosition = new Vector3(0f, BaseVisualOffsetY, 0f);
            baseVisual.localRotation = Quaternion.identity;
            baseVisual.localScale = Vector3.one;
            SpriteRenderer baseRenderer = GetOrAdd<SpriteRenderer>(baseVisual.gameObject);
            baseRenderer.sprite = baseSprite;
            baseRenderer.sortingOrder = 300;

            Transform doorPivot = GetOrCreateChild(root.transform, CampusObjectNames.DoorPivot, CampusObjectNames.LegacyDoorPivot);
            MoveRootChildUnder(root.transform, doorPivot, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual);
            MoveRootChildUnder(root.transform, doorPivot, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider);
            doorPivot.localPosition = new Vector3(DoorPivotOffsetX, DoorPivotOffsetY, 0f);
            doorPivot.localRotation = Quaternion.identity;
            doorPivot.localScale = Vector3.one;

            Transform doorVisual = GetOrCreateChild(doorPivot, CampusObjectNames.DoorVisual, CampusObjectNames.LegacyDoorVisual);
            doorVisual.localPosition = new Vector3(DoorVisualOffsetX, 0f, 0f);
            doorVisual.localRotation = Quaternion.identity;
            doorVisual.localScale = Vector3.one;
            SpriteRenderer doorRenderer = GetOrAdd<SpriteRenderer>(doorVisual.gameObject);
            doorRenderer.sprite = closedDoorSprite;
            doorRenderer.sortingOrder = 301;

            Transform frameColliders = GetOrCreateChild(root.transform, CampusObjectNames.FrameColliders, CampusObjectNames.LegacyFrameColliders);
            frameColliders.localPosition = Vector3.zero;
            ConfigureFrameColliders(frameColliders.gameObject);

            Transform doorColliderTransform = GetOrCreateChild(doorPivot, CampusObjectNames.DoorCollider, CampusObjectNames.LegacyDoorCollider);
            BoxCollider2D doorCollider = GetOrAdd<BoxCollider2D>(doorColliderTransform.gameObject);
            doorCollider.isTrigger = false;
            doorCollider.offset = new Vector2(DoorVisualOffsetX, 0f);
            doorCollider.size = new Vector2(DoorWidthWorldUnits, DoorHeightWorldUnits);

            RestroomStallDoor door = GetOrAdd<RestroomStallDoor>(root);
            door.DoorPivot = doorPivot;
            door.DoorRenderer = doorRenderer;
            door.ClosedSprite = closedDoorSprite;
            door.OpenSprite = openDoorSprite;
            door.DoorCollider = doorCollider;
            door.PlacedObject = placed;
            door.StartsOpen = false;
            door.OpenAngle = 90f;
            door.AnimationDuration = 0.22f;
            door.SetOpen(false);
        }

        private static void ConfigureUrinalPrefab(GameObject root, Sprite sprite)
        {
            root.name = CampusObjectNames.Urinal;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.sortingOrder = 300;

            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(root);
            collider.isTrigger = false;
            collider.offset = Vector2.zero;
            collider.size = new Vector2(0.55f, 0.8f);

            CampusPlacedObject placed = GetOrAdd<CampusPlacedObject>(root);
            placed.ObjectId = CampusObjectNames.Urinal;
            placed.FootprintSize = Vector2Int.one;
            placed.SortingOrderOffset = 0;
            placed.BlocksMovement = true;
            placed.BlocksSight = false;
            placed.IsInteractable = false;
        }

        private static void ConfigureFrameColliders(GameObject target)
        {
            BoxCollider2D[] existing = target.GetComponents<BoxCollider2D>();
            for (int i = existing.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(existing[i]);
            }

            AddBox(target, new Vector2(-0.91f, 0f), new Vector2(0.16f, 2.88f));
            AddBox(target, new Vector2(0.91f, 0f), new Vector2(0.16f, 2.88f));
            AddBox(target, new Vector2(0f, 1.36f), new Vector2(1.82f, 0.2f));
            AddBox(target, new Vector2(-0.73f, -1.32f), new Vector2(0.38f, 0.24f));
            AddBox(target, new Vector2(0.73f, -1.32f), new Vector2(0.38f, 0.24f));
            AddBox(target, new Vector2(0f, 0.12f), new Vector2(0.82f, 1.04f));
        }

        private static void AddBox(GameObject target, Vector2 offset, Vector2 size)
        {
            BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.offset = offset;
            collider.size = size;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName, params string[] legacyNames)
        {
            Transform child = FindDirectChild(parent, childName, legacyNames);
            if (child != null)
            {
                child.name = childName;
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void MoveRootChildUnder(Transform root, Transform parent, string childName, params string[] legacyNames)
        {
            Transform child = FindDirectChild(root, childName, legacyNames);
            if (child == null || child.parent != root)
            {
                return;
            }

            child.name = childName;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }

        private static Transform FindDirectChild(Transform parent, string name, params string[] aliases)
        {
            Transform child = CampusObjectNames.FindDirectChild(parent, name);
            if (child != null)
            {
                return child;
            }

            if (aliases == null)
            {
                return null;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                child = CampusObjectNames.FindDirectChild(parent, aliases[i]);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void AddPrefabToPalette(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            CampusPrefabPalette palette = AssetDatabase.LoadAssetAtPath<CampusPrefabPalette>(PrefabPalettePath);
            if (palette == null)
            {
                return;
            }

            palette.RemoveInvalidEntries();
            if (!palette.Prefabs.Contains(prefab))
            {
                palette.Prefabs.Add(prefab);
                EditorUtility.SetDirty(palette);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    public sealed class CampusPlacedObjectInteractionAnchor
    {
        public string AnchorId;
        public string DisplayName;
        public bool Enabled = true;
        public Vector3 LocalPosition = Vector3.zero;
        public float Radius = 0.65f;
        public string PromptText = "\u4ea4\u4e92";
        public string ActionId;
        public string Payload;
        public string TargetComponentType;
        public int Priority = 120;
        public bool UseTargetDoorStatePrompt;
        public bool LogInteraction = true;
        public string InteractionLogMessage;

        public CampusPlacedObjectInteractionAnchor Clone()
        {
            return new CampusPlacedObjectInteractionAnchor
            {
                AnchorId = AnchorId,
                DisplayName = DisplayName,
                Enabled = Enabled,
                LocalPosition = LocalPosition,
                Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(Radius),
                PromptText = PromptText,
                ActionId = ActionId,
                Payload = Payload,
                TargetComponentType = TargetComponentType,
                Priority = Priority,
                UseTargetDoorStatePrompt = UseTargetDoorStatePrompt,
                LogInteraction = LogInteraction,
                InteractionLogMessage = InteractionLogMessage
            };
        }
    }

    /// <summary>
    /// Metadata for an editor-placed campus object instance.
    /// </summary>
    public sealed class CampusPlacedObject : MonoBehaviour
    {
        private const string CustomInteractionAnchorRootName = "\u4ea4\u4e92\u951a\u70b9";
        private const string CustomInteractionAnchorName = "\u73a9\u5bb6\u4ea4\u4e92";
        private const string CustomInteractionAnchorPrefix = "\u81ea\u5b9a\u4e49\u4ea4\u4e92_";
        private const string CustomInteractionPromptFallback = "\u4ea4\u4e92";
        private const string WallMountedVisualRootName = "WallMountedVisual";
        private const string WallMountedMeshNamePrefix = "WallMountedPlateMesh_";
        private const string WallMountedMaterialNamePrefix = "WallMountedPlateMaterial_";
        private const float WallMountedThickness = 0.04f;
        private const float WallMountedSurfaceGap = 0.001f;
        private const int WallMountedSortingOrderOffset = 1;
        public const float DefaultInteractionAnchorRadius = 0.65f;
        public const float MinInteractionAnchorRadius = 0.3f;
        public const float MaxInteractionAnchorRadius = 8f;
        public static readonly Vector2Int DefaultStorageSize = new Vector2Int(4, 4);
        public const float DefaultStorageMaxWeight = 12f;

        public int FloorIndex = 1;
        public string ObjectId;
        public string TypeId;
        public string DisplayNameOverride;
        public Vector3Int Cell;
        public Vector2Int FootprintSize = Vector2Int.one;
        public bool OverrideFootprintSize;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
        public bool OverrideAllowRotation;
        public bool AllowRotation;
        public bool OverrideRotation0Sprite;
        public Sprite Rotation0Sprite;
        public string Rotation0SpritePath;
        public bool OverrideRotation90Sprite;
        public Sprite Rotation90Sprite;
        public string Rotation90SpritePath;
        public bool OverrideRotation180Sprite;
        public Sprite Rotation180Sprite;
        public string Rotation180SpritePath;
        public bool OverrideRotation270Sprite;
        public Sprite Rotation270Sprite;
        public string Rotation270SpritePath;
        public bool IsWallMounted;
        public int Rotation90;
        public int SortingOrderOffset;
        public bool BlocksMovement;
        public bool BlocksSight;
        public bool IsInteractable;
        public bool IsStorageContainer;
        public Vector2Int StorageSize = new Vector2Int(4, 4);
        public float StorageMaxWeight = DefaultStorageMaxWeight;
        public bool UseCustomInteractionAnchor;
        public Vector3 CustomInteractionAnchorLocalPosition = Vector3.zero;
        public float CustomInteractionAnchorRadius = DefaultInteractionAnchorRadius;
        public string CustomInteractionPromptText = CustomInteractionPromptFallback;
        public List<CampusPlacedObjectInteractionAnchor> CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();

        private Sprite runtimeDefaultSprite;

        public Vector2Int NormalizedFootprintSize => NormalizeFootprintSize(FootprintSize);

        public Vector2Int RotatedFootprintSize => RotateFootprintSize(FootprintSize, Rotation90);

        public Vector2 NormalizedVisualScale => NormalizeVisualScale(VisualScale);

        public Vector2Int NormalizedStorageSize => NormalizeStorageSize(StorageSize);

        public float NormalizedStorageMaxWeight => NormalizeStorageMaxWeight(StorageMaxWeight);

        public bool SuppressFlatSpriteRotation => IsWallMounted;

        public string EffectiveTypeId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TypeId))
                {
                    return TypeId.Trim();
                }

                return !string.IsNullOrWhiteSpace(ObjectId) ? ObjectId.Trim() : string.Empty;
            }
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayNameOverride))
                {
                    return DisplayNameOverride.Trim();
                }

                return !string.IsNullOrWhiteSpace(ObjectId)
                    ? CampusObjectNames.GetDisplayName(ObjectId)
                    : CampusObjectNames.GetDisplayName(gameObject.name);
            }
        }

        private void Awake()
        {
            CacheDefaultSprite();
            ApplyRotationVisualState();
            ApplyInteractionState();
            EnsureShadowRegistration();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureShadowRegistration();
            }
        }

        private void OnValidate()
        {
            NormalizeStorageSettings();
            if (!Application.isPlaying)
            {
                CacheDefaultSprite();
                ApplyRotationVisualState();
            }
        }

        public int ResolveAllowedRotation90(int requestedRotation90)
        {
            return AllowRotation ? NormalizeRotation90(requestedRotation90) : 0;
        }

        public void ApplyPlacementRotation(int requestedRotation90)
        {
            Rotation90 = ResolveAllowedRotation90(requestedRotation90);
            ApplyRotationVisualState();
        }

        public void ApplyRotationVisualState()
        {
            Rotation90 = ResolveAllowedRotation90(Rotation90);
            if (IsWallMounted && SortingOrderOffset < WallMountedSortingOrderOffset)
            {
                SortingOrderOffset = WallMountedSortingOrderOffset;
            }

            if (!CanEditSceneHierarchy())
            {
                return;
            }

            transform.localRotation = Quaternion.identity;
            ApplyColliderFootprintSize();
            ApplyDirectionalSprite();
            ApplyWallMountedVisualState();
            ApplyPlacementDrivenComponentVisuals();
            EnsureShadowRegistration();
        }

        public void ApplyVisualScaleState()
        {
            if (!CanEditSceneHierarchy())
            {
                return;
            }

            ApplyVisualScale(GetPrimarySpriteRenderer());
            ApplyWallMountedVisualState();
            EnsureShadowRegistration();
        }

        public void EnsureShadowRegistration()
        {
            if (!CanEditSceneHierarchy())
            {
                return;
            }

            if (GetComponentInParent<CampusFloorRoot>(true) == null)
            {
                return;
            }

            NtingShadowCasterProfile profile = NtingShadowCasterProfile.EnsureForPlacedObject(this);
            if (profile != null && (Application.isPlaying || NtingCustomShadowSystem.HasActiveSystemInstance()))
            {
                NtingCustomShadowSystem.EnsureSceneSystem().MarkSceneDirty();
            }
        }

        public void ApplyCustomInteractionAnchorState()
        {
            if (!CanEditSceneHierarchy())
            {
                return;
            }

            NormalizeCustomInteractionAnchors();
            Transform root = transform.Find(CustomInteractionAnchorRootName);
            if (!UseCustomInteractionAnchor)
            {
                RemoveStaleCustomInteractionAnchors(root, null);
                return;
            }

            IsInteractable = true;
            root = root != null ? root : EnsureCustomInteractionAnchorRoot();
            HashSet<string> expectedNames = new HashSet<string>();
            for (int i = 0; i < CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = CustomInteractionAnchors[i];
                if (data == null || !data.Enabled)
                {
                    continue;
                }

                string anchorName = BuildCustomInteractionAnchorName(data, i);
                expectedNames.Add(anchorName);
                ConfigureCustomInteractionAnchor(root, anchorName, data, i);
            }

            RemoveStaleCustomInteractionAnchors(root, expectedNames);
        }

        public void ApplyInteractionState()
        {
            NormalizeStorageSettings();
            NormalizeCustomInteractionAnchors();
            if (!CanEditSceneHierarchy())
            {
                return;
            }

            CampusInteractionAnchorDefaults.EnsureDefaultAnchors(this);
            EnsureUnifiedInteractionHandler();
            ApplyCustomInteractionAnchorState();
        }

        public void NormalizeCustomInteractionAnchors()
        {
            if (CustomInteractionAnchors == null)
            {
                CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
            }

            if (UseCustomInteractionAnchor && CustomInteractionAnchors.Count == 0)
            {
                CustomInteractionAnchors.Add(new CampusPlacedObjectInteractionAnchor
                {
                    AnchorId = "custom_1",
                    DisplayName = CustomInteractionAnchorName,
                    Enabled = true,
                    LocalPosition = CustomInteractionAnchorLocalPosition,
                    Radius = NormalizeInteractionAnchorRadius(CustomInteractionAnchorRadius),
                    PromptText = string.IsNullOrWhiteSpace(CustomInteractionPromptText) ? CustomInteractionPromptFallback : CustomInteractionPromptText,
                    Priority = 120,
                    LogInteraction = true
                });
            }

            if (UseCustomInteractionAnchor)
            {
                CampusPlacedObjectInteractionAnchor editablePrimary = GetFirstEnabledCustomInteractionAnchor();
                if (editablePrimary == null && CustomInteractionAnchors.Count > 0)
                {
                    editablePrimary = CustomInteractionAnchors[0];
                }

                if (editablePrimary != null)
                {
                    editablePrimary.LocalPosition = CustomInteractionAnchorLocalPosition;
                    editablePrimary.Radius = NormalizeInteractionAnchorRadius(CustomInteractionAnchorRadius);
                    editablePrimary.PromptText = string.IsNullOrWhiteSpace(CustomInteractionPromptText)
                        ? CustomInteractionPromptFallback
                        : CustomInteractionPromptText;
                }
            }

            for (int i = 0; i < CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = CustomInteractionAnchors[i];
                if (data == null)
                {
                    data = new CampusPlacedObjectInteractionAnchor();
                    CustomInteractionAnchors[i] = data;
                }

                if (string.IsNullOrWhiteSpace(data.AnchorId))
                {
                    data.AnchorId = "custom_" + (i + 1);
                }

                if (string.IsNullOrWhiteSpace(data.DisplayName))
                {
                    data.DisplayName = CustomInteractionAnchorName + " " + (i + 1);
                }

                if (string.IsNullOrWhiteSpace(data.PromptText))
                {
                    data.PromptText = CustomInteractionPromptFallback;
                }

                data.Radius = NormalizeInteractionAnchorRadius(data.Radius);
                data.Priority = Mathf.Max(0, data.Priority);
            }

            CampusPlacedObjectInteractionAnchor primary = GetFirstEnabledCustomInteractionAnchor();
            if (primary != null)
            {
                CustomInteractionAnchorLocalPosition = primary.LocalPosition;
                CustomInteractionAnchorRadius = NormalizeInteractionAnchorRadius(primary.Radius);
                CustomInteractionPromptText = string.IsNullOrWhiteSpace(primary.PromptText) ? CustomInteractionPromptFallback : primary.PromptText;
            }
        }

        public CampusPlacedObjectInteractionAnchor GetFirstEnabledCustomInteractionAnchor()
        {
            if (CustomInteractionAnchors == null)
            {
                return null;
            }

            for (int i = 0; i < CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor data = CustomInteractionAnchors[i];
                if (data != null && data.Enabled)
                {
                    return data;
                }
            }

            return CustomInteractionAnchors.Count > 0 ? CustomInteractionAnchors[0] : null;
        }

        public void NormalizeStorageSettings()
        {
            StorageSize = NormalizeStorageSize(StorageSize);
            StorageMaxWeight = NormalizeStorageMaxWeight(StorageMaxWeight);
        }

        public static List<CampusPlacedObjectInteractionAnchor> CloneInteractionAnchors(List<CampusPlacedObjectInteractionAnchor> source)
        {
            List<CampusPlacedObjectInteractionAnchor> clone = new List<CampusPlacedObjectInteractionAnchor>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor item = source[i];
                if (item != null)
                {
                    clone.Add(item.Clone());
                }
            }

            return clone;
        }

        private void ConfigureCustomInteractionAnchor(Transform root, string anchorName, CampusPlacedObjectInteractionAnchor data, int index)
        {
            Transform anchorTransform = root.Find(anchorName);
            if (anchorTransform == null)
            {
                GameObject anchorObject = new GameObject(anchorName);
                anchorObject.layer = gameObject.layer;
                anchorObject.transform.SetParent(root, false);
                anchorTransform = anchorObject.transform;
            }

            anchorTransform.gameObject.layer = gameObject.layer;
            anchorTransform.localPosition = RotateLocalPositionForAnchor(data.LocalPosition, Rotation90);
            anchorTransform.localRotation = Quaternion.identity;
            anchorTransform.localScale = Vector3.one;

            CircleCollider2D collider = anchorTransform.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = anchorTransform.gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.radius = NormalizeInteractionAnchorRadius(data.Radius);

            CampusInteractionAnchor anchor = anchorTransform.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = anchorTransform.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            string prompt = string.IsNullOrWhiteSpace(data.PromptText)
                ? CustomInteractionPromptFallback
                : data.PromptText.Trim();
            MonoBehaviour target = ResolveCustomInteractionTarget(data.TargetComponentType);
            string actionId = ResolveCustomInteractionAction(data, target);
            anchor.InteractionTarget = target;
            anchor.ActionId = actionId;
            anchor.Payload = data.Payload;
            anchor.PromptAnchor = anchorTransform;
            anchor.PromptText = prompt;
            anchor.KeyOverride = string.Empty;
            anchor.Icon = null;
            anchor.AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
            anchor.Priority = Mathf.Max(0, data.Priority);
            anchor.IsAvailable = true;
            anchor.UnavailableText = string.Empty;
            anchor.HideWhenUnavailable = false;
            anchor.UseTargetDoorStatePrompt = data.UseTargetDoorStatePrompt;
            anchor.LogInteraction = data.LogInteraction && CampusInteractionActionIds.Equals(actionId, CampusInteractionActionIds.Log);
            anchor.InteractionLogMessage = prompt + " " + DisplayName;
            if (!string.IsNullOrWhiteSpace(data.InteractionLogMessage))
            {
                anchor.InteractionLogMessage = data.InteractionLogMessage.Trim();
            }
        }

        private MonoBehaviour ResolveCustomInteractionTarget(string targetComponentType)
        {
            MonoBehaviour fallback = null;
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null ||
                    behaviour is CampusInteractionAnchor ||
                    behaviour is CampusSimpleInteractable ||
                    !(behaviour is ICampusInteractable))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(targetComponentType))
                {
                    return behaviour;
                }

                Type type = behaviour.GetType();
                if (type.Name == targetComponentType || type.FullName == targetComponentType)
                {
                    return behaviour;
                }

                if (fallback == null)
                {
                    fallback = behaviour;
                }
            }

            return fallback;
        }

        private string ResolveCustomInteractionAction(CampusPlacedObjectInteractionAnchor data, MonoBehaviour target)
        {
            if (data != null && !string.IsNullOrWhiteSpace(data.ActionId))
            {
                return CampusInteractionActionIds.Normalize(data.ActionId);
            }

            return target != null ? CampusInteractionActionIds.InteractTarget : CampusInteractionActionIds.Log;
        }

        private static Vector3 RotateLocalPositionForAnchor(Vector3 position, int rotation90)
        {
            switch (NormalizeRotation90(rotation90))
            {
                case 1:
                    return new Vector3(-position.y, position.x, position.z);
                case 2:
                    return new Vector3(-position.x, -position.y, position.z);
                case 3:
                    return new Vector3(position.y, -position.x, position.z);
                default:
                    return position;
            }
        }

        private static string BuildCustomInteractionAnchorName(CampusPlacedObjectInteractionAnchor data, int index)
        {
            string id = data != null && !string.IsNullOrWhiteSpace(data.AnchorId) ? data.AnchorId.Trim() : "custom_" + (index + 1);
            return CustomInteractionAnchorPrefix + SanitizeAnchorName(id);
        }

        private static string SanitizeAnchorName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "custom";
            }

            string sanitized = value.Trim().Replace('/', '_').Replace('\\', '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "custom" : sanitized;
        }

        public Sprite ResolveSpriteForRotation(int requestedRotation90, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90)
        {
            effectiveRotation90 = ResolveAllowedRotation90(requestedRotation90);
            if (IsWallMounted)
            {
                usesAuthoredDirectionalSprite = true;
                return Rotation0Sprite != null ? Rotation0Sprite : runtimeDefaultSprite;
            }

            Sprite directionalSprite = GetDirectionalSprite(effectiveRotation90);
            usesAuthoredDirectionalSprite = directionalSprite != null;
            if (usesAuthoredDirectionalSprite)
            {
                return directionalSprite;
            }

            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            return Rotation0Sprite != null ? Rotation0Sprite : (renderer != null ? renderer.sprite : null);
        }

        public void RefreshCellFromTransform(Grid grid)
        {
            if (grid == null)
            {
                return;
            }

            Cell = ResolveCellFromTransform(grid);
        }

        public void ApplyCellToTransform(Grid grid)
        {
            if (grid == null)
            {
                return;
            }

            transform.position = GetPlacementWorldCenter(grid, Cell, RotatedFootprintSize, IsWallMounted, Rotation90);
        }

        public Vector3Int ResolveCellFromTransform(Grid grid)
        {
            if (grid == null)
            {
                return Vector3Int.zero;
            }

            if (IsWallMounted)
            {
                return grid.WorldToCell(transform.position - GetWallMountedAnchorLocalOffset(Rotation90));
            }

            Vector2Int footprint = RotatedFootprintSize;
            Vector3 cellOffset = new Vector3((footprint.x - 1) * 0.5f, (footprint.y - 1) * 0.5f, 0f);
            Vector3 worldOffset = grid.transform.TransformVector(Vector3.Scale(cellOffset, grid.cellSize));
            return grid.WorldToCell(transform.position - worldOffset);
        }

        public bool ContainsCell(Vector3Int cell)
        {
            Vector2Int footprint = RotatedFootprintSize;
            return cell.z == Cell.z &&
                   cell.x >= Cell.x &&
                   cell.x < Cell.x + footprint.x &&
                   cell.y >= Cell.y &&
                   cell.y < Cell.y + footprint.y;
        }

        public static Vector2Int NormalizeFootprintSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        public static int NormalizeRotation90(int rotation90)
        {
            return ((rotation90 % 4) + 4) % 4;
        }

        public static Vector2Int RotateFootprintSize(Vector2Int size, int rotation90)
        {
            Vector2Int normalized = NormalizeFootprintSize(size);
            int turns = NormalizeRotation90(rotation90);
            return turns == 1 || turns == 3
                ? new Vector2Int(normalized.y, normalized.x)
                : normalized;
        }

        public static Vector2 NormalizeVisualScale(Vector2 scale)
        {
            float x = scale.x > 0f ? scale.x : 1f;
            float y = scale.y > 0f ? scale.y : 1f;
            return new Vector2(Mathf.Clamp(x, 0.05f, 16f), Mathf.Clamp(y, 0.05f, 16f));
        }

        public static Vector2Int NormalizeStorageSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        public static float NormalizeStorageMaxWeight(float maxWeight)
        {
            if (float.IsNaN(maxWeight) || float.IsInfinity(maxWeight))
            {
                return DefaultStorageMaxWeight;
            }

            return Mathf.Max(0f, maxWeight);
        }

        public static float NormalizeInteractionAnchorRadius(float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
            {
                return DefaultInteractionAnchorRadius;
            }

            return Mathf.Clamp(radius, MinInteractionAnchorRadius, MaxInteractionAnchorRadius);
        }

        public static Vector3 GetFootprintWorldCenter(Grid grid, Vector3Int anchorCell, Vector2Int rotatedFootprintSize)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            Vector2Int footprint = NormalizeFootprintSize(rotatedFootprintSize);
            Vector3Int farCell = new Vector3Int(
                anchorCell.x + footprint.x - 1,
                anchorCell.y + footprint.y - 1,
                anchorCell.z);
            return (grid.GetCellCenterWorld(anchorCell) + grid.GetCellCenterWorld(farCell)) * 0.5f;
        }

        public static Vector3 GetPlacementWorldCenter(
            Grid grid,
            Vector3Int anchorCell,
            Vector2Int rotatedFootprintSize,
            bool isWallMounted,
            int rotation90)
        {
            return isWallMounted
                ? GetWallMountedWorldCenter(grid, anchorCell, rotation90)
                : GetFootprintWorldCenter(grid, anchorCell, rotatedFootprintSize);
        }

        public static Vector3 GetWallMountedWorldCenter(Grid grid, Vector3Int anchorCell, int rotation90)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            Vector3 cellCenter = grid.GetCellCenterWorld(anchorCell);
            return cellCenter + GetWallMountedAnchorLocalOffset(rotation90);
        }

        private void CacheDefaultSprite()
        {
            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            if (renderer != null && runtimeDefaultSprite == null && !IsConfiguredDirectionalSprite(renderer.sprite))
            {
                runtimeDefaultSprite = renderer.sprite;
            }
        }

        private void ApplyColliderFootprintSize()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.enabled = !IsWallMounted;
                if (IsWallMounted)
                {
                    return;
                }

                Vector2Int footprint = RotatedFootprintSize;
                box.size = new Vector2(footprint.x, footprint.y);
            }
        }

        private void ApplyDirectionalSprite()
        {
            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            if (renderer == null)
            {
                return;
            }

            Sprite baseSprite = Rotation0Sprite != null ? Rotation0Sprite : (runtimeDefaultSprite != null ? runtimeDefaultSprite : renderer.sprite);
            Sprite directionalSprite = GetDirectionalSprite(Rotation90);
            bool usesAuthoredDirectionSprite = directionalSprite != null;
            renderer.sprite = usesAuthoredDirectionSprite ? directionalSprite : baseSprite;

            Transform visualTransform = renderer.transform;
            if (visualTransform != null)
            {
                ApplyVisualScale(renderer);
                visualTransform.localRotation = IsWallMounted || usesAuthoredDirectionSprite || !AllowRotation
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, Rotation90 * 90f);
            }

            renderer.enabled = !IsWallMounted;
        }

        private void ApplyVisualScale(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.transform == null)
            {
                return;
            }

            Transform visualTransform = renderer.transform;
            Vector2 visualScale = NormalizedVisualScale;
            float z = Mathf.Approximately(visualTransform.localScale.z, 0f) ? 1f : visualTransform.localScale.z;
            visualTransform.localScale = new Vector3(visualScale.x, visualScale.y, z);
        }

        private void ApplyWallMountedVisualState()
        {
            Transform root = transform.Find(WallMountedVisualRootName);
            if (!IsWallMounted)
            {
                if (root != null)
                {
                    DestroyImmediateSafe(root.gameObject);
                }

                SpriteRenderer spriteRenderer = GetPrimarySpriteRenderer();
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = true;
                }

                return;
            }

            Sprite sprite = Rotation0Sprite != null ? Rotation0Sprite : runtimeDefaultSprite;
            if (sprite == null)
            {
                if (root != null)
                {
                    DestroyImmediateSafe(root.gameObject);
                }

                return;
            }

            if (root == null)
            {
                GameObject rootObject = new GameObject(WallMountedVisualRootName);
                rootObject.layer = gameObject.layer;
                root = rootObject.transform;
                root.SetParent(transform, false);
            }

            root.gameObject.layer = gameObject.layer;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = root.gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = root.gameObject.AddComponent<MeshRenderer>();
            }

            Mesh mesh = filter.sharedMesh;
            mesh = GetOrCreateUniqueWallMountedMesh(filter, mesh);

            BuildWallMountedPlateMesh(mesh, sprite);
            meshRenderer.sharedMaterial = GetOrCreateWallMountedMaterial(meshRenderer.sharedMaterial, sprite.texture);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            SpriteRenderer renderer = GetPrimarySpriteRenderer();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void BuildWallMountedPlateMesh(Mesh mesh, Sprite sprite)
        {
            if (mesh == null || sprite == null)
            {
                return;
            }

            Vector2 visualScale = NormalizedVisualScale;
            float width = Mathf.Max(0.05f, sprite.bounds.size.x * visualScale.x);
            float height = Mathf.Max(0.05f, sprite.bounds.size.y * visualScale.y);
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            Vector3[] vertices =
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(-halfWidth, -halfHeight, WallMountedThickness),
                new Vector3(halfWidth, -halfHeight, WallMountedThickness),
                new Vector3(halfWidth, halfHeight, WallMountedThickness),
                new Vector3(-halfWidth, halfHeight, WallMountedThickness)
            };

            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;
            float textureWidth = Mathf.Max(1f, texture != null ? texture.width : textureRect.width);
            float textureHeight = Mathf.Max(1f, texture != null ? texture.height : textureRect.height);
            Vector2 uvMin = new Vector2(textureRect.xMin / textureWidth, textureRect.yMin / textureHeight);
            Vector2 uvMax = new Vector2(textureRect.xMax / textureWidth, textureRect.yMax / textureHeight);
            Vector2[] uvs =
            {
                new Vector2(uvMin.x, uvMin.y),
                new Vector2(uvMax.x, uvMin.y),
                new Vector2(uvMax.x, uvMax.y),
                new Vector2(uvMin.x, uvMax.y),
                new Vector2(uvMin.x, uvMin.y),
                new Vector2(uvMax.x, uvMin.y),
                new Vector2(uvMax.x, uvMax.y),
                new Vector2(uvMin.x, uvMax.y)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private Mesh GetOrCreateUniqueWallMountedMesh(MeshFilter filter, Mesh existing)
        {
            string expectedName = WallMountedMeshNamePrefix + GetInstanceID();
            if (existing != null && existing.name == expectedName)
            {
                return existing;
            }

            DestroyGeneratedObjectIfNeeded(existing, WallMountedMeshNamePrefix);
            Mesh mesh = new Mesh
            {
                name = expectedName,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            filter.sharedMesh = mesh;
            return mesh;
        }

        private Material GetOrCreateWallMountedMaterial(Material existing, Texture texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            string expectedName = WallMountedMaterialNamePrefix + GetInstanceID();
            Material material = existing;
            if (material == null || material.shader != shader || material.name != expectedName)
            {
                DestroyGeneratedObjectIfNeeded(material, WallMountedMaterialNamePrefix);
                material = new Material(shader)
                {
                    name = expectedName,
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
                };
            }

            if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = texture;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            return material;
        }

        private SpriteRenderer GetPrimarySpriteRenderer()
        {
            return GetComponentInChildren<SpriteRenderer>(true);
        }

        private Sprite GetDirectionalSprite(int rotation90)
        {
            switch (NormalizeRotation90(rotation90))
            {
                case 0:
                    return Rotation0Sprite;
                case 1:
                    return Rotation90Sprite;
                case 2:
                    return Rotation180Sprite;
                case 3:
                    return Rotation270Sprite;
                default:
                    return null;
            }
        }

        private bool IsConfiguredDirectionalSprite(Sprite sprite)
        {
            return sprite != null &&
                   (sprite == Rotation0Sprite ||
                    sprite == Rotation90Sprite ||
                    sprite == Rotation180Sprite ||
                    sprite == Rotation270Sprite);
        }

        private bool HasDirectionalSprites()
        {
            return Rotation0Sprite != null ||
                   Rotation90Sprite != null ||
                   Rotation180Sprite != null ||
                   Rotation270Sprite != null;
        }

        private void ApplyPlacementDrivenComponentVisuals()
        {
            CampusDoor3D[] doors = GetComponentsInChildren<CampusDoor3D>(true);
            for (int i = 0; i < doors.Length; i++)
            {
                CampusDoor3D door = doors[i];
                if (door != null)
                {
                    door.RebuildDoorMesh();
                }
            }
        }

        private static Vector3 GetWallMountedAnchorLocalOffset(int rotation90)
        {
            float outerCenter = (CampusWallMeshRenderer.WallHalfCell + CampusWallMeshRenderer.WallTopHalfWidth) * 0.5f;
            float depth = CampusWallMeshRenderer.WallBaseDepth + WallMountedSurfaceGap;
            switch (NormalizeRotation90(rotation90))
            {
                case 1:
                    return new Vector3(outerCenter, 0f, depth);
                case 2:
                    return new Vector3(0f, -outerCenter, depth);
                case 3:
                    return new Vector3(-outerCenter, 0f, depth);
                default:
                    return new Vector3(0f, outerCenter, depth);
            }
        }

        private void DestroyGeneratedObjectIfNeeded(UnityEngine.Object target, string expectedPrefix)
        {
            if (target == null || string.IsNullOrEmpty(expectedPrefix))
            {
                return;
            }

            if (!target.name.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                return;
            }

            DestroyImmediateSafe(target);
        }

        private void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Transform EnsureCustomInteractionAnchorRoot()
        {
            GameObject rootObject = new GameObject(CustomInteractionAnchorRootName);
            rootObject.layer = gameObject.layer;
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static void RemoveStaleCustomInteractionAnchors(Transform root, HashSet<string> expectedNames)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                bool isCustomAnchor = child.name.StartsWith(CustomInteractionAnchorPrefix, StringComparison.Ordinal) ||
                                      child.name == CustomInteractionAnchorName;
                if (!isCustomAnchor)
                {
                    continue;
                }

                if (expectedNames != null && expectedNames.Contains(child.name))
                {
                    continue;
                }

                if (child.GetComponent<CampusInteractionAnchor>() != null)
                {
                    DestroyUnityObject(child.gameObject);
                }
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void EnsureUnifiedInteractionHandler()
        {
            if (!ShouldUseUnifiedInteractionHandler())
            {
                return;
            }

            CampusSimpleInteractable handler = GetComponent<CampusSimpleInteractable>();
            if (handler == null)
            {
                handler = gameObject.AddComponent<CampusSimpleInteractable>();
            }

            handler.IsAvailable = true;
            handler.HideWhenUnavailable = false;
            if (IsStorageContainer)
            {
                if (string.IsNullOrWhiteSpace(handler.DefaultActionId))
                {
                    handler.DefaultActionId = CampusInteractionActionIds.OpenStorage;
                }

                if (string.IsNullOrWhiteSpace(handler.PromptText) || handler.PromptText == CustomInteractionPromptFallback)
                {
                    handler.PromptText = "\u6253\u5f00 " + DisplayName;
                }
            }
        }

        private bool ShouldUseUnifiedInteractionHandler()
        {
            return IsInteractable || UseCustomInteractionAnchor || IsStorageContainer;
        }

        private bool CanEditSceneHierarchy()
        {
            return gameObject.scene.IsValid();
        }
    }

}

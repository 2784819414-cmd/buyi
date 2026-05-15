using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.Gameplay.Skeleton
{
    [DisallowMultipleComponent]
    public sealed class CampusMischiefAnchorBootstrap : MonoBehaviour
    {
        public const string RootName = "NtingCampus_V01_MischiefSkeletonRoot";
        private const float InteractionColliderRadius = 1.35f;
        private const float InteractionColliderYOffset = 0.18f;
        private const float InteractionPromptYOffset = 0.46f;
        private const float SceneIconWorldSize = 0.86f;
        private const float ActorIconWorldSize = 0.92f;
        private const int IconTextureSize = 32;

        private static readonly CampusMischiefLocationDefinition[] LocationDefinitions =
        {
            new CampusMischiefLocationDefinition("ClassroomAnchor", "教室", "用于传纸条。", new Vector2(-2.4f, 1.6f), 2.4f, CampusMischiefIconKind.Classroom),
            new CampusMischiefLocationDefinition("CampusShopAnchor", "学校超市", "学校内部商业区，和书店在同一层楼。", new Vector2(2.1f, 1.7f), 2.4f, CampusMischiefIconKind.CampusShop),
            new CampusMischiefLocationDefinition("BookstoreAnchor", "学校书店", "主要卖笔、杂志、小玩意。", new Vector2(4.5f, 1.7f), 2.1f, CampusMischiefIconKind.Bookstore),
            new CampusMischiefLocationDefinition("CanteenAnchor", "食堂", "楼下两层都是食堂，本轮用于偷炸鸡。", new Vector2(-2.4f, -1.7f), 2.4f, CampusMischiefIconKind.Canteen),
            new CampusMischiefLocationDefinition("LibraryAnchor", "行政楼一楼图书馆", "柱子整理图书，外侧女老师登记借书。", new Vector2(2.1f, -1.7f), 2.4f, CampusMischiefIconKind.Library),
            new CampusMischiefLocationDefinition("OutsideDeliveryAnchor", "校外外卖灰区", "外卖只能放在校外灰区。", new Vector2(-0.2f, -4.0f), 2.5f, CampusMischiefIconKind.DeliveryZone),
            new CampusMischiefLocationDefinition("SkewerStandAnchor", "校外烤面筋摊", "本轮只生成老板和场景锚点。", new Vector2(3.1f, -4.0f), 2.1f, CampusMischiefIconKind.SkewerStand)
        };

        private static readonly CampusSkeletonActorDefinition[] ActorDefinitions =
        {
            new CampusSkeletonActorDefinition("超市店员", CampusSkeletonActorRole.ShopClerk, "CampusShopAnchor", new Vector2(0.55f, 0.38f), CampusSkeletonActorStates.Idle, 50, CampusMischiefIconKind.ShopClerk),
            new CampusSkeletonActorDefinition("食堂店员", CampusSkeletonActorRole.CanteenClerk, "CanteenAnchor", new Vector2(0.55f, 0.38f), CampusSkeletonActorStates.Busy, 0, CampusMischiefIconKind.CanteenClerk),
            new CampusSkeletonActorDefinition("柱子", CampusSkeletonActorRole.LibrarianZhuzi, "LibraryAnchor", new Vector2(-0.55f, 0.34f), CampusSkeletonActorStates.SortingBooks, 0, CampusMischiefIconKind.Zhuzi),
            new CampusSkeletonActorDefinition("登记女老师", CampusSkeletonActorRole.LibraryTeacher, "LibraryAnchor", new Vector2(0.95f, -0.95f), CampusSkeletonActorStates.Idle, 70, CampusMischiefIconKind.LibraryTeacher),
            new CampusSkeletonActorDefinition("外卖失主", CampusSkeletonActorRole.DeliveryOwnerStudent, "OutsideDeliveryAnchor", new Vector2(0.55f, 0.38f), CampusSkeletonActorStates.Idle, 0, CampusMischiefIconKind.DeliveryOwner),
            new CampusSkeletonActorDefinition("烤面筋老板", CampusSkeletonActorRole.SkewerBoss, "SkewerStandAnchor", new Vector2(0.55f, 0.38f), CampusSkeletonActorStates.Idle, 0, CampusMischiefIconKind.SkewerBoss)
        };

        private static readonly Dictionary<CampusMischiefIconKind, Sprite> IconSprites =
            new Dictionary<CampusMischiefIconKind, Sprite>();

        public static CampusMischiefAnchorBootstrap Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMischiefSkeletonAfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, "CampusMap", StringComparison.Ordinal))
            {
                return;
            }

            CampusGameBootstrap bootstrap = FindFirstObjectByType<CampusGameBootstrap>(FindObjectsInactive.Include);
            RebuildSkeleton(bootstrap);
        }

        public static CampusMischiefActionController RebuildSkeleton(CampusGameBootstrap bootstrap)
        {
            GameObject existingRoot = FindExistingRoot();
            if (existingRoot != null)
            {
                DestroyImmediate(existingRoot);
            }

            IconSprites.Clear();

            GameObject root = new GameObject(RootName);
            CampusMischiefAnchorBootstrap skeletonBootstrap = root.AddComponent<CampusMischiefAnchorBootstrap>();
            CampusMischiefActionController actionController = root.AddComponent<CampusMischiefActionController>();
            CampusMischiefConsequenceController consequenceController = root.AddComponent<CampusMischiefConsequenceController>();
            consequenceController.Initialize(bootstrap, actionController);
            actionController.Initialize(bootstrap);

            Vector3 origin = ResolvePlayerOrigin();
            CreateLocations(root.transform, actionController, origin);
            CreateActors(root.transform, actionController);
            actionController.Initialize(bootstrap);
            Active = skeletonBootstrap;
            return actionController;
        }

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private static void CreateLocations(
            Transform root,
            CampusMischiefActionController actionController,
            Vector3 origin)
        {
            for (int i = 0; i < LocationDefinitions.Length; i++)
            {
                CampusMischiefLocationDefinition location = LocationDefinitions[i];
                GameObject anchorObject = new GameObject(location.AnchorId);
                anchorObject.transform.SetParent(root, false);
                anchorObject.transform.position = origin + new Vector3(location.Offset.x, location.Offset.y, 0f);

                AddIcon(anchorObject.transform, location.IconKind, SceneIconWorldSize, "SceneIcon", 5000);

                if (TryGetActionDefinitionForAnchor(location.AnchorId, out CampusMischiefActionDefinition actionDefinition))
                {
                    ConfigureInteractionAnchor(anchorObject, actionController, actionDefinition);
                    actionController.RegisterActionPoint(actionDefinition, anchorObject.transform, location.TriggerRadius);
                }
            }
        }

        private static void CreateActors(Transform root, CampusMischiefActionController actionController)
        {
            for (int i = 0; i < ActorDefinitions.Length; i++)
            {
                CampusSkeletonActorDefinition definition = ActorDefinitions[i];
                Transform anchor = root.Find(definition.AnchorId);
                if (anchor == null)
                {
                    continue;
                }

                GameObject actorObject = new GameObject(definition.ActorName);
                actorObject.transform.SetParent(root, false);
                actorObject.transform.position = anchor.position + new Vector3(definition.AnchorLocalOffset.x, definition.AnchorLocalOffset.y, 0f);

                CampusSkeletonActor actor = actorObject.AddComponent<CampusSkeletonActor>();
                actor.Configure(definition.ActorName, definition.Role, definition.State, definition.Alertness);

                AddIcon(actorObject.transform, definition.IconKind, ActorIconWorldSize, "ActorIcon", 5002);
                actionController.RegisterActor(actor);
            }
        }

        private static void ConfigureInteractionAnchor(
            GameObject anchorObject,
            CampusMischiefActionController actionController,
            CampusMischiefActionDefinition actionDefinition)
        {
            CircleCollider2D collider = anchorObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = anchorObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.offset = new Vector2(0f, InteractionColliderYOffset);
            collider.radius = InteractionColliderRadius;

            CampusInteractionAnchor interactionAnchor = anchorObject.GetComponent<CampusInteractionAnchor>();
            if (interactionAnchor == null)
            {
                interactionAnchor = anchorObject.AddComponent<CampusInteractionAnchor>();
            }

            interactionAnchor.InteractionTarget = actionController;
            interactionAnchor.ActionId = CampusMischiefActionController.InteractionActionId;
            interactionAnchor.Payload = actionDefinition.FunctionId;
            interactionAnchor.PromptAnchor = EnsureInteractionPromptAnchor(anchorObject.transform);
            interactionAnchor.PromptText = actionDefinition.DisplayName;
            interactionAnchor.KeyOverride = string.Empty;
            interactionAnchor.Icon = null;
            interactionAnchor.AccentColor = new Color(0.95f, 0.82f, 0.38f, 1f);
            interactionAnchor.Priority = 90;
            interactionAnchor.IsAvailable = true;
            interactionAnchor.UnavailableText = string.Empty;
            interactionAnchor.HideWhenUnavailable = false;
            interactionAnchor.UseTargetDoorStatePrompt = false;
            interactionAnchor.LogInteraction = false;
            interactionAnchor.InteractionLogMessage = string.Empty;
        }

        private static Transform EnsureInteractionPromptAnchor(Transform owner)
        {
            const string promptAnchorName = "InteractionPromptAnchor";
            Transform promptAnchor = owner.Find(promptAnchorName);
            if (promptAnchor == null)
            {
                GameObject promptAnchorObject = new GameObject(promptAnchorName);
                promptAnchorObject.transform.SetParent(owner, false);
                promptAnchor = promptAnchorObject.transform;
            }

            promptAnchor.localPosition = new Vector3(0f, InteractionPromptYOffset, 0f);
            promptAnchor.localRotation = Quaternion.identity;
            promptAnchor.localScale = Vector3.one;
            return promptAnchor;
        }

        private static bool TryGetActionDefinitionForAnchor(
            string anchorId,
            out CampusMischiefActionDefinition actionDefinition)
        {
            IReadOnlyList<CampusMischiefActionDefinition> actions = CampusMischiefActionController.ActionDefinitions;
            for (int i = 0; i < actions.Count; i++)
            {
                CampusMischiefActionDefinition candidate = actions[i];
                if (string.Equals(candidate.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    actionDefinition = candidate;
                    return true;
                }
            }

            actionDefinition = default;
            return false;
        }

        private static Vector3 ResolvePlayerOrigin()
        {
            CampusTestPlayerController playerController =
                FindFirstObjectByType<CampusTestPlayerController>(FindObjectsInactive.Exclude);
            if (playerController != null)
            {
                return FlattenZ(playerController.transform.position);
            }

            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null)
            {
                return FlattenZ(taggedPlayer.transform.position);
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? FlattenZ(mainCamera.transform.position) : Vector3.zero;
        }

        private static Vector3 FlattenZ(Vector3 position)
        {
            position.z = 0f;
            return position;
        }

        private static GameObject FindExistingRoot()
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.gameObject == null ||
                    !candidate.gameObject.scene.IsValid() ||
                    !string.Equals(candidate.name, RootName, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate.gameObject;
            }

            return null;
        }

        private static void AddIcon(
            Transform parent,
            CampusMischiefIconKind iconKind,
            float worldSize,
            string name,
            int sortingOrder)
        {
            GameObject iconObject = new GameObject(name);
            iconObject.transform.SetParent(parent, false);
            iconObject.transform.localPosition = Vector3.zero;
            float normalizedSize = Mathf.Max(0.01f, worldSize);
            iconObject.transform.localScale = new Vector3(normalizedSize, normalizedSize, 1f);

            SpriteRenderer renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetIconSprite(iconKind);
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetIconSprite(CampusMischiefIconKind iconKind)
        {
            if (IconSprites.TryGetValue(iconKind, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = new Texture2D(IconTextureSize, IconTextureSize, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Clear(texture);
            DrawIcon(texture, iconKind);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, IconTextureSize, IconTextureSize),
                new Vector2(0.5f, 0.5f),
                IconTextureSize);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            IconSprites[iconKind] = sprite;
            return sprite;
        }

        private static void DrawIcon(Texture2D texture, CampusMischiefIconKind iconKind)
        {
            Color outline = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color white = new Color(0.94f, 0.94f, 0.9f, 1f);
            Color blue = new Color(0.24f, 0.48f, 0.95f, 1f);
            Color green = new Color(0.2f, 0.74f, 0.42f, 1f);
            Color purple = new Color(0.56f, 0.36f, 0.86f, 1f);
            Color orange = new Color(1f, 0.55f, 0.22f, 1f);
            Color teal = new Color(0.18f, 0.72f, 0.72f, 1f);
            Color yellow = new Color(0.96f, 0.78f, 0.22f, 1f);
            Color red = new Color(0.92f, 0.24f, 0.22f, 1f);
            Color skin = new Color(0.95f, 0.72f, 0.52f, 1f);
            Color uniform = new Color(0.34f, 0.42f, 0.58f, 1f);
            Color hair = new Color(0.16f, 0.11f, 0.08f, 1f);
            Color brown = new Color(0.55f, 0.33f, 0.18f, 1f);

            switch (iconKind)
            {
                case CampusMischiefIconKind.Classroom:
                    FillRect(texture, 4, 5, 24, 19, blue);
                    StrokeRect(texture, 4, 5, 24, 19, outline);
                    FillRect(texture, 8, 8, 16, 6, brown);
                    FillRect(texture, 11, 15, 10, 6, white);
                    DrawLine(texture, 15, 15, 20, 20, outline);
                    break;

                case CampusMischiefIconKind.CampusShop:
                    FillRect(texture, 4, 6, 24, 20, green);
                    StrokeRect(texture, 4, 6, 24, 20, outline);
                    FillRect(texture, 7, 9, 18, 3, outline);
                    FillRect(texture, 8, 13, 4, 8, blue);
                    FillRect(texture, 14, 13, 4, 8, yellow);
                    FillRect(texture, 20, 13, 4, 8, red);
                    FillRect(texture, 8, 23, 16, 3, white);
                    break;

                case CampusMischiefIconKind.Bookstore:
                    FillRect(texture, 5, 7, 7, 18, purple);
                    FillRect(texture, 13, 5, 7, 20, blue);
                    FillRect(texture, 21, 9, 6, 16, yellow);
                    StrokeRect(texture, 5, 7, 7, 18, outline);
                    StrokeRect(texture, 13, 5, 7, 20, outline);
                    StrokeRect(texture, 21, 9, 6, 16, outline);
                    FillRect(texture, 7, 10, 3, 2, white);
                    FillRect(texture, 15, 9, 3, 2, white);
                    break;

                case CampusMischiefIconKind.Canteen:
                    FillCircle(texture, 16, 16, 12, orange);
                    FillCircle(texture, 16, 16, 8, white);
                    StrokeCircle(texture, 16, 16, 12, outline);
                    FillCircle(texture, 13, 17, 4, brown);
                    FillRect(texture, 16, 14, 8, 5, brown);
                    FillCircle(texture, 23, 16, 3, white);
                    break;

                case CampusMischiefIconKind.Library:
                    FillRect(texture, 5, 5, 22, 22, teal);
                    StrokeRect(texture, 5, 5, 22, 22, outline);
                    FillRect(texture, 8, 8, 3, 16, purple);
                    FillRect(texture, 12, 8, 3, 16, yellow);
                    FillRect(texture, 16, 8, 3, 16, blue);
                    FillRect(texture, 20, 8, 3, 16, red);
                    FillRect(texture, 7, 15, 18, 2, outline);
                    break;

                case CampusMischiefIconKind.DeliveryZone:
                    FillRect(texture, 7, 10, 18, 14, yellow);
                    StrokeRect(texture, 7, 10, 18, 14, outline);
                    DrawLine(texture, 10, 10, 14, 6, outline);
                    DrawLine(texture, 14, 6, 20, 6, outline);
                    DrawLine(texture, 20, 6, 24, 10, outline);
                    FillRect(texture, 11, 15, 10, 3, white);
                    break;

                case CampusMischiefIconKind.SkewerStand:
                    FillRect(texture, 5, 7, 22, 7, red);
                    StrokeRect(texture, 5, 7, 22, 7, outline);
                    FillRect(texture, 8, 21, 16, 4, brown);
                    StrokeRect(texture, 8, 21, 16, 4, outline);
                    DrawLine(texture, 10, 12, 14, 25, outline);
                    DrawLine(texture, 16, 12, 16, 25, outline);
                    DrawLine(texture, 22, 12, 18, 25, outline);
                    FillRect(texture, 9, 16, 3, 3, orange);
                    FillRect(texture, 15, 16, 3, 3, orange);
                    FillRect(texture, 21, 16, 3, 3, orange);
                    break;

                case CampusMischiefIconKind.ShopClerk:
                    DrawPerson(texture, green, skin, hair, outline);
                    FillRect(texture, 5, 21, 22, 5, green);
                    StrokeRect(texture, 5, 21, 22, 5, outline);
                    break;

                case CampusMischiefIconKind.CanteenClerk:
                    DrawPerson(texture, orange, skin, hair, outline);
                    FillRect(texture, 10, 3, 12, 4, white);
                    StrokeRect(texture, 10, 3, 12, 4, outline);
                    FillCircle(texture, 12, 4, 3, white);
                    FillCircle(texture, 16, 3, 3, white);
                    FillCircle(texture, 20, 4, 3, white);
                    break;

                case CampusMischiefIconKind.Zhuzi:
                    DrawPerson(texture, uniform, skin, hair, outline);
                    FillRect(texture, 4, 20, 10, 4, purple);
                    FillRect(texture, 5, 16, 10, 4, yellow);
                    StrokeRect(texture, 4, 20, 10, 4, outline);
                    StrokeRect(texture, 5, 16, 10, 4, outline);
                    break;

                case CampusMischiefIconKind.LibraryTeacher:
                    DrawPerson(texture, teal, skin, hair, outline);
                    FillRect(texture, 20, 14, 7, 10, white);
                    StrokeRect(texture, 20, 14, 7, 10, outline);
                    DrawLine(texture, 22, 17, 25, 17, outline);
                    DrawLine(texture, 22, 20, 25, 20, outline);
                    break;

                case CampusMischiefIconKind.DeliveryOwner:
                    DrawPerson(texture, blue, skin, hair, outline);
                    FillRect(texture, 21, 14, 4, 8, outline);
                    FillRect(texture, 22, 15, 2, 5, white);
                    break;

                case CampusMischiefIconKind.SkewerBoss:
                    DrawPerson(texture, red, skin, hair, outline);
                    DrawLine(texture, 22, 11, 26, 23, outline);
                    FillRect(texture, 21, 15, 3, 3, orange);
                    FillRect(texture, 23, 19, 3, 3, orange);
                    break;
            }
        }

        private static void DrawPerson(Texture2D texture, Color body, Color skin, Color hair, Color outline)
        {
            FillCircle(texture, 16, 10, 6, skin);
            FillCircle(texture, 16, 8, 6, hair);
            FillRect(texture, 10, 10, 12, 4, skin);
            StrokeCircle(texture, 16, 10, 6, outline);
            FillRect(texture, 9, 17, 14, 10, body);
            StrokeRect(texture, 9, 17, 14, 10, outline);
            FillRect(texture, 12, 15, 8, 4, body);
        }

        private static void Clear(Texture2D texture)
        {
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < IconTextureSize; y++)
            {
                for (int x = 0; x < IconTextureSize; x++)
                {
                    texture.SetPixel(x, y, clear);
                }
            }
        }

        private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixelSafe(texture, px, py, color);
                }
            }
        }

        private static void StrokeRect(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            DrawLine(texture, x, y, x + width - 1, y, color);
            DrawLine(texture, x, y + height - 1, x + width - 1, y + height - 1, color);
            DrawLine(texture, x, y, x, y + height - 1, color);
            DrawLine(texture, x + width - 1, y, x + width - 1, y + height - 1, color);
        }

        private static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetPixelSafe(texture, x, y, color);
                    }
                }
            }
        }

        private static void StrokeCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int outerRadiusSquared = radius * radius;
            int innerRadius = Mathf.Max(0, radius - 1);
            int innerRadiusSquared = innerRadius * innerRadius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared <= outerRadiusSquared && distanceSquared >= innerRadiusSquared)
                    {
                        SetPixelSafe(texture, x, y, color);
                    }
                }
            }
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                SetPixelSafe(texture, x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubledError = error * 2;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || x >= IconTextureSize || y < 0 || y >= IconTextureSize)
            {
                return;
            }

            texture.SetPixel(x, y, color);
        }

        private readonly struct CampusMischiefLocationDefinition
        {
            public readonly string AnchorId;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly Vector2 Offset;
            public readonly float TriggerRadius;
            public readonly CampusMischiefIconKind IconKind;

            public CampusMischiefLocationDefinition(
                string anchorId,
                string displayName,
                string description,
                Vector2 offset,
                float triggerRadius,
                CampusMischiefIconKind iconKind)
            {
                AnchorId = anchorId;
                DisplayName = displayName;
                Description = description;
                Offset = offset;
                TriggerRadius = Mathf.Clamp(triggerRadius, 0.5f, 8f);
                IconKind = iconKind;
            }
        }

        private readonly struct CampusSkeletonActorDefinition
        {
            public readonly string ActorName;
            public readonly CampusSkeletonActorRole Role;
            public readonly string AnchorId;
            public readonly Vector2 AnchorLocalOffset;
            public readonly string State;
            public readonly int Alertness;
            public readonly CampusMischiefIconKind IconKind;

            public CampusSkeletonActorDefinition(
                string actorName,
                CampusSkeletonActorRole role,
                string anchorId,
                Vector2 anchorLocalOffset,
                string state,
                int alertness,
                CampusMischiefIconKind iconKind)
            {
                ActorName = actorName;
                Role = role;
                AnchorId = anchorId;
                AnchorLocalOffset = anchorLocalOffset;
                State = state;
                Alertness = Mathf.Max(0, alertness);
                IconKind = iconKind;
            }
        }

        private enum CampusMischiefIconKind
        {
            Classroom,
            CampusShop,
            Bookstore,
            Canteen,
            Library,
            DeliveryZone,
            SkewerStand,
            ShopClerk,
            CanteenClerk,
            Zhuzi,
            LibraryTeacher,
            DeliveryOwner,
            SkewerBoss
        }
    }
}

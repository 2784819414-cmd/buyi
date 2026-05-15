using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusCharacterRuntime))]
    public sealed class CampusNpcAgent : MonoBehaviour
    {
        private const string VisualRootName = "NpcVisual";
        private const string InteractionAnchorName = "NpcTalkAnchor";
        private const string InteractionTargetName = "NpcInteractionTarget";
        private const float ReachDistance = 0.08f;

        private static readonly Dictionary<int, Sprite> CachedBodySprites = new Dictionary<int, Sprite>();

        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusNpcInteractable interactable;
        [SerializeField] private CampusNpcSpeechBubble speechBubble;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private Transform speechAnchor;
        [SerializeField, Min(0.2f)] private float walkSpeed = 0.95f;
        [SerializeField, Min(0.25f)] private float minIdleSeconds = 0.8f;
        [SerializeField, Min(0.4f)] private float maxIdleSeconds = 2.2f;
        [SerializeField, Min(1f)] private float minAmbientSpeechSeconds = 5f;
        [SerializeField, Min(2f)] private float maxAmbientSpeechSeconds = 11f;

        private Vector3 targetPosition;
        private float nextDecisionTime;
        private float nextAmbientSpeechTime;
        private bool isMoving;

        public CampusCharacterRuntime Runtime => runtime;

        public void Initialize(CampusCharacterRuntime targetRuntime, CampusGameBootstrap targetBootstrap, CampusWorldService targetWorldService)
        {
            runtime = targetRuntime != null ? targetRuntime : GetComponent<CampusCharacterRuntime>();
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = targetWorldService != null
                ? targetWorldService
                : bootstrap != null
                    ? bootstrap.WorldService
                    : null;

            EnsurePresentation();
            EnsureInteraction();
            targetPosition = transform.position;
            nextDecisionTime = Time.time + Random.Range(0.25f, 1.1f);
            nextAmbientSpeechTime = Time.time + Random.Range(minAmbientSpeechSeconds, maxAmbientSpeechSeconds);
        }

        public bool TryTalk(GameObject actor, out string spokenLine)
        {
            spokenLine = BuildInteractiveLine(actor);
            if (string.IsNullOrWhiteSpace(spokenLine))
            {
                return false;
            }

            Say(spokenLine, 3.2f, true);
            if (runtime != null && runtime.Data != null)
            {
                runtime.Data.AddMemory("talked_to_player");
            }

            return true;
        }

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<CampusCharacterRuntime>();
            }

            Initialize(runtime, bootstrap, worldService);
        }

        private void Update()
        {
            if (!Application.isPlaying || runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            ResolveReferences();
            UpdateMovement();
            UpdateAmbientSpeech();
        }

        private void LateUpdate()
        {
            if (bodyRenderer == null)
            {
                return;
            }

            CampusRenderSortingUtility.ConfigureTopDownTransparencySort();
            int baseOrder = 1120 - Mathf.RoundToInt(transform.position.y * 100f);
            bodyRenderer.sortingOrder = baseOrder;

            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = baseOrder;
            }
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (worldService == null && bootstrap != null)
            {
                worldService = bootstrap.WorldService;
            }

            if (runtime == null)
            {
                runtime = GetComponent<CampusCharacterRuntime>();
            }

            if (speechBubble == null || interactable == null || bodyRenderer == null)
            {
                EnsurePresentation();
                EnsureInteraction();
            }
        }

        private void EnsurePresentation()
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
                if (sortingGroup == null)
                {
                    sortingGroup = gameObject.AddComponent<SortingGroup>();
                }
            }

            Transform visualRoot = transform.Find(VisualRootName);
            if (visualRoot == null)
            {
                GameObject visualObject = new GameObject(VisualRootName);
                visualObject.transform.SetParent(transform, false);
                visualRoot = visualObject.transform;
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = visualRoot.GetComponent<SpriteRenderer>();
                if (bodyRenderer == null)
                {
                    bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            bodyRenderer.sprite = GetBodySprite(ResolveShirtColor());
            bodyRenderer.color = Color.white;
            bodyRenderer.drawMode = SpriteDrawMode.Simple;
            bodyRenderer.transform.localScale = runtime.Data.Role == CampusCharacterRole.Teacher
                ? new Vector3(0.92f, 0.92f, 1f)
                : new Vector3(0.84f, 0.84f, 1f);

            if (speechAnchor == null)
            {
                GameObject speechAnchorObject = new GameObject("SpeechAnchor");
                speechAnchorObject.transform.SetParent(transform, false);
                speechAnchorObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                speechAnchor = speechAnchorObject.transform;
            }

            if (speechBubble == null)
            {
                speechBubble = GetComponent<CampusNpcSpeechBubble>();
                if (speechBubble == null)
                {
                    speechBubble = gameObject.AddComponent<CampusNpcSpeechBubble>();
                }
            }

            speechBubble.Bind(speechAnchor);
        }

        private void EnsureInteraction()
        {
            if (interactable == null)
            {
                Transform interactionRoot = transform.Find(InteractionTargetName);
                if (interactionRoot == null)
                {
                    GameObject interactionObject = new GameObject(InteractionTargetName);
                    interactionObject.transform.SetParent(transform, false);
                    interactionRoot = interactionObject.transform;
                }

                interactable = interactionRoot.GetComponent<CampusNpcInteractable>();
                if (interactable == null)
                {
                    interactable = interactionRoot.gameObject.AddComponent<CampusNpcInteractable>();
                }
            }

            interactable.Bind(this);

            Transform anchorRoot = transform.Find(InteractionAnchorName);
            if (anchorRoot == null)
            {
                GameObject anchorObject = new GameObject(InteractionAnchorName);
                anchorObject.transform.SetParent(transform, false);
                anchorObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                anchorRoot = anchorObject.transform;
            }

            CircleCollider2D trigger = anchorRoot.GetComponent<CircleCollider2D>();
            if (trigger == null)
            {
                trigger = anchorRoot.gameObject.AddComponent<CircleCollider2D>();
            }

            trigger.isTrigger = true;
            trigger.radius = 0.52f;

            CampusInteractionAnchor anchor = anchorRoot.GetComponent<CampusInteractionAnchor>();
            if (anchor == null)
            {
                anchor = anchorRoot.gameObject.AddComponent<CampusInteractionAnchor>();
            }

            anchor.InteractionTarget = interactable;
            anchor.ActionId = CampusInteractionActionIds.NpcTalk;
            anchor.PromptAnchor = speechAnchor != null ? speechAnchor : transform;
            anchor.PromptText = "Talk " + ResolveDisplayName();
            anchor.Priority = 95;
            anchor.IsAvailable = true;
            anchor.HideWhenUnavailable = false;
        }

        private void UpdateMovement()
        {
            if (ShouldStayStill())
            {
                isMoving = false;
                targetPosition = transform.position;
                return;
            }

            if (isMoving)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, walkSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetPosition) <= ReachDistance)
                {
                    isMoving = false;
                    nextDecisionTime = Time.time + Random.Range(minIdleSeconds, maxIdleSeconds);
                }

                return;
            }

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            CampusGameplayRoom room = ResolveCurrentRoom();
            Vector3 baseCenter = room != null ? room.WorldCenter : transform.position;
            Vector2 extents = ResolveRoamExtents(room);

            targetPosition = new Vector3(
                baseCenter.x + Random.Range(-extents.x, extents.x),
                baseCenter.y + Random.Range(-extents.y, extents.y),
                transform.position.z);
            isMoving = true;
        }

        private void UpdateAmbientSpeech()
        {
            if (Time.time < nextAmbientSpeechTime)
            {
                return;
            }

            nextAmbientSpeechTime = Time.time + Random.Range(minAmbientSpeechSeconds, maxAmbientSpeechSeconds);
            if (Random.value > 0.42f)
            {
                return;
            }

            string ambientLine = BuildAmbientLine();
            if (!string.IsNullOrWhiteSpace(ambientLine))
            {
                Say(ambientLine, 2.6f, false);
            }
        }

        private bool ShouldStayStill()
        {
            if (runtime == null || runtime.Data == null)
            {
                return true;
            }

            switch (runtime.Data.State)
            {
                case CampusCharacterState.Sleeping:
                case CampusCharacterState.Punished:
                    return true;
                default:
                    return false;
            }
        }

        private CampusGameplayRoom ResolveCurrentRoom()
        {
            if (runtime == null || runtime.Data == null)
            {
                return null;
            }

            if (worldService == null)
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
            return room ?? worldService.FindRoomForRuntime(runtime);
        }

        private Vector2 ResolveRoamExtents(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return new Vector2(0.9f, 0.65f);
            }

            float x = Mathf.Max(0.45f, room.MarkerBounds.size.x * 0.28f);
            float y = Mathf.Max(0.35f, room.MarkerBounds.size.y * 0.2f);
            if (runtime != null && runtime.Data != null && runtime.Data.Role == CampusCharacterRole.Teacher)
            {
                x *= 0.75f;
                y *= 0.75f;
            }

            return new Vector2(x, y);
        }

        private string BuildInteractiveLine(GameObject actor)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return "...";
            }

            if (data.State == CampusCharacterState.Sleeping)
            {
                return "Zzz... not now.";
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                if ((data.TeacherDuty & CampusTeacherDuty.PatrolDirector) != 0)
                {
                    return "I am watching both the hall and the classroom.";
                }

                if ((data.TeacherDuty & CampusTeacherDuty.MathTeacher) != 0)
                {
                    return "Finish the problem before you drift off.";
                }

                return "Keep it quiet during class.";
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return "I remember who keeps causing trouble.";
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                return "I barely slept last night...";
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return "The teacher may call on us soon.";
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return actor != null && actor.GetComponent<CampusPlayerCharacter>() != null
                    ? "Looking for trouble? I might be interested."
                    : "This class is painfully boring.";
            }

            return "Class first. Talk later.";
        }

        private string BuildAmbientLine()
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return string.Empty;
            }

            if (data.State == CampusCharacterState.Reprimanded)
            {
                return "Okay, I will settle down.";
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return (data.TeacherDuty & CampusTeacherDuty.WorldLanguageTeacher) != 0
                    ? "Open the textbook. We continue with dictation."
                    : "Prepare your scratch paper.";
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                return "So sleepy...";
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return "I need to remember this page.";
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return "Who is whispering again?";
            }

            return Random.value > 0.5f ? "Mm." : "This day is taking forever.";
        }

        private void Say(string line, float durationSeconds, bool writeToLog)
        {
            if (speechBubble != null)
            {
                speechBubble.Speak(line, durationSeconds);
            }

            if (writeToLog && bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog("[Talk] " + ResolveDisplayName() + ": " + line);
            }
        }

        private string ResolveDisplayName()
        {
            return runtime != null && runtime.Data != null && !string.IsNullOrWhiteSpace(runtime.Data.DisplayName)
                ? runtime.Data.DisplayName
                : gameObject.name;
        }

        private Color ResolveShirtColor()
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return new Color(0.33f, 0.57f, 0.78f, 1f);
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return (data.TeacherDuty & CampusTeacherDuty.MathTeacher) != 0
                    ? new Color(0.74f, 0.49f, 0.22f, 1f)
                    : new Color(0.28f, 0.51f, 0.73f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                return new Color(0.53f, 0.52f, 0.82f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return new Color(0.34f, 0.66f, 0.40f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return new Color(0.82f, 0.70f, 0.26f, 1f);
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return new Color(0.55f, 0.31f, 0.24f, 1f);
            }

            return new Color(0.33f, 0.57f, 0.78f, 1f);
        }

        private static Sprite GetBodySprite(Color shirtColor)
        {
            int spriteKey = ColorToKey(shirtColor);
            if (CachedBodySprites.TryGetValue(spriteKey, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.alphaIsTransparency = true;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color hair = new Color32(36, 20, 21, 255);
            Color skin = new Color32(150, 100, 90, 255);
            Color shirt = shirtColor;
            Color pants = new Color32(16, 28, 58, 255);
            Color shoes = new Color32(10, 14, 32, 255);

            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, 16, 4, 11, 8, 2, hair);
            FillRect(pixels, 16, 4, 9, 1, 2, hair);
            FillRect(pixels, 16, 5, 8, 6, 3, skin);
            FillRect(pixels, 16, 4, 5, 2, 3, skin);
            FillRect(pixels, 16, 10, 5, 2, 3, skin);
            FillRect(pixels, 16, 6, 4, 4, 4, shirt);
            FillRect(pixels, 16, 6, 0, 2, 4, pants);
            FillRect(pixels, 16, 8, 0, 2, 4, pants);
            FillRect(pixels, 16, 5, 0, 2, 1, shoes);
            FillRect(pixels, 16, 9, 0, 2, 1, shoes);
            FillRect(pixels, 16, 5, 7, 1, 1, hair);
            FillRect(pixels, 16, 10, 7, 1, 1, hair);

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.02f),
                16f);
            sprite.name = "CampusNpcBodySprite_" + spriteKey;
            CachedBodySprites[spriteKey] = sprite;
            return sprite;
        }

        private static void FillRect(Color[] pixels, int textureWidth, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    if (px < 0 || px >= textureWidth || py < 0 || py >= 16)
                    {
                        continue;
                    }

                    pixels[py * textureWidth + px] = color;
                }
            }
        }

        private static int ColorToKey(Color color)
        {
            Color32 c = color;
            return (c.r << 16) | (c.g << 8) | c.b;
        }
    }
}

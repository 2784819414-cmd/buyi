using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    public enum CampusPrankSpotVisualKind
    {
        Envelope = 0,
        BookChaos = 1,
        DeliveryBox = 2,
        Snack = 3,
        BottleCaps = 4
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(CampusInteractionAnchor))]
    public sealed class CampusPrankInteractionSpot : MonoBehaviour, ICampusInteractionAvailability
    {
        private const string VisualRootName = "VisualRoot";
        private const string BaseRendererName = "Base";
        private const string SymbolRendererAName = "SymbolA";
        private const string SymbolRendererBName = "SymbolB";

        [SerializeField] private string displayName = "Prank Spot";
        [SerializeField] private string prankPayload = CampusPrankPayloadIds.PassNote;
        [SerializeField] private CampusRoomType requiredRoomType = CampusRoomType.Unknown;
        [SerializeField] private CampusPrankSpotVisualKind visualKind = CampusPrankSpotVisualKind.Envelope;
        [SerializeField] private float interactionRadius = 0.95f;
        [SerializeField] private Color accentColor = new Color(0.96f, 0.79f, 0.22f, 1f);
        [SerializeField] private string unsupportedReason = "This formal prank is not implemented yet.";

        private CampusInteractionAnchor interactionAnchor;
        private CircleCollider2D interactionCollider;
        private CampusGameBootstrap bootstrap;
        private CampusPrankService prankService;

        public string DisplayName => displayName;
        public string PrankPayload => prankPayload;
        public CampusRoomType RequiredRoomType => requiredRoomType;
        public CampusPrankSpotVisualKind VisualKind => visualKind;
        public float InteractionRadius => Mathf.Max(0.1f, interactionRadius);
        public Color AccentColor => accentColor;
        public string UnsupportedReason => unsupportedReason;

        public void Configure(
            string targetDisplayName,
            string payload,
            CampusRoomType roomType,
            CampusPrankSpotVisualKind targetVisualKind,
            float radius,
            Color targetAccentColor,
            string unavailableText)
        {
            displayName = string.IsNullOrWhiteSpace(targetDisplayName) ? "Prank Spot" : targetDisplayName.Trim();
            prankPayload = string.IsNullOrWhiteSpace(payload) ? CampusPrankPayloadIds.PassNote : payload.Trim();
            requiredRoomType = roomType;
            visualKind = targetVisualKind;
            interactionRadius = Mathf.Max(0.1f, radius);
            accentColor = targetAccentColor;
            unsupportedReason = string.IsNullOrWhiteSpace(unavailableText)
                ? "This formal prank is not implemented yet."
                : unavailableText.Trim();
            EnsureSetup();
        }

        private void Awake()
        {
            EnsureSetup();
        }

        private void OnEnable()
        {
            EnsureSetup();
        }

        private void Reset()
        {
            EnsureSetup();
        }

        private void OnValidate()
        {
            EnsureSetup();
        }

        public bool CanInteract(GameObject actor, out string unavailableReason)
        {
            ResolveServices();

            if (prankService == null)
            {
                unavailableReason = CampusPrankTextCatalog.Get(CampusPrankTextId.MissingPrankService);
                return false;
            }

            if (prankService.CanExecutePayload(prankPayload, actor, out unavailableReason))
            {
                return true;
            }

            unavailableReason = string.IsNullOrWhiteSpace(unavailableReason)
                ? unsupportedReason
                : unavailableReason;
            return false;
        }

        private void EnsureSetup()
        {
            interactionAnchor = GetComponent<CampusInteractionAnchor>();
            interactionCollider = GetComponent<CircleCollider2D>();

            ConfigureCollider();
            ConfigureAnchor();
            EnsureVisuals();
        }

        private void ConfigureCollider()
        {
            if (interactionCollider == null)
            {
                return;
            }

            interactionCollider.isTrigger = true;
            interactionCollider.offset = new Vector2(0f, 0.12f);
            interactionCollider.radius = Mathf.Max(0.1f, interactionRadius);
        }

        private void ConfigureAnchor()
        {
            if (interactionAnchor == null)
            {
                return;
            }

            interactionAnchor.InteractionTarget = this;
            interactionAnchor.ActionId = CampusInteractionActionIds.PrankExecute;
            interactionAnchor.Payload = prankPayload ?? string.Empty;
            interactionAnchor.PromptText = string.IsNullOrWhiteSpace(displayName) ? "Prank Spot" : displayName.Trim();
            interactionAnchor.KeyOverride = string.Empty;
            interactionAnchor.Icon = null;
            interactionAnchor.AccentColor = accentColor;
            interactionAnchor.Priority = 125;
            interactionAnchor.IsAvailable = true;
            interactionAnchor.UnavailableText = unsupportedReason;
            interactionAnchor.HideWhenUnavailable = false;
            interactionAnchor.UseTargetDoorStatePrompt = false;
            interactionAnchor.LogInteraction = false;
            interactionAnchor.InteractionLogMessage = string.Empty;
        }

        private void ResolveServices()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (prankService == null && bootstrap != null)
            {
                prankService = bootstrap.PrankService;
            }

            if (prankService == null)
            {
                prankService = FindFirstObjectByType<CampusPrankService>(FindObjectsInactive.Include);
            }
        }

        private void EnsureVisuals()
        {
            Transform visualRoot = GetOrCreateChild(transform, VisualRootName);
            LineRenderer baseRenderer = GetOrCreateRenderer(visualRoot, BaseRendererName, 0.08f, accentColor * 0.8f);
            LineRenderer symbolRendererA = GetOrCreateRenderer(visualRoot, SymbolRendererAName, 0.06f, accentColor);
            LineRenderer symbolRendererB = GetOrCreateRenderer(visualRoot, SymbolRendererBName, 0.06f, accentColor);

            ConfigureBaseShape(baseRenderer);
            ConfigureSymbolShape(symbolRendererA, symbolRendererB);
        }

        private void ConfigureBaseShape(LineRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Vector3[] ring =
            {
                new Vector3(-0.42f, -0.42f, 0f),
                new Vector3(0f, -0.58f, 0f),
                new Vector3(0.42f, -0.42f, 0f),
                new Vector3(0.58f, 0f, 0f),
                new Vector3(0.42f, 0.42f, 0f),
                new Vector3(0f, 0.58f, 0f),
                new Vector3(-0.42f, 0.42f, 0f),
                new Vector3(-0.58f, 0f, 0f)
            };

            renderer.loop = true;
            renderer.positionCount = ring.Length;
            renderer.SetPositions(ring);
        }

        private void ConfigureSymbolShape(LineRenderer a, LineRenderer b)
        {
            switch (visualKind)
            {
                case CampusPrankSpotVisualKind.BookChaos:
                    SetPolyline(a, false,
                        new Vector3(-0.24f, -0.1f, 0f),
                        new Vector3(-0.08f, 0.22f, 0f),
                        new Vector3(0.12f, 0.16f, 0f),
                        new Vector3(0.26f, -0.08f, 0f));
                    SetPolyline(b, false,
                        new Vector3(-0.22f, 0.08f, 0f),
                        new Vector3(0.02f, -0.18f, 0f),
                        new Vector3(0.24f, 0.02f, 0f));
                    break;

                case CampusPrankSpotVisualKind.DeliveryBox:
                    SetPolyline(a, true,
                        new Vector3(-0.24f, -0.2f, 0f),
                        new Vector3(0.24f, -0.2f, 0f),
                        new Vector3(0.24f, 0.2f, 0f),
                        new Vector3(-0.24f, 0.2f, 0f));
                    SetPolyline(b, false,
                        new Vector3(-0.24f, 0.2f, 0f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0.24f, 0.2f, 0f),
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0f, -0.2f, 0f));
                    break;

                case CampusPrankSpotVisualKind.Snack:
                    SetPolyline(a, false,
                        new Vector3(-0.16f, -0.22f, 0f),
                        new Vector3(-0.06f, 0.18f, 0f),
                        new Vector3(0.1f, 0.26f, 0f),
                        new Vector3(0.18f, -0.06f, 0f));
                    SetPolyline(b, false,
                        new Vector3(-0.08f, -0.04f, 0f),
                        new Vector3(0.02f, 0.12f, 0f),
                        new Vector3(0.12f, 0f, 0f));
                    break;

                case CampusPrankSpotVisualKind.BottleCaps:
                    SetPolyline(a, true,
                        new Vector3(-0.18f, 0f, 0f),
                        new Vector3(-0.1f, -0.14f, 0f),
                        new Vector3(0.06f, -0.14f, 0f),
                        new Vector3(0.14f, 0f, 0f),
                        new Vector3(0.06f, 0.14f, 0f),
                        new Vector3(-0.1f, 0.14f, 0f));
                    SetPolyline(b, true,
                        new Vector3(0.02f, -0.02f, 0f),
                        new Vector3(0.16f, -0.14f, 0f),
                        new Vector3(0.32f, -0.06f, 0f),
                        new Vector3(0.3f, 0.12f, 0f),
                        new Vector3(0.14f, 0.18f, 0f),
                        new Vector3(0f, 0.08f, 0f));
                    break;

                case CampusPrankSpotVisualKind.Envelope:
                default:
                    SetPolyline(a, true,
                        new Vector3(-0.28f, -0.18f, 0f),
                        new Vector3(0.28f, -0.18f, 0f),
                        new Vector3(0.28f, 0.18f, 0f),
                        new Vector3(-0.28f, 0.18f, 0f));
                    SetPolyline(b, false,
                        new Vector3(-0.28f, 0.18f, 0f),
                        new Vector3(0f, -0.02f, 0f),
                        new Vector3(0.28f, 0.18f, 0f),
                        new Vector3(0f, -0.02f, 0f),
                        new Vector3(-0.28f, -0.18f, 0f),
                        new Vector3(0f, 0.04f, 0f),
                        new Vector3(0.28f, -0.18f, 0f));
                    break;
            }
        }

        private static void SetPolyline(LineRenderer renderer, bool loop, params Vector3[] points)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.loop = loop;
            renderer.positionCount = points != null ? points.Length : 0;
            if (points != null && points.Length > 0)
            {
                renderer.SetPositions(points);
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static LineRenderer GetOrCreateRenderer(Transform parent, string childName, float width, Color color)
        {
            Transform child = GetOrCreateChild(parent, childName);
            LineRenderer renderer = child.GetComponent<LineRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<LineRenderer>();
            }

            renderer.useWorldSpace = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = LineAlignment.View;
            renderer.numCapVertices = 4;
            renderer.numCornerVertices = 4;
            renderer.widthMultiplier = width;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.sortingOrder = 5100;
            renderer.textureMode = LineTextureMode.Stretch;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null && (renderer.sharedMaterial == null || renderer.sharedMaterial.shader != shader))
            {
                renderer.sharedMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return renderer;
        }
    }
}

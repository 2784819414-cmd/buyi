using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NtingCampus.EditorTools
{
    public static class CampusLegacyPrankSceneRepair
    {
        private const string ScenePath = "Assets/Scenes/CampusMap.unity";
        private const string LegacyRootName = "NtingCampus_V01_MischiefSkeletonRoot";

        [MenuItem("NtingCampus/Gameplay/Repair CampusMap Prank Scene")]
        public static void RepairCampusMapMenu()
        {
            RepairCampusMap();
        }

        public static void RepairCampusMap()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveLegacySkeletonRoot();
            RemoveExistingFormalSpots();

            CreateFormalSpot(
                "FormalPrankSpot_PassNote",
                new Vector3(-2.4f, 1.6f, 0f),
                "传纸条",
                CampusPrankPayloadIds.PassNote,
                CampusRoomType.Classroom,
                CampusPrankSpotVisualKind.Envelope,
                new Color(0.96f, 0.79f, 0.22f, 1f));
            CreateFormalSpot(
                "FormalPrankSpot_BookChaos",
                new Vector3(2.1f, -1.7f, 0f),
                "乱整理书",
                CampusPrankPayloadIds.ConfuseBooks,
                CampusRoomType.Library,
                CampusPrankSpotVisualKind.BookChaos,
                new Color(0.76f, 0.88f, 1f, 1f));
            CreateFormalSpot(
                "FormalPrankSpot_StealDelivery",
                new Vector3(-0.2f, -4f, 0f),
                "偷外卖",
                CampusPrankPayloadIds.StealDelivery,
                CampusRoomType.Outdoor,
                CampusPrankSpotVisualKind.DeliveryBox,
                new Color(1f, 0.67f, 0.41f, 1f));
            CreateFormalSpot(
                "FormalPrankSpot_StealSnack",
                new Vector3(-2.4f, -1.7f, 0f),
                "偷炸鸡",
                CampusPrankPayloadIds.StealFriedChicken,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(1f, 0.55f, 0.24f, 1f));
            CreateFormalSpot(
                "FormalPrankSpot_BottleCaps",
                new Vector3(2.1f, 1.7f, 0f),
                "拧瓶盖",
                CampusPrankPayloadIds.TwistBottleCaps,
                CampusRoomType.Store,
                CampusPrankSpotVisualKind.BottleCaps,
                new Color(0.76f, 1f, 0.82f, 1f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CampusLegacyPrankSceneRepair] CampusMap repaired.");
        }

        private static void RemoveLegacySkeletonRoot()
        {
            GameObject legacyRoot = GameObject.Find(LegacyRootName);
            if (legacyRoot != null)
            {
                Object.DestroyImmediate(legacyRoot);
            }
        }

        private static void RemoveExistingFormalSpots()
        {
            CampusPrankInteractionSpot[] spots = Object.FindObjectsByType<CampusPrankInteractionSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < spots.Length; i++)
            {
                CampusPrankInteractionSpot spot = spots[i];
                if (spot != null && spot.gameObject.name.StartsWith("FormalPrankSpot_"))
                {
                    Object.DestroyImmediate(spot.gameObject);
                }
            }
        }

        private static void CreateFormalSpot(
            string objectName,
            Vector3 position,
            string displayName,
            string payload,
            CampusRoomType requiredRoomType,
            CampusPrankSpotVisualKind visualKind,
            Color accentColor)
        {
            GameObject spotObject = new GameObject(objectName);
            spotObject.transform.position = position;

            CircleCollider2D collider = spotObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.offset = new Vector2(0f, 0.12f);
            collider.radius = 0.95f;

            CampusInteractionAnchor anchor = spotObject.AddComponent<CampusInteractionAnchor>();
            CampusPrankInteractionSpot spot = spotObject.AddComponent<CampusPrankInteractionSpot>();

            anchor.InteractionTarget = spot;
            anchor.ActionId = CampusInteractionActionIds.PrankExecute;
            anchor.Payload = payload;
            anchor.PromptText = displayName;
            anchor.AccentColor = accentColor;
            anchor.Priority = 125;
            anchor.IsAvailable = true;
            anchor.HideWhenUnavailable = false;
            anchor.LogInteraction = false;

            SerializedObject serializedSpot = new SerializedObject(spot);
            serializedSpot.FindProperty("displayName").stringValue = displayName;
            serializedSpot.FindProperty("prankPayload").stringValue = payload;
            serializedSpot.FindProperty("requiredRoomType").enumValueIndex = (int)requiredRoomType;
            serializedSpot.FindProperty("visualKind").enumValueIndex = (int)visualKind;
            serializedSpot.FindProperty("interactionRadius").floatValue = 0.95f;
            serializedSpot.FindProperty("accentColor").colorValue = accentColor;
            serializedSpot.FindProperty("unsupportedReason").stringValue = "This formal prank is not implemented yet.";
            serializedSpot.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(anchor);
            EditorUtility.SetDirty(spot);
            EditorUtility.SetDirty(spotObject);
        }
    }
}

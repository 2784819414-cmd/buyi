using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    internal sealed class CampusCanteenOrderPanel : MonoBehaviour
    {
        private CampusCharacterRuntime actor;
        private NtingCampusMapEditor.CampusPlacedObject window;
        private string[] menuItemIds;
        private Vector2 scroll;
        private string statusMessage = string.Empty;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;

        public static void Open(CampusCharacterRuntime actor, NtingCampusMapEditor.CampusPlacedObject window)
        {
            Open(actor, window, null);
        }

        public static void Open(
            CampusCharacterRuntime actor,
            NtingCampusMapEditor.CampusPlacedObject window,
            string[] menuItemIds)
        {
            if (actor == null || window == null)
            {
                return;
            }

            CampusCanteenOrderPanel panel = FindFirstObjectByType<CampusCanteenOrderPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                GameObject host = new GameObject("CampusCanteenOrderPanel");
                panel = host.AddComponent<CampusCanteenOrderPanel>();
            }

            panel.actor = actor;
            panel.window = window;
            panel.menuItemIds = CloneIds(menuItemIds);
            panel.statusMessage = string.Empty;
            panel.enabled = true;
        }

        private void Awake()
        {
            enabled = false;
        }

        private void Update()
        {
            if (NtingCampusMapEditor.CampusGameplayInputBindings.WasPressed(NtingCampusMapEditor.CampusGameplayInputActionId.Settings))
            {
                Close();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            Rect panelRect = ResolvePanelRect();
            GUILayout.BeginArea(panelRect, panelStyle);
            GUILayout.Label(CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderPanelTitle), titleStyle);
            GUILayout.Space(6f);
            GUILayout.Label(
                CampusCanteenTextCatalog.Format(
                    CampusCanteenTextId.OrderPanelBalanceLine,
                    CampusCanteenOrderService.ResolveBalance(actor)),
                bodyStyle);
            GUILayout.Space(10f);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(140f, panelRect.height - 170f)));
            CampusCanteenMenuItem[] items = CampusCanteenMenuCatalog.GetMenuItems(menuItemIds);
            for (int i = 0; i < items.Length; i++)
            {
                DrawMenuItem(items[i]);
            }
            GUILayout.EndScrollView();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label(statusMessage, bodyStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(CampusCanteenTextCatalog.Get(CampusCanteenTextId.CloseButton), buttonStyle, GUILayout.Height(34f)))
            {
                Close();
            }

            GUILayout.EndArea();
        }

        private void DrawMenuItem(CampusCanteenMenuItem item)
        {
            if (item == null)
            {
                return;
            }

            CampusCanteenOrderService.TryGetDefinition(item, out StorageItemDefinition definition);
            string itemName = item.ResolveDisplayName(definition);
            int price = CampusCanteenOrderService.ResolvePrice(item);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(itemName, titleStyle);
            if (definition != null)
            {
                string description = definition.ResolveDescription(NtingCampus.UI.Runtime.Gameplay.CampusLanguageState.CurrentLanguage);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    GUILayout.Label(description, bodyStyle);
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(CampusCanteenTextCatalog.Format(CampusCanteenTextId.PriceLine, price), bodyStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderButton), buttonStyle, GUILayout.Width(120f), GUILayout.Height(32f)))
            {
                SubmitOrder(item);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void SubmitOrder(CampusCanteenMenuItem item)
        {
            if (CampusCanteenOrderService.TryPlaceOrder(actor, window, item, out string message))
            {
                statusMessage = message;
                return;
            }

            statusMessage = string.IsNullOrWhiteSpace(message)
                ? CampusCanteenTextCatalog.Get(CampusCanteenTextId.OrderFailedLog)
                : message;
        }

        private void Close()
        {
            enabled = false;
            actor = null;
            window = null;
            menuItemIds = null;
            statusMessage = string.Empty;
        }

        private static string[] CloneIds(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return null;
            }

            string[] clone = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                clone[i] = ids[i];
            }

            return clone;
        }

        private static Rect ResolvePanelRect()
        {
            float width = Mathf.Min(560f, Screen.width - 48f);
            float height = Mathf.Min(520f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(18, 18, 16, 16)
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }
    }
}

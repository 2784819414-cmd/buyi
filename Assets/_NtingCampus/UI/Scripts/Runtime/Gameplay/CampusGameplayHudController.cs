using System;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Retail;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusGameplayHudController : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameplayHudView view;

        private CampusGameplayHudSnapshot lastSnapshot;
        private bool hasSnapshot;
        private int lastHandsHash;
        private int lastBackpackSlotHash;
        private bool initialized;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            ResolveView();
            initialized = true;
            Refresh(true);
        }

        private void Awake()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            ResolveView();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize(bootstrap);
            }

            Refresh(false);
        }

        private void Refresh(bool immediate)
        {
            if (view == null)
            {
                return;
            }

            CampusCharacterRuntime playerRuntime = ResolvePlayerRuntime();
            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(playerRuntime, true);
            StorageContainerModel[] hands = inventory != null ? inventory.Hands : System.Array.Empty<StorageContainerModel>();
            StorageContainerModel backpackSlot = inventory != null ? inventory.BackpackEquipmentSlot : null;
            StorageWindowUI storageWindow = ResolveStorageWindow();
            CampusGameplayHudSnapshot snapshot = BuildSnapshot(playerRuntime, inventory);
            int handsHash = CampusHandInventoryUtility.BuildHandsStateHash(hands);
            int backpackSlotHash = CampusBackpackInventoryUtility.BuildEquipmentSlotHash(backpackSlot);

            bool firstApply = !hasSnapshot;
            bool handsChanged = handsHash != lastHandsHash;
            bool backpackSlotChanged = backpackSlotHash != lastBackpackSlotHash;
            bool storageWindowChanged = (storageWindow != null && storageWindow.IsOpen) != view.LastStorageWindowOpen;
            bool snapshotChanged = immediate || firstApply || !snapshot.Equals(lastSnapshot);
            if (!snapshotChanged && !handsChanged && !backpackSlotChanged && !storageWindowChanged)
            {
                return;
            }

            lastSnapshot = snapshot;
            lastHandsHash = handsHash;
            lastBackpackSlotHash = backpackSlotHash;
            hasSnapshot = true;
            view.Apply(
                snapshot,
                hands,
                backpackSlot,
                playerRuntime,
                storageWindow,
                immediate,
                firstApply || handsChanged || backpackSlotChanged || storageWindowChanged);
        }

        private CampusGameplayHudSnapshot BuildSnapshot(CampusCharacterRuntime playerRuntime, CampusCharacterInventory inventory)
        {
            CampusTimeController timeController = bootstrap != null ? bootstrap.TimeController : null;
            CampusGameState gameState = bootstrap != null ? bootstrap.GameState : null;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            CampusInteractionController interactionController = ResolveInteractionController(playerRuntime);
            CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(playerRuntime) : null;
            CampusCharacterStaminaController staminaController =
                playerRuntime != null ? playerRuntime.GetComponent<CampusCharacterStaminaController>() : null;
            CampusRetailCheckoutSummary pendingSummary = CampusRetailService.BuildPendingSummary(playerRuntime);

            StorageContainerModel backpack = inventory != null && inventory.HasBackpack ? inventory.Backpack : null;
            int suspicion = gameState != null ? gameState.PlayerSuspicion : 0;
            int alertness = gameState != null ? gameState.TeacherAlertness : 0;
            int warnings = gameState != null ? gameState.DailyWarningCount : 0;
            int money = playerRuntime != null && playerRuntime.Data != null ? playerRuntime.Data.Money : 0;
            int staminaCurrent = Mathf.RoundToInt(staminaController != null
                ? staminaController.CurrentStamina
                : CampusCharacterStaminaTuning.BaseMaxStamina);
            int staminaMax = Mathf.RoundToInt(staminaController != null
                ? staminaController.MaxStamina
                : CampusCharacterStaminaTuning.BaseMaxStamina);

            return new CampusGameplayHudSnapshot(
                BuildDateText(timeController),
                BuildWeekdayText(timeController),
                timeController != null ? timeController.CurrentSegmentName : string.Empty,
                timeController != null ? timeController.CurrentClockText : string.Empty,
                suspicion,
                ResolveRiskLabel(suspicion),
                alertness,
                warnings,
                ResolveAreaName(currentRoom),
                currentRoom != null
                    ? CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.Floor) + " " + currentRoom.FloorIndex
                    : string.Empty,
                ResolveHeadingLabel(interactionController),
                ResolveAreaSubtitle(suspicion, warnings),
                money,
                staminaCurrent,
                staminaMax,
                ResolveBackpackStatus(backpack),
                pendingSummary.PendingItemCount,
                pendingSummary.TotalPrice,
                money >= pendingSummary.TotalPrice,
                ResolvePendingProtectedTransferMode(inventory, worldService));
        }

        private static CampusPendingProtectedTransferHudMode ResolvePendingProtectedTransferMode(
            CampusCharacterInventory inventory,
            CampusWorldService worldService)
        {
            bool hasCheckoutSource = false;
            bool hasRegistrationSource = false;
            AccumulatePendingProtectedTransferMode(inventory != null ? inventory.Hands : null, worldService, ref hasCheckoutSource, ref hasRegistrationSource);
            AccumulatePendingProtectedTransferMode(inventory != null ? inventory.Pockets : null, worldService, ref hasCheckoutSource, ref hasRegistrationSource);
            AccumulatePendingProtectedTransferMode(inventory != null ? inventory.Backpack : null, worldService, ref hasCheckoutSource, ref hasRegistrationSource);

            return hasRegistrationSource && !hasCheckoutSource
                ? CampusPendingProtectedTransferHudMode.Registration
                : CampusPendingProtectedTransferHudMode.Checkout;
        }

        private static void AccumulatePendingProtectedTransferMode(
            StorageContainerModel[] containers,
            CampusWorldService worldService,
            ref bool hasCheckoutSource,
            ref bool hasRegistrationSource)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                AccumulatePendingProtectedTransferMode(
                    containers[i],
                    worldService,
                    ref hasCheckoutSource,
                    ref hasRegistrationSource);
            }
        }

        private static void AccumulatePendingProtectedTransferMode(
            StorageContainerModel container,
            CampusWorldService worldService,
            ref bool hasCheckoutSource,
            ref bool hasRegistrationSource)
        {
            if (container == null || container.Items == null)
            {
                return;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null || !item.IsPendingProtectedTransfer)
                {
                    continue;
                }

                CampusGameplayRoom sourceRoom = worldService != null
                    ? worldService.FindRoomById(item.SourceRoomId)
                    : null;
                if (sourceRoom != null && sourceRoom.RoomType == CampusRoomType.Library)
                {
                    hasRegistrationSource = true;
                    continue;
                }

                hasCheckoutSource = true;
            }
        }

        private CampusCharacterRuntime ResolvePlayerRuntime()
        {
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private CampusInteractionController ResolveInteractionController(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return null;
            }

            return runtime.GetComponentInChildren<CampusInteractionController>(true);
        }

        private static StorageWindowUI ResolveStorageWindow()
        {
            StorageWindowUI window = FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
            return window != null && window.IsOpen ? window : null;
        }

        private void ResolveView()
        {
            if (view != null)
            {
                return;
            }

            view = GetComponent<CampusGameplayHudView>();
            if (view == null)
            {
                view = gameObject.AddComponent<CampusGameplayHudView>();
            }
        }

        private static string BuildDateText(CampusTimeController timeController)
        {
            if (timeController == null)
            {
                return string.Empty;
            }

            CampusGameDate date = timeController.CurrentDate;
            return date.Year + "." + date.Month + "." + date.Day;
        }

        private static string BuildWeekdayText(CampusTimeController timeController)
        {
            if (timeController == null)
            {
                return string.Empty;
            }

            return timeController.CurrentDate.ToDateTime().DayOfWeek switch
            {
                DayOfWeek.Monday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期一", "Monday"),
                DayOfWeek.Tuesday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期二", "Tuesday"),
                DayOfWeek.Wednesday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期三", "Wednesday"),
                DayOfWeek.Thursday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期四", "Thursday"),
                DayOfWeek.Friday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期五", "Friday"),
                DayOfWeek.Saturday => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期六", "Saturday"),
                _ => StorageTextCatalog.Resolve(CampusLanguageState.CurrentLanguage, "星期日", "Sunday")
            };
        }

        private static string ResolveRiskLabel(int suspicionPercent)
        {
            if (suspicionPercent >= 55)
            {
                return CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.HighRisk);
            }

            if (suspicionPercent >= 25)
            {
                return CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.MediumRisk);
            }

            return CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.LowRisk);
        }

        private static string ResolveHeadingLabel(CampusInteractionController interactionController)
        {
            if (interactionController == null || interactionController.Sensor == null)
            {
                return string.Empty;
            }

            Vector2 direction = interactionController.Sensor.FacingDirection;
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return direction.x >= 0f
                    ? CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.East)
                    : CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.West);
            }

            return direction.y >= 0f
                ? CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.North)
                : CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.South);
        }

        private static string ResolveAreaSubtitle(int suspicionPercent, int warnings)
        {
            return suspicionPercent >= 40 || warnings > 0
                ? CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.WarningSubtitleRisky)
                : CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.WarningSubtitleSafe);
        }

        private static string ResolveBackpackStatus(StorageContainerModel backpack)
        {
            if (backpack == null)
            {
                return CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.NoBackpack);
            }

            string weightUnit = CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.WeightUnit);
            return backpack.CurrentWeight.ToString("0.#") + "/" + backpack.MaxWeight.ToString("0.#") + " " + weightUnit;
        }

        private static string ResolveAreaName(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return CampusGameplayHudTextCatalog.Get(CampusGameplayHudTextId.UnknownArea);
            }

            return room.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }
    }
}

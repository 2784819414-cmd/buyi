using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusDroppedStorageItem : MonoBehaviour, ICampusInteractable, ICampusInteractionActionHandler, ICampusInteractionPromptProvider
    {
        public string DefinitionId;
        public string InstanceId;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Width = 1;
        public int Height = 1;
        public float Weight;
        [TextArea]
        public string Description;
        public CampusLocalizedText LocalizedDescription;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        public string UseText;
        public CampusLocalizedText LocalizedUseText;
        public StorageItemLegalState LegalState;
        public string OwnerId;
        public string SourceContainerId;
        public string SourceRoomId;
        public string SourceLocation;
        public bool StolenDuringSession;
        public int SuspicionRisk;

        public void Interact(GameObject actor)
        {
            CampusCharacterActionExecutor.TryPickUpDroppedItem(ResolveActorRuntime(actor), this, out _);
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string normalized = CampusInteractionActionIds.Normalize(actionId);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !CampusInteractionActionIds.Equals(normalized, CampusInteractionActionIds.PickupStorageItem) &&
                !CampusInteractionActionIds.Equals(normalized, CampusInteractionActionIds.InteractTarget))
            {
                return false;
            }

            return CampusCharacterActionExecutor.TryPickUpDroppedItem(ResolveActorRuntime(actor), this, out _);
        }

        public bool TryGetInteractionPrompt(GameObject actor, out CampusInteractionPromptData prompt)
        {
            string itemName = ResolveDisplayName();
            prompt = CampusInteractionPromptData.Create(CampusInteractionTextCatalog.Format(CampusInteractionTextId.PickupItem, itemName));
            prompt.Anchor = transform;
            prompt.WorldOffset = new Vector3(0f, 0.38f, 0f);
            prompt.Priority = 140;
            prompt.IsAvailable = true;
            return true;
        }

        public bool TryPickup(GameObject actor, out string errorMessage)
        {
            bool pickedUp = TryPickup(ResolveActorRuntime(actor), out StorageTransferResult result);
            errorMessage = result.Message;
            return pickedUp;
        }

        public bool TryPickup(CampusCharacterRuntime actorRuntime, out StorageTransferResult result)
        {
            if (actorRuntime == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusCharacterInventoryService.GetOrCreateInventory(actorRuntime, false);
            StorageItemModel item = BuildStorageItem(memory);
            if (item == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.CouldNotRebuildDroppedItem));
                return false;
            }

            StorageTransferContext context = StorageTransferContext.ForActor(actorRuntime.gameObject, StorageTransferReason.Pickup);
            context.SourceLocation = SourceLocation;
            context.OwnerId = OwnerId;
            context.ForceIllegal = ShouldMarkPickupIllegal(actorRuntime);
            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            if (!service.TryPickUpIntoHands(memory, item, context, out result))
            {
                WritePickupLog(result.Message);
                return false;
            }

            WritePickupLog(result.Message);
            Destroy(gameObject);
            return true;
        }

        private bool ShouldMarkPickupIllegal(CampusCharacterRuntime actorRuntime)
        {
            if (LegalState == StorageItemLegalState.Stolen || LegalState == StorageItemLegalState.Suspicious)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(OwnerId) || actorRuntime == null)
            {
                return false;
            }

            return !string.Equals(OwnerId.Trim(), actorRuntime.CharacterId, System.StringComparison.OrdinalIgnoreCase);
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            return actor != null ? actor.GetComponentInParent<CampusCharacterRuntime>() : null;
        }

        private StorageItemModel BuildStorageItem(StorageMemory memory)
        {
            StorageItemModel item = null;
            if (memory != null &&
                memory.ItemRegistry != null &&
                !string.IsNullOrWhiteSpace(DefinitionId))
            {
                item = memory.ItemRegistry.CreateItem(DefinitionId, InstanceId);
            }

            if (item == null)
            {
                item = new StorageItemModel
                {
                    Id = string.IsNullOrWhiteSpace(InstanceId) ? DefinitionId : InstanceId,
                    InstanceId = string.IsNullOrWhiteSpace(InstanceId) ? DefinitionId : InstanceId,
                    DefinitionId = DefinitionId
                };
            }

            item.DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? DefinitionId : DisplayName;
            item.LocalizedDisplayName = LocalizedDisplayName;
            item.Width = Mathf.Max(1, Width);
            item.Height = Mathf.Max(1, Height);
            item.Weight = Weight;
            item.Description = Description;
            item.LocalizedDescription = LocalizedDescription;
            item.ThemeColor = ThemeColor;
            item.IsUsable = IsUsable;
            item.UseActionId = UseActionId;
            item.ConsumeOnUse = ConsumeOnUse;
            item.UseText = UseText;
            item.LocalizedUseText = LocalizedUseText;
            item.LegalState = LegalState == StorageItemLegalState.Unknown ? StorageItemLegalState.Personal : LegalState;
            item.OwnerId = OwnerId;
            item.SourceContainerId = SourceContainerId;
            item.SourceRoomId = SourceRoomId;
            item.SourceLocation = SourceLocation;
            item.StolenDuringSession = StolenDuringSession;
            item.SuspicionRisk = SuspicionRisk;
            item.AllowTaking = !item.IsStolenEvidence;
            return item;
        }

        private string ResolveDisplayName()
        {
            return LocalizedDisplayName.HasAnyText
                ? LocalizedDisplayName.Current(DisplayName, DefinitionId)
                : string.IsNullOrWhiteSpace(DisplayName)
                    ? DefinitionId
                    : DisplayName;
        }

        private static void WritePickupLog(string message)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}

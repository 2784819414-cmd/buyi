using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusInventoryContextResolver
    {
        private readonly CampusWorldService worldService;

        public CampusInventoryContextResolver(CampusGameBootstrap bootstrap)
        {
            worldService = bootstrap != null ? bootstrap.WorldService : null;
        }

        public StorageTransferContext Normalize(StorageTransferContext context)
        {
            context = context ?? new StorageTransferContext();
            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            CampusCharacterCurrentRoomTracker.SyncRuntime(runtime, worldService);

            if (string.IsNullOrWhiteSpace(context.ActorId) &&
                runtime != null &&
                !string.IsNullOrWhiteSpace(runtime.CharacterId))
            {
                context.ActorId = runtime.CharacterId.Trim();
            }

            if (string.IsNullOrWhiteSpace(context.RoomId) && runtime != null)
            {
                context.RoomId = CampusProtectedTransferState.ResolveActorCurrentRoomId(runtime);
            }

            return context;
        }

        public CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            return context != null
                ? CampusCharacterActionUtility.ResolveActorRuntime(context.Actor)
                : null;
        }

        public string ResolveActorId(StorageTransferContext context)
        {
            if (context != null && !string.IsNullOrWhiteSpace(context.ActorId))
            {
                return context.ActorId.Trim();
            }

            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : string.Empty;
        }

        public Vector3 ResolveActorWorldPosition(StorageTransferContext context)
        {
            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            if (runtime != null)
            {
                return runtime.transform.position;
            }

            return context != null && context.Actor != null
                ? context.Actor.transform.position
                : Vector3.zero;
        }

        public string ResolveRoomId(StorageTransferContext context, StorageContainerModel source)
        {
            if (context != null && !string.IsNullOrWhiteSpace(context.RoomId))
            {
                return context.RoomId.Trim();
            }

            if (source != null && !string.IsNullOrWhiteSpace(source.RoomId))
            {
                return source.RoomId.Trim();
            }

            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            CampusCharacterCurrentRoomTracker.SyncRuntime(runtime, worldService);
            return CampusProtectedTransferState.ResolveActorCurrentRoomId(runtime);
        }

        public static string ResolveOwnerId(
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.OwnerId))
            {
                return item.OwnerId.Trim();
            }

            if (context != null && !string.IsNullOrWhiteSpace(context.OwnerId))
            {
                return context.OwnerId.Trim();
            }

            return source != null && !string.IsNullOrWhiteSpace(source.OwnerId)
                ? source.OwnerId.Trim()
                : string.Empty;
        }
    }
}

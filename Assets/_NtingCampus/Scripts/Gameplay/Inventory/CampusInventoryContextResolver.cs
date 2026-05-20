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
        private readonly CampusRosterService rosterService;

        public CampusInventoryContextResolver(CampusGameBootstrap bootstrap)
        {
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
        }

        public StorageTransferContext Normalize(StorageTransferContext context)
        {
            context = context ?? new StorageTransferContext();
            if (string.IsNullOrWhiteSpace(context.ActorId))
            {
                CampusCharacterRuntime runtime = ResolveActorRuntime(context);
                context.ActorId = runtime != null ? runtime.CharacterId : "player";
            }

            if (string.IsNullOrWhiteSpace(context.RoomId))
            {
                CampusCharacterRuntime runtime = ResolveActorRuntime(context);
                CampusGameplayRoom room = runtime != null && worldService != null
                    ? worldService.FindRoomForRuntime(runtime)
                    : null;
                context.RoomId = room != null ? room.RoomId : string.Empty;
            }

            return context;
        }

        public CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            if (context != null && context.Actor != null)
            {
                CampusCharacterRuntime runtime = context.Actor.GetComponentInParent<CampusCharacterRuntime>();
                if (runtime != null)
                {
                    return runtime;
                }
            }

            return rosterService != null ? rosterService.PlayerRuntime : null;
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
                : "player";
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
            CampusGameplayRoom room = runtime != null && worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            return room != null ? room.RoomId : string.Empty;
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

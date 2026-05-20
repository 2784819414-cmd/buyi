using System;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    internal sealed class CampusStoreFacts
    {
        public const string GeneralCategoryId = "general";

        private const float ClerkReachDistance = 1.05f;

        private CampusRosterService rosterService;
        private CampusWorldService worldService;
        private CampusScheduleService scheduleService;
        private Action<string, string> warningSink;
        private Func<CampusPlacedObject, string> shelfContainerIdResolver;

        public void SetContext(
            CampusRosterService targetRosterService,
            CampusWorldService targetWorldService,
            CampusScheduleService targetScheduleService,
            Action<string, string> targetWarningSink,
            Func<CampusPlacedObject, string> targetShelfContainerIdResolver)
        {
            rosterService = targetRosterService;
            worldService = targetWorldService;
            scheduleService = targetScheduleService;
            warningSink = targetWarningSink;
            shelfContainerIdResolver = targetShelfContainerIdResolver;
        }

        public bool IsStoreOpenNow()
        {
            CampusTimeSegment segment = scheduleService != null && scheduleService.TimeController != null
                ? scheduleService.TimeController.CurrentSegment
                : CampusTimeSegment.MorningClass1;
            return CampusNpcScheduleFacts.IsStoreOpen(segment);
        }

        public bool TryFindShelfBrowseTarget(
            CampusCharacterRuntime actor,
            string preferredCategoryId,
            out CampusPlacedObject shelf,
            out string roomId,
            out Vector3 targetPosition)
        {
            shelf = null;
            roomId = string.Empty;
            targetPosition = Vector3.zero;
            CampusGameplayRoom room = ResolvePreferredStoreRoom(actor);
            if (room == null)
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord shelfRecord = FindShelfByCategory(
                room,
                NormalizeCategoryId(preferredCategoryId),
                actor != null ? actor.transform.position : room.WorldCenter);
            if (shelfRecord == null || shelfRecord.PlacedObject == null)
            {
                return false;
            }

            shelf = shelfRecord.PlacedObject;
            roomId = room.RoomId;
            targetPosition = ResolveShelfCustomerPosition(ResolveFacilityWorldPosition(shelfRecord));
            return true;
        }

        public bool TryFindCheckoutTarget(
            CampusCharacterRuntime actor,
            out CampusPlacedObject checkout,
            out string roomId,
            out Vector3 targetPosition)
        {
            checkout = null;
            roomId = string.Empty;
            targetPosition = Vector3.zero;
            CampusGameplayRoom room = ResolvePreferredStoreRoom(actor);
            if (room == null)
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord checkoutRecord = FindNearestFacility(
                room,
                CampusFacilityType.StoreCheckout,
                actor != null ? actor.transform.position : room.WorldCenter);
            if (checkoutRecord == null || checkoutRecord.PlacedObject == null)
            {
                return false;
            }

            checkout = checkoutRecord.PlacedObject;
            roomId = room.RoomId;
            targetPosition = ResolveCheckoutCustomerPosition(ResolveFacilityWorldPosition(checkoutRecord));
            return true;
        }

        public bool TryResolveCheckout(
            CampusPlacedObject checkout,
            out CampusGameplayRoom room,
            out Vector3 checkoutPosition,
            out string checkoutDisplayName)
        {
            room = null;
            checkoutPosition = Vector3.zero;
            checkoutDisplayName = string.Empty;
            if (!IsStoreCheckout(checkout))
            {
                return false;
            }

            room = ResolveRoomForObject(checkout);
            if (room == null || room.RoomType != CampusRoomType.Store)
            {
                return false;
            }

            checkoutPosition = checkout.transform.position;
            checkoutDisplayName = checkout.DisplayName;
            return true;
        }

        public bool TryResolveShelf(
            CampusPlacedObject shelf,
            out CampusGameplayRoom room,
            out CampusGameplayRoom.FacilityRecord shelfRecord,
            out Vector3 shelfCustomerPosition)
        {
            room = null;
            shelfRecord = null;
            shelfCustomerPosition = Vector3.zero;
            if (!IsStoreShelf(shelf))
            {
                return false;
            }

            room = ResolveRoomForObject(shelf);
            if (room == null || room.RoomType != CampusRoomType.Store)
            {
                return false;
            }

            shelfRecord = FindFacilityForObject(room, shelf, CampusFacilityType.StoreShelf);
            if (shelfRecord == null)
            {
                return false;
            }

            shelfCustomerPosition = ResolveShelfCustomerPosition(ResolveFacilityWorldPosition(shelfRecord));
            return true;
        }

        public bool TryFindClerkAtCheckout(
            CampusGameplayRoom room,
            Vector3 checkoutPosition,
            out CampusCharacterRuntime clerk)
        {
            clerk = null;
            if (rosterService == null || worldService == null || room == null)
            {
                return false;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Staff))
            {
                if (runtime == null ||
                    runtime.Data == null ||
                    (runtime.Data.StaffDuty & (CampusStaffDuty.StoreOwner | CampusStaffDuty.BookstoreOwner)) == 0)
                {
                    continue;
                }

                CampusGameplayRoom runtimeRoom = worldService.FindRoomForRuntime(runtime);
                if (runtimeRoom == null ||
                    !string.Equals(runtimeRoom.RoomId, room.RoomId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Vector2.Distance(runtime.transform.position, checkoutPosition) <= ClerkReachDistance)
                {
                    clerk = runtime;
                    return true;
                }
            }

            return false;
        }

        public CampusGameplayRoom ResolveRoomForObject(CampusPlacedObject placedObject)
        {
            if (placedObject == null || worldService == null)
            {
                return null;
            }

            return worldService.FindRoomForPosition(placedObject.FloorIndex, placedObject.transform.position);
        }

        public string ResolveShelfCategoryId(
            CampusPlacedObject shelf,
            CampusStoreShelfDefinition shelfDefinition)
        {
            if (shelfDefinition != null)
            {
                return shelfDefinition.ResolveCategoryId();
            }

            string inferred = InferLegacyShelfCategory(shelf);
            if (!string.Equals(inferred, GeneralCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                string shelfContainerId = shelfContainerIdResolver != null
                    ? shelfContainerIdResolver(shelf)
                    : "store_shelf";
                warningSink?.Invoke(
                    "shelf.legacy_category." + shelfContainerId,
                    "Store shelf is using legacy TypeId category fallback. Add CampusStoreShelfDefinition for explicit mod data.");
            }

            return inferred;
        }

        public static bool IsStoreShelf(CampusPlacedObject placedObject)
        {
            return CampusFacilityTypeResolver.Resolve(placedObject) == CampusFacilityType.StoreShelf;
        }

        public static bool IsStoreCheckout(CampusPlacedObject placedObject)
        {
            return CampusFacilityTypeResolver.Resolve(placedObject) == CampusFacilityType.StoreCheckout;
        }

        public static Vector3 ResolveFacilityWorldPosition(CampusGameplayRoom.FacilityRecord facility)
        {
            if (facility == null)
            {
                return Vector3.zero;
            }

            if (facility.PlacedObject != null)
            {
                return facility.PlacedObject.transform.position;
            }

            Vector3Int cell = facility.Cell;
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        public static Vector3 ResolveShelfCustomerPosition(Vector3 shelfPosition)
        {
            return shelfPosition + Vector3.down * 0.48f;
        }

        public static Vector3 ResolveCheckoutCustomerPosition(Vector3 checkoutPosition)
        {
            return checkoutPosition + Vector3.down * 0.56f;
        }

        public static string NormalizeCategoryId(string value)
        {
            string normalized = NormalizeId(value);
            return string.IsNullOrWhiteSpace(normalized) ? GeneralCategoryId : normalized;
        }

        private CampusGameplayRoom ResolvePreferredStoreRoom(CampusCharacterRuntime actor)
        {
            CampusGameplayRoom actorRoom = ResolveStoreRoomForActor(actor);
            if (actorRoom != null)
            {
                return actorRoom;
            }

            return worldService != null
                ? worldService.FindFirstUsableRoom(CampusRoomType.Store) ?? worldService.FindFirstRoom(CampusRoomType.Store)
                : null;
        }

        private CampusGameplayRoom ResolveStoreRoomForActor(CampusCharacterRuntime actor)
        {
            if (actor == null || worldService == null)
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(actor);
            return room != null && room.RoomType == CampusRoomType.Store ? room : null;
        }

        private CampusGameplayRoom.FacilityRecord FindShelfByCategory(
            CampusGameplayRoom room,
            string categoryId,
            Vector3 referencePosition)
        {
            CampusGameplayRoom.FacilityRecord anyShelf = null;
            CampusGameplayRoom.FacilityRecord categoryShelf = null;
            float bestCategoryDistance = float.MaxValue;
            float bestAnyDistance = float.MaxValue;
            categoryId = NormalizeCategoryId(categoryId);

            if (room == null || room.Facilities == null)
            {
                return null;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = room.Facilities[i];
                if (facility == null || facility.FacilityType != CampusFacilityType.StoreShelf)
                {
                    continue;
                }

                Vector3 position = ResolveFacilityWorldPosition(facility);
                float distance = Vector2.SqrMagnitude((Vector2)(position - referencePosition));
                if (distance < bestAnyDistance)
                {
                    anyShelf = facility;
                    bestAnyDistance = distance;
                }

                CampusPlacedObject shelf = facility.PlacedObject;
                string shelfCategory = ResolveShelfCategoryId(
                    shelf,
                    shelf != null ? shelf.GetComponent<CampusStoreShelfDefinition>() : null);
                if (!string.Equals(shelfCategory, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (distance < bestCategoryDistance)
                {
                    categoryShelf = facility;
                    bestCategoryDistance = distance;
                }
            }

            return categoryShelf != null ? categoryShelf : anyShelf;
        }

        private static CampusGameplayRoom.FacilityRecord FindNearestFacility(
            CampusGameplayRoom room,
            CampusFacilityType facilityType,
            Vector3 referencePosition)
        {
            CampusGameplayRoom.FacilityRecord best = null;
            float bestDistance = float.MaxValue;
            if (room == null || room.Facilities == null)
            {
                return null;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = room.Facilities[i];
                if (facility == null || facility.FacilityType != facilityType)
                {
                    continue;
                }

                Vector3 position = ResolveFacilityWorldPosition(facility);
                float distance = Vector2.SqrMagnitude((Vector2)(position - referencePosition));
                if (distance < bestDistance)
                {
                    best = facility;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static CampusGameplayRoom.FacilityRecord FindFacilityForObject(
            CampusGameplayRoom room,
            CampusPlacedObject placedObject,
            CampusFacilityType facilityType)
        {
            if (room == null || placedObject == null || room.Facilities == null)
            {
                return null;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = room.Facilities[i];
                if (facility != null &&
                    facility.FacilityType == facilityType &&
                    ReferenceEquals(facility.PlacedObject, placedObject))
                {
                    return facility;
                }
            }

            return null;
        }

        private static string InferLegacyShelfCategory(CampusPlacedObject shelf)
        {
            if (shelf == null)
            {
                return GeneralCategoryId;
            }

            string key = ((shelf.TypeId ?? string.Empty) + "|" + (shelf.ObjectId ?? string.Empty) + "|" + shelf.DisplayName).ToLowerInvariant();
            if (key.Contains("snack") || key.Contains("food") || key.Contains("\u96f6\u98df"))
            {
                return "snack";
            }

            if (key.Contains("book") || key.Contains("textbook") || key.Contains("\u4e66"))
            {
                return "book";
            }

            if (key.Contains("stationery") || key.Contains("pencil") || key.Contains("pen") || key.Contains("\u6587\u5177"))
            {
                return "stationery";
            }

            if (key.Contains("phone") || key.Contains("electronic"))
            {
                return "electronics";
            }

            return GeneralCategoryId;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

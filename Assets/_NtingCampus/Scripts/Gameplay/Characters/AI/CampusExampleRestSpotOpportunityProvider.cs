using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public sealed class CampusExampleRestSpotOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public string ProviderId => "example.rest_spot";

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            return npc.IsValid &&
                   query.Purpose == CampusNpcOpportunityPurpose.FreeMovement &&
                   npc.WorldService != null;
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (!CanCollect(npc, query) || results == null)
            {
                return;
            }

            CampusGameplayRoom room = npc.WorldService.FindFirstUsableRoom(CampusRoomType.CommonActivityZone) ??
                                      npc.WorldService.FindFirstRoom(CampusRoomType.CommonActivityZone);
            if (room == null)
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "example_rest_spot",
                CampusCharacterAction.RunCommand(NoOpActionCommand.Instance),
                room.WorldCenter,
                room.RoomId,
                0.25f,
                5f,
                CampusNpcIntentKind.Roam,
                "ExampleRestSpot"));
        }

        private sealed class NoOpActionCommand : ICampusCharacterActionCommand
        {
            public static readonly NoOpActionCommand Instance = new NoOpActionCommand();

            public bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result)
            {
                result = new StorageTransferResult(actor != null, false, false, string.Empty, string.Empty);
                return actor != null;
            }
        }
    }
}

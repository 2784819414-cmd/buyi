using System;
using System.Collections.Generic;
using System.IO;
using NtingCampus.Gameplay.Characters;
using NtingCampusMapEditor;

namespace NtingCampus.UI.Runtime.Gameplay
{
    internal static class CampusRuntimeGameplayOverlayWorkflow
    {
        public static bool TryLoadExistingSnapshot(
            string mapPath,
            out string overlayPath,
            out CampusRuntimeGameplayOverlaySnapshot snapshot,
            out string errorMessage)
        {
            overlayPath = CampusRuntimeGameplayOverlayStore.GetPathForMapPath(mapPath);
            snapshot = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(overlayPath) || !File.Exists(overlayPath))
            {
                return false;
            }

            if (CampusRuntimeGameplayOverlayStore.TryReadSnapshot(mapPath, out snapshot, out errorMessage))
            {
                return true;
            }

            snapshot = null;
            return false;
        }

        public static CampusRuntimeGameplayOverlaySnapshot BuildSnapshot(
            string mapName,
            List<CampusRuntimeGameplayActorSnapshot> actors,
            Action<CampusRuntimeGameplayOverlaySnapshot> captureMarkers)
        {
            CampusRuntimeGameplayOverlaySnapshot snapshot = new CampusRuntimeGameplayOverlaySnapshot
            {
                Schema = CampusRuntimeGameplayOverlayStore.Schema,
                MapName = string.IsNullOrWhiteSpace(mapName) ? "CampusMap" : mapName,
                Actors = actors ?? new List<CampusRuntimeGameplayActorSnapshot>(),
                Rooms = new List<CampusRuntimeGameplayRoomSnapshot>(),
                Facilities = new List<CampusRuntimeGameplayFacilitySnapshot>(),
                ServiceStations = new List<CampusRuntimeGameplayServiceStationSnapshot>()
            };
            captureMarkers?.Invoke(snapshot);
            return snapshot;
        }

        public static List<CampusRuntimeGameplayActorSnapshot> ResolveActorsForSave(
            string mapPath,
            bool cacheInitialized,
            List<CampusRuntimeGameplayActorSnapshot> cachedActors,
            Action<List<CampusRuntimeGameplayActorSnapshot>> captureSceneActors,
            Action<string> logWarning)
        {
            if (cacheInitialized)
            {
                return CampusRuntimeGameplayOverlayStore.CloneActors(cachedActors);
            }

            string overlayPath;
            CampusRuntimeGameplayOverlaySnapshot snapshot;
            string errorMessage;
            if (TryLoadExistingSnapshot(mapPath, out overlayPath, out snapshot, out errorMessage))
            {
                if (snapshot.Actors != null && snapshot.Actors.Count > 0)
                {
                    return CampusRuntimeGameplayOverlayStore.CloneActors(snapshot.Actors);
                }
            }
            else if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                logWarning?.Invoke(errorMessage);
            }

            List<CampusRuntimeGameplayActorSnapshot> actors = new List<CampusRuntimeGameplayActorSnapshot>();
            captureSceneActors?.Invoke(actors);
            return actors;
        }

        public static void ReplaceActorCache(
            List<CampusRuntimeGameplayActorSnapshot> destination,
            List<CampusRuntimeGameplayActorSnapshot> source)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (source == null)
            {
                return;
            }

            List<CampusRuntimeGameplayActorSnapshot> clones =
                CampusRuntimeGameplayOverlayStore.CloneActors(source);
            for (int i = 0; i < clones.Count; i++)
            {
                destination.Add(clones[i]);
            }
        }
    }
}


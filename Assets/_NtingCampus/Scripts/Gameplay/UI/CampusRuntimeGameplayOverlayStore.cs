using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    public static class CampusRuntimeGameplayOverlayStore
    {
        public const string Schema = "NtingCampusGameplayOverlay.v1";
        public const string Extension = ".gameplay.json";

        public static string GetPathForMapPath(string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                return string.Empty;
            }

            string folder = Path.GetDirectoryName(mapPath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(mapPath);
            return Path.Combine(folder, fileName + Extension);
        }

        public static void WriteSnapshot(string mapPath, CampusRuntimeGameplayOverlaySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                throw new ArgumentException("Map path is required.", nameof(mapPath));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            string overlayPath = GetPathForMapPath(mapPath);
            string folder = Path.GetDirectoryName(overlayPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            snapshot.Schema = Schema;
            NormalizeSnapshot(snapshot);
            File.WriteAllText(overlayPath, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
        }

        public static bool TryReadSnapshot(
            string mapPath,
            out CampusRuntimeGameplayOverlaySnapshot snapshot,
            out string errorMessage)
        {
            snapshot = null;
            errorMessage = string.Empty;

            string overlayPath = GetPathForMapPath(mapPath);
            if (string.IsNullOrWhiteSpace(overlayPath) || !File.Exists(overlayPath))
            {
                return false;
            }

            try
            {
                snapshot = JsonUtility.FromJson<CampusRuntimeGameplayOverlaySnapshot>(
                    File.ReadAllText(overlayPath, Encoding.UTF8));
                if (snapshot == null)
                {
                    errorMessage = "Invalid gameplay overlay JSON.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(snapshot.Schema) &&
                    !string.Equals(snapshot.Schema, Schema, StringComparison.Ordinal))
                {
                    errorMessage = "Gameplay overlay schema is invalid: " + overlayPath;
                    snapshot = null;
                    return false;
                }

                NormalizeSnapshot(snapshot);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                snapshot = null;
                return false;
            }
        }

        public static void NormalizeSnapshot(CampusRuntimeGameplayOverlaySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.Schema))
            {
                snapshot.Schema = Schema;
            }

            snapshot.Actors = snapshot.Actors ?? new List<CampusRuntimeGameplayActorSnapshot>();
            snapshot.Rooms = snapshot.Rooms ?? new List<CampusRuntimeGameplayRoomSnapshot>();
            snapshot.Facilities = snapshot.Facilities ?? new List<CampusRuntimeGameplayFacilitySnapshot>();
            snapshot.PrankSpots = snapshot.PrankSpots ?? new List<CampusRuntimeGameplayPrankSpotSnapshot>();
            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                if (snapshot.Actors[i] != null)
                {
                    snapshot.Actors[i].Normalize();
                }
            }

            for (int i = 0; i < snapshot.Rooms.Count; i++)
            {
                if (snapshot.Rooms[i] != null)
                {
                    snapshot.Rooms[i].Normalize();
                }
            }

            for (int i = 0; i < snapshot.Facilities.Count; i++)
            {
                if (snapshot.Facilities[i] != null)
                {
                    snapshot.Facilities[i].Normalize();
                }
            }

            for (int i = 0; i < snapshot.PrankSpots.Count; i++)
            {
                if (snapshot.PrankSpots[i] != null)
                {
                    snapshot.PrankSpots[i].Normalize();
                }
            }
        }

        public static List<CampusRuntimeGameplayActorSnapshot> CloneActors(
            List<CampusRuntimeGameplayActorSnapshot> actors)
        {
            List<CampusRuntimeGameplayActorSnapshot> clones =
                new List<CampusRuntimeGameplayActorSnapshot>();
            if (actors == null)
            {
                return clones;
            }

            for (int i = 0; i < actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot source = actors[i];
                if (source == null)
                {
                    continue;
                }

                CampusRuntimeGameplayActorSnapshot clone = new CampusRuntimeGameplayActorSnapshot
                {
                    Id = source.Id,
                    DisplayName = source.DisplayName,
                    LocalizedDisplayName = source.LocalizedDisplayName,
                    RoleId = source.RoleId,
                    Role = source.Role,
                    TeacherDutyId = source.TeacherDutyId,
                    TeacherDuty = source.TeacherDuty,
                    StaffDutyId = source.StaffDutyId,
                    StaffDuty = source.StaffDuty,
                    ClassId = source.ClassId,
                    InitialStateId = source.InitialStateId,
                    InitialState = source.InitialState,
                    IsPlayerControlled = source.IsPlayerControlled,
                    FloorIndex = Mathf.Max(1, source.FloorIndex),
                    Cell = NormalizeCell(source.Cell),
                    Sleepiness = source.Sleepiness,
                    Mischief = source.Mischief,
                    TraitIds = source.TraitIds != null
                        ? (string[])source.TraitIds.Clone()
                        : Array.Empty<string>(),
                    Traits = source.Traits != null
                        ? (CampusCharacterTrait[])source.Traits.Clone()
                        : Array.Empty<CampusCharacterTrait>(),
                    Assignments = source.Assignments != null
                        ? source.Assignments.Clone()
                        : new CampusCharacterAssignmentData()
                };
                clone.Normalize();
                clones.Add(clone);
            }

            return clones;
        }

        public static void MergeRuntimeActorAssignments(List<CampusRuntimeGameplayActorSnapshot> actors)
        {
            if (actors == null || actors.Count == 0)
            {
                return;
            }

            Dictionary<string, CampusRuntimeGameplayActorSnapshot> actorsById =
                new Dictionary<string, CampusRuntimeGameplayActorSnapshot>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot actor = actors[i];
                if (actor == null || string.IsNullOrWhiteSpace(actor.Id))
                {
                    continue;
                }

                actorsById[actor.Id.Trim()] = actor;
            }

            CampusCharacterRuntime[] runtimes = UnityEngine.Object.FindObjectsByType<CampusCharacterRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < runtimes.Length; i++)
            {
                CampusCharacterData data = runtimes[i] != null ? runtimes[i].Data : null;
                if (data == null || string.IsNullOrWhiteSpace(data.Id) || !data.Assignments.HasAny())
                {
                    continue;
                }

                if (!actorsById.TryGetValue(data.Id.Trim(), out CampusRuntimeGameplayActorSnapshot actor))
                {
                    continue;
                }

                actor.Assignments = data.Assignments.Clone();
                actor.Normalize();
            }
        }

        private static Vector3Int NormalizeCell(Vector3Int cell)
        {
            return new Vector3Int(cell.x, cell.y, 0);
        }
    }
}

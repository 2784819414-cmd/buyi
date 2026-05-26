using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public sealed class CampusRosterIndex
    {
        private readonly List<CampusCharacterRuntime> runtimes = new List<CampusCharacterRuntime>();
        private readonly Dictionary<CampusCharacterRole, List<CampusCharacterRuntime>> runtimesByRole =
            new Dictionary<CampusCharacterRole, List<CampusCharacterRuntime>>();
        private readonly Dictionary<string, List<CampusCharacterRuntime>> runtimesByRoom =
            new Dictionary<string, List<CampusCharacterRuntime>>(StringComparer.OrdinalIgnoreCase);

        private int roomIndexFrame = -1;

        public IReadOnlyList<CampusCharacterRuntime> Runtimes => runtimes;

        public void Rebuild(IReadOnlyList<CampusCharacterRuntime> source)
        {
            runtimes.Clear();
            runtimesByRole.Clear();
            ClearRoomIndex();

            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                CampusCharacterRuntime runtime = source[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                runtimes.Add(runtime);
                GetOrCreateRoleList(runtime.Data.Role).Add(runtime);
            }
        }

        public IReadOnlyList<CampusCharacterRuntime> GetByRole(CampusCharacterRole role)
        {
            return runtimesByRole.TryGetValue(role, out List<CampusCharacterRuntime> matches)
                ? matches
                : Array.Empty<CampusCharacterRuntime>();
        }

        public IReadOnlyList<CampusCharacterRuntime> GetByRoom(string roomId)
        {
            RefreshRoomIndex();
            return runtimesByRoom.TryGetValue(NormalizeId(roomId), out List<CampusCharacterRuntime> matches)
                ? matches
                : Array.Empty<CampusCharacterRuntime>();
        }

        public IReadOnlyList<CampusCharacterRuntime> GetVisibleActorCandidates(string roomId, bool requireSameRoom)
        {
            return requireSameRoom ? GetByRoom(roomId) : Runtimes;
        }

        private void RefreshRoomIndex()
        {
            int frame = Time.frameCount;
            if (roomIndexFrame == frame)
            {
                return;
            }

            ClearRoomIndex();
            roomIndexFrame = frame;
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                string roomId = runtime != null && runtime.Data != null
                    ? NormalizeId(runtime.Data.CurrentRoomId)
                    : string.Empty;
                if (string.IsNullOrEmpty(roomId))
                {
                    continue;
                }

                GetOrCreateRoomList(roomId).Add(runtime);
            }
        }

        private void ClearRoomIndex()
        {
            runtimesByRoom.Clear();
            roomIndexFrame = -1;
        }

        private List<CampusCharacterRuntime> GetOrCreateRoleList(CampusCharacterRole role)
        {
            if (!runtimesByRole.TryGetValue(role, out List<CampusCharacterRuntime> matches))
            {
                matches = new List<CampusCharacterRuntime>();
                runtimesByRole.Add(role, matches);
            }

            return matches;
        }

        private List<CampusCharacterRuntime> GetOrCreateRoomList(string roomId)
        {
            if (!runtimesByRoom.TryGetValue(roomId, out List<CampusCharacterRuntime> matches))
            {
                matches = new List<CampusCharacterRuntime>();
                runtimesByRoom.Add(roomId, matches);
            }

            return matches;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

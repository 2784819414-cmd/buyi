using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusRoomValidationTextId
    {
        LegacyRoomTypeInference = 0,
        UnknownRoomTypeSource = 1
    }

    public static class CampusRoomValidationTextCatalog
    {
        private readonly struct Entry
        {
            public Entry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusRoomValidationTextId, Entry> Entries = new()
        {
            { CampusRoomValidationTextId.LegacyRoomTypeInference, new Entry("房间类型 {0} 来自旧版房间名推断。正常 mod 数据应使用 CampusGameplayRoomMarker 显式设置 RoomType。", "Room type {0} came from legacy room-name inference. Normal mod data should set RoomType explicitly with CampusGameplayRoomMarker.") },
            { CampusRoomValidationTextId.UnknownRoomTypeSource, new Entry("房间类型来源未知。请添加 CampusGameplayRoomMarker 并显式设置 RoomType。", "Room type source is unknown. Add a CampusGameplayRoomMarker and set RoomType explicitly.") }
        };

        public static string Format(CampusRoomValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusRoomValidationTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(CampusLanguageState.CurrentLanguage, entry.Chinese, entry.English);
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
        {
            return language switch
            {
                CampusDisplayLanguage.English => english,
                CampusDisplayLanguage.Bilingual => chinese + " / " + english,
                _ => chinese
            };
        }
    }
}

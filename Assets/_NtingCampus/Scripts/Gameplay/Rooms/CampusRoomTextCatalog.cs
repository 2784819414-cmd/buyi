using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusRoomTextCatalog
    {
        private static readonly Dictionary<CampusRoomType, Entry> Entries = new Dictionary<CampusRoomType, Entry>
        {
            { CampusRoomType.Unknown, new Entry("\u672A\u77E5\u533A\u57DF", "Unknown Area") },
            { CampusRoomType.Classroom, new Entry("\u6559\u5BA4", "Classroom") },
            { CampusRoomType.Office, new Entry("\u529E\u516C\u5BA4", "Office") },
            { CampusRoomType.Dormitory, new Entry("\u5BBF\u820D", "Dormitory") },
            { CampusRoomType.Restroom, new Entry("\u6D17\u624B\u95F4", "Restroom") },
            { CampusRoomType.ServiceArea, new Entry("\u670D\u52A1\u533A", "Service Area") },
            { CampusRoomType.RetailArea, new Entry("\u96F6\u552E\u533A", "Retail Area") },
            { CampusRoomType.Library, new Entry("\u56FE\u4E66\u9986", "Library") },
            { CampusRoomType.Corridor, new Entry("\u8D70\u5ECA", "Corridor") },
            { CampusRoomType.Stairwell, new Entry("\u697C\u68AF\u95F4", "Stairwell") },
            { CampusRoomType.Outdoor, new Entry("\u5BA4\u5916", "Outdoor") },
            { CampusRoomType.CommonActivityZone, new Entry("\u516C\u5171\u6D3B\u52A8\u533A", "Common Activity Zone") },
            { CampusRoomType.HumanResources, new Entry("\u4EBA\u4E8B\u5904", "Human Resources") },
            { CampusRoomType.ShrineRoom, new Entry("\u795E\u9F9B\u5BA4", "Shrine Room") }
        };

        public static string Get(CampusRoomType roomType)
        {
            return Get(CampusLanguageState.CurrentLanguage, roomType);
        }

        public static string Get(CampusDisplayLanguage language, CampusRoomType roomType)
        {
            CampusLocalizedText text = GetLocalizedText(roomType);
            return text.Get(language);
        }

        public static CampusLocalizedText GetLocalizedText(CampusRoomType roomType)
        {
            Entry entry = Entries.TryGetValue(roomType, out Entry resolved)
                ? resolved
                : Entries[CampusRoomType.Unknown];
            return new CampusLocalizedText(
                entry.Chinese,
                entry.English,
                entry.TraditionalChinese,
                entry.Russian,
                entry.Japanese);
        }
    }
}

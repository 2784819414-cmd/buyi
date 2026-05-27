using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using TextEntry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public enum CampusGameplayDebugTextId
    {
        GameDate = 0,
        Time = 1,
        Segment = 2,
        Schedule = 3,
        Mode = 4,
        Money = 5,
        RemovedResource = 6,
        TimeScale = 7,
        SpeedMode = 8,
        CameraOrtho = 9,
        M1Controls = 10,
        StudentBody = 11,
        GodView = 12,
        Pause = 13,
        Day = 14,
        CampusOrder = 15,
        CampusChaos = 16,
        TeacherAlertness = 17,
        DivineInterest = 18,
        DailyWarnings = 19,
        ShrineRoom = 20,
        LandExpansion = 21,
        WarnPlus = 22,
        AlertPlus = 23,
        ChaosPlus = 24,
        InterestPlus = 25,
        OrderPlus = 26,
        UnlockShrine = 27,
        UnlockLand = 28,
        Roster = 29,
        None = 30,
        Students = 31,
        Teachers = 32,
        Player = 33,
        RosterList = 34,
        SelectedCharacter = 35,
        Name = 36,
        Role = 37,
        Duty = 38,
        Class = 39,
        State = 40,
        Sleepiness = 41,
        Mischief = 42,
        StudyToday = 43,
        Mastery = 44,
        Traits = 45,
        Memories = 46,
        NoCharacters = 47,
        MischiefSkeleton = 48,
        MischiefHeat = 49,
        CurrentAction = 50,
        InteractHint = 51,
        TodayCounts = 52,
        DebugActions = 53,
        AreaRisk = 54,
        CurrentAreaSensitive = 55,
        AreaSuspicionAndAlertLevel = 56,
        Suspicion = 57,
        AlertLevel = 58,
        Hot = 59,
        RecentConsequences = 60,
        RecentEventLogs = 61,
        Unlocked = 62,
        Locked = 63,
        Yes = 64,
        No = 65,
        Perspective = 66,
        Language = 67,
        Math = 68,
        Normal = 69,
        Fast = 70,
        Custom = 71,
        Student = 72,
        Teacher = 73,
        WorldLanguageTeacher = 74,
        MathTeacher = 75,
        HomeroomTeacher = 76,
        PatrolDirector = 77,
        CharacterStateNormal = 78,
        Drowsy = 79,
        Sleeping = 80,
        Excited = 81,
        Nervous = 82,
        Reprimanded = 83,
        Fleeing = 84,
        Punished = 85,
        Ordinary = 86,
        Sleepyhead = 87,
        Troublemaker = 88,
        GoodStudent = 89,
        Tattletale = 90,
        Staff = 91,
        StaffSupport = 92,
        StaffPatrol = 93,
        PrivatePlanner = 94,
        SocialModel = 95,
        Mood = 96,
        Social = 97,
        Relationships = 98,
        Possessions = 99,
        FormalMainline = 100,
        StudyLoop = 102,
        ActiveClassroom = 103,
        DistractedTeacher = 104,
        DozeSneakCaughtToday = 105,
        ForceSleepyDistraction = 106,
        Prompt = 107,
        DailyPassNoteCount = 108,
        ProtectedItemCount = 109,
        StaffMember = 110,
        ClaimedItemCount = 111,
        Request = 112,
        SanctionServiceMissing = 113,
        LastSanction = 114,
        NpcBehaviorMissing = 115,
        NpcBehavior = 116,
        GossipEventsToday = 117,
        TradeRulesMissing = 118,
        TradeRules = 119,
        ProtectedTransfersToday = 120,
        UnpaidTransfers = 121,
        ServiceArea = 122,
        DropArea = 123
    }

    public enum CampusRuntimeEditorTextId
    {
        F10ToggleHintStatus = 0,
        EditorReadyStatus = 1,
        EditorOpenedStatus = 2,
        EditorClosedStatus = 3,
        RuntimeImportsRefreshedStatus = 4,
        BuildTools = 5,
        Pan = 6,
        PaintFloor = 7,
        PaintWall = 8,
        RectFloor = 9,
        RectWall = 10,
        RectErase = 11,
        Erase = 12,
        Pick = 13,
        BrushSize = 14,
        FloorImports = 15,
        WallImports = 16,
        ObjectImports = 17,
        RoomList = 18,
        FloorPalette = 19,
        WallPalette = 20,
        WallProfiles = 21,
        Add = 22,
        Lock = 23,
        Delete = 24,
        Floor = 25,
        Locked = 26,
        RoomChecklist = 27,
        NoRoomRequirementsExist = 28,
        Close = 29,
        Help = 30,
        Import = 31,
        Export = 32,
        Undo = 33,
        Redo = 34,
        GridOn = 35,
        GridOff = 36,
        Settings = 37,
        Rebuild = 38,
        WallVisualsRebuiltStatus = 39,
        Controls = 40,
        ControlsBody = 41,
        NoTileAvailable = 42,
        NoObjectAvailable = 43,
        LightPreview = 44,
        PointLight = 45,
        DayNight = 46,
        MissingDayNightController = 47,
        Set1x = 48,
        Set200x = 49,
        CustomWall = 50,
        Name = 51,
        CreateProfile = 52,
        ApplyToSelected = 53,
        RebuildSelectedFloor = 54,
        RefreshPresentation = 55,
        ChooseFile = 56,
        UseDragTarget = 57,
        ObjectSettings = 58,
        SelectObjectFirst = 59,
        MissingCampusPlacedObject = 60,
        SaveAndSync = 61,
        ApplyToAllSameType = 62,
        SyncPlacedObjects = 63,
        Footprint = 64,
        ApplySize = 65,
        Hint = 66,
        SaveObjectSettings = 67,
        SameNamedObjectsSync = 68,
        NotSet = 69,
        PickSprite = 70,
        Clear = 71,
        ImportFiles = 72,
        SelectFolder = 73,
        DropTarget = 74,
        PasteImage = 75,
        OpenFolder = 76,
        Refresh = 77,
        ActiveDropTarget = 78,
        ImportText = 79,
        PasteText = 80,
        OpenFile = 81,
        MapSource = 82,
        AutosavePlayerMap = 83,
        AutoloadPlayerMapOnStart = 84,
        SavePlayerMap = 85,
        LoadPlayerMap = 86,
        ExportAuthoring = 87,
        RestoreAuthoring = 88,
        DisplayName = 89,
        PreviewSprite = 90,
        NoSprite = 91,
        StorageContainer = 92,
        Size = 93,
        Scale = 94,
        LockAspect = 95,
        AllowFourDirections = 96,
        UseCustomInteractionAnchor = 97,
        X = 98,
        Y = 99,
        R = 100,
        Prompt = 101,
        ImportRoomTypesStatus = 102,
        ImportRoomTypesClipboardStatus = 103,
        NoRoomTypesFoundToImport = 104,
        NoRoomTypesFoundInClipboard = 105,
        SelectRoomDefinitionText = 106,
        GameTime = 107,
        RealMinutesPerGameDay = 108,
        PreviewGrid = 109,
        UniformScale = 110,
        RotationPreview = 111,
        DirectionalSprite = 112,
        DefaultSprite = 113,
        MultiInteractionAnchors = 114,
        AddAnchor = 115,
        RemoveAnchor = 116,
        AnchorId = 117,
        ActionId = 118,
        Payload = 119,
        Enabled = 120,
        ClickPreviewToPlaceAnchor = 121,
        AnchorList = 122,
        SelectedAnchor = 123,
        RetailShelfSection = 124,
        RetailShelfMode = 125,
        RetailItemDefinitionId = 126,
        RetailAutoRestock = 127,
        RetailStockCount = 128,
        RetailDisplayCount = 129,
        RetailContainerMode = 130,
        RetailDisplayMode = 131,
        InteractionPresetEid = 132,
        StudentDeskOwnerStatus = 133,
        StudentDeskOwnerUnassignedStatus = 134
    }

    public static class CampusGameplayDebugTextCatalog
    {
        private static readonly Dictionary<CampusGameplayDebugTextId, TextEntry> Entries = new()
        {
            { CampusGameplayDebugTextId.GameDate, new TextEntry("\u6e38\u620f\u65e5\u671f", "Game Date") },
            { CampusGameplayDebugTextId.Time, new TextEntry("\u65f6\u95f4", "Time") },
            { CampusGameplayDebugTextId.Segment, new TextEntry("\u65f6\u6bb5", "Segment") },
            { CampusGameplayDebugTextId.Schedule, new TextEntry("\u4f5c\u606f", "Schedule") },
            { CampusGameplayDebugTextId.Mode, new TextEntry("\u6a21\u5f0f", "Mode") },
            { CampusGameplayDebugTextId.Money, new TextEntry("\u91d1\u94b1", "Money") },
            { CampusGameplayDebugTextId.RemovedResource, new TextEntry("\u5df2\u5220\u9664", "Removed") },
            { CampusGameplayDebugTextId.TimeScale, new TextEntry("\u65f6\u95f4\u500d\u7387", "Time Scale") },
            { CampusGameplayDebugTextId.SpeedMode, new TextEntry("\u901f\u5ea6\u6a21\u5f0f", "Speed Mode") },
            { CampusGameplayDebugTextId.CameraOrtho, new TextEntry("\u76f8\u673a\u6b63\u4ea4", "Camera Ortho") },
            { CampusGameplayDebugTextId.M1Controls, new TextEntry("M1 \u63a7\u5236", "M1 Controls") },
            { CampusGameplayDebugTextId.StudentBody, new TextEntry("\u5b66\u751f\u672c\u4f53", "Student Body") },
            { CampusGameplayDebugTextId.GodView, new TextEntry("\u4e0a\u5e1d\u89c6\u89d2", "God View") },
            { CampusGameplayDebugTextId.Pause, new TextEntry("\u6682\u505c", "Pause") },
            { CampusGameplayDebugTextId.Day, new TextEntry("\u5929\u6570", "Day") },
            { CampusGameplayDebugTextId.CampusOrder, new TextEntry("\u6821\u56ed\u79e9\u5e8f", "Campus Order") },
            { CampusGameplayDebugTextId.CampusChaos, new TextEntry("\u6821\u56ed\u6df7\u4e71\u5ea6", "Campus Chaos") },
            { CampusGameplayDebugTextId.TeacherAlertness, new TextEntry("\u8001\u5e08\u8b66\u89c9\u5ea6", "Teacher Alertness") },
            { CampusGameplayDebugTextId.DivineInterest, new TextEntry("\u795e\u7684\u5174\u8da3", "Divine Interest") },
            { CampusGameplayDebugTextId.DailyWarnings, new TextEntry("\u6bcf\u65e5\u8b66\u544a\u6b21\u6570", "Daily Warnings") },
            { CampusGameplayDebugTextId.ShrineRoom, new TextEntry("\u795e\u9f9b\u5ba4", "Shrine Room") },
            { CampusGameplayDebugTextId.LandExpansion, new TextEntry("\u5730\u76ae\u6269\u5efa", "Land Expansion") },
            { CampusGameplayDebugTextId.WarnPlus, new TextEntry("\u8b66\u544a+1", "Warn +1") },
            { CampusGameplayDebugTextId.AlertPlus, new TextEntry("\u8b66\u89c9+5", "Alert +5") },
            { CampusGameplayDebugTextId.ChaosPlus, new TextEntry("\u6df7\u4e71+5", "Chaos +5") },
            { CampusGameplayDebugTextId.InterestPlus, new TextEntry("\u5174\u8da3+5", "Interest +5") },
            { CampusGameplayDebugTextId.OrderPlus, new TextEntry("\u79e9\u5e8f+5", "Order +5") },
            { CampusGameplayDebugTextId.UnlockShrine, new TextEntry("\u89e3\u9501\u795e\u9f9b\u5ba4", "Unlock Shrine") },
            { CampusGameplayDebugTextId.UnlockLand, new TextEntry("\u89e3\u9501\u6269\u5efa", "Unlock Land") },
            { CampusGameplayDebugTextId.Roster, new TextEntry("\u89d2\u8272\u5217\u8868", "Roster") },
            { CampusGameplayDebugTextId.None, new TextEntry("\u65e0", "None") },
            { CampusGameplayDebugTextId.Students, new TextEntry("\u5b66\u751f\u6570", "Students") },
            { CampusGameplayDebugTextId.Teachers, new TextEntry("\u6559\u5e08\u6570", "Teachers") },
            { CampusGameplayDebugTextId.Player, new TextEntry("\u73a9\u5bb6", "Player") },
            { CampusGameplayDebugTextId.RosterList, new TextEntry("\u89d2\u8272\u6e05\u5355", "Roster List") },
            { CampusGameplayDebugTextId.SelectedCharacter, new TextEntry("\u5f53\u524d\u89d2\u8272", "Selected Character") },
            { CampusGameplayDebugTextId.Name, new TextEntry("\u59d3\u540d", "Name") },
            { CampusGameplayDebugTextId.Role, new TextEntry("\u8eab\u4efd", "Role") },
            { CampusGameplayDebugTextId.Duty, new TextEntry("\u804c\u8d23", "Duty") },
            { CampusGameplayDebugTextId.Class, new TextEntry("\u73ed\u7ea7", "Class") },
            { CampusGameplayDebugTextId.State, new TextEntry("\u72b6\u6001", "State") },
            { CampusGameplayDebugTextId.Sleepiness, new TextEntry("\u56f0\u610f", "Sleepiness") },
            { CampusGameplayDebugTextId.Mischief, new TextEntry("\u987d\u52a3\u5ea6", "Mischief") },
            { CampusGameplayDebugTextId.StudyToday, new TextEntry("\u4eca\u65e5\u5b66\u4e60", "Study Today") },
            { CampusGameplayDebugTextId.Mastery, new TextEntry("\u638c\u63e1\u5ea6", "Mastery") },
            { CampusGameplayDebugTextId.Traits, new TextEntry("\u7279\u8d28", "Traits") },
            { CampusGameplayDebugTextId.Memories, new TextEntry("\u8bb0\u5fc6", "Memories") },
            { CampusGameplayDebugTextId.NoCharacters, new TextEntry("\u65e0\u89d2\u8272", "No Characters") },
            { CampusGameplayDebugTextId.MischiefSkeleton, new TextEntry("\u539f\u578b\u6076\u4f5c\u5267", "Mischief Skeleton") },
            { CampusGameplayDebugTextId.MischiefHeat, new TextEntry("\u6076\u4f5c\u5267\u70ed\u5ea6", "Mischief Heat") },
            { CampusGameplayDebugTextId.CurrentAction, new TextEntry("\u5f53\u524d\u52a8\u4f5c", "Current Action") },
            { CampusGameplayDebugTextId.InteractHint, new TextEntry("\u9760\u8fd1\u951a\u70b9\u540e\u6309 E \u4ea4\u4e92", "Press E near an anchor to interact") },
            { CampusGameplayDebugTextId.TodayCounts, new TextEntry("\u4eca\u65e5\u6b21\u6570", "Today Counts") },
            { CampusGameplayDebugTextId.DebugActions, new TextEntry("\u8c03\u8bd5\u52a8\u4f5c", "Debug Actions") },
            { CampusGameplayDebugTextId.AreaRisk, new TextEntry("\u533a\u57df\u98ce\u9669", "Area Risk") },
            { CampusGameplayDebugTextId.CurrentAreaSensitive, new TextEntry("\u5f53\u524d\u533a\u57df\u654f\u611f", "Current Area Sensitive") },
            { CampusGameplayDebugTextId.AreaSuspicionAndAlertLevel, new TextEntry("\u533a\u57df\u6000\u7591\u5ea6 / \u8b66\u6212\u7b49\u7ea7", "Area Suspicion / Alert Level") },
            { CampusGameplayDebugTextId.Suspicion, new TextEntry("\u6000\u7591", "Suspicion") },
            { CampusGameplayDebugTextId.AlertLevel, new TextEntry("\u8b66\u6212", "Alert") },
            { CampusGameplayDebugTextId.Hot, new TextEntry("\u70ed\u70b9", "Hot") },
            { CampusGameplayDebugTextId.RecentConsequences, new TextEntry("\u6700\u8fd1\u540e\u679c", "Recent Consequences") },
            { CampusGameplayDebugTextId.RecentEventLogs, new TextEntry("\u6700\u8fd1\u4e8b\u4ef6\u65e5\u5fd7", "Recent Event Logs") },
            { CampusGameplayDebugTextId.Unlocked, new TextEntry("\u5df2\u89e3\u9501", "Unlocked") },
            { CampusGameplayDebugTextId.Locked, new TextEntry("\u672a\u89e3\u9501", "Locked") },
            { CampusGameplayDebugTextId.Yes, new TextEntry("\u662f", "Yes") },
            { CampusGameplayDebugTextId.No, new TextEntry("\u5426", "No") },
            { CampusGameplayDebugTextId.Perspective, new TextEntry("\u900f\u89c6", "Perspective") },
            { CampusGameplayDebugTextId.Language, new TextEntry("\u8bed\u6587", "Language") },
            { CampusGameplayDebugTextId.Math, new TextEntry("\u6570\u5b66", "Math") },
            { CampusGameplayDebugTextId.Normal, new TextEntry("\u6b63\u5e38", "Normal") },
            { CampusGameplayDebugTextId.Fast, new TextEntry("\u5feb\u901f", "Fast") },
            { CampusGameplayDebugTextId.Custom, new TextEntry("\u81ea\u5b9a\u4e49", "Custom") },
            { CampusGameplayDebugTextId.Student, new TextEntry("\u5b66\u751f", "Student") },
            { CampusGameplayDebugTextId.Teacher, new TextEntry("\u6559\u5e08", "Teacher") },
            { CampusGameplayDebugTextId.WorldLanguageTeacher, new TextEntry("\u8bed\u6587\u6559\u5e08", "World Language Teacher") },
            { CampusGameplayDebugTextId.MathTeacher, new TextEntry("\u6570\u5b66\u6559\u5e08", "Math Teacher") },
            { CampusGameplayDebugTextId.HomeroomTeacher, new TextEntry("\u73ed\u4e3b\u4efb", "Homeroom Teacher") },
            { CampusGameplayDebugTextId.PatrolDirector, new TextEntry("\u5de1\u67e5\u4e3b\u4efb", "Patrol Director") },
            { CampusGameplayDebugTextId.CharacterStateNormal, new TextEntry("\u6b63\u5e38", "Normal") },
            { CampusGameplayDebugTextId.Drowsy, new TextEntry("\u56f0\u5026", "Drowsy") },
            { CampusGameplayDebugTextId.Sleeping, new TextEntry("\u7761\u7720\u4e2d", "Sleeping") },
            { CampusGameplayDebugTextId.Excited, new TextEntry("\u5174\u594b", "Excited") },
            { CampusGameplayDebugTextId.Nervous, new TextEntry("\u7d27\u5f20", "Nervous") },
            { CampusGameplayDebugTextId.Reprimanded, new TextEntry("\u88ab\u8bad\u8beb", "Reprimanded") },
            { CampusGameplayDebugTextId.Fleeing, new TextEntry("\u9003\u79bb\u4e2d", "Fleeing") },
            { CampusGameplayDebugTextId.Punished, new TextEntry("\u53d7\u7f5a", "Punished") },
            { CampusGameplayDebugTextId.Ordinary, new TextEntry("\u666e\u901a", "Ordinary") },
            { CampusGameplayDebugTextId.Sleepyhead, new TextEntry("\u778c\u7761\u866b", "Sleepyhead") },
            { CampusGameplayDebugTextId.Troublemaker, new TextEntry("\u60f9\u4e8b\u751f\u975e", "Troublemaker") },
            { CampusGameplayDebugTextId.GoodStudent, new TextEntry("\u597d\u5b66\u751f", "Good Student") },
            { CampusGameplayDebugTextId.Tattletale, new TextEntry("\u7231\u6253\u5c0f\u62a5\u544a", "Tattletale") },
            { CampusGameplayDebugTextId.Staff, new TextEntry("\u804c\u5458", "Staff") },
            { CampusGameplayDebugTextId.StaffSupport, new TextEntry("\u5c97\u4f4d\u804c\u5458", "Support Staff") },
            { CampusGameplayDebugTextId.StaffPatrol, new TextEntry("\u5de1\u89c6\u804c\u5458", "Patrol Staff") },
            { CampusGameplayDebugTextId.PrivatePlanner, new TextEntry("\u79c1\u4e0b\u5b89\u6392", "Private Planner") },
            { CampusGameplayDebugTextId.SocialModel, new TextEntry("\u793e\u4f1a\u6a21\u578b", "Social Model") },
            { CampusGameplayDebugTextId.Mood, new TextEntry("\u5fc3\u60c5", "Mood") },
            { CampusGameplayDebugTextId.Social, new TextEntry("\u793e\u4ea4", "Social") },
            { CampusGameplayDebugTextId.Relationships, new TextEntry("\u5173\u7cfb", "Relationships") },
            { CampusGameplayDebugTextId.Possessions, new TextEntry("\u6301\u6709\u7269", "Possessions") },
            { CampusGameplayDebugTextId.FormalMainline, new TextEntry("\u6b63\u5f0f\u4e3b\u7ebf", "Formal Mainline") },
            { CampusGameplayDebugTextId.StudyLoop, new TextEntry("\u5b66\u4e60\u95ed\u73af", "Study Loop") },
            { CampusGameplayDebugTextId.ActiveClassroom, new TextEntry("\u6d3b\u52a8\u6559\u5ba4", "Active Classroom") },
            { CampusGameplayDebugTextId.DistractedTeacher, new TextEntry("\u5206\u5fc3\u8001\u5e08", "Distracted Teacher") },
            { CampusGameplayDebugTextId.DozeSneakCaughtToday, new TextEntry("\u4eca\u65e5\u7761\u7740/\u6e9c\u8d70/\u88ab\u6293", "Doze/Sneak/Caught Today") },
            { CampusGameplayDebugTextId.ForceSleepyDistraction, new TextEntry("\u5f3a\u5236\u56f0\u5026\u5206\u5fc3", "Force Sleepy Distraction") },
            { CampusGameplayDebugTextId.Prompt, new TextEntry("\u63d0\u793a", "Prompt") },
            { CampusGameplayDebugTextId.DailyPassNoteCount, new TextEntry("\u4eca\u65e5\u4f20\u7eb8\u6761\u6b21\u6570", "Daily Pass Note Count") },
            { CampusGameplayDebugTextId.ProtectedItemCount, new TextEntry("\u53d7\u4fdd\u62a4\u7269\u54c1\u6b21\u6570", "Protected Item Count") },
            { CampusGameplayDebugTextId.StaffMember, new TextEntry("\u804c\u5458", "Staff Member") },
            { CampusGameplayDebugTextId.ClaimedItemCount, new TextEntry("\u5df2\u8ba4\u9886\u7269\u54c1\u6b21\u6570", "Claimed Item Count") },
            { CampusGameplayDebugTextId.Request, new TextEntry("\u8bf7\u6c42", "Request") },
            { CampusGameplayDebugTextId.SanctionServiceMissing, new TextEntry("\u5904\u5206\u670d\u52a1\uff1a\u65e0", "SanctionService: none") },
            { CampusGameplayDebugTextId.LastSanction, new TextEntry("\u4e0a\u6b21\u5904\u5206", "Last Sanction") },
            { CampusGameplayDebugTextId.NpcBehaviorMissing, new TextEntry("NPC \u884c\u4e3a\uff1a\u65e0", "NPC Behavior: none") },
            { CampusGameplayDebugTextId.NpcBehavior, new TextEntry("NPC \u884c\u4e3a", "NPC Behavior") },
            { CampusGameplayDebugTextId.GossipEventsToday, new TextEntry("\u4eca\u65e5\u4f20\u95fb/\u793e\u4ea4\u4e8b\u4ef6", "Gossip/Social Events Today") },
            { CampusGameplayDebugTextId.TradeRulesMissing, new TextEntry("\u4ea4\u6613\u89c4\u5219\uff1a\u65e0", "Trade Rules: none") },
            { CampusGameplayDebugTextId.TradeRules, new TextEntry("\u4ea4\u6613\u89c4\u5219", "Trade Rules") },
            { CampusGameplayDebugTextId.ProtectedTransfersToday, new TextEntry("\u4eca\u65e5\u53d7\u4fdd\u62a4\u8f6c\u79fb", "Protected Transfers Today") },
            { CampusGameplayDebugTextId.UnpaidTransfers, new TextEntry("\u672a\u7ed3\u7b97\u8f6c\u79fb", "Unpaid Transfers") },
            { CampusGameplayDebugTextId.ServiceArea, new TextEntry("\u670d\u52a1\u533a", "Service Area") },
            { CampusGameplayDebugTextId.DropArea, new TextEntry("\u6295\u653e\u533a", "Drop Area") }
        };

        public static string Get(CampusDisplayLanguage language, CampusGameplayDebugTextId id)
        {
            TextEntry entry = Entries.TryGetValue(id, out TextEntry resolved)
                ? resolved
                : new TextEntry(id.ToString(), id.ToString());

            return entry.Get(language);
        }

        public static string Get(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english, traditionalChinese, russian, japanese);
        }

        public static string FormatLine(CampusDisplayLanguage language, CampusGameplayDebugTextId id, object value)
        {
            return Get(language, id) + ": " + value;
        }

        public static string FormatMode(CampusDisplayLanguage language, CampusGameMode mode)
        {
            return mode switch
            {
                CampusGameMode.StudentBody => Get(language, CampusGameplayDebugTextId.StudentBody),
                CampusGameMode.GodView => Get(language, CampusGameplayDebugTextId.GodView),
                _ => mode.ToString()
            };
        }

        public static string FormatSpeedMode(CampusDisplayLanguage language, CampusTimeSpeedMode mode)
        {
            return mode switch
            {
                CampusTimeSpeedMode.Paused => Get(language, CampusGameplayDebugTextId.Pause),
                CampusTimeSpeedMode.Normal => Get(language, CampusGameplayDebugTextId.Normal),
                CampusTimeSpeedMode.Fast => Get(language, CampusGameplayDebugTextId.Fast),
                CampusTimeSpeedMode.Custom => Get(language, CampusGameplayDebugTextId.Custom),
                _ => mode.ToString()
            };
        }

        public static string FormatCharacterRole(CampusDisplayLanguage language, CampusCharacterRole role)
        {
            return role switch
            {
                CampusCharacterRole.Student => Get(language, CampusGameplayDebugTextId.Student),
                CampusCharacterRole.Teacher => Get(language, CampusGameplayDebugTextId.Teacher),
                CampusCharacterRole.Staff => Get(language, CampusGameplayDebugTextId.Staff),
                _ => role.ToString()
            };
        }

        public static string FormatStaffDuty(CampusDisplayLanguage language, CampusStaffDuty duty)
        {
            if (duty == CampusStaffDuty.None)
            {
                return Get(language, CampusGameplayDebugTextId.None);
            }

            return Get(language, CampusGameplayDebugTextId.StaffSupport);
        }

        public static string FormatTeacherDuty(CampusDisplayLanguage language, CampusTeacherDuty duty)
        {
            if (duty == CampusTeacherDuty.None)
            {
                return Get(language, CampusGameplayDebugTextId.None);
            }

            List<string> names = new();
            AppendFlagName(names, language, duty, CampusTeacherDuty.WorldLanguageTeacher, CampusGameplayDebugTextId.WorldLanguageTeacher);
            AppendFlagName(names, language, duty, CampusTeacherDuty.MathTeacher, CampusGameplayDebugTextId.MathTeacher);
            AppendFlagName(names, language, duty, CampusTeacherDuty.HomeroomTeacher, CampusGameplayDebugTextId.HomeroomTeacher);
            AppendFlagName(names, language, duty, CampusTeacherDuty.PatrolDirector, CampusGameplayDebugTextId.PatrolDirector);
            return names.Count > 0 ? string.Join(", ", names) : duty.ToString();
        }

        public static string FormatCharacterState(CampusDisplayLanguage language, CampusCharacterState state)
        {
            return state switch
            {
                CampusCharacterState.Normal => Get(language, CampusGameplayDebugTextId.CharacterStateNormal),
                CampusCharacterState.Drowsy => Get(language, CampusGameplayDebugTextId.Drowsy),
                CampusCharacterState.Sleeping => Get(language, CampusGameplayDebugTextId.Sleeping),
                CampusCharacterState.Excited => Get(language, CampusGameplayDebugTextId.Excited),
                CampusCharacterState.Nervous => Get(language, CampusGameplayDebugTextId.Nervous),
                CampusCharacterState.Reprimanded => Get(language, CampusGameplayDebugTextId.Reprimanded),
                CampusCharacterState.Fleeing => Get(language, CampusGameplayDebugTextId.Fleeing),
                CampusCharacterState.Punished => Get(language, CampusGameplayDebugTextId.Punished),
                _ => state.ToString()
            };
        }

        public static string FormatCharacterTrait(CampusDisplayLanguage language, CampusCharacterTrait trait)
        {
            return trait switch
            {
                CampusCharacterTrait.Ordinary => Get(language, CampusGameplayDebugTextId.Ordinary),
                CampusCharacterTrait.Sleepyhead => Get(language, CampusGameplayDebugTextId.Sleepyhead),
                CampusCharacterTrait.Troublemaker => Get(language, CampusGameplayDebugTextId.Troublemaker),
                CampusCharacterTrait.GoodStudent => Get(language, CampusGameplayDebugTextId.GoodStudent),
                CampusCharacterTrait.Tattletale => Get(language, CampusGameplayDebugTextId.Tattletale),
                CampusCharacterTrait.PrivatePlanner => Get(language, CampusGameplayDebugTextId.PrivatePlanner),
                _ => trait.ToString()
            };
        }

        public static string FormatBool(CampusDisplayLanguage language, bool value)
        {
            return Get(language, value ? CampusGameplayDebugTextId.Yes : CampusGameplayDebugTextId.No);
        }

        public static string FormatLockState(CampusDisplayLanguage language, bool unlocked)
        {
            return Get(language, unlocked ? CampusGameplayDebugTextId.Unlocked : CampusGameplayDebugTextId.Locked);
        }

        public static string FormatPerspective(CampusDisplayLanguage language, string fieldOfView)
        {
            return Get(language, CampusGameplayDebugTextId.Perspective) + "(" + fieldOfView + ")";
        }

        private static void AppendFlagName(
            List<string> names,
            CampusDisplayLanguage language,
            CampusTeacherDuty actual,
            CampusTeacherDuty flag,
            CampusGameplayDebugTextId textId)
        {
            if ((actual & flag) != flag)
            {
                return;
            }

            names.Add(Get(language, textId));
        }

    }

    public static class CampusRuntimeEditorTextCatalog
    {
        private static readonly Dictionary<CampusRuntimeEditorTextId, TextEntry> Entries = new()
        {
            { CampusRuntimeEditorTextId.F10ToggleHintStatus, new TextEntry("F10 \u6253\u5f00\u6216\u5173\u95ed\u8fd0\u884c\u65f6\u5730\u56fe\u7f16\u8f91\u5668\u3002", "F10 opens or closes the runtime map editor.") },
            { CampusRuntimeEditorTextId.EditorReadyStatus, new TextEntry("\u8fd0\u884c\u65f6\u5730\u56fe\u7f16\u8f91\u5668\u5df2\u5c31\u7eea\uff1aF10 \u5207\u6362\uff0c\u5de6\u952e\u653e\u7f6e\uff0c\u53f3\u952e\u64e6\u9664\u3002", "Runtime map editor ready: F10 toggles, left click places, right click erases.") },
            { CampusRuntimeEditorTextId.EditorOpenedStatus, new TextEntry("\u8fd0\u884c\u65f6\u5730\u56fe\u7f16\u8f91\u5668\u5df2\u6253\u5f00\u3002", "Runtime map editor opened.") },
            { CampusRuntimeEditorTextId.EditorClosedStatus, new TextEntry("\u8fd0\u884c\u65f6\u5730\u56fe\u7f16\u8f91\u5668\u5df2\u5173\u95ed\u3002", "Runtime map editor closed.") },
            { CampusRuntimeEditorTextId.RuntimeImportsRefreshedStatus, new TextEntry("\u8fd0\u884c\u65f6\u5bfc\u5165\u8d44\u6e90\u5df2\u5237\u65b0\u3002", "Runtime imports refreshed.") },
            { CampusRuntimeEditorTextId.BuildTools, new TextEntry("\u5efa\u9020\u5de5\u5177", "Build Tools") },
            { CampusRuntimeEditorTextId.Pan, new TextEntry("\u5e73\u79fb", "Pan") },
            { CampusRuntimeEditorTextId.PaintFloor, new TextEntry("\u5237\u5730\u677f", "Paint Floor") },
            { CampusRuntimeEditorTextId.PaintWall, new TextEntry("\u5237\u5899", "Paint Wall") },
            { CampusRuntimeEditorTextId.RectFloor, new TextEntry("\u77e9\u5f62\u5730\u677f", "Rect Floor") },
            { CampusRuntimeEditorTextId.RectWall, new TextEntry("\u77e9\u5f62\u5899", "Rect Wall") },
            { CampusRuntimeEditorTextId.RectErase, new TextEntry("\u77e9\u5f62\u64e6\u9664", "Rect Erase") },
            { CampusRuntimeEditorTextId.Erase, new TextEntry("\u64e6\u9664", "Erase") },
            { CampusRuntimeEditorTextId.Pick, new TextEntry("\u62fe\u53d6", "Pick") },
            { CampusRuntimeEditorTextId.BrushSize, new TextEntry("\u7b14\u5237\u5927\u5c0f", "Brush Size") },
            { CampusRuntimeEditorTextId.FloorImports, new TextEntry("\u5730\u677f\u5bfc\u5165", "Floor Imports") },
            { CampusRuntimeEditorTextId.WallImports, new TextEntry("\u5899\u9762\u5bfc\u5165", "Wall Imports") },
            { CampusRuntimeEditorTextId.ObjectImports, new TextEntry("\u7269\u4ef6\u5bfc\u5165", "Object Imports") },
            { CampusRuntimeEditorTextId.RoomList, new TextEntry("\u533a\u57df\u5217\u8868", "Area List") },
            { CampusRuntimeEditorTextId.FloorPalette, new TextEntry("\u5730\u677f\u8c03\u8272\u76d8", "Floor Palette") },
            { CampusRuntimeEditorTextId.WallPalette, new TextEntry("\u5899\u9762\u8c03\u8272\u76d8", "Wall Palette") },
            { CampusRuntimeEditorTextId.WallProfiles, new TextEntry("\u5899\u4f53\u914d\u7f6e", "Wall Profiles") },
            { CampusRuntimeEditorTextId.Add, new TextEntry("\u65b0\u589e", "Add") },
            { CampusRuntimeEditorTextId.Lock, new TextEntry("\u9501\u5b9a", "Lock") },
            { CampusRuntimeEditorTextId.Delete, new TextEntry("\u5220\u9664", "Delete") },
            { CampusRuntimeEditorTextId.Floor, new TextEntry("\u697c\u5c42", "Floor") },
            { CampusRuntimeEditorTextId.Locked, new TextEntry("\u5df2\u9501\u5b9a", "Locked") },
            { CampusRuntimeEditorTextId.RoomChecklist, new TextEntry("\u533a\u57df\u68c0\u67e5\u6e05\u5355", "Area Checklist") },
            { CampusRuntimeEditorTextId.NoRoomRequirementsExist, new TextEntry("\u5f53\u524d\u6ca1\u6709\u533a\u57df\u9700\u6c42\u3002", "No area requirements exist.") },
            { CampusRuntimeEditorTextId.Close, new TextEntry("\u5173\u95ed", "Close") },
            { CampusRuntimeEditorTextId.Help, new TextEntry("\u5e2e\u52a9", "Help") },
            { CampusRuntimeEditorTextId.Import, new TextEntry("\u5bfc\u5165", "Import") },
            { CampusRuntimeEditorTextId.Export, new TextEntry("\u5bfc\u51fa", "Export") },
            { CampusRuntimeEditorTextId.Undo, new TextEntry("\u64a4\u9500", "Undo") },
            { CampusRuntimeEditorTextId.Redo, new TextEntry("\u91cd\u505a", "Redo") },
            { CampusRuntimeEditorTextId.GridOn, new TextEntry("\u7f51\u683c\u5f00", "Grid On") },
            { CampusRuntimeEditorTextId.GridOff, new TextEntry("\u7f51\u683c\u5173", "Grid Off") },
            { CampusRuntimeEditorTextId.Settings, new TextEntry("\u8bbe\u7f6e", "Settings") },
            { CampusRuntimeEditorTextId.Rebuild, new TextEntry("\u91cd\u5efa", "Rebuild") },
            { CampusRuntimeEditorTextId.WallVisualsRebuiltStatus, new TextEntry("\u5899\u4f53\u8868\u73b0\u5df2\u91cd\u5efa\u3002", "Wall visuals rebuilt.") },
            { CampusRuntimeEditorTextId.Controls, new TextEntry("\u64cd\u4f5c\u8bf4\u660e", "Controls") },
            { CampusRuntimeEditorTextId.ControlsBody, new TextEntry(
                "F10\uff1a\u6253\u5f00\u6216\u5173\u95ed\u7f16\u8f91\u5668\n\u5de6\u952e\uff1a\u4f7f\u7528\u5f53\u524d\u7b14\u5237\u653e\u7f6e\u6216\u6d82\u5237\n\u4e2d\u952e\u62d6\u52a8 / Space + \u5de6\u952e\u62d6\u52a8\uff1a\u79fb\u52a8\u89c6\u56fe\n\u9f20\u6807\u6eda\u8f6e\uff1a\u4ee5\u5149\u6807\u4e3a\u4e2d\u5fc3\u7f29\u653e\n\u53f3\u952e / Shift + \u5de6\u952e\uff1a\u64e6\u9664\u5f53\u524d\u683c\u5b50\nR\uff1a\u65cb\u8f6c\u7269\u4ef6\u3001\u697c\u68af\u6216\u706f\u5149\n[ / ]\uff1a\u8c03\u6574\u7b14\u5237\u5927\u5c0f\nCtrl+Z / Ctrl+Y\uff1a\u64a4\u9500 / \u91cd\u505a\n\u73a9\u5bb6\u5efa\u9020\u5185\u5bb9\u4f1a\u81ea\u52a8\u4fdd\u5b58\u5230 Application.persistentDataPath/CampusPlayerMapSave\n\u5f00\u53d1\u671f\u5730\u56fe\u5305\u53ea\u7528\u4e8e\u56fa\u5316\u573a\u666f\u57fa\u7ebf",
                "F10: Open or close the editor\nLeft click: Place or paint with the active brush\nMiddle drag / Space + left drag: Pan the view\nMouse wheel: Zoom to cursor\nRight click / Shift + left click: Erase the current cell\nR: Rotate object, stair, or light\n[ / ]: Change brush size\nCtrl+Z / Ctrl+Y: Undo / redo\nPlayer builds autosave to Application.persistentDataPath/CampusPlayerMapSave\nAuthoring packages are only for baking the scene baseline") },
            { CampusRuntimeEditorTextId.NoTileAvailable, new TextEntry("\u6ca1\u6709\u53ef\u7528\u5730\u5757\uff0c\u8bf7\u68c0\u67e5 Resources/NtingCampusRuntime \u4e2d\u7684 tile palette\u3002", "No tile is available. Check the tile palette in Resources/NtingCampusRuntime.") },
            { CampusRuntimeEditorTextId.NoObjectAvailable, new TextEntry("\u6ca1\u6709\u53ef\u7528\u7269\u4ef6\uff0c\u8bf7\u68c0\u67e5 Resources/NtingCampusRuntime \u4e2d\u7684\u7269\u4ef6\u8d44\u6e90\u3002", "No object is available. Check object resources in Resources/NtingCampusRuntime.") },
            { CampusRuntimeEditorTextId.LightPreview, new TextEntry("\u5149\u7167\u9884\u89c8", "Light Preview") },
            { CampusRuntimeEditorTextId.PointLight, new TextEntry("\u70b9\u5149\u6e90", "Point Light") },
            { CampusRuntimeEditorTextId.DayNight, new TextEntry("\u663c\u591c", "Day Night") },
            { CampusRuntimeEditorTextId.MissingDayNightController, new TextEntry("\u573a\u666f\u4e2d\u7f3a\u5c11 CampusDayNightController\u3002", "CampusDayNightController is missing in the scene.") },
            { CampusRuntimeEditorTextId.Set1x, new TextEntry("\u8bbe\u4e3a 1x", "Set 1x") },
            { CampusRuntimeEditorTextId.Set200x, new TextEntry("\u8bbe\u4e3a 200x", "Set 200x") },
            { CampusRuntimeEditorTextId.CustomWall, new TextEntry("\u81ea\u5b9a\u4e49\u5899\u4f53", "Custom Wall") },
            { CampusRuntimeEditorTextId.Name, new TextEntry("\u540d\u79f0", "Name") },
            { CampusRuntimeEditorTextId.CreateProfile, new TextEntry("\u521b\u5efa\u914d\u7f6e", "Create Profile") },
            { CampusRuntimeEditorTextId.ApplyToSelected, new TextEntry("\u5e94\u7528\u5230\u5f53\u524d\u9009\u4e2d", "Apply To Selected") },
            { CampusRuntimeEditorTextId.RebuildSelectedFloor, new TextEntry("\u91cd\u5efa\u5f53\u524d\u697c\u5c42", "Rebuild Selected Floor") },
            { CampusRuntimeEditorTextId.RefreshPresentation, new TextEntry("\u5237\u65b0\u8868\u73b0", "Refresh Presentation") },
            { CampusRuntimeEditorTextId.ChooseFile, new TextEntry("\u9009\u62e9\u6587\u4ef6", "Choose File") },
            { CampusRuntimeEditorTextId.UseDragTarget, new TextEntry("\u8bbe\u4e3a\u62d6\u653e\u76ee\u6807", "Use Drag Target") },
            { CampusRuntimeEditorTextId.ObjectSettings, new TextEntry("\u7269\u54c1\u8bbe\u7f6e", "Object Settings") },
            { CampusRuntimeEditorTextId.SelectObjectFirst, new TextEntry("\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002", "Select an object first.") },
            { CampusRuntimeEditorTextId.MissingCampusPlacedObject, new TextEntry("\u8be5\u7269\u54c1\u7f3a\u5c11 CampusPlacedObject\uff0c\u8bf7\u5148\u5728\u9879\u76ee\u4e2d\u914d\u7f6e\u540e\u518d\u8bbe\u7f6e\u3002", "This object is missing CampusPlacedObject. Configure it in the project before editing settings.") },
            { CampusRuntimeEditorTextId.SaveAndSync, new TextEntry("\u4fdd\u5b58\u5e76\u540c\u6b65", "Save And Sync") },
            { CampusRuntimeEditorTextId.ApplyToAllSameType, new TextEntry("\u4e00\u952e\u5e94\u7528\u5230\u573a\u4e0a\u540c\u7c7b", "Apply To All Same Type") },
            { CampusRuntimeEditorTextId.SyncPlacedObjects, new TextEntry("\u7edf\u4e00\u5df2\u653e\u7f6e\u7269\u54c1", "Sync Placed Objects") },
            { CampusRuntimeEditorTextId.Footprint, new TextEntry("\u5360\u5730", "Footprint") },
            { CampusRuntimeEditorTextId.ApplySize, new TextEntry("\u5e94\u7528\u5c3a\u5bf8", "Apply Size") },
            { CampusRuntimeEditorTextId.Hint, new TextEntry("\u63d0\u793a", "Prompt") },
            { CampusRuntimeEditorTextId.SaveObjectSettings, new TextEntry("\u4fdd\u5b58\u7269\u54c1\u8bbe\u7f6e", "Save Object Settings") },
            { CampusRuntimeEditorTextId.SameNamedObjectsSync, new TextEntry("\u5df2\u653e\u7f6e\u7684\u540c\u540d\u7269\u54c1\u4f1a\u540c\u6b65", "Placed objects with the same name will sync together") },
            { CampusRuntimeEditorTextId.NotSet, new TextEntry("\u672a\u8bbe\u7f6e", "Not Set") },
            { CampusRuntimeEditorTextId.PickSprite, new TextEntry("\u9009\u56fe", "Pick") },
            { CampusRuntimeEditorTextId.Clear, new TextEntry("\u6e05\u7a7a", "Clear") },
            { CampusRuntimeEditorTextId.ImportFiles, new TextEntry("\u5bfc\u5165\u6587\u4ef6", "Import Files") },
            { CampusRuntimeEditorTextId.SelectFolder, new TextEntry("\u9009\u62e9\u6587\u4ef6\u5939", "Select Folder") },
            { CampusRuntimeEditorTextId.DropTarget, new TextEntry("\u62d6\u653e\u76ee\u6807", "Drop Target") },
            { CampusRuntimeEditorTextId.PasteImage, new TextEntry("\u7c98\u8d34\u56fe\u7247", "Paste Image") },
            { CampusRuntimeEditorTextId.OpenFolder, new TextEntry("\u6253\u5f00\u6587\u4ef6\u5939", "Open Folder") },
            { CampusRuntimeEditorTextId.Refresh, new TextEntry("\u5237\u65b0", "Refresh") },
            { CampusRuntimeEditorTextId.ActiveDropTarget, new TextEntry("\u5f53\u524d\u62d6\u653e\u76ee\u6807", "Active drop target") },
            { CampusRuntimeEditorTextId.ImportText, new TextEntry("\u5bfc\u5165\u6587\u672c", "Import Text") },
            { CampusRuntimeEditorTextId.PasteText, new TextEntry("\u7c98\u8d34\u6587\u672c", "Paste Text") },
            { CampusRuntimeEditorTextId.OpenFile, new TextEntry("\u6253\u5f00\u6587\u4ef6", "Open File") },
            { CampusRuntimeEditorTextId.MapSource, new TextEntry("\u5730\u56fe\u6765\u6e90", "Map source") },
            { CampusRuntimeEditorTextId.AutosavePlayerMap, new TextEntry("\u81ea\u52a8\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe", "Autosave player map") },
            { CampusRuntimeEditorTextId.AutoloadPlayerMapOnStart, new TextEntry("\u542f\u52a8\u65f6\u81ea\u52a8\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe", "Autoload player map on start") },
            { CampusRuntimeEditorTextId.SavePlayerMap, new TextEntry("\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe", "Save Player Map") },
            { CampusRuntimeEditorTextId.LoadPlayerMap, new TextEntry("\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe", "Load Player Map") },
            { CampusRuntimeEditorTextId.ExportAuthoring, new TextEntry("\u5bfc\u51fa\u5f00\u53d1\u671f\u5730\u56fe\u5305", "Export Authoring") },
            { CampusRuntimeEditorTextId.RestoreAuthoring, new TextEntry("\u6062\u590d\u5f00\u53d1\u671f\u5730\u56fe\u5305", "Restore Authoring") },
            { CampusRuntimeEditorTextId.DisplayName, new TextEntry("\u663e\u793a\u540d", "Display Name") },
            { CampusRuntimeEditorTextId.PreviewSprite, new TextEntry("\u9884\u89c8\u7cbe\u7075", "Preview Sprite") },
            { CampusRuntimeEditorTextId.NoSprite, new TextEntry("\u65e0\u7cbe\u7075", "No Sprite") },
            { CampusRuntimeEditorTextId.StorageContainer, new TextEntry("\u50a8\u7269\u5bb9\u5668", "Storage Container") },
            { CampusRuntimeEditorTextId.Size, new TextEntry("\u5c3a\u5bf8", "Size") },
            { CampusRuntimeEditorTextId.Scale, new TextEntry("\u7f29\u653e", "Scale") },
            { CampusRuntimeEditorTextId.LockAspect, new TextEntry("\u9501\u5b9a\u7b49\u6bd4", "Lock Aspect") },
            { CampusRuntimeEditorTextId.AllowFourDirections, new TextEntry("\u5141\u8bb8\u56db\u5411\u65cb\u8f6c", "Allow Four Directions") },
            { CampusRuntimeEditorTextId.UseCustomInteractionAnchor, new TextEntry("\u542f\u7528\u81ea\u5b9a\u4e49\u4ea4\u4e92\u951a\u70b9", "Use Custom Interaction Anchor") },
            { CampusRuntimeEditorTextId.X, new TextEntry("X", "X") },
            { CampusRuntimeEditorTextId.Y, new TextEntry("Y", "Y") },
            { CampusRuntimeEditorTextId.R, new TextEntry("R", "R") },
            { CampusRuntimeEditorTextId.Prompt, new TextEntry("\u63d0\u793a", "Prompt") },
            { CampusRuntimeEditorTextId.ImportRoomTypesStatus, new TextEntry("\u5df2\u5bfc\u5165 {0} \u4e2a\u533a\u57df\u7c7b\u578b\u3002", "Imported {0} area types.") },
            { CampusRuntimeEditorTextId.ImportRoomTypesClipboardStatus, new TextEntry("\u5df2\u4ece\u526a\u8d34\u677f\u5bfc\u5165 {0} \u4e2a\u533a\u57df\u7c7b\u578b\u3002", "Imported {0} area types from clipboard.") },
            { CampusRuntimeEditorTextId.NoRoomTypesFoundToImport, new TextEntry("\u6ca1\u6709\u627e\u5230\u53ef\u5bfc\u5165\u7684\u533a\u57df\u7c7b\u578b\u3002", "No area types found to import.") },
            { CampusRuntimeEditorTextId.NoRoomTypesFoundInClipboard, new TextEntry("\u526a\u8d34\u677f\u4e2d\u6ca1\u6709\u533a\u57df\u7c7b\u578b\u3002", "No area types found in clipboard.") },
            { CampusRuntimeEditorTextId.SelectRoomDefinitionText, new TextEntry("\u9009\u62e9\u533a\u57df\u5b9a\u4e49\u6587\u672c", "Select area definition text") },
            { CampusRuntimeEditorTextId.GameTime, new TextEntry("\u6e38\u620f\u65f6\u95f4", "Game Time") },
            { CampusRuntimeEditorTextId.RealMinutesPerGameDay, new TextEntry("1x \u7ea6\u7b49\u4e8e\u6bcf\u4e2a\u6e38\u620f\u65e5 {0} \u5206\u949f\u73b0\u5b9e\u65f6\u95f4", "1x = about {0} real minutes per game day") },
            { CampusRuntimeEditorTextId.PreviewGrid, new TextEntry("\u9884\u89c8\u7f51\u683c", "Preview Grid") },
            { CampusRuntimeEditorTextId.UniformScale, new TextEntry("\u7b49\u6bd4\u7f29\u653e", "Uniform Scale") },
            { CampusRuntimeEditorTextId.RotationPreview, new TextEntry("\u56db\u5411\u9884\u89c8", "Rotation Preview") },
            { CampusRuntimeEditorTextId.DirectionalSprite, new TextEntry("\u56db\u5411\u8d34\u56fe", "Directional Sprite") },
            { CampusRuntimeEditorTextId.DefaultSprite, new TextEntry("\u9ed8\u8ba4\u8d34\u56fe", "Default Sprite") },
            { CampusRuntimeEditorTextId.MultiInteractionAnchors, new TextEntry("\u591a\u4ea4\u4e92\u951a\u70b9", "Multi Interaction Anchors") },
            { CampusRuntimeEditorTextId.AddAnchor, new TextEntry("\u65b0\u589e\u951a\u70b9", "Add Anchor") },
            { CampusRuntimeEditorTextId.RemoveAnchor, new TextEntry("\u5220\u9664\u951a\u70b9", "Remove Anchor") },
            { CampusRuntimeEditorTextId.AnchorId, new TextEntry("\u951a\u70b9ID", "Anchor ID") },
            { CampusRuntimeEditorTextId.ActionId, new TextEntry("\u4ea4\u4e92ID", "Action ID") },
            { CampusRuntimeEditorTextId.Payload, new TextEntry("\u8f7d\u8377", "Payload") },
            { CampusRuntimeEditorTextId.Enabled, new TextEntry("\u542f\u7528", "Enabled") },
            { CampusRuntimeEditorTextId.ClickPreviewToPlaceAnchor, new TextEntry("\u5728\u9884\u89c8\u56fe\u4e0a\u70b9\u51fb\u53ef\u76f4\u63a5\u8bbe\u7f6e\u9009\u4e2d\u951a\u70b9\u5750\u6807", "Click the preview to place the selected anchor") },
            { CampusRuntimeEditorTextId.AnchorList, new TextEntry("\u951a\u70b9\u5217\u8868", "Anchor List") },
            { CampusRuntimeEditorTextId.SelectedAnchor, new TextEntry("\u5f53\u524d\u951a\u70b9", "Selected Anchor") },
            { CampusRuntimeEditorTextId.RetailShelfSection, new TextEntry("\u96f6\u552e\u8d27\u67b6", "Retail Shelf") },
            { CampusRuntimeEditorTextId.RetailShelfMode, new TextEntry("\u8d27\u67b6\u6a21\u5f0f", "Shelf Mode") },
            { CampusRuntimeEditorTextId.RetailItemDefinitionId, new TextEntry("\u5546\u54c1ID", "Item Definition ID") },
            { CampusRuntimeEditorTextId.RetailAutoRestock, new TextEntry("\u81ea\u52a8\u8865\u8d27", "Auto Restock") },
            { CampusRuntimeEditorTextId.RetailStockCount, new TextEntry("\u5bb9\u5668\u5e93\u5b58", "Container Stock") },
            { CampusRuntimeEditorTextId.RetailDisplayCount, new TextEntry("\u76f4\u6446\u6570\u91cf", "Display Count") },
            { CampusRuntimeEditorTextId.RetailContainerMode, new TextEntry("\u5bb9\u5668\u8d27\u67b6", "Container Shelf") },
            { CampusRuntimeEditorTextId.RetailDisplayMode, new TextEntry("\u76f4\u6446\u8d27\u67b6", "Direct Pickup Display") },
            { CampusRuntimeEditorTextId.InteractionPresetEid, new TextEntry("\u4ea4\u4e92EID", "Interaction EID") },
            { CampusRuntimeEditorTextId.StudentDeskOwnerStatus, new TextEntry("\u8bfe\u684c\u6240\u5c5e\u5b66\u751f\uff1a{0}", "Student desk owner: {0}") },
            { CampusRuntimeEditorTextId.StudentDeskOwnerUnassignedStatus, new TextEntry("\u8bfe\u684c\u5c1a\u672a\u5206\u914d\u5b66\u751f\u3002", "This student desk is not assigned to a student.") }
        };

        public static string Get(CampusDisplayLanguage language, CampusRuntimeEditorTextId id)
        {
            TextEntry entry = Entries.TryGetValue(id, out TextEntry resolved)
                ? resolved
                : new TextEntry(id.ToString(), id.ToString());

            return entry.Get(language);
        }

        public static string Get(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return Resolve(language, chinese, english, traditionalChinese, russian, japanese);
        }

        private static string Resolve(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english, traditionalChinese, russian, japanese);
        }

        public static string Format(CampusDisplayLanguage language, CampusRuntimeEditorTextId id, params object[] args)
        {
            return string.Format(Get(language, id), args);
        }

        public static string Format(CampusDisplayLanguage language, string chinese, string english, params object[] args)
        {
            return string.Format(Get(language, chinese, english), args);
        }

        public static string FormatFloorButton(CampusDisplayLanguage language, int floorIndex, bool isUnlocked)
        {
            string floor = Get(language, CampusRuntimeEditorTextId.Floor);
            if (isUnlocked)
            {
                return floor + " " + floorIndex;
            }

            return floor + " " + floorIndex + "  " + Get(language, CampusRuntimeEditorTextId.Locked);
        }

        public static string FormatActiveDropTarget(CampusDisplayLanguage language, string label)
        {
            return Get(language, CampusRuntimeEditorTextId.ActiveDropTarget) + ": " + label;
        }

        public static string FormatMapSource(CampusDisplayLanguage language, string source)
        {
            return Get(language, CampusRuntimeEditorTextId.MapSource) + ": " + source;
        }

        public static string FormatPointLightStats(CampusDisplayLanguage language, string intensity, string outer, string inner)
        {
            string intensityLabel = Resolve(language, "\u5f3a\u5ea6", "Intensity", "\u5f37\u5ea6", "\u0418\u043d\u0442\u0435\u043d\u0441\u0438\u0432\u043d\u043e\u0441\u0442\u044c", "\u5f37\u5ea6");
            string outerLabel = Resolve(language, "\u5916\u534a\u5f84", "Outer", "\u5916\u534a\u5f91", "\u0412\u043d\u0435\u0448\u043d\u0438\u0439", "\u5916\u5074");
            string innerLabel = Resolve(language, "\u5185\u534a\u5f84", "Inner", "\u5167\u534a\u5f91", "\u0412\u043d\u0443\u0442\u0440\u0435\u043d\u043d\u0438\u0439", "\u5185\u5074");
            return intensityLabel + " " + intensity + " / " + outerLabel + " " + outer + " / " + innerLabel + " " + inner;
        }

        public static string FormatGameTime(CampusDisplayLanguage language, string time)
        {
            return Get(language, CampusRuntimeEditorTextId.GameTime) + " " + time;
        }
    }
}


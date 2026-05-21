using System.Collections.Generic;

namespace NtingCampus.Gameplay.UI
{
    public enum CampusPlayerUiTextId
    {
        StartupTitle = 0,
        StartupDescription = 1,
        Language = 2,
        Chinese = 3,
        English = 4,
        Bilingual = 5,
        Map = 6,
        Save = 7,
        Refresh = 8,
        StartGame = 9,
        StartHint = 10,
        SceneDefault = 11,
        NoSave = 12,
        AutoSave = 13,
        AuthoringPackage = 14,
        NoLaunchOptions = 15,
        Loading = 16,
        LoadingGameplayScene = 17,
        LoadingFailed = 18,
        PreparingSceneTransition = 19,
        SettingsTitle = 20,
        SettingsDescription = 21,
        Continue = 22,
        ReturnToMainMenu = 23,
        CreateNewMap = 24,
        NewMapName = 25,
        EnterNewMapName = 26,
        MapAlreadyExists = 27,
        GameplayOverlayMissing = 28,
        GameplayOverlayApplied = 29,
        StartupLoadFailed = 30,
        StartupSelectionApplied = 31
    }

    public static class CampusPlayerUiTextCatalog
    {
        private readonly struct TextEntry
        {
            public TextEntry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusPlayerUiTextId, TextEntry> Entries = new()
        {
            { CampusPlayerUiTextId.StartupTitle, new TextEntry("开始", "Startup") },
            { CampusPlayerUiTextId.StartupDescription, new TextEntry("选择地图基线和可选存档，然后点击右下角的开始游戏。", "Choose a map baseline and an optional save snapshot, then click Start Game in the lower-right corner.") },
            { CampusPlayerUiTextId.Language, new TextEntry("语言", "Language") },
            { CampusPlayerUiTextId.Chinese, new TextEntry("简体中文", "Chinese") },
            { CampusPlayerUiTextId.English, new TextEntry("English", "English") },
            { CampusPlayerUiTextId.Bilingual, new TextEntry("双语", "Bilingual") },
            { CampusPlayerUiTextId.Map, new TextEntry("地图", "Map") },
            { CampusPlayerUiTextId.Save, new TextEntry("存档", "Save") },
            { CampusPlayerUiTextId.Refresh, new TextEntry("刷新", "Refresh") },
            { CampusPlayerUiTextId.StartGame, new TextEntry("开始游戏", "Start Game") },
            { CampusPlayerUiTextId.StartHint, new TextEntry("确认选择后，点击右下角的开始游戏。", "Confirm your selections, then click Start Game in the lower-right.") },
            { CampusPlayerUiTextId.SceneDefault, new TextEntry("场景默认", "Scene Default") },
            { CampusPlayerUiTextId.NoSave, new TextEntry("不加载存档", "No Save") },
            { CampusPlayerUiTextId.AutoSave, new TextEntry("自动存档", "Auto Save") },
            { CampusPlayerUiTextId.AuthoringPackage, new TextEntry("作者包", "Authoring Package") },
            { CampusPlayerUiTextId.NoLaunchOptions, new TextEntry("没有找到可用的启动选项。", "No launch options were found.") },
            { CampusPlayerUiTextId.Loading, new TextEntry("加载中", "Loading") },
            { CampusPlayerUiTextId.LoadingGameplayScene, new TextEntry("正在加载游戏场景...", "Loading gameplay scene...") },
            { CampusPlayerUiTextId.LoadingFailed, new TextEntry("开始加载场景失败。", "Failed to start scene loading.") },
            { CampusPlayerUiTextId.PreparingSceneTransition, new TextEntry("正在准备场景切换...", "Preparing scene transition...") },
            { CampusPlayerUiTextId.SettingsTitle, new TextEntry("设置", "Settings") },
            { CampusPlayerUiTextId.SettingsDescription, new TextEntry("在这里调整当前游戏的显示语言，修改会立即生效。", "Adjust the current display language here. Changes apply immediately.") },
            { CampusPlayerUiTextId.Continue, new TextEntry("继续游戏", "Continue") },
            { CampusPlayerUiTextId.ReturnToMainMenu, new TextEntry("返回主菜单", "Return To Main Menu") },
            { CampusPlayerUiTextId.CreateNewMap, new TextEntry("新建地图", "Create New Map") },
            { CampusPlayerUiTextId.NewMapName, new TextEntry("新地图名称", "New Map Name") },
            { CampusPlayerUiTextId.EnterNewMapName, new TextEntry("请输入新地图名称。", "Enter a new map name.") },
            { CampusPlayerUiTextId.MapAlreadyExists, new TextEntry("地图已存在：{0}", "Map already exists: {0}") },
            { CampusPlayerUiTextId.GameplayOverlayMissing, new TextEntry("[系统] 没有找到 {0} 的玩法覆盖数据。", "[System] No gameplay overlay found for {0}.") },
            { CampusPlayerUiTextId.GameplayOverlayApplied, new TextEntry("[系统] 已应用玩法覆盖数据：{0}。角色 {1}，设施 {2}。", "[System] Gameplay overlay applied: {0}. Actors={1}, Facilities={2}.") },
            { CampusPlayerUiTextId.StartupLoadFailed, new TextEntry("[系统] 启动加载失败：{0}", "[System] Startup load failed: {0}") },
            { CampusPlayerUiTextId.StartupSelectionApplied, new TextEntry("[系统] 启动选择已应用。地图 {0}，存档 {1}。", "[System] Startup selection applied. Map={0}, Save={1}.") }
        };

        public static string Get(CampusPlayerUiTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusPlayerUiTextId id)
        {
            TextEntry entry = Entries.TryGetValue(id, out TextEntry resolved)
                ? resolved
                : new TextEntry(id.ToString(), id.ToString());

            return language switch
            {
                CampusDisplayLanguage.Chinese => entry.Chinese,
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }

        public static string Format(CampusPlayerUiTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}

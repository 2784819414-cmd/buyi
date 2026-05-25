using System.Collections.Generic;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public enum CampusPlayerUiTextId
    {
        StartupTitle = 0,
        StartupDescription = 1,
        Language = 2,
        Chinese = 3,
        English = 4,
        TraditionalChinese = 80,
        Russian = 81,
        Japanese = 82,
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
        StartupSelectionApplied = 31,
        TimeTestTitle = 32,
        TimeTestDescription = 33,
        TimeTestDate = 34,
        TimeTestHour = 35,
        TimeTestMinute = 36,
        TimeTestApply = 37,
        TimeTestReset = 38,
        TimeTestInvalidDate = 39,
        TimeTestInvalidHour = 40,
        TimeTestInvalidMinute = 41,
        TimeControlTitle = 42,
        TimeControlDescription = 43,
        TimePause = 44,
        TimeResume = 45,
        TimePauseStatus = 46,
        TimeRunningStatus = 47,
        KeyBindingTitle = 48,
        KeyBindingDescription = 49,
        KeyBindingWaiting = 50,
        KeyBindingConflict = 51,
        KeyBindingReset = 52,
        KeyBindingResetAll = 53,
        KeyBindingApplied = 54,
        InputMoveUpPrimary = 55,
        InputMoveDownPrimary = 56,
        InputMoveLeftPrimary = 57,
        InputMoveRightPrimary = 58,
        InputMoveUpSecondary = 59,
        InputMoveDownSecondary = 60,
        InputMoveLeftSecondary = 61,
        InputMoveRightSecondary = 62,
        InputInteract = 63,
        InputSprint = 64,
        InputBackpack = 65,
        InputSettings = 66,
        KeyBindingListening = 67,
        InputToggleMode = 68,
        InputTimePause = 69,
        InputTimeNormalSpeed = 70,
        InputTimeFastSpeed = 71,
        InputTimeMaxSpeed = 72,
        EscMenuTitle = 73,
        EscMenuDescription = 74,
        AdjustTime = 75,
        TimeSettingsDescription = 76,
        BackToEscMenu = 77,
        About = 78,
        AboutDescription = 79
    }

    public static class CampusPlayerUiTextCatalog
    {
        private static readonly Dictionary<CampusPlayerUiTextId, CampusLocalizedTextEntry> Entries = new()
        {
            { CampusPlayerUiTextId.StartupTitle, new CampusLocalizedTextEntry("开始", "Startup", "開始", "Запуск", "スタート") },
            { CampusPlayerUiTextId.StartupDescription, new CampusLocalizedTextEntry("选择地图基线和可选存档，然后点击右下角的开始游戏。", "Choose a map baseline and an optional save snapshot, then click Start Game in the lower-right corner.", "選擇地圖基線和可選存檔，然後點擊右下角的開始遊戲。", "Выберите базовую карту и необязательное сохранение, затем нажмите «Начать игру» в правом нижнем углу.", "マップの基準と任意のセーブを選び、右下の「ゲーム開始」を押してください。") },
            { CampusPlayerUiTextId.Language, new CampusLocalizedTextEntry("语言", "Language", "語言", "Язык", "言語") },
            { CampusPlayerUiTextId.Chinese, new CampusLocalizedTextEntry("简体中文", "Simplified Chinese", "簡體中文", "Упрощённый китайский", "簡体字中国語") },
            { CampusPlayerUiTextId.English, new CampusLocalizedTextEntry("英文", "English", "英文", "Английский", "英語") },
            { CampusPlayerUiTextId.TraditionalChinese, new CampusLocalizedTextEntry("繁体中文", "Traditional Chinese", "繁體中文", "Традиционный китайский", "繁体字中国語") },
            { CampusPlayerUiTextId.Russian, new CampusLocalizedTextEntry("俄语", "Russian", "俄語", "Русский", "ロシア語") },
            { CampusPlayerUiTextId.Japanese, new CampusLocalizedTextEntry("日语", "Japanese", "日語", "Японский", "日本語") },
            { CampusPlayerUiTextId.Map, new CampusLocalizedTextEntry("地图", "Map", "地圖", "Карта", "マップ") },
            { CampusPlayerUiTextId.Save, new CampusLocalizedTextEntry("存档", "Save", "存檔", "Сохранение", "セーブ") },
            { CampusPlayerUiTextId.Refresh, new CampusLocalizedTextEntry("刷新", "Refresh", "重新整理", "Обновить", "更新") },
            { CampusPlayerUiTextId.StartGame, new CampusLocalizedTextEntry("开始游戏", "Start Game", "開始遊戲", "Начать игру", "ゲーム開始") },
            { CampusPlayerUiTextId.StartHint, new CampusLocalizedTextEntry("确认选择后，点击右下角的开始游戏。", "Confirm your selections, then click Start Game in the lower-right.", "確認選擇後，點擊右下角的開始遊戲。", "Подтвердите выбор и нажмите «Начать игру» в правом нижнем углу.", "選択を確認し、右下の「ゲーム開始」を押してください。") },
            { CampusPlayerUiTextId.SceneDefault, new CampusLocalizedTextEntry("场景默认", "Scene Default", "場景預設", "Сцена по умолчанию", "シーン既定") },
            { CampusPlayerUiTextId.NoSave, new CampusLocalizedTextEntry("不加载存档", "No Save", "不載入存檔", "Без сохранения", "セーブなし") },
            { CampusPlayerUiTextId.AutoSave, new CampusLocalizedTextEntry("自动存档", "Auto Save", "自動存檔", "Автосохранение", "オートセーブ") },
            { CampusPlayerUiTextId.AuthoringPackage, new CampusLocalizedTextEntry("作者包", "Authoring Package", "作者包", "Пакет автора", "作者パッケージ") },
            { CampusPlayerUiTextId.NoLaunchOptions, new CampusLocalizedTextEntry("没有找到可用的启动选项。", "No launch options were found.", "沒有找到可用的啟動選項。", "Доступные варианты запуска не найдены.", "利用できる起動オプションがありません。") },
            { CampusPlayerUiTextId.Loading, new CampusLocalizedTextEntry("加载中", "Loading", "載入中", "Загрузка", "読み込み中") },
            { CampusPlayerUiTextId.LoadingGameplayScene, new CampusLocalizedTextEntry("正在加载游戏场景...", "Loading gameplay scene...", "正在載入遊戲場景...", "Загрузка игровой сцены...", "ゲームシーンを読み込み中...") },
            { CampusPlayerUiTextId.LoadingFailed, new CampusLocalizedTextEntry("开始加载场景失败。", "Failed to start scene loading.", "開始載入場景失敗。", "Не удалось начать загрузку сцены.", "シーン読み込みを開始できませんでした。") },
            { CampusPlayerUiTextId.PreparingSceneTransition, new CampusLocalizedTextEntry("正在准备场景切换...", "Preparing scene transition...", "正在準備場景切換...", "Подготовка перехода сцены...", "シーン切り替えを準備中...") },
            { CampusPlayerUiTextId.SettingsTitle, new CampusLocalizedTextEntry("设置", "Settings", "設定", "Настройки", "設定") },
            { CampusPlayerUiTextId.SettingsDescription, new CampusLocalizedTextEntry("在这里调整当前游戏的显示语言，修改会立即生效。", "Adjust the current display language here. Changes apply immediately.", "在這裡調整目前遊戲的顯示語言，修改會立即生效。", "Здесь можно изменить язык отображения. Изменения применяются сразу.", "ここで表示言語を変更できます。変更はすぐに反映されます。") },
            { CampusPlayerUiTextId.Continue, new CampusLocalizedTextEntry("继续游戏", "Continue", "繼續遊戲", "Продолжить", "続ける") },
            { CampusPlayerUiTextId.ReturnToMainMenu, new CampusLocalizedTextEntry("返回主菜单", "Return To Main Menu", "返回主選單", "В главное меню", "メインメニューへ戻る") },
            { CampusPlayerUiTextId.CreateNewMap, new CampusLocalizedTextEntry("新建地图", "Create New Map", "新增地圖", "Создать карту", "新規マップ作成") },
            { CampusPlayerUiTextId.NewMapName, new CampusLocalizedTextEntry("新地图名称", "New Map Name", "新地圖名稱", "Название новой карты", "新しいマップ名") },
            { CampusPlayerUiTextId.EnterNewMapName, new CampusLocalizedTextEntry("请输入新地图名称。", "Enter a new map name.", "請輸入新地圖名稱。", "Введите название новой карты.", "新しいマップ名を入力してください。") },
            { CampusPlayerUiTextId.MapAlreadyExists, new CampusLocalizedTextEntry("地图已存在：{0}", "Map already exists: {0}", "地圖已存在：{0}", "Карта уже существует: {0}", "マップは既に存在します: {0}") },
            { CampusPlayerUiTextId.GameplayOverlayMissing, new CampusLocalizedTextEntry("[系统] 没有找到 {0} 的玩法覆盖数据。", "[System] No gameplay overlay found for {0}.", "[系統] 沒有找到 {0} 的玩法覆蓋資料。", "[Система] Данные игрового слоя для {0} не найдены.", "[システム] {0} のゲームプレイ上書きデータが見つかりません。") },
            { CampusPlayerUiTextId.GameplayOverlayApplied, new CampusLocalizedTextEntry("[系统] 已应用玩法覆盖数据：{0}。角色={1}，设施={2}。", "[System] Gameplay overlay applied: {0}. Actors={1}, Facilities={2}.", "[系統] 已套用玩法覆蓋資料：{0}。角色={1}，設施={2}。", "[Система] Игровой слой применён: {0}. Персонажи={1}, Объекты={2}.", "[システム] ゲームプレイ上書きデータを適用しました: {0}。キャラクター={1}、施設={2}。") },
            { CampusPlayerUiTextId.StartupLoadFailed, new CampusLocalizedTextEntry("[系统] 启动加载失败：{0}", "[System] Startup load failed: {0}", "[系統] 啟動載入失敗：{0}", "[Система] Ошибка запуска: {0}", "[システム] 起動読み込みに失敗しました: {0}") },
            { CampusPlayerUiTextId.StartupSelectionApplied, new CampusLocalizedTextEntry("[系统] 启动选择已应用。地图={0}，存档={1}。", "[System] Startup selection applied. Map={0}, Save={1}.", "[系統] 啟動選擇已套用。地圖={0}，存檔={1}。", "[Система] Выбор запуска применён. Карта={0}, Сохранение={1}.", "[システム] 起動設定を適用しました。マップ={0}、セーブ={1}。") },
            { CampusPlayerUiTextId.TimeTestTitle, new CampusLocalizedTextEntry("测试时间", "Test Time", "測試時間", "Тест времени", "時間テスト") },
            { CampusPlayerUiTextId.TimeTestDescription, new CampusLocalizedTextEntry("直接设置游戏日期和时钟，用于测试日程、光照和玩法切换。", "Set the in-game date and clock directly for schedule, lighting, and gameplay testing.", "直接設定遊戲日期和時鐘，用於測試日程、光照和玩法切換。", "Установите игровую дату и время для проверки расписаний, света и игровых переключений.", "ゲーム内の日付と時刻を直接設定し、予定、照明、ゲーム処理をテストします。") },
            { CampusPlayerUiTextId.TimeTestDate, new CampusLocalizedTextEntry("日期（年 / 月 / 日）", "Date (YYYY / MM / DD)", "日期（年 / 月 / 日）", "Дата (ГГГГ / ММ / ДД)", "日付（年 / 月 / 日）") },
            { CampusPlayerUiTextId.TimeTestHour, new CampusLocalizedTextEntry("小时（HH）", "Hour (HH)", "小時（HH）", "Час (HH)", "時（HH）") },
            { CampusPlayerUiTextId.TimeTestMinute, new CampusLocalizedTextEntry("分钟（MM）", "Minute (MM)", "分鐘（MM）", "Минута (MM)", "分（MM）") },
            { CampusPlayerUiTextId.TimeTestApply, new CampusLocalizedTextEntry("应用测试时间", "Apply Test Time", "套用測試時間", "Применить время", "テスト時間を適用") },
            { CampusPlayerUiTextId.TimeTestReset, new CampusLocalizedTextEntry("读取当前时间", "Use Current Time", "讀取目前時間", "Взять текущее время", "現在時刻を使用") },
            { CampusPlayerUiTextId.TimeTestInvalidDate, new CampusLocalizedTextEntry("日期无效，请输入真实存在的年月日。", "Invalid date. Enter a real calendar year, month, and day.", "日期無效，請輸入真實存在的年月日。", "Недопустимая дата. Введите реальный год, месяц и день.", "日付が無効です。実在する年月日を入力してください。") },
            { CampusPlayerUiTextId.TimeTestInvalidHour, new CampusLocalizedTextEntry("小时无效，请输入 0 到 23。", "Invalid hour. Enter a value from 0 to 23.", "小時無效，請輸入 0 到 23。", "Недопустимый час. Введите значение от 0 до 23.", "時が無効です。0 から 23 の値を入力してください。") },
            { CampusPlayerUiTextId.TimeTestInvalidMinute, new CampusLocalizedTextEntry("分钟无效，请输入 0 到 59。", "Invalid minute. Enter a value from 0 to 59.", "分鐘無效，請輸入 0 到 59。", "Недопустимая минута. Введите значение от 0 до 59.", "分が無効です。0 から 59 の値を入力してください。") },
            { CampusPlayerUiTextId.TimeControlTitle, new CampusLocalizedTextEntry("时间控制", "Time Control", "時間控制", "Управление временем", "時間操作") },
            { CampusPlayerUiTextId.TimeControlDescription, new CampusLocalizedTextEntry("暂停会冻结游戏时间；恢复后继续按当前速度流动。", "Pause freezes in-game time. Resume continues with the current active speed.", "暫停會凍結遊戲時間；恢復後繼續按目前速度流動。", "Пауза замораживает игровое время; после возобновления оно идёт с текущей скоростью.", "一時停止でゲーム内時間を止め、再開後は現在の速度で進みます。") },
            { CampusPlayerUiTextId.TimePause, new CampusLocalizedTextEntry("暂停时间", "Pause Time", "暫停時間", "Остановить время", "時間停止") },
            { CampusPlayerUiTextId.TimeResume, new CampusLocalizedTextEntry("恢复时间", "Resume Time", "恢復時間", "Возобновить время", "時間再開") },
            { CampusPlayerUiTextId.TimePauseStatus, new CampusLocalizedTextEntry("当前状态：已暂停", "Current State: Paused", "目前狀態：已暫停", "Текущее состояние: пауза", "現在の状態：停止中") },
            { CampusPlayerUiTextId.TimeRunningStatus, new CampusLocalizedTextEntry("当前状态：运行中", "Current State: Running", "目前狀態：執行中", "Текущее состояние: идёт", "現在の状態：進行中") },
            { CampusPlayerUiTextId.KeyBindingTitle, new CampusLocalizedTextEntry("键位设置", "Key Bindings", "鍵位設定", "Назначение клавиш", "キー設定") },
            { CampusPlayerUiTextId.KeyBindingDescription, new CampusLocalizedTextEntry("点击按键按钮后按下新的键位。键位会立即保存。", "Click a key button, then press the new key. Changes are saved immediately.", "點擊按鍵按鈕後按下新的鍵位。鍵位會立即保存。", "Нажмите кнопку клавиши, затем новую клавишу. Изменения сохраняются сразу.", "キーのボタンを押してから新しいキーを押してください。変更はすぐ保存されます。") },
            { CampusPlayerUiTextId.KeyBindingWaiting, new CampusLocalizedTextEntry("请按下新的键位：{0}", "Press a new key for {0}.", "請按下新的鍵位：{0}", "Нажмите новую клавишу для {0}.", "{0} の新しいキーを押してください。") },
            { CampusPlayerUiTextId.KeyBindingConflict, new CampusLocalizedTextEntry("{0} 已被 {1} 使用。", "{0} is already used by {1}.", "{0} 已被 {1} 使用。", "{0} уже используется для {1}.", "{0} は既に {1} で使用されています。") },
            { CampusPlayerUiTextId.KeyBindingReset, new CampusLocalizedTextEntry("重置", "Reset", "重設", "Сброс", "リセット") },
            { CampusPlayerUiTextId.KeyBindingResetAll, new CampusLocalizedTextEntry("重置全部键位", "Reset All Keys", "重設全部鍵位", "Сбросить все клавиши", "すべてのキーをリセット") },
            { CampusPlayerUiTextId.KeyBindingApplied, new CampusLocalizedTextEntry("已设置：{0} = {1}", "Set {0} = {1}", "已設定：{0} = {1}", "Назначено: {0} = {1}", "設定しました：{0} = {1}") },
            { CampusPlayerUiTextId.InputMoveUpPrimary, new CampusLocalizedTextEntry("上移（主）", "Move Up (Primary)", "上移（主）", "Вверх (основная)", "上へ移動（メイン）") },
            { CampusPlayerUiTextId.InputMoveDownPrimary, new CampusLocalizedTextEntry("下移（主）", "Move Down (Primary)", "下移（主）", "Вниз (основная)", "下へ移動（メイン）") },
            { CampusPlayerUiTextId.InputMoveLeftPrimary, new CampusLocalizedTextEntry("左移（主）", "Move Left (Primary)", "左移（主）", "Влево (основная)", "左へ移動（メイン）") },
            { CampusPlayerUiTextId.InputMoveRightPrimary, new CampusLocalizedTextEntry("右移（主）", "Move Right (Primary)", "右移（主）", "Вправо (основная)", "右へ移動（メイン）") },
            { CampusPlayerUiTextId.InputMoveUpSecondary, new CampusLocalizedTextEntry("上移（备用）", "Move Up (Secondary)", "上移（備用）", "Вверх (запасная)", "上へ移動（予備）") },
            { CampusPlayerUiTextId.InputMoveDownSecondary, new CampusLocalizedTextEntry("下移（备用）", "Move Down (Secondary)", "下移（備用）", "Вниз (запасная)", "下へ移動（予備）") },
            { CampusPlayerUiTextId.InputMoveLeftSecondary, new CampusLocalizedTextEntry("左移（备用）", "Move Left (Secondary)", "左移（備用）", "Влево (запасная)", "左へ移動（予備）") },
            { CampusPlayerUiTextId.InputMoveRightSecondary, new CampusLocalizedTextEntry("右移（备用）", "Move Right (Secondary)", "右移（備用）", "Вправо (запасная)", "右へ移動（予備）") },
            { CampusPlayerUiTextId.InputInteract, new CampusLocalizedTextEntry("互动", "Interact", "互動", "Взаимодействие", "インタラクト") },
            { CampusPlayerUiTextId.InputSprint, new CampusLocalizedTextEntry("奔跑", "Sprint", "奔跑", "Бег", "走る") },
            { CampusPlayerUiTextId.InputBackpack, new CampusLocalizedTextEntry("背包", "Backpack", "背包", "Рюкзак", "バックパック") },
            { CampusPlayerUiTextId.InputSettings, new CampusLocalizedTextEntry("设置面板", "Settings Panel", "設定面板", "Панель настроек", "設定パネル") },
            { CampusPlayerUiTextId.KeyBindingListening, new CampusLocalizedTextEntry("等待按键", "Listening", "等待按鍵", "Ожидание клавиши", "キー入力待ち") },
            { CampusPlayerUiTextId.InputToggleMode, new CampusLocalizedTextEntry("切换模式", "Toggle Mode", "切換模式", "Переключить режим", "モード切替") },
            { CampusPlayerUiTextId.InputTimePause, new CampusLocalizedTextEntry("暂停/恢复时间", "Pause/Resume Time", "暫停/恢復時間", "Пауза/возобновление времени", "時間停止/再開") },
            { CampusPlayerUiTextId.InputTimeNormalSpeed, new CampusLocalizedTextEntry("正常速度", "Normal Speed", "正常速度", "Обычная скорость", "通常速度") },
            { CampusPlayerUiTextId.InputTimeFastSpeed, new CampusLocalizedTextEntry("快速速度", "Fast Speed", "快速速度", "Быстрая скорость", "高速") },
            { CampusPlayerUiTextId.InputTimeMaxSpeed, new CampusLocalizedTextEntry("最高速度", "Max Speed", "最高速度", "Максимальная скорость", "最高速") },
            { CampusPlayerUiTextId.EscMenuTitle, new CampusLocalizedTextEntry("暂停菜单", "Pause Menu", "暫停選單", "Меню паузы", "ポーズメニュー") },
            { CampusPlayerUiTextId.EscMenuDescription, new CampusLocalizedTextEntry("选择继续游戏、进入设置、调整时间或返回主菜单。", "Continue, open settings, adjust time, or return to the main menu.", "選擇繼續遊戲、進入設定、調整時間或返回主選單。", "Продолжите игру, откройте настройки, измените время или вернитесь в главное меню.", "ゲーム続行、設定、時間調整、メインメニューへの移動を選びます。") },
            { CampusPlayerUiTextId.AdjustTime, new CampusLocalizedTextEntry("调整时间", "Adjust Time", "調整時間", "Настроить время", "時間調整") },
            { CampusPlayerUiTextId.TimeSettingsDescription, new CampusLocalizedTextEntry("单独调整时间流动和测试日期。", "Adjust time flow and test date in their own panels.", "單獨調整時間流動和測試日期。", "Настройте ход времени и тестовую дату на отдельных панелях.", "時間の流れとテスト日付を個別のパネルで調整します。") },
            { CampusPlayerUiTextId.BackToEscMenu, new CampusLocalizedTextEntry("返回上一级", "Back", "返回上一級", "Назад", "戻る") },
            { CampusPlayerUiTextId.About, new CampusLocalizedTextEntry("关于", "About", "關於", "О проекте", "情報") },
            { CampusPlayerUiTextId.AboutDescription, new CampusLocalizedTextEntry("不义校园是一个开放源码、面向模组的校园生活模拟项目。当前版本优先打通角色、地图、物品、时间和基础校园生态。", "Unrighteous Campus is an open-source, mod-facing campus life simulation project. This version focuses on characters, maps, items, time, and the basic campus ecology.", "不義校園是一個開放原始碼、面向模組的校園生活模擬專案。目前版本優先打通角色、地圖、物品、時間和基礎校園生態。", "Unrighteous Campus — проект симуляции школьной жизни с открытым кодом и поддержкой модов. В этой версии основной упор на персонажей, карты, предметы, время и базовую школьную экосистему.", "Unrighteous Campus は、オープンソースでMod向けの学園生活シミュレーションです。現バージョンではキャラクター、マップ、アイテム、時間、基礎的な学園生態を優先して整備しています。") }
        };

        public static string Get(CampusPlayerUiTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusPlayerUiTextId id)
        {
            if (id == CampusPlayerUiTextId.BackToEscMenu)
            {
                return CampusDisplayLanguageCatalog.Resolve(
                    language,
                    "\u8fd4\u56de",
                    "Back",
                    "\u8fd4\u56de",
                    "\u041d\u0430\u0437\u0430\u0434",
                    "\u623b\u308b");
            }

            CampusLocalizedTextEntry entry = Entries.TryGetValue(id, out CampusLocalizedTextEntry resolved)
                ? resolved
                : new CampusLocalizedTextEntry(id.ToString(), id.ToString());

            return entry.Get(language);
        }

        public static string Format(CampusPlayerUiTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static CampusPlayerUiTextId GetLanguageNameTextId(CampusDisplayLanguage language)
        {
            switch (CampusDisplayLanguageCatalog.Normalize(language))
            {
                case CampusDisplayLanguage.English:
                    return CampusPlayerUiTextId.English;
                case CampusDisplayLanguage.TraditionalChinese:
                    return CampusPlayerUiTextId.TraditionalChinese;
                case CampusDisplayLanguage.Russian:
                    return CampusPlayerUiTextId.Russian;
                case CampusDisplayLanguage.Japanese:
                    return CampusPlayerUiTextId.Japanese;
                default:
                    return CampusPlayerUiTextId.Chinese;
            }
        }
    }
}


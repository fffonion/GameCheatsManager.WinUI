namespace GameCheatsManager.WinUI.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.Ordinal)
    {
        ["Game Cheats Manager"] = new() { ["zh_CN"] = "游戏修改器管理器", ["zh_TW"] = "遊戲修改器管理器", ["de_DE"] = "Game Cheats Manager" },
        ["Options"] = new() { ["zh_CN"] = "选项", ["zh_TW"] = "選項", ["de_DE"] = "Optionen" },
        ["Settings"] = new() { ["zh_CN"] = "设置", ["zh_TW"] = "設定", ["de_DE"] = "Einstellungen" },
        ["Import Trainers"] = new() { ["zh_CN"] = "导入修改器", ["zh_TW"] = "匯入修改器", ["de_DE"] = "Trainer importieren" },
        ["Open Trainer Download Path"] = new() { ["zh_CN"] = "打开修改器下载路径", ["zh_TW"] = "開啟修改器下載路徑", ["de_DE"] = "Trainer-Ordner öffnen" },
        ["Add Paths to Whitelist"] = new() { ["zh_CN"] = "添加路径到白名单", ["zh_TW"] = "新增路徑到白名單", ["de_DE"] = "Pfade zur Whitelist hinzufügen" },
        ["About"] = new() { ["zh_CN"] = "关于", ["zh_TW"] = "關於", ["de_DE"] = "Info" },
        ["Data Update"] = new() { ["zh_CN"] = "数据更新", ["zh_TW"] = "資料更新", ["de_DE"] = "Daten aktualisieren" },
        ["Update Translation Data"] = new() { ["zh_CN"] = "更新翻译数据", ["zh_TW"] = "更新翻譯資料", ["de_DE"] = "Übersetzungsdaten aktualisieren" },
        ["Update Trainer Search Data"] = new() { ["zh_CN"] = "更新搜索数据", ["zh_TW"] = "更新搜尋資料", ["de_DE"] = "Suchdaten aktualisieren" },
        ["Update Trainers"] = new() { ["zh_CN"] = "更新修改器", ["zh_TW"] = "更新修改器", ["de_DE"] = "Trainer aktualisieren" },
        ["Trainer Management"] = new() { ["zh_CN"] = "修改器管理", ["zh_TW"] = "修改器管理", ["de_DE"] = "Trainer-Verwaltung" },
        ["Open Trainer Management"] = new() { ["zh_CN"] = "打开修改器管理", ["zh_TW"] = "開啟修改器管理", ["de_DE"] = "Trainer-Verwaltung öffnen" },
        ["Upload Trainer"] = new() { ["zh_CN"] = "上传修改器", ["zh_TW"] = "上傳修改器", ["de_DE"] = "Trainer hochladen" },
        ["Open Upload Trainer"] = new() { ["zh_CN"] = "打开上传界面", ["zh_TW"] = "開啟上傳介面", ["de_DE"] = "Upload öffnen" },
        ["Browse All Trainers"] = new() { ["zh_CN"] = "浏览全部修改器", ["zh_TW"] = "瀏覽全部修改器", ["de_DE"] = "Alle Trainer durchsuchen" },
        ["Open Trainers Page"] = new() { ["zh_CN"] = "打开修改器页面", ["zh_TW"] = "開啟修改器頁面", ["de_DE"] = "Trainer-Seite öffnen" },
        ["Installed Trainers"] = new() { ["zh_CN"] = "已安装修改器", ["zh_TW"] = "已安裝修改器", ["de_DE"] = "Installierte Trainer" },
        ["Search for installed trainers"] = new() { ["zh_CN"] = "搜索已安装的修改器", ["zh_TW"] = "搜尋已安裝的修改器", ["de_DE"] = "Installierte Trainer suchen" },
        ["Launch"] = new() { ["zh_CN"] = "启动", ["zh_TW"] = "啟動", ["de_DE"] = "Starten" },
        ["Delete"] = new() { ["zh_CN"] = "删除", ["zh_TW"] = "刪除", ["de_DE"] = "Löschen" },
        ["Download Trainers"] = new() { ["zh_CN"] = "下载修改器", ["zh_TW"] = "下載修改器", ["de_DE"] = "Trainer herunterladen" },
        ["Enter keywords to download trainers"] = new() { ["zh_CN"] = "输入关键字下载修改器", ["zh_TW"] = "輸入關鍵字下載修改器", ["de_DE"] = "Suchbegriff zum Herunterladen eingeben" },
        ["Search"] = new() { ["zh_CN"] = "搜索", ["zh_TW"] = "搜尋", ["de_DE"] = "Suchen" },
        ["Idle"] = new() { ["zh_CN"] = "空闲", ["zh_TW"] = "閒置", ["de_DE"] = "Leerlauf" },
        ["Trainer download path:"] = new() { ["zh_CN"] = "修改器下载路径：", ["zh_TW"] = "修改器下載路徑：", ["de_DE"] = "Trainer-Speicherort:" },
        ["Browse"] = new() { ["zh_CN"] = "浏览", ["zh_TW"] = "瀏覽", ["de_DE"] = "Durchsuchen" },
        ["Activity"] = new() { ["zh_CN"] = "活动", ["zh_TW"] = "活動", ["de_DE"] = "Aktivität" },
        ["Warning"] = new() { ["zh_CN"] = "警告", ["zh_TW"] = "警告", ["de_DE"] = "Warnung" },
        ["This software is open source and provided free of charge. Resale is strictly prohibited."] = new() { ["zh_CN"] = "本软件开源且免费提供，严禁转售。", ["zh_TW"] = "本軟體開源且免費提供，嚴禁轉售。", ["de_DE"] = "Diese Software ist Open Source und kostenlos. Weiterverkauf ist verboten." },
        ["Don't show again"] = new() { ["zh_CN"] = "不再显示", ["zh_TW"] = "不再顯示", ["de_DE"] = "Nicht erneut anzeigen" },
        ["Announcement"] = new() { ["zh_CN"] = "公告", ["zh_TW"] = "公告", ["de_DE"] = "Ankündigung" },
        ["Theme"] = new() { ["zh_CN"] = "主题", ["zh_TW"] = "主題", ["de_DE"] = "Design" },
        ["Language"] = new() { ["zh_CN"] = "语言", ["zh_TW"] = "語言", ["de_DE"] = "Sprache" },
        ["Launch app on system startup"] = new() { ["zh_CN"] = "系统启动时自动运行", ["zh_TW"] = "系統啟動時自動執行", ["de_DE"] = "Beim Systemstart ausführen" },
        ["Check for software updates at startup"] = new() { ["zh_CN"] = "启动时检查软件更新", ["zh_TW"] = "啟動時檢查軟體更新", ["de_DE"] = "Beim Start nach Updates suchen" },
        ["Use safe launch path"] = new() { ["zh_CN"] = "使用安全启动路径", ["zh_TW"] = "使用安全啟動路徑", ["de_DE"] = "Sicheren Startpfad verwenden" },
        ["Always show search results in English"] = new() { ["zh_CN"] = "搜索结果始终显示英文", ["zh_TW"] = "搜尋結果始終顯示英文", ["de_DE"] = "Suchergebnisse immer auf Englisch anzeigen" },
        ["Sort search results by origin"] = new() { ["zh_CN"] = "按来源排序搜索结果", ["zh_TW"] = "按來源排序搜尋結果", ["de_DE"] = "Suchergebnisse nach Quelle sortieren" },
        ["Update trainer translations automatically"] = new() { ["zh_CN"] = "自动更新修改器翻译数据", ["zh_TW"] = "自動更新修改器翻譯資料", ["de_DE"] = "Trainer-Übersetzungen automatisch aktualisieren" },
        ["Apply"] = new() { ["zh_CN"] = "应用", ["zh_TW"] = "套用", ["de_DE"] = "Übernehmen" },
        ["Cancel"] = new() { ["zh_CN"] = "取消", ["zh_TW"] = "取消", ["de_DE"] = "Abbrechen" },
        ["Settings saved."] = new() { ["zh_CN"] = "设置已保存。", ["zh_TW"] = "設定已儲存。", ["de_DE"] = "Einstellungen gespeichert." },
        ["Theme and language changes are saved. Restart the app to fully apply them."] = new() { ["zh_CN"] = "主题和语言设置已保存，重启应用后可完全生效。", ["zh_TW"] = "主題與語言設定已儲存，重新啟動後可完全生效。", ["de_DE"] = "Design- und Spracheinstellungen wurden gespeichert. Bitte neu starten." },
        ["Current version:"] = new() { ["zh_CN"] = "当前版本：", ["zh_TW"] = "目前版本：", ["de_DE"] = "Aktuelle Version:" },
        ["Newest version:"] = new() { ["zh_CN"] = "最新版本：", ["zh_TW"] = "最新版本：", ["de_DE"] = "Neueste Version:" },
        ["Unavailable"] = new() { ["zh_CN"] = "不可用", ["zh_TW"] = "不可用", ["de_DE"] = "Nicht verfügbar" },
        ["Cheat Engine Required"] = new() { ["zh_CN"] = "需要 Cheat Engine", ["zh_TW"] = "需要 Cheat Engine", ["de_DE"] = "Cheat Engine erforderlich" },
        [".ct/.cetrainer files require Cheat Engine to run. Please install Cheat Engine and open these files with it."] = new() { ["zh_CN"] = ".ct/.cetrainer 文件需要使用 Cheat Engine 打开，请先安装 Cheat Engine。", ["zh_TW"] = ".ct/.cetrainer 檔案需要使用 Cheat Engine 開啟，請先安裝 Cheat Engine。", ["de_DE"] = ".ct/.cetrainer-Dateien benötigen Cheat Engine." },
        ["Delete Trainer"] = new() { ["zh_CN"] = "删除修改器", ["zh_TW"] = "刪除修改器", ["de_DE"] = "Trainer löschen" },
        ["Delete Original Trainers"] = new() { ["zh_CN"] = "删除原始修改器", ["zh_TW"] = "刪除原始修改器", ["de_DE"] = "Originaldateien löschen" },
        ["Do you want to delete the original trainer files?"] = new() { ["zh_CN"] = "是否删除原始修改器文件？", ["zh_TW"] = "是否刪除原始修改器檔案？", ["de_DE"] = "Originaldateien löschen?" },
        ["Keep"] = new() { ["zh_CN"] = "保留", ["zh_TW"] = "保留", ["de_DE"] = "Behalten" },
        ["Path"] = new() { ["zh_CN"] = "路径", ["zh_TW"] = "路徑", ["de_DE"] = "Pfad" },
        ["Please choose a new path."] = new() { ["zh_CN"] = "请选择一个新路径。", ["zh_TW"] = "請選擇新的路徑。", ["de_DE"] = "Bitte einen neuen Pfad wählen." },
        ["Migration complete."] = new() { ["zh_CN"] = "迁移完成。", ["zh_TW"] = "遷移完成。", ["de_DE"] = "Migration abgeschlossen." },
        ["Import"] = new() { ["zh_CN"] = "导入", ["zh_TW"] = "匯入", ["de_DE"] = "Import" },
        ["Trainer import complete."] = new() { ["zh_CN"] = "修改器导入完成。", ["zh_TW"] = "修改器匯入完成。", ["de_DE"] = "Trainer importiert." },
        ["Administrator Access Required"] = new() { ["zh_CN"] = "需要管理员权限", ["zh_TW"] = "需要管理員權限", ["de_DE"] = "Administratorrechte erforderlich" },
        ["Adding paths to the Windows Defender whitelist requires administrator rights. Continue?"] = new() { ["zh_CN"] = "将路径加入 Windows Defender 白名单需要管理员权限，是否继续？", ["zh_TW"] = "將路徑加入 Windows Defender 白名單需要管理員權限，是否繼續？", ["de_DE"] = "Administratorrechte zum Whitelisten erforderlich. Fortfahren?" },
        ["Continue"] = new() { ["zh_CN"] = "继续", ["zh_TW"] = "繼續", ["de_DE"] = "Fortfahren" },
        ["Whitelist"] = new() { ["zh_CN"] = "白名单", ["zh_TW"] = "白名單", ["de_DE"] = "Whitelist" },
        ["Paths added to Windows Defender whitelist."] = new() { ["zh_CN"] = "路径已加入 Windows Defender 白名单。", ["zh_TW"] = "路徑已加入 Windows Defender 白名單。", ["de_DE"] = "Pfade wurden hinzugefügt." },
        ["Failed to add paths to whitelist."] = new() { ["zh_CN"] = "添加白名单失败。", ["zh_TW"] = "加入白名單失敗。", ["de_DE"] = "Whitelist konnte nicht aktualisiert werden." },
        ["Translation Data"] = new() { ["zh_CN"] = "翻译数据", ["zh_TW"] = "翻譯資料", ["de_DE"] = "Übersetzungsdaten" },
        ["Translation data updated."] = new() { ["zh_CN"] = "翻译数据已更新。", ["zh_TW"] = "翻譯資料已更新。", ["de_DE"] = "Übersetzungsdaten aktualisiert." },
        ["Translation data update failed."] = new() { ["zh_CN"] = "翻译数据更新失败。", ["zh_TW"] = "翻譯資料更新失敗。", ["de_DE"] = "Übersetzungsdaten konnten nicht aktualisiert werden." },
        ["Search Data"] = new() { ["zh_CN"] = "搜索数据", ["zh_TW"] = "搜尋資料", ["de_DE"] = "Suchdaten" },
        ["Trainer search data updated."] = new() { ["zh_CN"] = "修改器搜索数据已更新。", ["zh_TW"] = "修改器搜尋資料已更新。", ["de_DE"] = "Suchdaten aktualisiert." },
        ["One or more data sources failed to update."] = new() { ["zh_CN"] = "一个或多个数据源更新失败。", ["zh_TW"] = "一個或多個資料來源更新失敗。", ["de_DE"] = "Mindestens eine Quelle konnte nicht aktualisiert werden." },
        ["Updates"] = new() { ["zh_CN"] = "更新", ["zh_TW"] = "更新", ["de_DE"] = "Updates" },
        ["No trainer updates found."] = new() { ["zh_CN"] = "未发现修改器更新。", ["zh_TW"] = "未發現修改器更新。", ["de_DE"] = "Keine Trainer-Updates gefunden." },
        ["Update Available"] = new() { ["zh_CN"] = "发现更新", ["zh_TW"] = "發現更新", ["de_DE"] = "Update verfügbar" },
        ["Open"] = new() { ["zh_CN"] = "打开", ["zh_TW"] = "開啟", ["de_DE"] = "Öffnen" },
        ["Later"] = new() { ["zh_CN"] = "稍后", ["zh_TW"] = "稍後", ["de_DE"] = "Später" },
        ["Download"] = new() { ["zh_CN"] = "下载", ["zh_TW"] = "下載", ["de_DE"] = "Download" },
        ["Download Failed"] = new() { ["zh_CN"] = "下载失败", ["zh_TW"] = "下載失敗", ["de_DE"] = "Download fehlgeschlagen" },
        ["Delete Failed"] = new() { ["zh_CN"] = "删除失败", ["zh_TW"] = "刪除失敗", ["de_DE"] = "Löschen fehlgeschlagen" },
        ["Backend config detected: signed download available."] = new() { ["zh_CN"] = "已检测到后端配置：签名下载可用。", ["zh_TW"] = "已偵測到後端設定：簽名下載可用。", ["de_DE"] = "Backend-Konfiguration erkannt: Signierte Downloads verfügbar." },
        ["Backend config detected: signed download missing."] = new() { ["zh_CN"] = "未检测到后端配置：签名下载不可用。", ["zh_TW"] = "未偵測到後端設定：簽名下載不可用。", ["de_DE"] = "Backend-Konfiguration fehlt: Signierte Downloads nicht verfügbar." },
        ["Private backend is not configured. Switched FLiNG to official mode; GCM/XiaoXing/CT signed downloads are unavailable."] = new() { ["zh_CN"] = "未配置私有后端，已自动切换到 FLiNG 官方模式；GCM/XiaoXing/CT 的签名下载不可用。", ["zh_TW"] = "未設定私有後端，已自動切換到 FLiNG 官方模式；GCM/XiaoXing/CT 的簽名下載不可用。", ["de_DE"] = "Privates Backend nicht konfiguriert. FLiNG wurde auf offiziellen Modus umgestellt." },
        ["This source requires the private Game-Zone backend configuration, which is not present in this workspace."] = new() { ["zh_CN"] = "该来源依赖私有 Game-Zone 后端配置，当前工作区中不存在该配置。", ["zh_TW"] = "此來源依賴私有 Game-Zone 後端設定，目前工作區中不存在該設定。", ["de_DE"] = "Diese Quelle benötigt die private Game-Zone-Backend-Konfiguration." }
    };

    public string Translate(string text, string language)
    {
        if (language == "en_US")
        {
            return text;
        }

        if (_translations.TryGetValue(text, out var map) && map.TryGetValue(language, out var translated))
        {
            return translated;
        }

        return text;
    }
}

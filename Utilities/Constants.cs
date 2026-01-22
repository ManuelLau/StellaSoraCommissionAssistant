namespace StellaSoraCommissionAssistant.Utilities;

public static class Constants
{
    public const string AppVersion = "0.1.0";
    public const string PlatformTag = "win-x";
    public const string ProjectGitHubUrl = "https://github.com/ManuelLau/StellaSoraCommissionAssistant";
    public const string ProjectGiteeUrl = "https://gitee.com/manuel33/StellaSoraCommissionAssistant";
    public const string AppGitHubApiUrl = "https://api.github.com/repos/ManuelLau/StellaSoraCommissionAssistant/releases";
    public const string AppGiteeApiUrl = "https://gitee.com/api/v5/repos/manuel33/StellaSoraCommissionAssistant/releases";
    public const string ResourcesGitHubApiUrl = "https://api.github.com/repos/ManuelLau/StellaSoraCommissionAssistantResources/releases";
    public const string ResourcesGiteeApiUrl = "https://gitee.com/api/v5/repos/manuel33/StellaSoraCommissionAssistantResources/releases";
    public const string BilibiliLink = "https://space.bilibili.com/3493267989596771";

    public static readonly string ConfigJsonDirectory = GetPath(@"config");
    public static readonly string ConfigJsonFilePath = GetPath(@"config\config.json");
    public static readonly string CacheFilePath = GetPath(@"config\cache.json");
    public static readonly string LogFilePath = GetPath(@"debug\log.txt");
    public static readonly string ResourcesVersionJsonPath = GetPath(@"resources\version.json");
    public static readonly string ReadmeDocPath = GetPath(@"README.md");

    public static readonly string MaaSourceDirectory = GetPath(@"resources\base");
    public static readonly string MaaSourceBiliBiliOverride = GetPath(@"resources\bilibili");
    public static readonly string MaaSourceZhTwOverride = GetPath(@"resources\zh-tw");
    public static readonly string MaaSourceEnOverride = GetPath(@"resources\en");
    public static readonly string MaaSourceJpOverride = GetPath(@"resources\jp");

    public const string GameClientName = "xtlr";

    public static readonly TimeOnly GameRefreshTimeOnly = new(4, 0, 0);

    public const short AutoRunDeviceDefaultWaittingTimeSpan = 10; // 默认等待模拟器启动时间(秒)
    public const short AutoRunDeviceMinWaittingTimeSpan = 10; // 最小等待模拟器启动时间(秒)
    public const short AutoRunDeviceMaxWaittingTimeSpan = 600; // 最大等待模拟器启动时间(秒)
    public const short AutoSearchDeviceMaxWaittingTimeSpan = 120; // 最大等待搜索设备时间(秒)

    public const short MaintenanceTaskChainDelayDuration = 30;  // 服务器维护时每次任务推迟的间隔(分钟)
    private static string GetPath(string path)
    {
        string appPath = AppDomain.CurrentDomain.BaseDirectory;
        return System.IO.Path.Combine(appPath, path);
    }
}

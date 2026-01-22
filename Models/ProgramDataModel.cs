using CommunityToolkit.Mvvm.ComponentModel;
using StellaSoraCommissionAssistant.Utilities;

namespace StellaSoraCommissionAssistant.Models;

public partial class ProgramDataModel : ObservableObject
{
    private static readonly ProgramDataModel _instance = new();
    public static ProgramDataModel Instance
    {
        get => _instance ?? new();
    }

    /// <summary>挂机任务是否开始执行</summary>
    [ObservableProperty]
    public bool isAfkTaskRunning;
    /// <summary>当前任务是否正在执行</summary>
    [ObservableProperty]
    public bool isCurrentTaskExecuting;
    [ObservableProperty]
    public string updateTaskInfo = string.Empty;  // 更新任务信息，用于显示当前状态或结果
    [ObservableProperty]
    public bool isCheckingNewVersion;
    [ObservableProperty]
    public bool hasNewVersion;
    [ObservableProperty]
    public bool isDownloadingFiles;
    [ObservableProperty]
    public bool isReadyForApplyUpdate;
    [ObservableProperty]
    public double downloadProgress;
    [ObservableProperty]
    public string downloadedSizeInfo = string.Empty;
    [ObservableProperty]
    public string updateInfoTitle = string.Empty;
    [ObservableProperty]
    public string updateInfoContent = string.Empty;
    [ObservableProperty]
    public string resourcesVersion = "0.0.0.0";
    public SettingsDataModel SettingsData { get; set; }

    public ProgramDataModel()
    {
        IsAfkTaskRunning = false;
        IsCurrentTaskExecuting = false;
        IsCheckingNewVersion = false;
        HasNewVersion = false;
        IsDownloadingFiles = false;
        IsReadyForApplyUpdate = false;
        ResourcesVersion = ResourcesVersionInfo.GetResourcesVersion();
        SettingsData = new();
    }
}

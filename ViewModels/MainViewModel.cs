using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.Utilities;
using StellaSoraCommissionAssistant.Views;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace StellaSoraCommissionAssistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static MainViewModel? _instance;
    public static MainViewModel Instance
    {
        get => _instance ??= new();
    }
    [ObservableProperty]
    public ProgramDataModel programData = ProgramDataModel.Instance;
    [ObservableProperty]
    //等待执行的任务队列，该队列中的任务也可以选择是否显示在主界面任务列表内
    public ObservableCollection<TaskChainModel> waitingTaskList;
    [ObservableProperty]
    public ObservableCollection<MainViewLogItemModel> logDataList;
    [ObservableProperty]
    public bool isStoppingCurrentTask = false;
    [ObservableProperty]
    public string createTaskButtonText;
    [ObservableProperty]
    public string refreshTaskButtonText;

    private UpdateWindow? _updateWindow;
    private HelpWindow? _helpWindow;

    public MainViewModel()
    {

        WaitingTaskList = [];
        LogDataList = [];
        CreateTaskButtonText = "生成任务";
        RefreshTaskButtonText = "刷新任务";
    }

    [RelayCommand]
    public static void StartTaskButton()
    {
        Utility.CustomDebugWriteLine("手动点击开始任务按钮");
        TaskManager.Instance.Start();
    }

    [RelayCommand]
    public static void StopTaskButton()
    {
        Utility.CustomDebugWriteLine("手动点击停止任务按钮");
        TaskManager.Instance.Stop(true);
    }

    [RelayCommand]
    public static void CreateButton()
    {
        Utility.CustomDebugWriteLine("手动点击生成/刷新任务按钮");
        // 用户手动点击，先清空当前任务列表
        Instance.WaitingTaskList.Clear();
        TaskManager.Instance.CreateTaskChain();
    }

    public void OpenUpdateWindow()
    {
        if (_updateWindow == null || !_updateWindow.IsVisible)
        {
            _updateWindow = new();
            _updateWindow.Closed += (s, args) => _updateWindow = null;
            _updateWindow.Left = Application.Current.MainWindow.Left + 110;
            _updateWindow.Top = Application.Current.MainWindow.Top + 100;
            _updateWindow.Show();
        }
        else
        {
            _updateWindow.Activate();
            _updateWindow.WindowState = WindowState.Normal;
        }
    }

    public void OpenHelpWindow()
    {
        if (_helpWindow == null || !_helpWindow.IsVisible)
        {
            _helpWindow = new();
            _helpWindow.Closed += (s, args) => _helpWindow = null;
            _helpWindow.Show();
        }
        else
        {
            _helpWindow.Activate();
            _helpWindow.WindowState = WindowState.Normal;
        }
    }

    public void SetUpdateWindowTopmost(bool isTopmost)
    {
        if (_updateWindow != null)
        {
            _updateWindow.Topmost = isTopmost;
        }
    }
    public void AppStart()
    {
        Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Utility.CustomDebugWriteLine($"启动程序 - Version:{version}");
        // 读取存储的任务列表
        if (File.Exists(Constants.CacheFilePath))
        {
            string json = File.ReadAllText(Constants.CacheFilePath);
            ObservableCollection<TaskChainModel>? deserializedCollection = JsonConvert.DeserializeObject<ObservableCollection<TaskChainModel>>(json);
            if (deserializedCollection != null)
                WaitingTaskList = deserializedCollection;
            foreach (var taskChainTtem in WaitingTaskList)
            {
                taskChainTtem.Status = ETaskChainStatus.Waiting;
            }
        }
        // 启动时自动检测更新
        if (ProgramData.SettingsData.IsAutoCheckAppUpdate)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000); // 延迟1秒
                Utility.PrintLog("正在检查更新...");
                UpdateTool.CheckBothNewVersion(true, out _, out _);
            });
        }
    }
    public void AppClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 存储当前任务列表
        string json = JsonConvert.SerializeObject(WaitingTaskList);
        File.WriteAllText(Constants.CacheFilePath, json);
        // 释放连接客户端的资源
        TaskManager.Instance.DisposeMaaTasker();
        Utility.CustomDebugWriteLine("手动关闭程序");
    }
}

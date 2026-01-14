using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace StellaSoraCommissionAssistant.Models;

public enum ETaskChainStatus
{
    Waiting, //等待进入执行任务队列
    InCurrentQueue, //时间已经到了，在执行任务的队列中，等待执行(未使用)
    Running, //当前正在执行
}
public enum ETaskChainType
{
    Commission,
    AcquireEnergy,
    System, //系统自动添加的任务，比如启动游戏、重启游戏
}

/// <summary>
/// 任务列表item，表示一个任务链，可以包含多个小的Pipeline任务
/// </summary>
public partial class TaskChainModel : ObservableObject
{
    [ObservableProperty][JsonIgnore]
    public string name;
    [ObservableProperty][JsonIgnore]
    public DateTime executeDateTime;
    [ObservableProperty][JsonIgnore]
    public ETaskChainStatus status;

    [JsonIgnore]
    public string StartLogMessage { get => $"开始任务：{Name}"; }
    [JsonIgnore]
    public string SuccessLogMessage { get => $"{Name}已完成"; }
    [JsonIgnore]
    public string FailedLogMessage { get => $"{Name}失败！"; }

    public ETaskChainType TaskChainType { get; set; }
    public bool ShowInWaitingTaskChainList { get; set; }  //是否在任务列表里显示出来
    public bool PrintStartLog { get; set; }  //是否需要在执行成功时打Log到主界面
    public bool PrintSuccessLog { get; set; }

    public Queue<TaskModel> TaskQueue { get; set; }

    /// <summary>不需要的string用string.Empty</summary>
    public TaskChainModel(
        string name,
        DateTime executeDateTime,
        ETaskChainType taskChainType,
        bool showInWaitingTaskChainList,
        bool printStartLog,
        bool printSuccessLog,
        Queue<TaskModel> taskQueue)
    {
        Name = name;
        ExecuteDateTime = executeDateTime;
        Status = ETaskChainStatus.Waiting;
        TaskChainType = taskChainType;
        ShowInWaitingTaskChainList = showInWaitingTaskChainList;
        PrintStartLog = printStartLog;
        PrintSuccessLog = printSuccessLog;
        TaskQueue = taskQueue;
    }
}

using HandyControl.Tools.Extension;
using MaaFramework.Binding;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using static StellaSoraCommissionAssistant.Utilities.CustomTask;

namespace StellaSoraCommissionAssistant.Utilities;

public class TaskManager
{
    /// <summary>当前任务链是否需要打印任务结束的Log，包括成功、失败、无效</summary>
    public bool CurrentTaskChainPrintFinishedLog;
    private static TaskManager? _instance;
    public static TaskManager Instance
    {
        get => _instance ??= new();
    }
    private readonly MaaToolkit _maaToolkit;
    private MaaTasker? _maaTasker;
    private string _maaControllerName = string.Empty;
    //等待队列，用于主界面显示的任务链列表，包含执行中的、待机的任务
    private readonly ObservableCollection<TaskChainModel> _waitingTaskChainList = MainViewModel.Instance.WaitingTaskList;
    //执行队列，正在执行或即将要执行的任务链列表，执行前会进行客户端连接的流程，执行后进行返回主界面
    private readonly List<TaskChainModel> _executingTaskChainList = [];
    private readonly SettingsDataModel _settingsData = ProgramDataModel.Instance.SettingsData;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly List<DateTime> _commissionCompleteDateTime = [];
    private int _lastTaskDurationInHours = 0;
    private bool _isCustomDispatch = false;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public TaskManager()
    {
        _maaToolkit = new MaaToolkit(true);
        _cancellationTokenSource = new();
    }
    
    public async void Start()
    {
        ProgramDataModel.Instance.IsAfkTaskRunning = true;
        _cancellationTokenSource = new();
        await Task.Run(async () =>
        {
            // 如果正确填写了客户端路径，则开始任务时不检测客户端是否连接
            if (!System.IO.Path.Exists(_settingsData.GamePath))
            {
                if (!await InitMaaTasker(_cancellationTokenSource.Token))
                {
                    Stop(false);
                    return;
                }
            }
            PreventSleepTool.PreventSleep();
            await StartAfkTask(_cancellationTokenSource.Token);
        });
    }

    public async void Stop(bool isExecutingCurrentTask)
    {
        if (isExecutingCurrentTask)
        {
            Utility.PrintLog("正在停止任务...");
        }
        MainViewModel.Instance.IsStoppingCurrentTask = true;
        foreach (var taskChainTtem in _waitingTaskChainList)
        {
            taskChainTtem.Status = ETaskChainStatus.Waiting;
        }
        PreventSleepTool.RestoreSleep();
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (Exception e)
        {
            Utility.CustomDebugWriteLine("Stop()-_cancellationTokenSource.Cancel() failed!");
            Utility.CustomDebugWriteLine($"Error: {e.Message}");
        }
        await Task.Run(DisposeMaaTasker);
        if (!isExecutingCurrentTask)
        {
            StopCurrentTask();
        }
    }

    // 生成任务链
    public void CreateTaskChain()
    {
        Queue<TaskModel> tempQueue = new();
        tempQueue.Enqueue(new("进入委托界面", "CommissionEnter", string.Empty, ETaskType.CommissionEnter));
        tempQueue.Enqueue(new("识别委托状态", "CommissionCheckState", string.Empty, ETaskType.CommissionCheckState));
        AddToWaitingTaskChainList(new("委托派遣", DateTime.Now, true, true, false, tempQueue));

        if (_settingsData.IsAcquireFriendEnergy)
        {
            AddAcquireFriendEnergyTaskChain();
        }

        //tempQueue = new([new("Test", "HomeScreen@ClickExitButton", string.Empty, ETaskType.Normal)]);
        //AddToWaitingTaskChainList(new("Test", DateTime.Now, true, true, true, tempQueue));
        SortWaitingTaskChainList();
        CheckCommissionRationality();
    }

    // 初始化MaaTasker，自动连接客户端和Maa资源
    private async Task<bool> InitMaaTasker(CancellationToken token)
    {
        if (_maaTasker is not null && !_maaTasker.IsInvalid)
        {
            // 已有有效的MaaTasker，直接获取Window
            if (_maaTasker.Controller.IsConnected && !_maaTasker.Controller.IsInvalid)
            {
                DesktopWindowInfo? gameWindow = FindWindow();
                if (gameWindow is not null)
                {
                    // 已有有效的Window，直接跳过自动连接
                    return true;
                }
            }
            Utility.CustomDebugWriteLine("Controller is not connected or invalid!");
            Utility.PrintLog("客户端失去连接，正在重新打开...");
        }
        Utility.CustomDebugWriteLine("MaaTasker is null or invalid");
        DisposeMaaTasker();

        MaaController? maaController = await InitMaaController(token);
        if (maaController is null || token.IsCancellationRequested)
        {
            Utility.CustomDebugWriteLine($"InitMaaController failed - maaController is null or token.IsCancellationRequested:{token.IsCancellationRequested}");
            return false;
        }

        MaaResource? maaResource = LoadMaaSource();
        if (maaResource is null)
        {
            return false;
        }

        Utility.PrintLog("正在连接客户端...");
        try
        {
            _maaTasker = new()
            {
                Controller = maaController,
                Resource = maaResource,
                DisposeOptions = DisposeOptions.All,
            };
            // 注册自定义识别、动作
            _maaTasker.Resource.Register(new CustomAddRemainingTime());
            _maaTasker.Resource.Register(new CustomRecogniseLastTaskDuration());
            _maaTasker.Resource.Register(new CustomCreateCommissionTaskChain());
            _maaTasker.Resource.Register(new CustomDispatchFailed());
            _maaTasker.Resource.Register(new CustomLogFriendEnergy());
            _maaTasker.Resource.Register(new CustomRepeatedLoginStopTask());
            _maaTasker.Resource.Register(new CustomMaintenanceDelayTaskChain());
            _maaTasker.Resource.Register(new CustomAppendRestartClientTask());
            //_maaTasker.Resource.Register(new CustomClientUpdateStopTask());
        }
        catch (Exception e)
        {
            Utility.PrintLog("客户端初始化失败，请尝试重启客户端或系统");
            Utility.CustomDebugWriteLine($"Error: {e.Message}");
            return false;
        }
        if (_maaTasker == null || !_maaTasker.IsInitialized)
        {
            Utility.PrintLog("客户端初始化失败，请尝试重启客户端或系统");
            Utility.CustomDebugWriteLine("_maaTasker is null or _maaTasker.IsInitialized is false");
            return false;
        }
        Utility.PrintLog("成功连接至客户端" + _maaControllerName);
        return true;
    }

    private DesktopWindowInfo? FindWindow()
    {
        var windows = _maaToolkit.Desktop.Window.Find();
        DesktopWindowInfo? gameWindow = null;
        foreach (var e in windows)
        {
            if (e.Name.Equals(Constants.GameClientName))
            {
                gameWindow = e;
                break;
            }
        }
        return gameWindow;
    }

    private async Task<DesktopWindowInfo?> AutoRunDevice(CancellationToken token)
    {
        if (string.IsNullOrEmpty(_settingsData.GamePath))
        {
            Utility.PrintError("未设置客户端路径，请先设置路径或手动打开客户端");
        }
        else if (!System.IO.Path.Exists(_settingsData.GamePath))
        {
            Utility.PrintError("自动连接客户端失败，请检查路径是否正确");
            Utility.CustomDebugWriteLine(_settingsData.GamePath);
        }
        else
        {
            try
            {
                Utility.CustomDebugWriteLine("找不到正在运行的客户端，将自动打开：" + _settingsData.GamePath);
                Utility.PrintLog("正在打开客户端...");
                Process.Start(new ProcessStartInfo(_settingsData.GamePath)
                {
                    UseShellExecute = true
                });

                // 等待客户端启动，默认/最小等待时间为10秒
                await Task.Delay(_settingsData.AutoRunDeviceWaittingTimeSpan * 1000, token);
                DateTime startTime = DateTime.Now;
                while (!token.IsCancellationRequested)
                {
                    var windows = _maaToolkit.Desktop.Window.Find();
                    foreach (var e in windows)
                    {
                        if (e.Name.Equals(Constants.GameClientName))
                        {
                            return e;
                        }
                    }
                    if (DateTime.Now > startTime.AddSeconds(Constants.AutoSearchDeviceMaxWaittingTimeSpan))
                    {
                        Utility.PrintError("等待超时，自动打开客户端失败！");
                        return null;
                    }
                    Thread.Sleep(1000); // 每秒执行一次
                }
            }
            catch (TaskCanceledException)
            {
                Utility.CustomDebugWriteLine("手动停止自动打开客户端");
            }
            catch (Exception e)
            {
                Utility.CustomDebugWriteLine($"无法打开客户端: {e.Message}");
                Utility.PrintError("打开客户端失败！请检查路径是否正确");
            }
        }
        return null;
    }

    private async Task<MaaController?> InitMaaController(CancellationToken token)
    {
        DesktopWindowInfo? gameWindow = FindWindow();
        gameWindow ??= await AutoRunDevice(token);
        if (gameWindow is null || token.IsCancellationRequested)
        {
            Utility.CustomDebugWriteLine($"自动打开客户端失败-token.IsCancellationRequested:{token.IsCancellationRequested}");
            return null;
        }
        // 寻找到窗口，但是最小化了，切换至前台
        if (IsIconic(gameWindow.Handle))
        {
            ShowWindow(gameWindow.Handle, 4);
        }
        _maaControllerName = gameWindow.Name;
        return gameWindow.ToWin32ControllerWith(Win32ScreencapMethod.FramePool, Win32InputMethod.SendMessageWithCursorPos, Win32InputMethod.SendMessageWithCursorPos);
    }

    private static MaaResource? LoadMaaSource()
    {
        MaaResource? resource = null;
        //根据配置里的客户端类型选项改变读取的文件路径
        string[] maaSourcePaths = ProgramDataModel.Instance.SettingsData.ClientTypeSettingIndex switch
        {
            (int)EClientTypeSettingOptions.Zh_CN_PC => [Constants.MaaSourceDirectory],
            (int)EClientTypeSettingOptions.Zh_CN_Bilibili_Emulator => [Constants.MaaSourceDirectory, Constants.MaaSourceBiliBiliOverride],
            (int)EClientTypeSettingOptions.Zh_TW_PC => [Constants.MaaSourceDirectory, Constants.MaaSourceZhTwOverride],
            (int)EClientTypeSettingOptions.Jp_PC => [Constants.MaaSourceDirectory, Constants.MaaSourceJpOverride],
            _ => [Constants.MaaSourceDirectory],
        };
        try
        {
            resource = new(maaSourcePaths);
        }
        catch (Exception e)
        {
            Utility.PrintLog("加载资源文件失败");
            Utility.CustomDebugWriteLine($"Error: {e.Message}");
            return resource;
        }
        return resource;
    }

    // 把已经到达时间的任务入列。同时在头部添加一个启动客户端的任务链，在尾部添加一个返回主界面的任务链。
    private void CreateCurrentTaskQueue()
    {
        Queue<TaskModel> tempQueue0 = new();
        tempQueue0.Enqueue(new("启动游戏", "HomeScreen", string.Empty, ETaskType.HomeScreen));
        _executingTaskChainList.Add(new("启动游戏", DateTime.Now, false, true, true, tempQueue0));
        foreach (var task in _waitingTaskChainList)
        {
            if (DateTime.Now >= task.ExecuteDateTime)
            {
                task.Status = ETaskChainStatus.InCurrentQueue;
                // 读取设置(进入执行中队列时候才会读取)
                foreach (var e in task.TaskQueue)
                {
                    e.PipelineOverride = GetOverrideJsonWithReadConfig(e.Type);
                }
                _executingTaskChainList.Add(task);
            }
        }
        Queue<TaskModel> tempQueue1 = new();
        tempQueue1.Enqueue(new("返回主界面", "HomeScreen", string.Empty, ETaskType.HomeScreen));
        _executingTaskChainList.Add(new("返回主界面", DateTime.Now, false, false, false, tempQueue1));
        foreach (var item in _executingTaskChainList)
        {
            Utility.CustomDebugWriteLine($"{item.ExecuteDateTime} - {item.Name}");
        }
    }

    private async Task ExecuteCurrentTask(CancellationToken token)
    {
        // 初始化数据
        _commissionCompleteDateTime.Clear();
        _lastTaskDurationInHours = 0;
        while (_executingTaskChainList.Count > 0)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            var currentTaskChain = _executingTaskChainList[0];
            currentTaskChain.Status = ETaskChainStatus.Running;
            if (!await InitMaaTasker(token))
            {
                Utility.CustomDebugWriteLine("maaTasker初始化失败");
            }
            if (_maaTasker == null)
            {
                Utility.CustomDebugWriteLine("maaTasker依然是null，直接停止任务");
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Stop(true);
                }
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }
            Utility.CustomDebugWriteLine($"[任务链]   - 执行 - {currentTaskChain.Name}");
            if (currentTaskChain.PrintStartLog)
            {
                Utility.PrintLog(currentTaskChain.StartLogMessage);
            }
            bool anyTaskFailed = false;
            CurrentTaskChainPrintFinishedLog = true; // 是否打印任务结束的信息(成功、失败、异常)
            foreach (var taskItem in currentTaskChain.TaskQueue)
            {
                Utility.CustomDebugWriteLine($"[单个任务] - 执行 - {taskItem.Name} - maaTasker.AppendPipeline({taskItem.Entry})");
                Utility.CustomDebugWriteLine($"PipelineOverride - {taskItem.PipelineOverride}");
                var status = string.IsNullOrEmpty(taskItem.PipelineOverride) ?
                    _maaTasker.AppendTask(taskItem.Entry).Wait() :
                    _maaTasker.AppendTask(taskItem.Entry, taskItem.PipelineOverride).Wait();
                if (status.IsSucceeded())
                {
                    Utility.CustomDebugWriteLine($"[单个任务] - 完成 - {taskItem.Name} - maaTasker.AppendPipeline({taskItem.Entry})");
                }
                else
                {
                    anyTaskFailed = true;
                    Utility.CustomDebugWriteLine($"[单个任务] - 失败! - {taskItem.Name} - maaTasker.AppendPipeline({taskItem.Entry}) - {status}");

                    // 检测是否因为停止任务导致释放了maaTasker，如果不break会导致闪退
                    if (token.IsCancellationRequested)
                    {
                        CurrentTaskChainPrintFinishedLog = false;
                        Utility.CustomDebugWriteLine("停止任务 - 跳出当前任务链");
                        break;
                    }
                    // 不是手动或系统停止的话,有Pipeline失败后会继续执行后面的Pipeline
                }
            }

            if (anyTaskFailed)
            {
                if (CurrentTaskChainPrintFinishedLog)
                {
                    Utility.PrintError(currentTaskChain.FailedLogMessage);
                }
                Utility.CustomDebugWriteLine($"[任务链]   - 异常! - 执行任务链过程中出现异常 - {currentTaskChain.Name}");
            }
            else
            {
                if (CurrentTaskChainPrintFinishedLog && currentTaskChain.PrintSuccessLog)
                {
                    Utility.PrintLog(currentTaskChain.SuccessLogMessage);
                }
                Utility.CustomDebugWriteLine($"[任务链]   - 完成 - {currentTaskChain.Name}");
            }
            // 移除任务链。如果是因为用户手动取消的话就不移除，并将状态设为等待中
            if (token.IsCancellationRequested)
            {
                currentTaskChain.Status = ETaskChainStatus.Waiting;
            }
            else
            {
                // 重新添加领取好友体力任务
                bool ReaddAcquireFriendEnergyTaskChain = false;
                if (_settingsData.IsAcquireFriendEnergy)
                {
                    if (currentTaskChain.TaskQueue.Peek().Type == ETaskType.AcquireFriendsEnergy)
                    {
                        ReaddAcquireFriendEnergyTaskChain = true;
                    }
                }
                _executingTaskChainList.Remove(currentTaskChain);
                if (currentTaskChain.ShowInWaitingTaskChainList)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _waitingTaskChainList.Remove(currentTaskChain);
                        if (ReaddAcquireFriendEnergyTaskChain)
                        {
                            AddAcquireFriendEnergyTaskChain();
                        }
                    });
                }
            }
        }

        if (_settingsData.IsCloseGameAfterFinishingTask)
        {
            ExitGameClient();
        }
        Utility.PrintLog("当前任务已完成，小助手待机中");
    }

    // 开启挂机任务
    private async Task StartAfkTask(CancellationToken token)
    {
        bool firstTimeExecute = true;

        while (!token.IsCancellationRequested)
        {
            // 判断有无已经到达时间的任务
            foreach (var taskChainItem in _waitingTaskChainList)
            {
                if (DateTime.Now >= taskChainItem.ExecuteDateTime)
                {
                    Utility.CustomDebugWriteLine($"检测到有任务链可以执行");
                    ProgramDataModel.Instance.IsCurrentTaskExecuting = true;
                    firstTimeExecute = false;
                    CreateCurrentTaskQueue();
                    await ExecuteCurrentTask(token);
                    Utility.CustomDebugWriteLine($"已入列的任务链执行完成或任务停止");
                    ProgramDataModel.Instance.IsCurrentTaskExecuting = false;
                    break;
                }
            }
            if (firstTimeExecute)
            {
                Utility.PrintLog("暂无可执行任务，小助手待机中");
                firstTimeExecute = false;
            }
            try
            {
                await Task.Delay(1000, token); // 每秒检查一次
            }
            catch (TaskCanceledException)
            {

            }
        }
        StopCurrentTask();
    }

    private void StopCurrentTask()
    {
        _executingTaskChainList.Clear();
        ProgramDataModel.Instance.IsAfkTaskRunning = false;
        ProgramDataModel.Instance.IsCurrentTaskExecuting = false;
        MainViewModel.Instance.IsStoppingCurrentTask = false;
        _cancellationTokenSource.Dispose();
        Utility.PrintLog("任务已停止");
    }

    // 添加一个任务链到任务列表
    private void AddToWaitingTaskChainList(TaskChainModel item)
    {
        if (item.ShowInWaitingTaskChainList)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _waitingTaskChainList.Add(item);
            });
        }
    }

    public void SortWaitingTaskChainList()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var sortedList = _waitingTaskChainList.OrderBy(item => item.ExecuteDateTime).ToList();
            _waitingTaskChainList.Clear();
            _waitingTaskChainList.AddRange(sortedList);
        });
    }

    private void AddAcquireFriendEnergyTaskChain()
    {
        if (_settingsData.IsAcquireFriendEnergy)
        {
            DateTime dateTime = DateTime.Now.Date + _settingsData.AcquireFriendEnergyTime.ToTimeSpan();
            if (dateTime < DateTime.Now)
            {
                dateTime = dateTime.AddDays(1);
            }
            Queue<TaskModel> tempQueue = new();
            tempQueue.Enqueue(new("领取好友体力", "AcquireFriendsEnergy", string.Empty, ETaskType.AcquireFriendsEnergy));
            AddToWaitingTaskChainList(new("领取好友体力", dateTime, true, true, true, tempQueue));
            SortWaitingTaskChainList();
        }
    }

    // 读取设置来生成所需的pipeline override json
    private string GetOverrideJsonWithReadConfig(ETaskType type)
    {
        if (type == ETaskType.CommissionCheckState)
        {
            string overrideJson = "{";
            if (_settingsData.CommissionDispatchTypeSettingIndex == (int)ECommissionDispatchTypeSettingOptions.Custom)
            {
                overrideJson += "\"Commission@Complete\":{\"next\":[{\"name\":\"Commission@SkipDialogue\",\"jump_back\":true},{\"name\":\"ObtainReward\",\"jump_back\":true},{\"name\":\"Commission@ClickWhenComplete\",\"jump_back\":true},\"Commission@CustomDispatchStart\"]}";
                overrideJson += ",\"Commission@ClickWhenComplete\":{\"recognition\":{\"param\":{\"template\":[\"ReturnButton.png\"]}}}";
                for (int i = 0; i < 4; i++)
                {
                    string templateName = $"CommissionClass{_settingsData.CommissionTaskTypeSettingIndexs[i] / 6}.png";
                    overrideJson += ",\"Commission@SelectClass" + i + "\":{\"recognition\":{\"param\":{\"template\":[\"" + templateName + "\"]}}}";
                    string typeName = Utility.GetEnumDescriptions<ECommissionTypeSettingOptions>()[_settingsData.CommissionTaskTypeSettingIndexs[i]];
                    overrideJson += ",\"Commission@RecogniseType" + i + "\":{\"recognition\":{\"param\":{\"expected\":[\"" + typeName.Replace(" ", "") + "\"]}}}";
                }
                var enumValue = ((ECommissionDurationSettingOptions[])Enum.GetValues(typeof(ECommissionDurationSettingOptions)))[_settingsData.CommissionDurationSettingIndex];
                string time = (int)enumValue + "小时";
                overrideJson += ",\"Commission@SelectTime\":{\"recognition\":{\"param\":{\"expected\":[\"" + time + "\"]}}}";
            }
            overrideJson += "}";
            Debug.WriteLine("OverrideJson:" + overrideJson);
            return overrideJson;
        }
        return string.Empty;
    }

    public void DisposeMaaTasker()
    {
        try
        {
            if (_maaTasker != null)
            {
                _maaTasker.Stop().Wait();
                _maaTasker.Dispose();
                _maaTasker = null;
            }
        }
        catch (ObjectDisposedException)
        {
            Utility.CustomDebugWriteLine("MaaTasker was already disposed");
        }
        catch (Exception e)
        {
            Utility.CustomDebugWriteLine($"MaaTasker Dispose failed: {e.Message}");
        }
    }

    private void ExitGameClient()
    {
        if (_maaTasker != null)
        {
            DesktopWindowInfo? gameWindow = FindWindow();
            if (gameWindow is null)
            {
                Utility.PrintLog("未找到游戏窗口，无法自动退出客户端");
                return;
            }
            bool success = PostMessage(gameWindow.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
            if (success)
            {
                Utility.PrintLog("即将自动退出客户端");
            }
            else
            {
                Utility.PrintLog("自动退出客户端失败!");
            }
        }
    }

    public void AddCommissionCompleteTime(DateTime dateTime, bool isCustomDispatch)
    {
        // 两者之间只使用一个，另一个要置0
        _commissionCompleteDateTime.Add(dateTime);
        _lastTaskDurationInHours = 0;
        _isCustomDispatch = isCustomDispatch;
    }

    public void SetLastTaskDurationInHours(int hours)
    {
        _commissionCompleteDateTime.Clear();
        _lastTaskDurationInHours = hours;
    }

    public void CreateCommissionTaskChain()
    {
        DateTime dateTime;
        if (_commissionCompleteDateTime.Count != 0 && _lastTaskDurationInHours == 0)
        {
            if (_commissionCompleteDateTime.Count != 4)
            {
                Utility.CustomDebugWriteLine("CreateCommissionTaskChain - _commissionCompleteDateTime的长度不为4！");
            }
            // 设置为最晚的时间
            dateTime = _commissionCompleteDateTime.Max();
            if (_isCustomDispatch)
            {
                Utility.PrintLog("委托已派遣");
            }
            else
            {
                Utility.PrintLog("委托进行中，剩余时间 " + (dateTime - DateTime.Now).ToString(@"hh\:mm\:ss"));
            }
        }
        else if (_commissionCompleteDateTime.Count == 0 && _lastTaskDurationInHours != 0)
        {
            Utility.CustomDebugWriteLine("CreateCommissionTaskChain - _lastTaskDurationInHours - " + _lastTaskDurationInHours);
            dateTime = DateTime.Now.AddHours(_lastTaskDurationInHours);
            Utility.PrintLog("已再次派遣委托");
        }
        else
        {
            Utility.CustomDebugWriteLine("CreateCommissionTaskChain - dateTime错误！");
            return;
        }
        Queue<TaskModel> tempQueue = new();
        tempQueue.Enqueue(new("进入委托界面", "CommissionEnter", string.Empty, ETaskType.Normal));
        tempQueue.Enqueue(new("识别委托状态", "CommissionCheckState", string.Empty, ETaskType.Normal));
        AddToWaitingTaskChainList(new("委托派遣", dateTime, true, true, false, tempQueue));
        Utility.PrintLog("下次委托派遣时间：" + dateTime.ToString("MM-dd HH:mm:ss"));
        // 添加完成后重新排序+清空数据
        SortWaitingTaskChainList();
        _commissionCompleteDateTime.Clear();
        _lastTaskDurationInHours = 0;
    }

    public void DelayTaskChain(DateTime openDateTime)
    {
        Utility.CustomDebugWriteLine("DelayTaskChain - openDateTime - " + openDateTime);
        foreach (var taskChainItem in _waitingTaskChainList)
        {
            if (DateTime.Now < openDateTime)
            {
                // 把当前任务推迟到已识别到的服务器开启时间
                if (taskChainItem.ExecuteDateTime < openDateTime)
                {
                    taskChainItem.ExecuteDateTime = openDateTime;
                }
            }
            else
            {
                // 如果当前任务时间比已识别到的服务器开启时间还晚，就再延后半小时
                if (taskChainItem.ExecuteDateTime < DateTime.Now)
                {
                    taskChainItem.ExecuteDateTime = DateTime.Now.AddMinutes(Constants.MaintenanceTaskChainDelayDuration);
                }
            }
            Utility.CustomDebugWriteLine($"延后任务链{taskChainItem.Name} - 新时间{taskChainItem.ExecuteDateTime}");
            ExitGameClient();
        }
        // 重新排序任务列表
        SortWaitingTaskChainList();
        // 清空当前任务
        _executingTaskChainList[0].PrintSuccessLog = false;
        _executingTaskChainList.Clear();
        foreach(var e in  _waitingTaskChainList)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                e.Status = ETaskChainStatus.Waiting;
            });
        }
    }

    public void AppendRestartClientTask()
    {
        if (_executingTaskChainList[0].TaskQueue.Peek().Type != ETaskType.HomeScreen)
        {
            Utility.PrintError("当前第一个任务不是HomeScreen!");
        }
        _executingTaskChainList[0].PrintSuccessLog = false;
        Queue<TaskModel> tempQueue = new();
        tempQueue.Enqueue(new("重启游戏", "HomeScreen", string.Empty, ETaskType.HomeScreen));
        _executingTaskChainList.Insert(1, new("重启游戏", DateTime.Now, false, false, true, tempQueue));
    }

    private void CheckCommissionRationality()
    {
        // 检查委托任务时间设置是否合理:卡带委托任务时长不能为4、8小时
        if ((ECommissionDispatchTypeSettingOptions)_settingsData.CommissionDispatchTypeSettingIndex == ECommissionDispatchTypeSettingOptions.Custom)
        {
            var enumValue = ((ECommissionDurationSettingOptions[])Enum.GetValues(typeof(ECommissionDurationSettingOptions)))[_settingsData.CommissionDurationSettingIndex];
            switch (enumValue)
            {
                case ECommissionDurationSettingOptions.Hours4:
                    foreach (var e in _settingsData.CommissionTaskTypeSettingIndexs)
                    {
                        if (e >= (int)ECommissionTypeSettingOptions.D1)
                        {
                            Utility.PrintError("游戏卡带委托任务时长不能为4小时，请修改设置");
                            break;
                        }
                    }
                    break;
                case ECommissionDurationSettingOptions.Hours8:
                    foreach (var e in _settingsData.CommissionTaskTypeSettingIndexs)
                    {
                        if (e >= (int)ECommissionTypeSettingOptions.D1)
                        {
                            Utility.PrintError("游戏卡带委托任务时长不能为8小时，请修改设置");
                            break;
                        }
                    }
                    break;
            }
        }
        // 检查委托任务是否有重复
        if (_settingsData.CommissionTaskTypeSettingIndexs.Length != _settingsData.CommissionTaskTypeSettingIndexs.Distinct().Count())
        {
            Utility.PrintError("委托任务重复，请修改设置");
        }
    }
}
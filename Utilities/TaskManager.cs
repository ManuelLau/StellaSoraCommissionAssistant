using HandyControl.Controls;
using MaaFramework.Binding;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
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
    //等待队列，用于主界面显示的任务链列表，包含执行中的、待机的任务
    private readonly ObservableCollection<TaskChainModel> _waitingTaskChainList = MainViewModel.Instance.WaitingTaskList;
    //执行队列，正在执行或即将要执行的任务链列表，执行前会进行客户端连接的流程，执行后进行返回主界面
    private readonly List<TaskChainModel> _currentTaskChainList = [];
    private readonly SettingsDataModel _settingsData = ProgramDataModel.Instance.SettingsData;
    private CancellationTokenSource _cancellationTokenSource;
    private TimeSpan _commissionRemainingTime = TimeSpan.MinValue;
    private int _lastTaskDurationInHours = 0;

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
            // 如果填写了客户端路径，则开始任务时不检测客户端是否连接
            if (string.IsNullOrEmpty(_settingsData.GamePath))
            {
                if (!await AutoConnect(_cancellationTokenSource.Token))
                {
                    Stop(false);
                    return;
                }
            }
            PreventSleepTool.PreventSleep();
            await StartAfkTask(_cancellationTokenSource.Token);
        });
    }

    /// <summary>
    /// 点击停止任务按钮，可能任务在执行中
    /// </summary>
    /// <param name="isOutOfAfkTask">是否正在执行当前任务</param>
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

    //生成任务链
    public void CreateTaskChain()
    {
        Queue<TaskModel> tempQueue = new();
        tempQueue.Enqueue(new("进入委托界面", "CommissionEnter", string.Empty, ETaskType.Normal));
        tempQueue.Enqueue(new("识别委托状态", "CommissionCheckState", string.Empty, ETaskType.Normal));
        AddToWaitingTaskList(new("完成委托", DateTime.Now, ETaskChainType.Commission, true, true, false, tempQueue));

        //Queue<TaskModel> tempQueue2 = new();
        //tempQueue2.Enqueue(new("Commission@RecogniseDispatchAgain", "Commission@RecogniseDispatchAgain", string.Empty, ETaskType.Normal));
        //AddToWaitingTaskList(new("Test", DateTime.Now, ETaskChainType.System, true, true, string.Empty, tempQueue2));

    }

    //自动连接客户端和Maa资源
    private async Task<bool> AutoConnect(CancellationToken token)
    {
        if (_maaTasker is not null)
        {
            if (_maaTasker.IsInvalid)
            {
                DisposeMaaTasker();
            }
            else
            {
                // 已有有效的MaaTasker，直接获取window
                if (_maaTasker.Controller.IsConnected && !_maaTasker.Controller.IsInvalid)
                {
                    // 已有有效的Window，直接跳过自动连接
                    return true;
                }
                Utility.CustomDebugWriteLine("Controller is not connected or invalid!");
                Utility.PrintLog("客户端失去连接，正在重新打开...");
            }
        }
        Utility.CustomDebugWriteLine("MaaTasker is null or invalid!");
        //Utility.PrintLog("客户端未连接");

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
        gameWindow ??= await AutoRunGameClient(token);
        if (gameWindow is null || token.IsCancellationRequested)
        {
            Utility.CustomDebugWriteLine($"自动打开客户端失败-AutoRunEmulator()-token.IsCancellationRequested:{token.IsCancellationRequested}");
            return false;
        }

        if (!LoadMaaSource(out MaaResource? maaResource) || maaResource is null)
        {
            return false;
        }

        Utility.PrintLog("正在连接客户端...");
        try
        {
            _maaTasker = new()
            {
                Controller = gameWindow.ToWin32ControllerWith(Win32ScreencapMethod.FramePool),
                Resource = maaResource,
                DisposeOptions = DisposeOptions.All,
            };
            // 注册自定义识别、动作
            _maaTasker.Resource.Register(new CustomRecogniseRemainingTime());
            _maaTasker.Resource.Register(new CustomRecogniseLastTaskDuration());
            _maaTasker.Resource.Register(new CustomCreateCommissionTaskChain());
            _maaTasker.Resource.Register(new CustomMaintenanceDelayTaskChain());
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
        Utility.PrintLog("成功连接至客户端" + gameWindow.Name);
        return true;
    }

    private async Task<DesktopWindowInfo?> AutoRunGameClient(CancellationToken token)
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
                Utility.CustomDebugWriteLine("找不到客户端，将自动打开：" + _settingsData.GamePath);
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
                StopCurrentTask();
            }
            catch (Exception e)
            {
                Utility.CustomDebugWriteLine($"无法打开客户端: {e.Message}");
                Utility.PrintError("打开客户端失败！请检查路径是否正确");
            }
        }
        return null;
    }

    private static bool LoadMaaSource(out MaaResource? resource)
    {
        resource = null;
        //根据配置里的客户端类型选项改变读取的文件路径
        string[] maaSourcePaths = ProgramDataModel.Instance.SettingsData.ClientTypeSettingIndex switch
        {
            (int)EClientTypeSettingOptions.Zh_CN => [Constants.MaaSourceDirectory],
            (int)EClientTypeSettingOptions.Zh_CN_Bilibili => [Constants.MaaSourceDirectory, Constants.MaaSourceBiliBiliOverride],
            (int)EClientTypeSettingOptions.Zh_TW => [Constants.MaaSourceDirectory, Constants.MaaSourceZhTwOverride],
            (int)EClientTypeSettingOptions.Jp => [Constants.MaaSourceDirectory, Constants.MaaSourceJpOverride],
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
            return false;
        }
        return true;
    }

    //开启挂机任务
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

    private async Task ExecuteCurrentTask(CancellationToken token)
    {
        DateTime startDateTime = DateTime.Now;
        while (_currentTaskChainList.Count > 0)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            var currentTaskChain = _currentTaskChainList[0];
            currentTaskChain.Status = ETaskChainStatus.Running;
            /*
            if (_maaTasker == null || _maaTasker.IsInvalid)
            {
                Utility.CustomDebugWriteLine($"maaTasker is null or invalid!");
                Utility.PrintLog("客户端未连接");
                if (!await AutoConnect(token))
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Stop(true);
                    }
                    return;
                }
            }
            var device = _maaTasker.Toolkit.AdbDevice.Find();
            if (device.IsEmpty || device.IsInvalid)
            {
                Utility.CustomDebugWriteLine($"device is empty or invalid!");
                Utility.PrintLog("客户端失去连接，正在重新打开...");
                if (!await AutoConnect(token))
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Stop(true);
                    }
                    return;
                }
            }*/
            await AutoConnect(token);
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
            //移除任务链。如果是因为用户手动取消的话就不移除，并将状态设为等待中
            if (token.IsCancellationRequested)
            {
                currentTaskChain.Status = ETaskChainStatus.Waiting;
            }
            else
            {
                _currentTaskChainList.Remove(currentTaskChain);
                if (currentTaskChain.ShowInWaitingTaskChainList)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _waitingTaskChainList.Remove(currentTaskChain);
                    });
                }
            }
        }

        if (_settingsData.IsCloseGameAfterFinishingTask)
        {
            if (_maaTasker != null)
            {
                var status = _maaTasker.AppendTask("CloseGame").Wait();
                if (status.IsSucceeded())
                {
                    Utility.PrintLog("已自动退出客户端");
                }
                else
                {
                    Utility.PrintLog("自动退出客户端失败!");
                }
            }
        }
        Utility.PrintLog("当前任务已完成，小助手待机中");
    }

    private void StopCurrentTask()
    {
        _currentTaskChainList.Clear();
        ProgramDataModel.Instance.IsAfkTaskRunning = false;
        ProgramDataModel.Instance.IsCurrentTaskExecuting = false;
        MainViewModel.Instance.IsStoppingCurrentTask = false;
        _cancellationTokenSource.Dispose();
        Utility.PrintLog("任务已停止");
    }

    // 添加一个任务链到任务列表
    private void AddToWaitingTaskList(TaskChainModel item)
    {
        if (item.ShowInWaitingTaskChainList)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _waitingTaskChainList.Add(item);
            });
        }
    }

    // 把已经到达时间的任务入列。同时在头部添加一个启动客户端的任务链，在尾部添加一个返回主界面的任务链。
    private void CreateCurrentTaskQueue()
    {
        // 如果第一个是重启游戏任务，那么就不用再添加启动游戏任务了
        if (_waitingTaskChainList[0].TaskQueue.Peek().Type == ETaskType.RestartGame)
        {
            // 自动更新资源文件
            if (ProgramDataModel.Instance.SettingsData.IsAutoUpdateResources)
            {
                //UpdateTool.CheckNewVersion(false, true, out _, out _, false);
                // 等待2秒，用于覆盖更新资源文件
                Task.Delay(2000).Wait();
                if (!LoadMaaSource(out MaaResource? maaResource) || maaResource == null)
                {
                    Utility.CustomDebugWriteLine("更新资源文件后，重新加载失败！");
                }
                else
                {
                    if (_maaTasker != null && !_maaTasker.IsInvalid)
                    {
                        _maaTasker = new()
                        {

                            Controller = _maaTasker.Controller,
                            Resource = maaResource,
                            DisposeOptions = DisposeOptions.All,
                        };
                    }
                    else
                    {
                        Utility.CustomDebugWriteLine("_maaTasker is null!");
                    }
                }
                
            }
        }
        else
        {
            Queue<TaskModel> tempQueue0 = new();
            tempQueue0.Enqueue(new("启动游戏", "HomeScreen", string.Empty, ETaskType.HomeScreen));
            _currentTaskChainList.Add(new("启动游戏", DateTime.Now, ETaskChainType.System, false, true, true, tempQueue0));
        }

        foreach (var taskChainItem in _waitingTaskChainList)
        {
            // 判断有无已经到达时间的任务
            if (DateTime.Now >= taskChainItem.ExecuteDateTime)
            {
                taskChainItem.Status = ETaskChainStatus.InCurrentQueue;
                // 读取设置(进入执行中队列时候才会读取)
                foreach (var taskItem in taskChainItem.TaskQueue)
                {
                    taskItem.PipelineOverride = GetOverrideJsonWithReadConfig(taskItem.Type);
                }
                _currentTaskChainList.Add(taskChainItem);
            }
        }

        Queue<TaskModel> tempQueue1 = new();
        tempQueue1.Enqueue(new("返回主界面", "HomeScreen", string.Empty, ETaskType.HomeScreen));
        _currentTaskChainList.Add(new("返回主界面", DateTime.Now, ETaskChainType.System, false, false, false, tempQueue1));
        foreach (var item in _currentTaskChainList)
        {
            Utility.CustomDebugWriteLine($"{item.ExecuteDateTime} - {item.Name}");
        }
    }

    // 读取设置来生成所需的pipeline override json
    private string GetOverrideJsonWithReadConfig(ETaskType type)
    {
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

    // 两者之间只使用一个，另一个要置0
    public void SetCommissionRemainingTime(TimeSpan time)
    {
        _commissionRemainingTime = time;
        _lastTaskDurationInHours = 0;
    }

    public void SetLastTaskDurationInHours(int hours)
    {
        _commissionRemainingTime = TimeSpan.MinValue;
        _lastTaskDurationInHours = hours;
    }

    public void CreateCommissionTaskChain()
    {
        DateTime dateTime;
        Utility.CustomDebugWriteLine("CreateCommissionTaskChain - _commissionRemainingTime - "+ _commissionRemainingTime);
        Utility.CustomDebugWriteLine("CreateCommissionTaskChain - _lastTaskDurationInHours - "+ _lastTaskDurationInHours);
        if (_commissionRemainingTime != TimeSpan.MinValue && _lastTaskDurationInHours == 0)
        {
            dateTime = DateTime.Now.Add(_commissionRemainingTime);
        }
        else if (_commissionRemainingTime == TimeSpan.MinValue && _lastTaskDurationInHours != 0)
        {
            dateTime = DateTime.Now.AddHours(_lastTaskDurationInHours);
        }
        else
        {
            Utility.CustomDebugWriteLine("CreateCommissionTaskChain - dateTime错误！");
            return;
        }
        Queue<TaskModel> tempQueue = new();
        tempQueue.Enqueue(new("进入委托界面", "CommissionEnter", string.Empty, ETaskType.Normal));
        tempQueue.Enqueue(new("识别委托状态", "CommissionCheckState", string.Empty, ETaskType.Normal));
        AddToWaitingTaskList(new("完成委托", dateTime, ETaskChainType.Commission, true, true, false, tempQueue));
        Utility.PrintLog("下次委托任务时间：" + dateTime.ToString("MM-dd HH:mm:ss"));
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
        }
        // 重新排序等待队列

        // 清空当前任务
        _currentTaskChainList[0].PrintSuccessLog = false;
        _currentTaskChainList.Clear();
        foreach(var e in  _waitingTaskChainList)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                e.Status = ETaskChainStatus.Waiting;
            });
        }
    }
}
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using Newtonsoft.Json.Linq;
using StellaSoraCommissionAssistant.Models;

namespace StellaSoraCommissionAssistant.Utilities;

/// <summary>
/// 自定义任务，注意：需要在TaskManager.cs中注册
/// </summary>
public static class CustomTask
{
    // 识别委托剩余时间并添加到List中
    public class CustomAddRemainingTime : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomAddRemainingTime);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            int count = GetFilteredCount(args);
            // 假定能在同一秒内执行完，不考虑误差
            for (int i = 0; i < count; i++)
            {
                string result = GetFilteredNodeContent(args, i, "text");
                TimeOnly remainingTimeOnly;
                if (string.IsNullOrEmpty(result))
                {
                    Utility.CustomDebugWriteLine($"{Name} - 识别结果result为空");
                    return false;
                }
                if (result.Contains("委托中"))
                {
                    result = result.Replace("委托中", "").Replace("：", ":");
                    try
                    {
                        remainingTimeOnly = TimeOnly.ParseExact(result, "HH:mm:ss");
                    }
                    catch (Exception)
                    {
                        Utility.CustomDebugWriteLine($"CustomAddRemainingTime() - 识别结果格式错误，无法解析为TimeOnly - {result}");
                        return false;
                    }
                }
                else
                {
                    Utility.CustomDebugWriteLine($"{Name} - 识别结果不包含'委托中'！");
                    if (result.Contains("完成"))
                    {
                        // 再次添加一个识别任务
                        Utility.CustomDebugWriteLine("任务恰好刚刚完成，剩余时间设置为1分钟。最终会自动添加一个任务，1分钟后执行");
                        remainingTimeOnly = TimeOnly.Parse("00:01:00");
                    }
                    else
                    {
                        Utility.CustomDebugWriteLine($"{Name} - 识别结果也不包含完成'！");
                        return false;
                    }
                }
                Utility.CustomDebugWriteLine("委托剩余时间 " + remainingTimeOnly.ToString("HH:mm:ss"));
                // 读取参数
                JObject? root = JObject.Parse(args.ActionParam);
                if (root == null)
                {
                    Utility.CustomDebugWriteLine("参数 识别结果的json解析为空 | JObject is null");
                    return false;
                }
                bool isCustomDispatch = root["IsCustomDispatch"]?.ToObject<bool>() ?? false;

                TaskManager.Instance.AddCommissionCompleteTime(DateTime.Now + remainingTimeOnly.ToTimeSpan(), isCustomDispatch);
            }
            // 重置hit次数，修改max_hit数值
            context.ClearHitCount("Commission@RecogniseLabel");
            context.GetNodeData("Commission@RecogniseLabel", out string? data);
            JObject? dateRoot = JObject.Parse(data ?? "");
            if (dateRoot == null)
            {
                Utility.CustomDebugWriteLine("识别结果的json解析为空 | JObject is null");
                return false;
            }
            var currentMaxHitJToken = dateRoot["max_hit"];
            if (currentMaxHitJToken == null)
            {
                Utility.CustomDebugWriteLine("找不到max_hit节点");
                return false;
            }
            var currentMaxHit = (uint)currentMaxHitJToken;
            if (currentMaxHit > 4)
            {
                Utility.CustomDebugWriteLine("Commission@RecogniseLabel节点的currentMaxHit>4");
                return false;
            }
            int newMaxHit = (int)currentMaxHit - count;
            if (newMaxHit <= 0)
            {
                context.OverridePipeline("{\"Commission@GoToNextClass\":{\"max_hit\":0}}");
            }
            context.OverridePipeline("{\"Commission@RecogniseLabel\":{\"max_hit\":"+ newMaxHit + "}}");
            return true;
        }
    }

    // 用于再次派遣界面识别上一次任务的持续时间
    public class CustomRecogniseLastTaskDuration : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomRecogniseLastTaskDuration);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredNodeContent(args, 0, "text");
            if (string.IsNullOrEmpty(result))
            {
                Utility.CustomDebugWriteLine($"{Name} - 识别结果result为空");
                return false;
            }
            if (!result.Contains("小时"))
            {
                Utility.CustomDebugWriteLine($"{Name} - 识别结果不包含'小时'！");
                return false;
            }
            result = result.Replace("小时", "");
            int hours;
            try
            {
                hours = int.Parse(result);
            }
            catch (Exception)
            {
                Utility.CustomDebugWriteLine($"CustomRecogniseLastTaskDuration() - 识别结果格式错误，无法解析为int - {result}");
                return false;
            }
            Utility.CustomDebugWriteLine($"上次委托时间{hours}小时");
            TaskManager.Instance.SetLastTaskDurationInHours(hours);
            return true;
        }
    }

    public class CustomCreateCommissionTaskChain : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomCreateCommissionTaskChain);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            if (TaskManager.Instance.CreateCommissionTaskChain())
            {
                return true;
            }
            return false;
        }
    }

    public class CustomDispatchFailed : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomDispatchFailed);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            Utility.PrintError("委托派遣失败，未满足委托要求或时长设置错误");
            return true;
        }
    }

    public class CustomLogFriendEnergy : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomLogFriendEnergy);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredNodeContent(args, 0, "text");
            Utility.PrintLog("好友体力已领取" + result);
            return true;
        }
    }

    public class CustomRepeatedLoginStopTask : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomRepeatedLoginStopTask);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            if (ProgramDataModel.Instance.SettingsData.IsStopTaskWhenLoginRepeatedly)
            {

                Utility.PrintLog("发现重复登录，即将停止任务");
                TaskManager.Instance.Stop(true);
            }
            else
            {
                Utility.PrintLog("发现重复登录，重新登录中");
            }
            return true;
        }
    }

    public class CustomMaintenanceDelayTaskChain : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomMaintenanceDelayTaskChain);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredNodeContent(args, 0, "text");
            DateTime t;
            try
            {
                t = DateTime.ParseExact(result, "yyyy-MM-dd HH:mm:ss", null);
            }
            catch (Exception)
            {
                Utility.CustomDebugWriteLine($"CustomMaintenanceDelayTaskChain() - 识别结果格式错误，无法解析为DateTime - {result}");
                return false;
            }
            Utility.PrintLog("服务器维护中，任务将延后执行");
            TaskManager.Instance.DelayTaskChain(t);
            return true;
        }
    }

    public class CustomAppendRestartClientTask : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomAppendRestartClientTask);
        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            Utility.PrintLog("更新完成，即将自动重启客户端");
            TaskManager.Instance.AppendRestartClientTask();
            return true;
        }
    }

    // 获取filtered节点第index个元素的指定字段内容
    private static string GetFilteredNodeContent(RunArgs args, int index, string nodeName)
    {
        string result;
        if (string.IsNullOrWhiteSpace(args.RecognitionDetail.Detail))
        {
            return string.Empty;
        }
        try
        {
            JObject? root = JObject.Parse(args.RecognitionDetail.Detail);
            if (root == null)
            {
                Utility.CustomDebugWriteLine("识别结果的json解析为空 | JObject is null");
                return string.Empty;
            }
            JArray? filteredToken = root["filtered"] as JArray;
            if (filteredToken == null)
            {
                Utility.CustomDebugWriteLine("filtered节点为空 | filteredToken is null");
                return string.Empty;
            }
            var textToken = filteredToken[index][nodeName];
            if (textToken == null)
            {
                Utility.CustomDebugWriteLine($"{nodeName}节点为空 | {nodeName} is null");
                return string.Empty;
            }
            result = textToken.ToObject<string>() ?? string.Empty;
            return result;
        }
        catch (Exception e)
        {
            Utility.CustomDebugWriteLine("解析json出现异常：" + e.Message);
            return string.Empty;
        }
    }

    // 获取filtered节点长度
    private static int GetFilteredCount(RunArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.RecognitionDetail.Detail))
        {
            return 0;
        }
        try
        {
            JObject? root = JObject.Parse(args.RecognitionDetail.Detail);
            if (root == null)
            {
                Utility.CustomDebugWriteLine("识别结果的json解析为空 | JObject is null");
                return 0;
            }
            JArray? filteredToken = root["filtered"] as JArray;
            if (filteredToken == null)
            {
                Utility.CustomDebugWriteLine("filtered节点为空 | filteredToken is null");
                return 0;
            }
            return filteredToken.Count;
        }
        catch (Exception e)
        {
            Utility.CustomDebugWriteLine("解析json出现异常：" + e.Message);
            return 0;
        }
    }

    //public class CustomClientUpdateStopTask : IMaaCustomAction
    //{
    //    public string Name { get; set; } = nameof(CustomClientUpdateStopTask);
    //    public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
    //    {
    //        Utility.PrintError("游戏客户端需要更新，任务即将停止。请手动更新后再启动任务");
    //        TaskManager.Instance.Stop(true);
    //        return true;
    //    }
    //}
}

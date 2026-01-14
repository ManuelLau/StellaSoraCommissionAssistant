using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using Newtonsoft.Json.Linq;

namespace StellaSoraCommissionAssistant.Utilities;

/// <summary>
/// 自定义任务，注意：需要在TaskManager.cs中注册
/// </summary>
public static class CustomTask
{
    // 识别委托剩余时间
    public class CustomRecogniseRemainingTime : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomRecogniseRemainingTime);

        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredFirstNode(args, "text");
            if (string.IsNullOrEmpty(result))
            {
                Utility.CustomDebugWriteLine($"{Name} - 识别结果result为空");
                return false;
            }
            if (!result.Contains("委托中"))
            {
                Utility.CustomDebugWriteLine($"{Name} - 识别结果不包含'委托中'！");
                if (result.Contains("完成"))
                {
                    // 再次添加一个识别任务
                    Utility.CustomDebugWriteLine("任务恰好刚刚完成，自动添加一个任务，1分钟后执行");
                    TaskManager.Instance.SetCommissionRemainingTime(TimeSpan.FromMinutes(1));
                }
                return false;
            }
            result = result.Replace("委托中", "").Replace("：", ":");
            TimeOnly remainingTime = TimeOnly.ParseExact(result, "HH:mm:ss");
            Utility.PrintLog("委托剩余时间 " + remainingTime.ToString("HH:mm:ss"));
            TaskManager.Instance.SetCommissionRemainingTime(remainingTime.ToTimeSpan());
            return true;
        }
    }

    // 用于再次派遣界面识别上一次任务的持续时间
    public class CustomRecogniseLastTaskDuration : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomRecogniseLastTaskDuration);

        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredFirstNode(args, "text");
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
            int hours = int.Parse(result);
            Utility.CustomDebugWriteLine($"上次委托时间 {hours}小时");
            Utility.PrintLog("委托已完成");
            TaskManager.Instance.SetLastTaskDurationInHours(hours);
            return true;
        }
    }

    public class CustomCreateCommissionTaskChain : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomCreateCommissionTaskChain);

        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            TaskManager.Instance.CreateCommissionTaskChain();
            return true;
        }
    }

    public class CustomMaintenanceDelayTaskChain : IMaaCustomAction
    {
        public string Name { get; set; } = nameof(CustomMaintenanceDelayTaskChain);

        public bool Run(in IMaaContext context, in RunArgs args, in RunResults results)
        {
            string result = GetFilteredFirstNode(args, "text");
            var t = DateTime.ParseExact(result, "yyyy-MM-dd HH:mm:ss", null);
            Utility.PrintLog("服务器维护中，任务将延后执行");
            TaskManager.Instance.DelayTaskChain(t);
            return true;
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

    // 获取filtered节点第0个元素的指定字段内容
    private static string GetFilteredFirstNode(RunArgs args, string nodeName)
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
            var textToken = filteredToken[0][nodeName];
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
}

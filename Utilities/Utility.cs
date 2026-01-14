using System.Windows;

namespace StellaSoraCommissionAssistant.Utilities;

public static class Utility
{
    public static void CustomDebugWriteLine(string content)
    {
        Serilog.Log.Information("[WriteLine]  " + content);
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now}  {content}");
#endif
    }

    /// <summary>打印输出到程序的主页上</summary>
    public static void PrintLog(string content)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ViewModels.MainViewModel.Instance.LogDataList.Add(new($"{DateTime.Now:MM/dd HH:mm:ss}   ", content, false));
        });
        Serilog.Log.Information("[PrintLog]   " + content);
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now}  PrintLog - {content}");
#endif
    }
    public static void PrintError(string content)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ViewModels.MainViewModel.Instance.LogDataList.Add(new($"{DateTime.Now:MM/dd HH:mm:ss}   ", content, true));
        });
        Serilog.Log.Error("[PrintError] " + content);
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now}  PrintError - {content}");
#endif
    }

    public static void CustomGrowlInfo(string content)
    {
        Serilog.Log.Information("[GrowlInfo]  " + content);
        HandyControl.Controls.Growl.Info(content);
    }
    public static void CustomGrowlError(string content)
    {
        Serilog.Log.Error("[GrowlError] " + content);
        HandyControl.Controls.Growl.Error(content);
    }
    public static void CustomGrowlAsk(string content, Func<bool, bool> funcBeforeClose)
    {
        Serilog.Log.Information("[GrowlAsk]   " + content);
        HandyControl.Controls.Growl.Ask(content, funcBeforeClose);
    }
}

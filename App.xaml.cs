using Serilog;
using System.Windows;

namespace StellaSoraCommissionAssistant;

public partial class App : Application
{
    public App()
    {
        Log.Logger = new LoggerConfiguration()
        .WriteTo.File(Utilities.Constants.LogFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10)
        .CreateLogger();
    }
}

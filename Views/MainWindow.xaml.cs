using HandyControl.Controls;
using StellaSoraCommissionAssistant.Utilities;
using StellaSoraCommissionAssistant.ViewModels;

namespace StellaSoraCommissionAssistant.Views;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainViewModel.Instance;
        MainViewModel.Instance.AppStart();
        Closing += MainViewModel.Instance.AppClosing;
#if DEBUG
        CheckVersionIsSame();
#endif
    }

    // 屏蔽ScrollViewer的滚动事件
    private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        ScrollViewer scrollViewer = (ScrollViewer)sender;
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    public void DeleteTaskChain(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            if (button.DataContext is Models.TaskChainModel item)
                MainViewModel.Instance.WaitingTaskList.Remove(item);
        }
    }

#if DEBUG
    private static void CheckVersionIsSame()
    {
        Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (version == null)
        {
            throw new Exception("版本号为空！");
        }
        else
        {
            Version myConstantVersion = new(Constants.AppVersion + ".0");
            if (version.Major == myConstantVersion.Major && version.Minor == myConstantVersion.Minor && version.Build == myConstantVersion.Build
                == false)
            {
                throw new Exception("软件版本号前3位不一致！");
            }
        }
    }
#endif
}
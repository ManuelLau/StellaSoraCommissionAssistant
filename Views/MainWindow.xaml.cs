using HandyControl.Controls;
using StellaSoraCommissionAssistant.Utilities;
using StellaSoraCommissionAssistant.ViewModels;
using System.Windows;
using System.Windows.Input;

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
    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewer scrollViewer = (ScrollViewer)sender;
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    public void DeleteTaskChain(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            if (button.DataContext is Models.TaskChainModel item)
                MainViewModel.Instance.WaitingTaskList.Remove(item);
        }
    }

    private void TextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        TaskManager.Instance.SortWaitingTaskChainList();
    }

    // 输入日期后按下回车自动切换焦点来达到确认输入的效果
    private void TextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }

    private void MenuItemClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MainViewModel.Instance.LogDataList.Clear();
        });
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
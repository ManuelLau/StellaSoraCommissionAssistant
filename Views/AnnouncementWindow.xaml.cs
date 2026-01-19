using Microsoft.Web.WebView2.Core;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.Utilities;
using StellaSoraCommissionAssistant.ViewModels;
using System.Windows;

namespace StellaSoraCommissionAssistant.Views;

public partial class AnnouncementWindow : Window
{
    public AnnouncementWindow()
    {
        InitializeComponent();
        DataContext = ProgramDataModel.Instance;
        webView.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
        webView.EnsureCoreWebView2Async();
    }

    private void CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            string? markdownText;
            try
            {
                // 通过api获取json，解析，获取body节点内容

                //markdownText = "body节点内容";
                markdownText = "## 公告";
                markdownText = markdownText.Replace("\r\n", "\n");
            }
            catch (Exception ex)
            {
                Utility.CustomDebugWriteLine("失败：" + ex.ToString());
                markdownText = "# 网络连接出错，无法获取更新内容";
            }
            webView.CoreWebView2.NavigateToString(Utility.MarkdownToHTML(markdownText));
        }
        else
        {
            Utility.CustomDebugWriteLine($"WebView2初始化失败: {e.InitializationException.Message}");
        }
    }

    private void CheckBoxOnClick(object sender, RoutedEventArgs e)
    {
        SettingsViewModel.UpdateConfigJsonFile();
    }
}
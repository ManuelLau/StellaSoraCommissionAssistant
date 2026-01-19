using Microsoft.Web.WebView2.Core;
using StellaSoraCommissionAssistant.Utilities;
using System.IO;
using System.Windows;

namespace StellaSoraCommissionAssistant.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
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
                markdownText = File.ReadAllText(Constants.ReadmeDocPath);
            }
            catch (Exception)
            {
                Utility.CustomDebugWriteLine("读取文件README.md失败");
                markdownText = "# 读取说明文档失败\n请确保本地README.md文件完整";
            }
            webView.CoreWebView2.NavigateToString(Utility.MarkdownToHTML(markdownText));
        }
        else
        {
            Utility.CustomDebugWriteLine($"WebView2初始化失败: {e.InitializationException.Message}");
        }
    }
}
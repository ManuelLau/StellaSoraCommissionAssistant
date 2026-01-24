using StellaSoraCommissionAssistant.Utilities;
using System.IO;
using System.Windows;

namespace StellaSoraCommissionAssistant.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        await webView.EnsureCoreWebView2Async();
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
}
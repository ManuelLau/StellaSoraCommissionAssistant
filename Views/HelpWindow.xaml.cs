using StellaSoraCommissionAssistant.Utilities;
using System.IO;
using System.Windows;

namespace StellaSoraCommissionAssistant.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();

        try
        {
            markdownViewer.Markdown = File.ReadAllText(Constants.ReadmeDocPath);
        }
        catch (IOException ex)
        {
            Utility.CustomDebugWriteLine("读取文件README.md失败：" + ex.ToString());
            markdownViewer.Markdown = "# 读取说明文档失败\n请确保README.md文件完整";
        }

        //MarkdownViewer.Markdown = "# Hello World\nThis is a test.";

        //var mdText = await File.ReadAllTextAsync("Datas/Arpg.md");
        //Dispatcher.Invoke(() =>
        //{
        //    markdownViewer.Markdown = mdText;
        //});
    }
}
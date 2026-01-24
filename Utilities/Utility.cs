using Markdig;
using System.ComponentModel;
using System.IO;
using System.Reflection;
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

    // 获取枚举所有值的 Description
    public static List<string> GetEnumDescriptions<T>() where T : Enum
    {
        var descriptions = new List<string>();
        var values = Enum.GetValues(typeof(T));

        foreach (var value in values)
        {
            var field = typeof(T).GetField(value.ToString() ?? string.Empty);
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            if (attribute != null)
            {
                descriptions.Add(attribute.Description);
            }
            else
            {
                descriptions.Add(value.ToString() ?? string.Empty);
            }
        }
        return descriptions;
    }

    public static string MarkdownToHTML(string markdownText)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()  // 启用扩展（表格、任务列表等）
            .Build();
        var body = Markdown.ToHtml(markdownText, pipeline);
        return string.Format(_html, _css, body);
    }
    private static readonly string _html = @"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
            <title>""</title>
            <style>
                {0}
            </style>
        </head>
        <body>
            <article class=""markdown-body"">
                {1}
            </article>
        </body>
        </html>";
    // GitHub风格的Markdown样式
    private static readonly string _css = @"
        .markdown-body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
            font-size: 16px;
            line-height: 1.6;
            word-wrap: break-word;
            max-width: 800px;
            margin: 0 auto;
        }
        .markdown-body h1, .markdown-body h2 {
            border-bottom: 1px solid #eaecef;
            padding-bottom: 0.3em;
            margin-bottom: 16px;
        }
        .markdown-body code {
            background-color: rgba(27,31,35,0.05);
            border-radius: 3px;
            font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, Courier, monospace;
            padding: 0.2em 0.4em;
        }
        .markdown-body pre {
            background-color: #f6f8fa;
            border-radius: 3px;
            padding: 16px;
            overflow: auto;
        }
        .markdown-body blockquote {
            border-left: 4px solid #dfe2e5;
            color: #6a737d;
            padding: 0 1em;
            margin-left: 0;
        }
        .markdown-body table {
            border-collapse: collapse;
            border-spacing: 0;
        }
        .markdown-body table th, .markdown-body table td {
            border: 1px solid #dfe2e5;
            padding: 6px 13px;
        }
        .markdown-body table tr:nth-child(2n) {
            background-color: #f6f8fa;
        }";
}

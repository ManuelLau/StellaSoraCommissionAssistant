using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.ViewModels;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;

namespace StellaSoraCommissionAssistant.Utilities;

/* 更新策略：
 * -启动时更新：
 * 如果勾选了"启动软件时自动检查更新"，则每次启动软件时先检查软件版本更新
 * 如果软件有新版本则提示用户手动更新软件，且不再检查资源文件更新
 * 如果软件版本已是最新，则检查资源文件版本更新
 * 如果资源文件有更新，则提示用户。如果勾选了"自动更新资源文件"，则自动执行更新
 * -定时自动更新：
 * 如果勾选了"自动更新资源文件"，每日凌晨4点(服务器时间重置时)自动检测资源文件更新并自动更新资源文件
 * 注：更新资源文件时不包括ocr模型文件
 */
public static class UpdateTool
{
    public enum ECheckUpdateType
    {
        App,
        Resources
    }
    /// <summary>
    /// 检查软件、资源文件更新
    /// 如果软件有新版本则不检查资源文件。默认更新软件会同时更新资源文件
    /// </summary>
    /// <param name="fromMainView">入口是否为主页，是的话则需要打印Log到主界面</param>
    /// <returns>是否发现新版本</returns>
    public static void CheckBothNewVersion(bool fromMainView, out bool appCanUpdate, out bool resourcesCanUpdate)
    {
        ProgramDataModel.Instance.UpdateInfoTitle = string.Empty;
        ProgramDataModel.Instance.UpdateInfoContent = string.Empty;
        CheckNewVersion(ECheckUpdateType.App, fromMainView, out bool appIsUpToDate, out appCanUpdate, out string updateInfoTitle, out string updateInfoContent);
        if (appIsUpToDate)
        {
            CheckNewVersion(ECheckUpdateType.Resources, fromMainView, out bool resourcesIsUpToDate, out resourcesCanUpdate, out updateInfoTitle, out updateInfoContent);
            if (resourcesIsUpToDate)
            {
                ProgramDataModel.Instance.UpdateTaskInfo = "当前已是最新版本";
                if (fromMainView)
                    Utility.PrintLog(ProgramDataModel.Instance.UpdateTaskInfo);
            }
            else
            {
                ProgramDataModel.Instance.UpdateInfoTitle = updateInfoTitle;
                ProgramDataModel.Instance.UpdateInfoContent = updateInfoContent;
            }
        }
        else
        {
            ProgramDataModel.Instance.UpdateInfoTitle = updateInfoTitle;
            ProgramDataModel.Instance.UpdateInfoContent = updateInfoContent;
            resourcesCanUpdate = false;
        }
    }

    public static void CheckNewVersion(ECheckUpdateType checkUpdateType, bool fromMainView, 
        out bool isUpToDate, out bool canUpdate, out string updateInfoTitle, out string updateInfoContent)
    {
        isUpToDate = false;
        canUpdate = false;
        updateInfoTitle = string.Empty;
        updateInfoContent = string.Empty;
        // 获取并检查Version是否非空
        Version? localAppVersion = null;
        Version? localResourceVersion = null;
        if (checkUpdateType == ECheckUpdateType.App)
        {
            localAppVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (localAppVersion is null)
            {
                ProgramDataModel.Instance.UpdateTaskInfo = "获取当前软件版本失败";
                if (fromMainView)
                    Utility.PrintLog(ProgramDataModel.Instance.UpdateTaskInfo);
                Utility.CustomDebugWriteLine("获取当前软件版本失败，LocalVersion为空");
                return;
            }
        }
        else
        {
            localResourceVersion = new(ProgramDataModel.Instance.ResourcesVersion);
            if (localResourceVersion is null)
            {
                ProgramDataModel.Instance.UpdateTaskInfo = "获取当前资源文件版本失败";
                if (fromMainView)
                    Utility.PrintLog(ProgramDataModel.Instance.UpdateTaskInfo);
                Utility.CustomDebugWriteLine("获取当前资源文件版本失败，LocalResourceVersion为空");
                return;
            }
        }
        // 获取版本号
        string apiUrl;
        if (checkUpdateType == ECheckUpdateType.App)
        {
            apiUrl = ProgramDataModel.Instance.SettingsData.DownloadSource == EDownloadSource.Gitee ? Constants.AppGiteeApiUrl : Constants.AppGitHubApiUrl;
        }
        else
        {
            apiUrl = ProgramDataModel.Instance.SettingsData.DownloadSource == EDownloadSource.Gitee ? Constants.ResourcesGiteeApiUrl : Constants.ResourcesGitHubApiUrl;
        }
        if (!GetLatestVersionAndDownloadUrl(apiUrl, Constants.PlatformTag, fromMainView, out string latestVersionString, out _, out updateInfoTitle, out updateInfoContent))
        {
            if (checkUpdateType == ECheckUpdateType.App)
            {
                if (fromMainView)
                    Utility.PrintLog("检查软件新版本时网络请求出错");
                Utility.CustomDebugWriteLine("检查软件新版本时网络请求出错");
            }
            else
            {
                if (fromMainView)
                    Utility.PrintLog("检查资源文件新版本时网络请求出错");
                Utility.CustomDebugWriteLine("检查资源文件新版本时网络请求出错");
            }
            return;
        }
        // 比较版本号大小
        Version latestVersion = new(RemoveFirstLetterV(latestVersionString));
        if (checkUpdateType == ECheckUpdateType.App)
        {
            Utility.CustomDebugWriteLine($"App - LocalVersion:{localAppVersion} | LatestVersion:{latestVersion}");
            if (localAppVersion != null && localAppVersion.CompareTo(latestVersion) >= 0)
            {
                isUpToDate = true;
            }
            else
            {
                canUpdate = true;
                ProgramDataModel.Instance.UpdateTaskInfo = $"发现软件新版本{latestVersionString}，是否更新？";
                if (fromMainView)
                    Utility.PrintLog($"发现软件新版本{latestVersionString}，请前往设置页面手动更新");
                Utility.CustomDebugWriteLine($"发现软件新版本 {latestVersionString}");
            }
        }
        else
        {
            Utility.CustomDebugWriteLine($"Resource - LocalVersion:{localResourceVersion} | LatestVersion:{latestVersion}");
            if (localResourceVersion != null && localResourceVersion.CompareTo(latestVersion) >= 0)
            {
                isUpToDate = true;
            }
            else
            {
                canUpdate = true;
                ProgramDataModel.Instance.UpdateTaskInfo = $"发现资源文件新版本{latestVersionString}，是否更新？";
                if (fromMainView)
                {
                    Utility.PrintLog($"发现资源文件新版本{latestVersionString}，请前往设置页面手动更新");
                }
                else
                {
                    Utility.CustomDebugWriteLine($"发现资源文件新版本{latestVersionString}");
                }
                
            }
        }
    }

    /// <summary>
    /// 通过api获取最新的版本号及其下载链接，不论是否为pre-release版本，此方法获取的json体积更小，更节省资源
    /// <para>读取tag_name作为版本号，上传Release的时候请注意正确填写tag，格式为"v1.1.1"</para>
    /// <para>读取browser_download_url作为下载链接，请注意上传的文件要为.zip .7z .rar等压缩文件</para>
    /// <para>platformTag字段为文件名中含有的平台标识，根据自己的需求填写</para>
    /// </summary>
    /// <param name="apiUrl">Api链接，不要带/latest后缀</param>
    /// <param name="fromMainView">执行入口是否为主页。从主页执行，需要跳过url、updateinfo</param>
    /// <returns>获取失败返回false</returns>
    private static bool GetLatestVersionAndDownloadUrl(string apiUrl, string platformTag, bool fromMainView, 
        out string latestVersionString, out string downloadUrl, out string updateInfoTitle, out string updateInfoContent)
    {
        latestVersionString = string.Empty;
        downloadUrl = string.Empty;
        updateInfoTitle = string.Empty;
        updateInfoContent = string.Empty;
        ProgramDataModel.Instance.IsCheckingNewVersion = true;
        ProgramDataModel.Instance.UpdateTaskInfo = "正在检查更新...";
        Utility.CustomDebugWriteLine($"正在检查更新,apiUrl = {apiUrl}");
        apiUrl += "/latest"; //获取最新版本，不论是否为pre-release
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json");

        try
        {
            using var response = httpClient.GetAsync(apiUrl).Result;
            if (response.IsSuccessStatusCode)
            {
                using var read = response.Content.ReadAsStringAsync();
                read.Wait();
                string jsonString = read.Result;
                JObject json = JObject.Parse(jsonString);
                if (json == null)
                {
                    Utility.CustomDebugWriteLine("获取的Json为空");
                    ProgramDataModel.Instance.IsCheckingNewVersion = false;
                    return false;
                }
                var tagNameToken = json["tag_name"];
                if (tagNameToken == null)
                {
                    Utility.CustomDebugWriteLine("获取的tag_name为空");
                    ProgramDataModel.Instance.IsCheckingNewVersion = false;
                    return false;
                }
                latestVersionString = tagNameToken.ToString();
                if (!fromMainView)
                {
                    // 读取下载链接
                    if (json["assets"] is JArray assetsJsonArray && assetsJsonArray.Count > 0)
                    {
                        foreach (var assetJsonObject in assetsJsonArray)
                        {
                            string? browserDownloadUrl = assetJsonObject["browser_download_url"]?.ToString();
                            if (!string.IsNullOrEmpty(browserDownloadUrl))
                            {
                                if (browserDownloadUrl.Contains(platformTag))
                                {
                                    if (browserDownloadUrl.EndsWith(".zip") || browserDownloadUrl.EndsWith(".7z") || browserDownloadUrl.EndsWith(".rar"))
                                    {
                                        downloadUrl = browserDownloadUrl;
                                        break;
                                    }
                                }
                                else
                                {
                                    ProgramDataModel.Instance.UpdateTaskInfo = "错误:返回的json中找不到对应的PlatformTag";
                                    Utility.CustomDebugWriteLine("返回的json中找不到对应的PlatformTag");
                                }
                            }
                        }
                    }
                    // 读取更新内容
                    var body = json["body"];
                    if (body is null)
                    {
                        Utility.CustomDebugWriteLine("获取的body为空");
                        ProgramDataModel.Instance.IsCheckingNewVersion = false;
                        return false;
                    }
                    string[] strs = body.ToString().Split("\r\n", 2);
                    updateInfoTitle = strs[0].Replace("## ", "");
                    updateInfoContent = strs.Length > 1 ? strs[1] : "";
                }
            }
            else
            {
                ProgramDataModel.Instance.UpdateTaskInfo = $"检查新版本时网络请求出错-{response.StatusCode}";
                Utility.CustomDebugWriteLine($"检查新版本时网络请求出错: {response.StatusCode} - {response.ReasonPhrase}");
                ProgramDataModel.Instance.IsCheckingNewVersion = false;
                return false;
            }
        }
        catch (Exception e)
        {
            ProgramDataModel.Instance.UpdateTaskInfo = "检查新版本时网络请求出错";
            Utility.CustomDebugWriteLine($"检查新版本时网络请求出错: {e.Message}");
            ProgramDataModel.Instance.IsCheckingNewVersion = false;
            return false;
        }
        finally
        {
            httpClient.Dispose();
        }
        ProgramDataModel.Instance.IsCheckingNewVersion = false;
        return true;
    }

    /// <summary>去除版本号前的v或者V</summary>
    private static string RemoveFirstLetterV(string input)
    {
        if (!string.IsNullOrEmpty(input) && (input[0] == 'v' || input[0] == 'V'))
        {
            return input.Substring(1);
        }
        return input;
    }

    public static async Task<string> UpdateApp()
    {
        string apiUrl = ProgramDataModel.Instance.SettingsData.DownloadSource == EDownloadSource.Gitee ? Constants.AppGiteeApiUrl : Constants.AppGitHubApiUrl;
        if (!GetLatestVersionAndDownloadUrl(apiUrl, Constants.PlatformTag, false, out string latestVersionString, out string downloadUrl, out _, out _))
        {
            ProgramDataModel.Instance.HasNewVersion = false;
            Utility.CustomDebugWriteLine("检查软件新版本号出错! - UpdateApp()");
            return string.Empty;
        }
        Utility.CustomDebugWriteLine($"开始下载软件更新 - 由v{Constants.AppVersion}更新至{latestVersionString}");
        Utility.CustomDebugWriteLine("DownloadUrl:" + downloadUrl);
        // 创建临时文件存放路径temp\
        var tempFileDirectory = @".\temp";
        if (Directory.Exists(tempFileDirectory))
        {
            Directory.Delete(tempFileDirectory, true);
            Directory.CreateDirectory(tempFileDirectory);
        }
        else
        {
            Directory.CreateDirectory(tempFileDirectory);
        }
        string tempFileName = GetFileNameFromUrl(downloadUrl);
        // 下载+解压文件到temp\
        MainViewModel.Instance.ProgramData.IsDownloadingFiles = true;
        MainViewModel.Instance.ProgramData.UpdateTaskInfo = $"正在下载{tempFileName}";
        if (!await DownloadAndExtractFile(downloadUrl, tempFileDirectory, tempFileName))
        {
            MainViewModel.Instance.ProgramData.UpdateTaskInfo = "文件下载失败！";
            Utility.CustomDebugWriteLine("文件下载失败");
            MainViewModel.Instance.ProgramData.IsDownloadingFiles = false;
            return string.Empty;
        }
        MainViewModel.Instance.ProgramData.UpdateTaskInfo = $"文件下载完成，是否重启并更新软件";
        Utility.CustomDebugWriteLine($"{tempFileName}下载+解压完成");
        MainViewModel.Instance.ProgramData.IsDownloadingFiles = false;
        MainViewModel.Instance.ProgramData.IsReadyForApplyUpdate = true;
        MainViewModel.Instance.SetUpdateWindowTopmost(true);
        return tempFileName;
    }

    // 资源文件更新，下载+解压+覆盖文件
    public static async Task UpdateResource(bool needPrintLog)
    {
        string apiUrl = ProgramDataModel.Instance.SettingsData.DownloadSource == EDownloadSource.Gitee ? Constants.ResourcesGiteeApiUrl : Constants.ResourcesGitHubApiUrl;
        if (!GetLatestVersionAndDownloadUrl(apiUrl, Constants.PlatformTag, false, out string latestVersionString, out string downloadUrl, out _, out _))
        {
            ProgramDataModel.Instance.HasNewVersion = false;
            if (needPrintLog)
                Utility.PrintLog("检查资源文件新版本号出错");
            Utility.CustomDebugWriteLine("检查资源文件新版本号出错! - UpdateResource()");
            return;
        }
        Utility.CustomDebugWriteLine($"开始下载资源文件更新 - 由v{ProgramDataModel.Instance.ResourcesVersion}更新至{latestVersionString}");
        Utility.CustomDebugWriteLine("DownloadUrl:" + downloadUrl);
        // 创建临时文件存放路径temp\
        var tempFileDirectory = @".\temp";
        if (Directory.Exists(tempFileDirectory))
        {
            Directory.Delete(tempFileDirectory, true);
            Directory.CreateDirectory(tempFileDirectory);
        }
        else
        {
            Directory.CreateDirectory(tempFileDirectory);
        }
        string tempFileName = GetFileNameFromUrl(downloadUrl);
        // 下载+解压文件到temp\
        MainViewModel.Instance.ProgramData.IsDownloadingFiles = true;
        if (needPrintLog)
            Utility.PrintLog($"正在下载并自动更新资源文件{latestVersionString}...");
        MainViewModel.Instance.ProgramData.UpdateTaskInfo = $"正在下载并自动更新资源文件{latestVersionString}...";
        if (!await DownloadAndExtractFile(downloadUrl, tempFileDirectory, tempFileName))
        {
            if (needPrintLog)
                Utility.PrintLog($"资源文件文件下载失败");
            MainViewModel.Instance.ProgramData.UpdateTaskInfo = "资源文件文件下载失败！";
            Utility.CustomDebugWriteLine("资源文件文件下载失败");
            MainViewModel.Instance.ProgramData.IsDownloadingFiles = false;
            return;
        }
        MainViewModel.Instance.ProgramData.UpdateTaskInfo = $"资源文件下载完成，正在自动更新...";
        Utility.CustomDebugWriteLine($"{tempFileName}下载+解压完成，自动更新资源文件");

        // 生成update.bat文件执行文件删除+复制。不处理model文件夹
        var utf8Bytes = Encoding.UTF8.GetBytes(AppContext.BaseDirectory);
        var utf8BaseDirectory = Encoding.UTF8.GetString(utf8Bytes);
        var batFilePath = Path.Combine(utf8BaseDirectory, "temp", "update.bat");
        var extractedPath = $"\"{utf8BaseDirectory}temp\\{Path.GetFileNameWithoutExtension(tempFileName)}\\*.*\"";
        var targetPath = $"\"{utf8BaseDirectory}resources\"";
        await using (StreamWriter sw = new(batFilePath))
        {
            await sw.WriteLineAsync("@echo off");
            await sw.WriteLineAsync($"cd /d {targetPath}");
            await sw.WriteLineAsync("for /d %%D in (*) do (");
            await sw.WriteLineAsync("    if exist \"%%D\\model\" (");
            await sw.WriteLineAsync("        for /d %%F in (\"%%D\\*\") do (");
            await sw.WriteLineAsync("            if /i not \"%%~nxF\" == \"model\" (");
            await sw.WriteLineAsync("                rmdir /s /q \"%%F\"");
            await sw.WriteLineAsync("            )");
            await sw.WriteLineAsync("        )");
            await sw.WriteLineAsync("        for %%G in (\"%%D\\*.*\") do ( ");
            await sw.WriteLineAsync("            del /q \"%%G\"");
            await sw.WriteLineAsync("        )");
            await sw.WriteLineAsync("    ) else (");
            await sw.WriteLineAsync("        rmdir /s /q \"%%D\"");
            await sw.WriteLineAsync("    )");
            await sw.WriteLineAsync(")");
            await sw.WriteLineAsync($"xcopy /e /y {extractedPath} {targetPath}");
            await sw.WriteLineAsync("rd /s /q \"..\\temp\"");
        }
        var psi = new ProcessStartInfo(batFilePath)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
        if (needPrintLog)
            Utility.PrintLog($"资源文件更新完成");
        MainViewModel.Instance.ProgramData.UpdateTaskInfo = "资源文件更新完成";
        Utility.CustomDebugWriteLine($"资源文件更新完成");
        MainViewModel.Instance.ProgramData.IsDownloadingFiles = false;
        MainViewModel.Instance.ProgramData.HasNewVersion = false;
        ProgramDataModel.Instance.ResourcesVersion = RemoveFirstLetterV(latestVersionString);
    }

    private static string GetFileNameFromUrl(string downloadUrl)
    {
        // 下载地址最后部分内容则为文件名，如果不符合规则，则使用默认文件名与格式TempFile.zip
        string tempFileName = "TempFile.zip";
        int lastIndex = downloadUrl.LastIndexOf('/');
        if (lastIndex != -1 && lastIndex < downloadUrl.Length - 1)
        {
            tempFileName = downloadUrl.Substring(lastIndex + 1);
        }
        return tempFileName;
    }

    /// <summary>
    /// 下载并解压文件，解压支持.zip .rar .7z
    /// </summary>
    private static async Task<bool> DownloadAndExtractFile(string url, string tempFileDirectory, string tempFileName)
    {
        string tempFilePath = Path.Combine(tempFileDirectory, tempFileName);
        Utility.CustomDebugWriteLine(tempFilePath);
        MainViewModel.Instance.ProgramData.DownloadProgress = 0;
        MainViewModel.Instance.ProgramData.DownloadedSizeInfo = "";
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long? contentLength = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
            byte[] buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead = 0;
            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                totalRead += bytesRead;

                if (contentLength.HasValue)
                {
                    double percentage = ((double)totalRead / contentLength.Value) * 100;
                    string totalReadMB = ((double)totalRead / 1024f / 1024f).ToString("0.00");
                    string contentLengthMB = ((double)contentLength / 1024f / 1024f).ToString("0.00");
                    MainViewModel.Instance.ProgramData.DownloadProgress = percentage;
                    MainViewModel.Instance.ProgramData.DownloadedSizeInfo = $"已下载 {totalReadMB}MB / {contentLengthMB}MB";
                }
                // 保存文件
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }
            fileStream.Close();

            // 解压文件
            var extractDir = Path.Combine(tempFileDirectory, Path.GetFileNameWithoutExtension(tempFileName));
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }
            if (!File.Exists(tempFilePath))
            {
                Utility.CustomDebugWriteLine("找不到已下载的文件!");
                return false;
            }
            switch (Path.GetExtension(tempFilePath))
            {
                case ".zip":
                    ZipFile.ExtractToDirectory(tempFilePath, extractDir);
                    break;
                case ".rar":
                case ".7z":
                    Directory.CreateDirectory(extractDir);
                    var archive = ArchiveFactory.Open(tempFilePath);
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            if (!Directory.Exists(extractDir))
                            {
                                Directory.CreateDirectory(extractDir);
                            }
                            entry.WriteToDirectory(extractDir, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                    break;
            }
        }
        catch (HttpRequestException httpEx)
        {
            Utility.CustomDebugWriteLine($"HTTP请求出现异常: {httpEx.Message}");
            return false;
        }
        catch (IOException ioEx)
        {
            Utility.CustomDebugWriteLine($"文件操作出现异常: {ioEx.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Utility.CustomDebugWriteLine($"出现未知异常: {ex.Message}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 应用更新。替换文件+重启软件
    /// </summary>
    public static async void ApplyUpdate(string tempFileName)
    {
        Utility.CustomDebugWriteLine("开始应用更新");
        if (!Directory.Exists(Path.Combine(@".\temp", Path.GetFileNameWithoutExtension(tempFileName))))
        {
            Utility.CustomGrowlError("解压的文件不存在!");
            return;
        }
        // 把不提醒公告的设置改为false
        MainViewModel.Instance.ProgramData.SettingsData.DoNotShowAnnouncementAgain = false;
        SettingsViewModel.UpdateConfigJsonFile();
        
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null)
        {
            Utility.CustomDebugWriteLine("GetEntryAssembly 失败");
            return;
        }
        // 生成update.bat文件来实现复制文件+启动应用
        var currentExeFileName = assembly.GetName().Name + ".exe";
        var utf8Bytes = Encoding.UTF8.GetBytes(AppContext.BaseDirectory);
        var utf8BaseDirectory = Encoding.UTF8.GetString(utf8Bytes);
        var batFilePath = Path.Combine(utf8BaseDirectory, "temp", "update.bat");
        var extractedPath = $"\"{utf8BaseDirectory}temp\\{Path.GetFileNameWithoutExtension(tempFileName)}\\*.*\"";
        var targetPath = $"\"{utf8BaseDirectory}\"";
        await using (StreamWriter sw = new(batFilePath))
        {
            await sw.WriteLineAsync("@echo off");
            await sw.WriteLineAsync("chcp 65001");
            await sw.WriteLineAsync("ping 127.0.0.1 -n 3 > nul");
            await sw.WriteLineAsync($"cd /d {targetPath}");
            await sw.WriteLineAsync("for /d %%D in (*) do (");
            await sw.WriteLineAsync("    if /i not \"%%D\"==\"config\" if /i not \"%%D\"==\"debug\" if /i not \"%%D\"==\"images\" if /i not \"%%D\"==\"temp\" (");
            await sw.WriteLineAsync("        rd /s /q \"%%D\"");
            await sw.WriteLineAsync("    )");
            await sw.WriteLineAsync(")");
            await sw.WriteLineAsync("del /q \"*\"");
            await sw.WriteLineAsync($"xcopy /e /y {extractedPath} {targetPath}");
            await sw.WriteLineAsync($"start /d \"{utf8BaseDirectory}\" {currentExeFileName}");
            await sw.WriteLineAsync("ping 127.0.0.1 -n 1 > nul");
            await sw.WriteLineAsync("rd /s /q \"temp\"");
        }
        var psi = new ProcessStartInfo(batFilePath)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(psi);
        Application.Current.Shutdown();
    }
}
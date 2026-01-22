using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Models;
using StellaSoraCommissionAssistant.Utilities;
using System.IO;

namespace StellaSoraCommissionAssistant.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public List<string> ClientTypeSettingOptionsText { get; set; }
    public List<string> CommissionDispatchTypeSettingOptionsText { get; set; }
    public List<string> CommissionDurationSettingOptionsText { get; set; }
    public List<string> CommissionTypeSettingOptionsText { get; set; }

    [ObservableProperty]
    public ProgramDataModel programData = ProgramDataModel.Instance;

    public SettingsViewModel()
    {
        // 初始化设置选项文本
        ClientTypeSettingOptionsText = Utility.GetEnumDescriptions<EClientTypeSettingOptions>();
        CommissionDispatchTypeSettingOptionsText = Utility.GetEnumDescriptions<ECommissionDispatchTypeSettingOptions>();
        CommissionDurationSettingOptionsText = [];
        foreach (var item in Enum.GetValues(typeof(ECommissionDurationSettingOptions)))
        {
            CommissionDurationSettingOptionsText.Add(((int)item).ToString());
        }
        CommissionTypeSettingOptionsText = Utility.GetEnumDescriptions<ECommissionTypeSettingOptions>();

        //查找config.json,如果没有则使用默认的规则生成配置文件
        if (File.Exists(Constants.ConfigJsonFilePath))
        {
            LoadConfigJsonFile();
            UpdateConfigJsonFile();
        }
        else
        {
            Directory.CreateDirectory(Constants.ConfigJsonDirectory);
            File.Create(Constants.ConfigJsonFilePath).Close();
            UpdateConfigJsonFile();
        }
    }

    //从config.json文件中读取配置
    private static void LoadConfigJsonFile()
    {
        string settingsJson = File.ReadAllText(Constants.ConfigJsonFilePath);
        SettingsDataModel? settingsData = JsonConvert.DeserializeObject<SettingsDataModel>(settingsJson);
        if (settingsData == null)
        {
            throw new Exception("无法读取config.json");
        }
        ProgramDataModel.Instance.SettingsData = settingsData;
    }

    //更新配置文件，把当前的配置写入json
    public static void UpdateConfigJsonFile()
    {
        string formattedJson = JsonConvert.SerializeObject(ProgramDataModel.Instance.SettingsData, Formatting.Indented);
        File.WriteAllText(Constants.ConfigJsonFilePath, formattedJson);
    }

    [RelayCommand]
    public void SelectGamePath()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Executable Files (*.exe)|*.exe",
            Title = "选择客户端路径"
        };

        // 显示对话框并检查用户是否选择了文件
        if (openFileDialog.ShowDialog() == true)
        {
            ProgramData.SettingsData.GamePath = openFileDialog.FileName;
            UpdateConfigJsonFile();
        }
    }

    [RelayCommand]
    public static void OpenUpdateWindow()
    {
        MainViewModel.Instance.OpenUpdateWindow();
    }

    [RelayCommand]
    public static void OpenHelpWindow()
    {
        MainViewModel.Instance.OpenHelpWindow();
    }
}

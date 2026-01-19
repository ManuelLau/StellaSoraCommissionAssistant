using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Utilities;

namespace StellaSoraCommissionAssistant.Models;

public partial class SettingsDataModel : ObservableObject
{
    // 所有设置项目
    public int ClientTypeSettingIndex { get; set; } = (int)EClientTypeSettingOptions.Zh_CN_PC;
    [ObservableProperty]
    public int commissionDispatchTypeSettingIndex = (int)ECommissionDispatchTypeSettingOptions.Repeat;
    public int CommissionDurationSettingIndex { get; set; } = (int)ECommissionDurationSettingOptions.Hours4;
    public int[] CommissionTaskTypeSettingIndexs { get; set; } = [0, 0, 0, 0];
    public bool IsAcquiceFriendEnergy { get; set; } = false;
    public bool IsCloseGameAfterFinishingTask { get; set; } = false;
    public bool IsAutoCheckAppUpdate { get; set; } = false;
    public bool IsAutoUpdateResources { get; set; } = false;
    [ObservableProperty][JsonIgnore]
    public string gamePath = string.Empty;
    [ObservableProperty][JsonIgnore]
    public int autoRunDeviceWaittingTimeSpan = Constants.AutoRunDeviceDefaultWaittingTimeSpan;
    public bool DoNotShowAnnouncementAgain { get; set; } = false;
}

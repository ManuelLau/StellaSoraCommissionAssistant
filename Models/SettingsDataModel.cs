using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Utilities;

namespace StellaSoraCommissionAssistant.Models;

public partial class SettingsDataModel : ObservableObject
{
    // 所有设置项目
    public int ClientTypeSettingIndex { get; set; } = (int)EClientTypeSettingOptions.Zh_CN;
    [ObservableProperty]
    public int commissionDispatchTypeSettingIndex = (int)ECommissionDispatchTypeSettingOptions.Repeat;
    public int CommissionDurationSettingIndex { get; set; } = (int)ECommissionDurationSettingOptions.Hours4;
    public int Commission0TypeSettingIndex { get; set; } = 0;
    public int Commission1TypeSettingIndex { get; set; } = 0;
    public int Commission2TypeSettingIndex { get; set; } = 0;
    public int Commission3TypeSettingIndex { get; set; } = 0;
    public bool IsAcquiceFriendEnergy { get; set; } = false;
    public bool IsAutoCheckAppUpdate { get; set; } = true;
    public bool IsAutoUpdateResources { get; set; } = true;
    [ObservableProperty][JsonIgnore]
    public string gamePath = string.Empty;
    [ObservableProperty][JsonIgnore]
    public int autoRunDeviceWaittingTimeSpan = Constants.AutoRunDeviceDefaultWaittingTimeSpan;
    public bool IsCloseGameAfterFinishingTask { get; set; } = false;
    public bool DoNotShowAnnouncementAgain { get; set; } = false;
}

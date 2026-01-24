using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Utilities;

namespace StellaSoraCommissionAssistant.Models;

public partial class SettingsDataModel : ObservableObject
{
    public int ClientTypeSettingIndex { get; set; } = (int)EClientTypeSettingOptions.Zh_CN_PC;
    [ObservableProperty]
    public int commissionDispatchTypeSettingIndex = (int)ECommissionDispatchTypeSettingOptions.Repeat;
    public int CommissionDurationSettingIndex { get; set; } = (int)ECommissionDurationSettingOptions.Hours12;
    public int[] CommissionTaskTypeSettingIndexs { get; set; } = [6, 7, 8, 12];
    public bool IsAcquireFriendEnergy { get; set; } = false;
    public TimeOnly AcquireFriendEnergyTime { get; set; } = TimeOnly.Parse("23:30:00");
    public bool IsCloseGameAfterFinishingTask { get; set; } = false;
    public bool IsStopTaskWhenLoginRepeatedly { get; set; } = true;
    public bool IsAutoCheckAppUpdate { get; set; } = false;
    [ObservableProperty][JsonIgnore]
    public string gamePath = string.Empty;
    [ObservableProperty][JsonIgnore]
    public int autoRunDeviceWaittingTimeSpan = Constants.AutoRunDeviceDefaultWaittingTimeSpan;
    public EDownloadSource DownloadSource { get; set; } = EDownloadSource.Gitee;
    public bool DoNotShowAnnouncementAgain { get; set; } = false;
}

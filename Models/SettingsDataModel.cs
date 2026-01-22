using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StellaSoraCommissionAssistant.Utilities;

namespace StellaSoraCommissionAssistant.Models;

public partial class SettingsDataModel : ObservableObject
{
    public int ClientTypeSettingIndex { get; set; } = (int)EClientTypeSettingOptions.Zh_CN_PC;
    [ObservableProperty]
    public int commissionDispatchTypeSettingIndex = (int)ECommissionDispatchTypeSettingOptions.Repeat;
    public int CommissionDurationSettingIndex { get; set; } = (int)ECommissionDurationSettingOptions.Hours4;
    public int[] CommissionTaskTypeSettingIndexs { get; set; } = [0, 0, 0, 0];
    public bool IsAcquireFriendEnergy { get; set; } = false;
    public TimeOnly AcquireFriendEnergyTime { get; set; } = TimeOnly.Parse("23:30:00");
    public bool IsCloseGameAfterFinishingTask { get; set; } = false;
    public bool IsAutoCheckAppUpdate { get; set; } = false;
    [ObservableProperty][JsonIgnore]
    public string gamePath = string.Empty;
    [ObservableProperty][JsonIgnore]
    public int autoRunDeviceWaittingTimeSpan = Constants.AutoRunDeviceDefaultWaittingTimeSpan;
    public EDownloadSource DownloadSource { get; set; } = EDownloadSource.Gitee;
    public bool DoNotShowAnnouncementAgain { get; set; } = false;
}

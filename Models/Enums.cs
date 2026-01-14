using System.ComponentModel;

namespace StellaSoraCommissionAssistant.Models;

// 枚举声明

public enum EClientTypeSettingOptions
{
    [Description("官服")] Zh_CN = 0,
    [Description("B服")] Zh_CN_Bilibili,
    [Description("国际服-繁体中文")] Zh_TW,
    [Description("日服")] Jp,
    [Description("国际服-英文")] En,
};
public enum ECommissionDispatchTypeSettingOptions
{
    [Description("重复上一次")] Repeat = 0,
    [Description("自定义")] Custom,
};
public enum ECommissionDurationSettingOptions
{
    Hours4 = 0,
    Hours8 = 1,
    Hours12 = 2,
    Hours20 = 3,
};
public enum EDownloadSource
{
    Gitee,
    GitHub
}
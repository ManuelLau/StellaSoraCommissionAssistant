using System.ComponentModel;

namespace StellaSoraCommissionAssistant.Models;

// 枚举声明

public enum EClientTypeSettingOptions
{
    [Description("客户端 官服")] Zh_CN_PC,
    [Description("模拟器 官服")] Zh_CN_Emulator,
    [Description("模拟器 B服")] Zh_CN_Bilibili_Emulator,
    [Description("客户端 港澳台")] Zh_TW_PC,
    [Description("模拟器 港澳台")] Zh_TW_Emulator,
    [Description("客户端 日服")] Jp_PC,
    [Description("模拟器 日服")] Jp_Emulator,
    [Description("客户端 国际服")] En_PC,
    [Description("模拟器 国际服")] En_Emulator,
};
public enum ECommissionDispatchTypeSettingOptions
{
    [Description("重复上一次")] Repeat,
    [Description("自定义")] Custom,
};
public enum ECommissionDurationSettingOptions
{
    Hours4 = 4,
    Hours8 = 8,
    Hours12 = 12,
    Hours20 = 20,
};
public enum ECommissionTypeSettingOptions
{
    [Description("资金报酬 高级")] A1,
    [Description("经验积累 高级")] A2,
    [Description("秘纹素材 高级")] A3,
    [Description("资金报酬 中级")] A4,
    [Description("经验积累 中级")] A5,
    [Description("秘纹素材 中级")] A6,
    [Description("旅人升阶素材A 高级")] B1,
    [Description("旅人升阶素材B 高级")] B2,
    [Description("旅人升阶素材C 高级")] B3,
    [Description("旅人升阶素材A 中级")] B4,
    [Description("旅人升阶素材B 中级")] B5,
    [Description("旅人升阶素材C 中级")] B6,
    [Description("秘纹升阶素材A 高级")] C1,
    [Description("秘纹升阶素材B 高级")] C2,
    [Description("秘纹升阶素材C 高级")] C3,
    [Description("秘纹升阶素材A 中级")] C4,
    [Description("秘纹升阶素材B 中级")] C5,
    [Description("秘纹升阶素材C 中级")] C6,
    [Description("节奏游戏委托 高级")] D1,
    [Description("射击游戏委托 高级")] D2,
    [Description("功夫游戏委托 高级")] D3,
    [Description("节奏游戏委托 中级")] D4,
    [Description("射击游戏委托 中级")] D5,
    [Description("功夫游戏委托 中级")] D6,
};
public enum EDownloadSource
{
    Gitee,
    GitHub
}
namespace StellaSoraCommissionAssistant.Models;

//非Normal、RestartGame类型表示会根据设置来覆写Pipeline
public enum ETaskType
{
    Normal = 0,
    HomeScreen,
    CommissionEnter,
    CommissionCheckState,
    FriendsEnergy,
    RestartGame,
}

//单个Pipeline任务
/// <summary>不需要的string用string.Empty</summary>
public class TaskModel(string name, string entry, string pipelineOverride, ETaskType type)
{
    public string Name = name;
    /// <summary>可以根据Entry是否为empty来判断是哪种类型任务</summary>
    public string Entry { get; set; } = entry;
    public string PipelineOverride { get; set; } = pipelineOverride;
    public ETaskType Type { get; set; } = type;
}

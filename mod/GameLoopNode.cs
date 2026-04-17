using Godot;
using STS2Agent.Services;

namespace STS2Agent;

public partial class GameLoopNode : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Logger.Info("GameLoopNode: _Ready，节点已加入场景树");
    }

    public override void _Process(double delta)
    {
        STS2Agent.Update();
    }
}

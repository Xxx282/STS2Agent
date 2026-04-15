using Godot;

namespace STS2Agent;

public partial class GameLoopNode : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        STS2Agent.Log("GameLoopNode: _Ready，节点已加入场景树");
    }

    public override void _Process(double delta)
    {
        STS2Agent.Update();
    }
}

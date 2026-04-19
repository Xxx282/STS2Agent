using Godot;

namespace STS2Agent.Designer;

public partial class Main : Control
{
    public override void _Ready()
    {
        Logger.Info("[Main] UI Designer ready. Edit scenes in Godot editor, then copy tscn to mod/.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            GetTree().Quit();
    }
}

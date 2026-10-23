using Godot;

namespace PlanetLab;

// 中文：P-002.3 可玩灰盒控制器。复用 Lab 的 HUD、R 重开和测试入口；
// 只增加终点确认、越界/失去重力的自动重开和 HUD 标题，不改变运动规则。
public partial class PlaygroundLevel : Lab
{
    [Export] public NodePath GoalPath { get; set; } = "Gameplay/GoalBeacon";
    [Export] public NodePath ObjectivePath { get; set; } = "HUD/Objective";
    [Export] public NodePath StatusPath { get; set; } = "HUD/Status";
    [Export] public float RestartBelowY { get; set; } = 3200;
    [Export] public float RestartOutsideX { get; set; } = 6000;
    // A player who leaves every gravity source would otherwise float forever.
    // The only intentional gravity-free stretch is the interplanetary dash,
    // which is shorter than this and also exempted below.
    [Export] public float NoGravityRestartSeconds { get; set; } = 4;

    public bool GoalReached { get; private set; }

    private GoalZone _goal = null!;
    private Label _objective = null!;
    private Label _status = null!;
    private Player _levelPlayer = null!;
    private bool _restartQueued;
    private bool _testing;
    private float _noGravityTime;

    public override void _Ready()
    {
        base._Ready();
        _goal = GetNode<GoalZone>(GoalPath);
        _objective = GetNode<Label>(ObjectivePath);
        _status = GetNode<Label>(StatusPath);
        _levelPlayer = GetNode<Player>("Player");
        _goal.PlayerReached += OnPlayerReached;

        // A regression run must observe a failed route instead of having the
        // level silently reload and restart the test forever.
        _testing = System.Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--playground-tests");
        if (_testing)
            CallDeferred(MethodName.AddChild, new PlaygroundTests());
    }

    private void OnPlayerReached()
    {
        if (GoalReached) return;
        GoalReached = true;
        _objective.Text = "COMPLETE — capsule beacon reached.";
        _status.Text = "GOAL REACHED  ·  press R to replay";
        _status.Modulate = new Color("7ef0bb");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_testing || _restartQueued || GoalReached || !IsInstanceValid(_levelPlayer)) return;

        // 中文：离开所有重力源且不在下砸中超过 4 秒，说明玩家飘走了；自动重开，
        // 避免永远漂浮。正常的星际下砸期间 State=DownDash，不会触发。
        Vector2 position = _levelPlayer.GlobalPosition;
        bool outside = position.Y > RestartBelowY || Mathf.Abs(position.X) > RestartOutsideX;
        if (_levelPlayer.HasActiveGravity || _levelPlayer.State == Player.MovementState.DownDash)
            _noGravityTime = 0;
        else
            _noGravityTime += (float)delta;
        if (!outside && _noGravityTime <= NoGravityRestartSeconds) return;

        _restartQueued = true;
        _status.Text = outside ? "Left the lab — restarting…" : "Lost gravity — restarting…";
        GetTree().CreateTimer(0.8).Timeout += () => GetTree().ReloadCurrentScene();
    }
}

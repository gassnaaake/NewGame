using Godot;
using System.Collections.Generic;

namespace PlanetLab;

// 中文：空间重力源的统一接口。GravityLayer 是玩法优先级，不是 Godot 碰撞层；
// 数值越小优先级越高（0 最高），同层向量相加，低层不参与。
public interface IGravitySource
{
    int GravityLayer { get; }
    string GravityLabel { get; }
    bool TryGetGravity(Vector2 worldPosition, out Vector2 gravity);
}

// 中文：某一帧中某个有效重力源及其 Layer/向量，供 HUD 和调试读取。
public readonly record struct GravityContribution(
    IGravitySource Source,
    string Name,
    int Layer,
    Vector2 Vector);

// 中文：一次按位置选择的结果。列表每物理帧复用，调用方不要长期保存。
public sealed class GravitySelection
{
    public List<GravityContribution> Active { get; } = new();
    public List<GravityContribution> Contributing { get; } = new();
    public int HighestPriorityLayer { get; internal set; } = -1;
    public Vector2 Total { get; internal set; }
    public bool HasActive => Active.Count > 0;

    public void Clear()
    {
        Active.Clear();
        Contributing.Clear();
        HighestPriorityLayer = -1;
        Total = Vector2.Zero;
    }
}

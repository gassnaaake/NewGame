using Godot;

namespace PlanetLab;

// 中文：编辑器辅助。策划说明字段应当是“只读、可看、不写入场景”的元数据，
// 不通过不可靠的 Tooltip 特性实现。Godot 4.7.2 支持 _ValidateProperty。
internal static class EditorHelp
{
    public static void MakeReadOnly(Godot.Collections.Dictionary property, StringName name)
    {
        if (property["name"].AsStringName() != name) return;

        PropertyUsageFlags usage = property["usage"].As<PropertyUsageFlags>();
        usage |= PropertyUsageFlags.ReadOnly;
        usage &= ~PropertyUsageFlags.Storage;
        property["usage"] = (int)usage;
    }
}

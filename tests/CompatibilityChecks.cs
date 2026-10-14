using System.Reflection;
using System.Reflection.Emit;
using PluginChatOverlay;
using Dalamud.Plugin.Services;

internal static class CompatibilityChecks
{
    internal static void Run()
    {
        var type = typeof(IChatGui).Assembly.GetType("Dalamud.Game.Gui.ChatGui")!;
        var assembly = typeof(IChatGui).Assembly;
        Check(FragmentLayout.SupportsSchema(assembly.GetType("Dalamud.Interface.ImGuiSeStringRenderer.Internal.SeStringRenderer")!,
            assembly.GetType("Dalamud.Interface.ImGuiSeStringRenderer.Internal.TextFragment")!), "text selection accepts the loaded renderer schema without a build identifier");
        Check(!FragmentLayout.SupportsSchema(typeof(CompatibilityChecks), typeof(string)), "missing renderer fields disable private text layout access");
        Check(ChatCompatibility.Matches(type.GetMethod("HandlePrintMessageDetour", BindingFlags.Instance | BindingFlags.NonPublic)!,
            type.GetMethod("UpdateQueue", BindingFlags.Instance | BindingFlags.NonPublic)!), "loaded chat operations match independently of DLL version/hash");
        Check(!ChatCompatibility.MatchesFingerprints("changed", ChatCompatibility.QueueFingerprint)
            && !ChatCompatibility.MatchesFingerprints(ChatCompatibility.DetourFingerprint, "changed"), "changing either critical chat operation disables separation");
        Check(ChatCompatibility.Diagnose("changed", ChatCompatibility.QueueFingerprint).Contains("HandlePrintMessageDetour")
            && ChatCompatibility.Diagnose(ChatCompatibility.DetourFingerprint, "changed").Contains("UpdateQueue"),
            "debug reason identifies the incompatible chat operation");
        Check(NativeTooltipCompatibility.Diagnostic == "互換性あり", "native compatibility debug status is explicit");
        var changedParts = new Dictionary<string, string>(NativeTooltipContractDetails.Expected);
        const string changedField = "field:FFXIV.Component.GUI.AtkResNode.ScreenX";
        changedParts[changedField] = "changed";
        Check(NativeTooltipContractDetails.Explain(changedParts).Contains(changedField), "debug reason names the moved native field");
        var visibility = new NativeChatVisibility();
        Check(visibility.Diagnostic.StartsWith("未確認"), "disabled native expansion is reported as unverified instead of incompatible");
        var first = Fixture(false, 7); var rebuilt = Fixture(true, 7); var changed = Fixture(true, 8);
        Check(!first.GetMethodBody()!.GetILAsByteArray()!.SequenceEqual(rebuilt.GetMethodBody()!.GetILAsByteArray()!),
            "fixture recompilation actually renumbers unrelated metadata tokens");
        Check(MethodCompatibility.Fingerprint(first) == MethodCompatibility.Fingerprint(rebuilt),
            "unrelated assembly edits and token renumbering preserve method compatibility");
        Check(MethodCompatibility.Fingerprint(first) != MethodCompatibility.Fingerprint(changed),
            "changed executable constants invalidate method compatibility");
        Check(NativeTooltipCompatibility.Compatible && !NativeTooltipCompatibility.Matches("changed"),
            "native tooltip contract accepts compatible bindings and rejects changed fields/API");
        var original = typeof(FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode);
        string Contract(Type node) => NativeTooltipCompatibility.Fingerprint(name => name == original.FullName ? node : original.Assembly.GetType(name, true)!);
        Check(NativeTooltipCompatibility.Matches(Contract(NodeFixture(false))), "native layout contract accepts identical fields from a rebuilt assembly");
        Check(!NativeTooltipCompatibility.Matches(Contract(NodeFixture(true))), "moving a used native field disables tooltip compatibility");
        Check(!NativeTooltipCompatibility.Matches(Contract(NodeFixture(false, true))), "changing a used native API argument disables tooltip compatibility");
        Check(!NativeTooltipCompatibility.Matches(Contract(NodeFixture(false, false, 16))), "growing the allocated native node disables tooltip compatibility");
    }
    private static Type NodeFixture(bool moveField, bool changeApi = false, int growSize = 0)
    {
        var original = typeof(FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("native").DefineType(original.FullName!,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout, typeof(ValueType), PackingSize.Unspecified, original.StructLayoutAttribute!.Size + growSize);
        foreach (var name in new[] { "ScreenX", "ScreenY", "ScaleX" })
        {
            var field = original.GetField(name)!;
            var offset = field.GetCustomAttribute<System.Runtime.InteropServices.FieldOffsetAttribute>()!.Value;
            type.DefineField(name, field.FieldType, FieldAttributes.Public).SetOffset(offset + (moveField && name == "ScreenX" ? 4 : 0));
        }
        foreach (var method in original.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name is "Ctor" or "SetPositionFloat" or "GetScaleY" or "GetWidth" or "GetHeight" or "SetWidth" or "SetHeight" or "Destroy"))
        {
            var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
            if (changeApi && method.Name == "SetWidth") parameters[0] = typeof(uint);
            var generated = type.DefineMethod(method.Name, MethodAttributes.Public | (method.IsStatic ? MethodAttributes.Static : 0), method.ReturnType, parameters);
            // These methods are metadata fixtures; no native call or execution occurs.
            generated.GetILGenerator().Emit(OpCodes.Ret);
        }
        return type.CreateType()!;
    }
    private static MethodInfo Fixture(bool unrelated, int value)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("fixture");
        var type = module.DefineType("CompatibilityFixture.Operations", TypeAttributes.Public);
        if (unrelated)
        {
            var unused = type.DefineMethod("Unrelated", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            var il = unused.GetILGenerator(); il.Emit(OpCodes.Ldstr, "unrelated string");
            il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)])!); il.Emit(OpCodes.Ret);
        }
        var method = type.DefineMethod("Operation", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
        var code = method.GetILGenerator(); code.Emit(OpCodes.Ldstr, "same operation");
        code.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)])!);
        code.Emit(OpCodes.Ldc_I4, value); code.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod("Operation")!;
    }
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}

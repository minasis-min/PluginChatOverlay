using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class MaskedAudit
{
    public static void Run(string path)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(path);
        var type = assembly.MainModule.Types.Single(t => t.FullName == "MaskedDalamud.Plugin");
        var method = type.Methods.Single(m => m.Name == "OnPluginChatFilter");
        var instructions = method.Body.Instructions;
        bool Calls(string name) => instructions.Any(i => i.Operand is MethodReference m && m.Name == name);
        if (!Calls("get_GeneralChatType") || !Calls("PreventOriginal") || !Calls("get_hidePluginChat"))
            throw new Exception("Installed filter differs from inspected source");
        if (Calls("Print") || Calls("PrintError")) throw new Exception("Unexpected re-output in filter");
        var subscription = type.Methods.Any(m => m.HasBody
            && m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == "OnPluginChatFilter")
            && m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.Name == "add_ChatMessage"));
        if (!subscription) throw new Exception("First-pass filter subscription missing");
        Console.WriteLine($"PASS Masked Dalamud {assembly.Name.Version} installed filter uses channel and PreventOriginal, without Print re-output");
        if (Environment.GetEnvironmentVariable("PCO_DUMP_MASKED_IL") == "1")
            foreach (var i in instructions) Console.WriteLine(i);
    }
}

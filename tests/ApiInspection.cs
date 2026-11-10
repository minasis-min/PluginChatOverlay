using Mono.Cecil;
internal static class ApiInspection
{
    public static void Run(string directory)
    {
        using (var bindings = AssemblyDefinition.ReadAssembly(Path.Combine(directory, "Dalamud.Bindings.ImGui.dll")))
        {
            foreach (var type in bindings.MainModule.Types)
                foreach (var method in type.Methods.Where(m => m.Name.Contains("FocusWindow") || m.Name is "SetWindowFocus" or "ClearActiveID"))
                {
                    Console.WriteLine(method);
                    if (method.HasBody) foreach (var instruction in method.Body.Instructions) Console.WriteLine(instruction);
                }
            foreach (var type in bindings.MainModule.Types.Where(t => t.Name is "ImGui" or "ImGuiIOPtr"))
                foreach (var method in type.Methods.Where(m => m.Name.Contains("FocusWindow") || m.Name.Contains("SetWindowFocus") || m.Name.Contains("CaptureKeyboard"))) Console.WriteLine(method);
        }
        foreach(var pair in new[]{("Lumina.Excel.dll","Lumina.Excel.Sheets.LogKind"),("Dalamud.dll","Dalamud.Game.Text.XivChatType"),("Dalamud.Bindings.ImGui.dll","Dalamud.Bindings.ImGui.ImFontPtr")})
        {
            using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(directory,pair.Item1));
            var type=assembly.MainModule.Types.First(t=>t.FullName==pair.Item2);
            Console.WriteLine(type.FullName);
            foreach(var p in type.Properties) Console.WriteLine($"PROPERTY {p.PropertyType} {p.Name}");
            foreach(var f in type.Fields.Where(f=>f.HasConstant)) Console.WriteLine($"ENUM {f.Name}={f.Constant}");
            foreach(var m in type.Methods.Where(m=>m.Name.Contains("CalcTextSize") || m.Name.Contains("Glyph") || m.Name.Contains("Distance"))) Console.WriteLine(m);
        }
    }
    public static void PluginColors(string path)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(path);
        foreach (var type in assembly.MainModule.Types)
            foreach (var method in type.Methods.Where(m => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference target && target.Name is "Print" or "PrintError" or "AddUiForeground" or "SetMessage" or "add_ChatMessage" || i.Operand is FieldReference field && field.Name == "_songEchoMsg" || i.Operand is string text && text.Contains("of the set"))))
            {
                Console.WriteLine(method.FullName);
                foreach (var instruction in method.Body.Instructions) Console.WriteLine(instruction);
            }
    }
    public static void GameColors(string directory)
    {
        using var game=new Lumina.GameData(directory,new Lumina.LuminaOptions { DefaultExcelLanguage=Lumina.Data.Language.Japanese });
        var sheet=game.Excel.GetSheet<Lumina.Excel.Sheets.LogKind>();
        foreach(var id in new uint[]{1,2,3,10,56,57,58,60})
            Console.WriteLine($"LogKind {id}: {sheet.GetRow(id).Format.ToMacroString()}");
    }
}

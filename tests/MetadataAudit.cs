using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

internal static class MetadataAudit
{
    public static void Run(string path)
    {
        using var stream=File.OpenRead(path);
        var hash=Convert.ToHexString(SHA256.HashData(stream));
        if(hash!="C726C1DE70B76A7ACC970F48E29589CD61205F08DAEC9586D112A977583055F5") throw new Exception("DLL mismatch");
        stream.Position=0;
        using var pe=new PEReader(stream);
        var reader=pe.GetMetadataReader();
        Console.WriteLine($"DLL SHA256 {hash}; assembly {reader.GetAssemblyDefinition().Version}; MVID {reader.GetGuid(reader.GetModuleDefinition().Mvid)}");
        var type=reader.TypeDefinitions.First(h=>reader.GetString(reader.GetTypeDefinition(h).Name)=="ChatGui");
        var methods=reader.GetTypeDefinition(type).GetMethods();
        var detour=methods.First(h=>reader.GetString(reader.GetMethodDefinition(h).Name)=="HandlePrintMessageDetour");
        var update=methods.First(h=>reader.GetString(reader.GetMethodDefinition(h).Name)=="UpdateQueue");
        var il=pe.GetMethodBody(reader.GetMethodDefinition(update).RelativeVirtualAddress).GetILBytes()!;
        var called=Calls(il).ToArray();
        if(!called.Contains(MetadataTokens.GetToken(detour))) throw new Exception("UpdateQueue no direct detour call");
        Console.WriteLine("PASS installed DLL UpdateQueue directly calls HandlePrintMessageDetour");
        foreach(var h in new[]{detour,update}) {
            var m=reader.GetMethodDefinition(h);
            Console.WriteLine($"{reader.GetString(m.Name)} token 0x{MetadataTokens.GetToken(h):X8}; impl {m.ImplAttributes}; IL bytes {pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!.Length}");
        }
        var print=methods.First(h=>reader.GetString(reader.GetMethodDefinition(h).Name)=="Print");
        Console.WriteLine($"First Print token 0x{MetadataTokens.GetToken(print):X8}");
    }
    private static IEnumerable<int> Calls(byte[] il)
    {
        var opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
        for(var i=0;i<il.Length;) {
            ushort code=il[i++]; if(code==0xFE) code=(ushort)(0xFE00|il[i++]);
            var op=opcodes[code];
            if(op==OpCodes.Call || op==OpCodes.Callvirt) yield return BitConverter.ToInt32(il,i);
            i+=op.OperandType switch {
                OperandType.InlineNone=>0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
                OperandType.InlineVar=>2,
                OperandType.InlineI8 or OperandType.InlineR=>8,
                OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,i),
                _=>4
            };
        }
    }
}

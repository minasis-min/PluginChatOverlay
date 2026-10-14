using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace PluginChatOverlay;

// Compare only the audited managed operation, resolving tokens to names so unrelated
// assembly recompiles, version stamps and metadata token renumbering do not disable it.
internal static class MethodCompatibility
{
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields()
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => unchecked((ushort)o.Value));

    internal static string Fingerprint(MethodBase method)
    {
        var body = method.GetMethodBody() ?? throw new NotSupportedException("Managed body unavailable");
        var bytes = body.GetILAsByteArray()!;
        var instructions = new List<(int Offset, string Opcode, string Operand, int[]? Targets)>();
        var typeArgs = method.DeclaringType?.GetGenericArguments();
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (var p = 0; p < bytes.Length;)
        {
            var start = p;
            ushort value = bytes[p++]; if (value == 0xFE) value = (ushort)(0xFE00 | bytes[p++]);
            var op = Opcodes[value];
            string operand = ""; int[]? targets = null;
            int ReadInt() { var result = BitConverter.ToInt32(bytes, p); p += 4; return result; }
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: var delta = (sbyte)bytes[p++]; targets = [p + delta]; break;
                case OperandType.InlineBrTarget: var jump = ReadInt(); targets = [p + jump]; break;
                case OperandType.InlineSwitch:
                    var count = ReadInt(); var end = checked(p + count * 4); targets = new int[count];
                    for (var i = 0; i < count; i++) targets[i] = checked(end + ReadInt());
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    operand = Member(method.Module.ResolveMember(ReadInt(), typeArgs, methodArgs)!); break;
                case OperandType.InlineString: operand = Convert.ToHexString(Encoding.UTF8.GetBytes(method.Module.ResolveString(ReadInt()))); break;
                case OperandType.InlineSig: operand = Convert.ToHexString(method.Module.ResolveSignature(ReadInt())); break;
                case OperandType.ShortInlineI: operand = ((sbyte)bytes[p++]).ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case OperandType.ShortInlineVar: operand = bytes[p++].ToString(); break;
                case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, p).ToString(); p += 2; break;
                case OperandType.InlineI: operand = ReadInt().ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case OperandType.InlineI8: operand = Convert.ToHexString(bytes.AsSpan(p, 8)); p += 8; break;
                case OperandType.ShortInlineR: operand = Convert.ToHexString(bytes.AsSpan(p, 4)); p += 4; break;
                case OperandType.InlineR: operand = Convert.ToHexString(bytes.AsSpan(p, 8)); p += 8; break;
                default: throw new NotSupportedException("Unknown IL operand");
            }
            instructions.Add((start, op.Name!, operand, targets));
        }
        var offsets = instructions.Select((i, index) => (i.Offset, index)).ToDictionary(p => p.Offset, p => p.index);
        offsets[bytes.Length] = instructions.Count;
        var text = new StringBuilder(Member(method)).Append('|').Append(body.InitLocals);
        foreach (var local in body.LocalVariables) text.Append('|').Append(TypeName(local.LocalType)).Append(':').Append(local.IsPinned);
        foreach (var i in instructions)
        {
            var opcode = i.Targets != null && i.Opcode.EndsWith(".s", StringComparison.Ordinal) ? i.Opcode[..^2] : i.Opcode;
            text.Append('|').Append(opcode).Append(':').Append(i.Operand);
            if (i.Targets != null) foreach (var target in i.Targets) text.Append(':').Append(offsets[target]);
        }
        foreach (var region in body.ExceptionHandlingClauses)
        {
            text.Append('|').Append(region.Flags).Append(':').Append(offsets[region.TryOffset]).Append(':').Append(offsets[region.TryOffset + region.TryLength])
                .Append(':').Append(offsets[region.HandlerOffset]).Append(':').Append(offsets[region.HandlerOffset + region.HandlerLength]);
            if (region.Flags == ExceptionHandlingClauseOptions.Clause) text.Append(':').Append(TypeName(region.CatchType!));
            if (region.Flags == ExceptionHandlingClauseOptions.Filter) text.Append(':').Append(offsets[region.FilterOffset]);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    internal static string Member(MemberInfo member) => member switch
    {
        Type type => TypeName(type),
        FieldInfo field => TypeName(field.DeclaringType!) + "." + field.Name + ":" + TypeName(field.FieldType),
        MethodBase method => (method.IsStatic ? "static " : "instance ") + TypeName(method.DeclaringType!) + "." + method.Name
            + (method.IsGenericMethod ? "<" + string.Join(",", method.GetGenericArguments().Select(TypeName)) + ">" : "")
            + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")"
            + (method is MethodInfo info ? ":" + TypeName(info.ReturnType) : ""),
        _ => throw new NotSupportedException("Unsupported IL member"),
    };
    private static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!) + "&";
        if (type.IsPointer) return TypeName(type.GetElementType()!) + "*";
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsFunctionPointer) return "fn[" + type.IsUnmanagedFunctionPointer + ";"
            + string.Join(",", type.GetFunctionPointerCallingConventions().Select(TypeName)) + "](" 
            + string.Join(",", type.GetFunctionPointerParameterTypes().Select(TypeName)) + "):" + TypeName(type.GetFunctionPointerReturnType());
        if (type.IsGenericParameter) return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition;
        if (type.IsGenericType) return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
        return type.FullName ?? throw new NotSupportedException("Unnamed IL type");
    }
}

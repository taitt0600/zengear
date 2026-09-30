using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.EntityFrameworkCore;

namespace ZenGear.ArchitectureTests.Infrastructure;

/// <summary>
/// The entity writes found in the IL of an assembly (docs/ARCHITECTURE.md, rule 4): creating an entity, calling one of
/// its setters or void methods, a static method of it that returns it, <c>DbSet</c> Add/Attach/Update/Remove and
/// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>. Methods are read from the metadata tables, so private methods and the
/// compiler-generated types of lambdas and async methods are scanned too.
/// </summary>
internal static class EntityWrites
{
    private static readonly string[] DbSetWrites =
        ["Add", "AddAsync", "AddRange", "AddRangeAsync", "Attach", "AttachRange", "Update", "UpdateRange", "Remove", "RemoveRange"];

    private static readonly string[] BulkWrites = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .GroupBy(code => code.Value)
        .ToDictionary(group => group.Key, group => group.First());

    public static List<(MethodBase Caller, Type Entity)> In(Assembly assembly, Func<Type, bool> isEntity) =>
        Methods(assembly)
            .SelectMany(method => CalledMethods(method).Select(called => (Caller: method, Entity: WrittenEntity(called, isEntity))))
            .Where(write => write.Entity is not null)
            .Select(write => (write.Caller, write.Entity!))
            .ToList();

    private static Type? WrittenEntity(MethodBase called, Func<Type, bool> isEntity)
    {
        var declaring = called.DeclaringType;
        if (declaring is null)
        {
            return null;
        }

        if (isEntity(declaring))
        {
            return called switch
            {
                ConstructorInfo => declaring,
                MethodInfo { IsSpecialName: true } accessor => accessor.Name.StartsWith("set_", StringComparison.Ordinal) ? declaring : null,
                MethodInfo { IsStatic: true } factory => factory.ReturnType == declaring ? declaring : null,
                MethodInfo instance => instance.ReturnType == typeof(void) ? declaring : null,
                _ => null,
            };
        }

        if (declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(DbSet<>) && DbSetWrites.Contains(called.Name))
        {
            return EntityOrNull(declaring.GetGenericArguments()[0]);
        }

        return called.IsGenericMethod && BulkWrites.Contains(called.Name) ? EntityOrNull(called.GetGenericArguments()[0]) : null;

        Type? EntityOrNull(Type type) => isEntity(type) ? type : null;
    }

    /// <summary>Every method and constructor defined in the assembly, whatever its accessibility.</summary>
    private static List<MethodBase> Methods(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.MethodDefinitions
            .Select(handle => assembly.ManifestModule.ResolveMethod(MetadataTokens.GetToken(handle)))
            .OfType<MethodBase>()
            .ToList();
    }

    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        for (var offset = 0; offset < il.Length;)
        {
            var code = il[offset] == 0xFE ? OpCodesByValue[unchecked((short)(0xFE00 | il[offset + 1]))] : OpCodesByValue[il[offset]];
            offset += code.Size;

            if (code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj)
            {
                var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset), typeArguments, methodArguments);
                if (called is not null)
                {
                    yield return called;
                }
            }

            offset += OperandSize(code.OperandType, il, offset);
        }
    }

    private static int OperandSize(OperandType operand, byte[] il, int offset) => operand switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
        _ => 4,
    };
}

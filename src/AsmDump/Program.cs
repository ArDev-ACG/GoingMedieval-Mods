using Mono.Cecil;

// Explores the game assembly so Harmony patches can target verified members.
//   dotnet run -- types <substring>            list matching type names
//   dotnet run -- members <FullTypeName>       list fields and method signatures
//   dotnet run -- find <substring>             list methods whose name matches

var asmPath = Environment.GetEnvironmentVariable("GM_ASM") ?? @"C:\Program Files (x86)\Steam\steamapps\common\Going Medieval\Going Medieval_Data\Managed\Assembly-CSharp.dll";
var mode = args.Length > 0 ? args[0] : "types";
var needle = args.Length > 1 ? args[1] : "";

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(asmPath));
var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { AssemblyResolver = resolver });

bool Match(string s) => s.Contains(needle, StringComparison.OrdinalIgnoreCase);

if (mode == "types")
{
    foreach (var t in asm.MainModule.Types.Where(t => Match(t.FullName)).OrderBy(t => t.FullName))
        Console.WriteLine(t.FullName);
}
else if (mode == "members")
{
    var type = AllTypes(asm).FirstOrDefault(t => t.FullName == needle)
               ?? AllTypes(asm).FirstOrDefault(t => t.Name == needle);
    if (type == null) { Console.WriteLine("type not found: " + needle); return; }

    Console.WriteLine("== " + type.FullName);
    for (var b = type.BaseType; b != null; b = (b as TypeDefinition)?.BaseType ?? (b.Resolve()?.BaseType))
        Console.WriteLine("  base   " + b.FullName);
    foreach (var f in type.Fields)
        Console.WriteLine($"  field  {f.FieldType.Name} {f.Name}");
    foreach (var m in type.Methods)
    {
        var ps = string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({ps})");
    }
}
else if (mode == "cmds")
{
    foreach (var t in asm.MainModule.Types.Where(t => t.FullName.Contains("DevConsole.Command")))
    {
        var ctor = t.Methods.FirstOrDefault(m => m.IsConstructor && m.HasBody);
        if (ctor == null) continue;
        var strs = ctor.Body.Instructions
            .Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldstr)
            .Select(i => i.Operand as string)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (strs.Count == 0) continue;
        if (needle.Length > 0 && !strs.Any(Match) && !Match(t.Name)) continue;
        Console.WriteLine(string.Join("  |  ", strs));
    }
}
else if (mode == "find")
{
    foreach (var t in AllTypes(asm))
        foreach (var m in t.Methods.Where(m => Match(m.Name)))
        {
            var ps = string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name));
            Console.WriteLine($"{t.FullName}:{m.Name}({ps})");
        }
}
else if (mode == "il")
{
    // dotnet run -- il <TypeName>::<MethodName>   dump IL of a method
    var parts = needle.Split("::");
    var tn = parts[0];
    var mn = parts.Length > 1 ? parts[1] : "";
    var types = AllTypes(asm).Where(t => t.FullName == tn || t.Name == tn
                                         || t.FullName.Contains(tn, StringComparison.Ordinal)).ToList();
    if (types.Count == 0) { Console.WriteLine("type not found: " + tn); return; }
    foreach (var m in types.SelectMany(t => t.Methods)
                           .Where(m => mn.Length == 0 || m.Name.Contains(mn, StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("== " + m.FullName);
        if (!m.HasBody) { Console.WriteLine("  (no body)"); continue; }
        foreach (var i in m.Body.Instructions)
            Console.WriteLine("  " + i);
    }
}
else if (mode == "enum")
{
    // dotnet run -- enum <TypeName>   list an enum's members with their values
    var type = AllTypes(asm).FirstOrDefault(t => t.FullName == needle)
               ?? AllTypes(asm).FirstOrDefault(t => t.Name == needle);
    if (type == null) { Console.WriteLine("type not found: " + needle); return; }

    Console.WriteLine("== " + type.FullName);
    foreach (var f in type.Fields.Where(f => f.HasConstant))
        Console.WriteLine($"  {f.Name} = {f.Constant}");
}
else if (mode == "xref")
{
    // dotnet run -- xref <substring of member/string referenced in IL>
    foreach (var t in AllTypes(asm))
        foreach (var m in t.Methods.Where(m => m.HasBody))
        {
            foreach (var i in m.Body.Instructions)
            {
                var op = i.Operand?.ToString();
                if (op != null && Match(op)) { Console.WriteLine($"{t.FullName}::{m.Name}  ->  {op}"); break; }
            }
        }
}

static IEnumerable<TypeDefinition> AllTypes(AssemblyDefinition a)
{
    IEnumerable<TypeDefinition> Walk(TypeDefinition t)
    {
        yield return t;
        foreach (var n in t.NestedTypes)
            foreach (var x in Walk(n)) yield return x;
    }
    return a.MainModule.Types.SelectMany(Walk);
}

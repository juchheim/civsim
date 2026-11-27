using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

sealed class LocalLoadContext : AssemblyLoadContext
{
    private readonly string _directory;
    public LocalLoadContext(string directory) : base(isCollectible: true) => _directory = directory;
    protected override Assembly? Load(AssemblyName name)
    {
        string path = Path.Combine(_directory, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}

static class Program
{
    static string TypeName(Type t)
    {
        if (t.IsGenericParameter) return (t.DeclaringMethod is null ? "!" : "!!") + t.GenericParameterPosition;
        if (t.IsArray) return TypeName(t.GetElementType()!) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
        if (t.IsByRef) return TypeName(t.GetElementType()!) + "&";
        if (t.IsPointer) return TypeName(t.GetElementType()!) + "*";
        if (t.IsGenericType) return t.GetGenericTypeDefinition().FullName + "<" + string.Join(',', t.GetGenericArguments().Select(TypeName)) + ">";
        return t.FullName ?? t.Name;
    }
    static string MethodKey(MethodBase m)
    {
        var pars = string.Join(",", m.GetParameters().Select(p => TypeName(p.ParameterType)));
        string ret = m is MethodInfo mi ? TypeName(mi.ReturnType) : "ctor";
        return $"{m.Name}`{(m.IsGenericMethodDefinition ? m.GetGenericArguments().Length : 0)}({pars})->{ret}";
    }
    static string LoadFingerprint(string path)
    {
        var alc = new LocalLoadContext(Path.GetDirectoryName(path)!);
        var asm = alc.LoadFromAssemblyPath(path);
        var lines = new List<string>();
        foreach (var t in asm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add($"T|{t.FullName}|{(int)t.Attributes}|{TypeName(t.BaseType ?? typeof(object))}");
            foreach (var f in t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(f=>f.Name,StringComparer.Ordinal))
                lines.Add($"F|{t.FullName}|{f.Name}|{TypeName(f.FieldType)}|{(int)f.Attributes}");
            foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)).OrderBy(MethodKey,StringComparer.Ordinal))
            {
                string key=MethodKey(m);
                var body=m.GetMethodBody();
                lines.Add($"M|{t.FullName}|{key}|{(int)m.Attributes}|{(int)m.CallingConvention}");
                if(body is null) { lines.Add("B|null"); continue; }
                byte[] il=body.GetILAsByteArray() ?? Array.Empty<byte>();
                lines.Add("B|"+Convert.ToHexString(SHA256.HashData(il))+"|"+body.MaxStackSize+"|"+body.InitLocals);
                lines.Add("L|"+string.Join(';',body.LocalVariables.Select(v=>$"{v.LocalIndex}:{TypeName(v.LocalType)}:{v.IsPinned}")));
                lines.Add("E|"+string.Join(';',body.ExceptionHandlingClauses.Select(e=>$"{e.Flags}:{e.TryOffset}:{e.TryLength}:{e.HandlerOffset}:{e.HandlerLength}:{(e.Flags == ExceptionHandlingClauseOptions.Clause ? TypeName(e.CatchType!) : "")}:{(e.Flags == ExceptionHandlingClauseOptions.Filter ? e.FilterOffset : -1)}")));
            }
        }
        string result=string.Join("\n",lines);
        alc.Unload();
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result)))+" "+lines.Count;
    }
    static int Main(string[] args)
    {
        if(args.Length!=2) return 2;
        var a=LoadFingerprint(args[0]); var b=LoadFingerprint(args[1]);
        Console.WriteLine($"A {a}\nB {b}\nIL_METADATA_EQUIVALENT={a==b}");
        return a==b?0:1;
    }
}

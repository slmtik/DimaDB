namespace DimaDB.SourceGenerator;

public readonly record struct AstNodeData
{
    public readonly string Name;
    public readonly string Arguments;
    public readonly string BaseClassName;

    public AstNodeData(string name, string arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public AstNodeData(string name, string arguments, string baseClassName) : this(name, arguments)
    {
        BaseClassName = baseClassName;
    }
}

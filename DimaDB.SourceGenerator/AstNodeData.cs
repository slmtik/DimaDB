using System.Collections.Immutable;

namespace DimaDB.SourceGenerator;

public readonly record struct AstNodeData
{
    public readonly string Name;
    public readonly string Arguments;

    public AstNodeData(string name, string arguments)
    {
        Name = name;
        Arguments = arguments;
    }
}

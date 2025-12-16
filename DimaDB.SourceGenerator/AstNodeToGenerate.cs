using System.Collections.Immutable;

namespace DimaDB.SourceGenerator;

public readonly record struct AstNodesToGenerate
{
    public readonly string BaseClassName;
    public readonly ImmutableArray<AstNodeData> AstNodes;

    public AstNodesToGenerate(string baseClassName, ImmutableArray<AstNodeData> astNodes)
    {
        BaseClassName = baseClassName;
        AstNodes = astNodes;
    }
}

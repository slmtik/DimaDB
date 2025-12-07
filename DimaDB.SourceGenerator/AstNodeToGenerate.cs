using System.Collections.Immutable;

namespace DimaDB.SourceGenerator;

[Flags]
public enum VisitorInterfaceType
{
    None = 0,
    NonGeneric = 1 << 0,
    Generic = 1 << 1,
}

public readonly record struct AstNodesToGenerate
{
    public readonly string BaseClassName;
    public readonly VisitorInterfaceType VisitorInterfaceTypes;
    public readonly ImmutableArray<AstNodeData> AstNodes;

    public AstNodesToGenerate(string baseClassName, VisitorInterfaceType visitorInterfaceTypes, ImmutableArray<AstNodeData> astNodes)
    {
        BaseClassName = baseClassName;
        VisitorInterfaceTypes = visitorInterfaceTypes;
        AstNodes = astNodes;
    }
}

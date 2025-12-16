using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("FromClause", "Component.TableReference TableRefence")]
[AstNode("WhereClause", "Expression Expression")]
public abstract partial record Clause
{
}

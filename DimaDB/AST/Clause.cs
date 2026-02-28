using DimaDB.SourceGenerator;

namespace DimaDB.Parsing;

[AstNode("FromClause", "Component.TableReference TableRefence")]
[AstNode("WhereClause", "Expression Expression")]
public abstract partial record Clause
{
}

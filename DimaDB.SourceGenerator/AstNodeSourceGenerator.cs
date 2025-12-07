using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace DimaDB.SourceGenerator;

[Generator]
public class AstNodeSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "AstNodeAttribute.g.cs",
            SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8)));

        IncrementalValuesProvider<AstNodesToGenerate?> astNodesToGenerate = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "DimaDB.SourceGenerator.AstNodeAttribute",
                predicate: static (s, _) => s is TypeDeclarationSyntax,
                transform: static (ctx, _) => GetAstNodeToGenerate(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(astNodesToGenerate, static (spc, source) => Execute(source, spc));
    }

    private static AstNodesToGenerate? GetAstNodeToGenerate(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is not TypeDeclarationSyntax typeDecl)
        {
            return null;
        }

        if (ctx.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol namedTypeSymbol)
        {
            return null;
        }

        var attrData = namedTypeSymbol.GetAttributes().Where(ad =>
            ad.AttributeClass?.ToDisplayString() == "DimaDB.SourceGenerator.AstNodeAttribute");

        var className = namedTypeSymbol.ToString();

        var visitorInterfaceTypes = VisitorInterfaceType.None;
        foreach (var visitor in namedTypeSymbol.GetTypeMembers("IVisitor"))
        {
            if (visitor.Arity > 0 || visitor.TypeParameters.Length > 0)
            {
                visitorInterfaceTypes |= VisitorInterfaceType.Generic;
            }
            else
            {
                visitorInterfaceTypes |= VisitorInterfaceType.NonGeneric;
            }
        }

        if (visitorInterfaceTypes == VisitorInterfaceType.None)
        {
            return null;
        }

        var astNodes = new List<AstNodeData>();

        foreach (var attr in attrData)
        {
            AstNodeData? astNode = CreateAstNodeDataFromAttribute(attr);
            if (astNode is not null)
            {
                astNodes.Add(astNode.Value);
            }
        }
        return new AstNodesToGenerate(className, visitorInterfaceTypes, [..astNodes]);
    }

    private static AstNodeData? CreateAstNodeDataFromAttribute(AttributeData attrData)
    {
        // Node name
        var astNodeName = attrData.ConstructorArguments[0].Value?.ToString();
        if (string.IsNullOrWhiteSpace(astNodeName))
        {
            return null;
        }

        // Node arguments
        var arguments = attrData.ConstructorArguments[1].Value?.ToString();

        return new AstNodeData(astNodeName, arguments);
    }

    static void Execute(AstNodesToGenerate? astNodeToGenerate, SourceProductionContext context)
    {
        if (astNodeToGenerate is { } value)
        {
            string result = SourceGenerationHelper.GenerateExtensionClass(value);
            context.AddSource($"{value.BaseClassName}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }
}

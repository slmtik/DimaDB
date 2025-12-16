using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
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

        var astNodes = new List<AstNodeData>();

        foreach (var attr in attrData)
        {
            AstNodeData? astNode = CreateAstNodeDataFromAttribute(attr);
            if (astNode is not null)
            {
                astNodes.Add(astNode.Value);
            }
        }
        return new AstNodesToGenerate(className, [..astNodes]);
    }

    private static AstNodeData? CreateAstNodeDataFromAttribute(AttributeData attrData)
    {
        var astNodeName = attrData.ConstructorArguments[0].Value?.ToString();
        if (string.IsNullOrWhiteSpace(astNodeName))
        {
            return null;
        }

        var arguments = attrData.ConstructorArguments[1].Value?.ToString();

        if (attrData.ConstructorArguments.Length == 3 && attrData.ConstructorArguments[2].Value is string baseClassName)
        {
            return new AstNodeData(astNodeName, arguments, baseClassName);
        }

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


using System.Text;

namespace DimaDB.SourceGenerator;

public static class SourceGenerationHelper
{
    public const string Attribute = @"
namespace DimaDB.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AstNodeAttribute : Attribute
    {
        public string ClassName { get; }
        public string Arguments { get; }

        public AstNodeAttribute(string className, string arguments)
        {
            ClassName = className;
            Arguments = arguments;
        }
    }
}";

    public static string GenerateExtensionClass(AstNodesToGenerate node)
    {
        var fullName = node.BaseClassName ?? string.Empty;

        int lastDot = fullName.LastIndexOf('.');

        var (ns, baseClassName) = lastDot >= 0
            ? (fullName.Substring(0, lastDot), fullName.Substring(lastDot + 1))
            : (string.Empty, fullName);

        var sb = new StringBuilder();

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using DimaDB.Core;");
        sb.AppendLine("using DimaDB.Core.Lexing;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"    public abstract partial record {baseClassName}");
        sb.AppendLine("    {");

        GenerateIVisitorInterfaces(sb, node, baseClassName);

        GenerateAstNodes(sb, node, baseClassName);

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void GenerateIVisitorInterfaces(StringBuilder sb, AstNodesToGenerate node, string baseClassName)
    {
        var variants = new (VisitorInterfaceType, string, string)[]
        {
            (VisitorInterfaceType.Generic, "IVisitor<T>", "T"),
            (VisitorInterfaceType.NonGeneric, "IVisitor", "void")
        };

        foreach(var (flag, interfaceName, returnType) in variants)
        {
            if (node.VisitorInterfaceTypes.HasFlag(flag))
            {
                sb.AppendLine($"        public partial interface {interfaceName}");
                sb.AppendLine("        {");

                foreach (var astNode in node.AstNodes)
                {
                    var acceptMethodSuffix = astNode.Name.EndsWith(baseClassName) ? "" : baseClassName;
                    sb.AppendLine($"            {returnType} Visit{astNode.Name}{acceptMethodSuffix}({astNode.Name} {baseClassName.ToLower()});");
                }

                sb.AppendLine("        }");
                sb.AppendLine("");
            }
        }
    }

    private static void GenerateAstNodes(StringBuilder sb, AstNodesToGenerate node, string baseClassName)
    {
        var variants = new (VisitorInterfaceType, string, string, string)[]
        {
            (VisitorInterfaceType.Generic, "IVisitor<T>", "T", "Accept<T>"),
            (VisitorInterfaceType.NonGeneric, "IVisitor", "void", "Accept")
        };

        foreach (var astNode in node.AstNodes)
        {
            sb.AppendLine($"        public record {astNode.Name}({astNode.Arguments}) : {baseClassName} ");
            sb.AppendLine("        {");

            foreach (var (flag, interfaceName, returnType, acceptMethodName) in variants)
            {
                if (node.VisitorInterfaceTypes.HasFlag(flag))
                {
                    var acceptMethodSuffix = astNode.Name.EndsWith(baseClassName) ? "" : baseClassName;
                    sb.AppendLine($"            override public {returnType} {acceptMethodName}({interfaceName} visitor) => visitor.Visit{astNode.Name}{acceptMethodSuffix}(this);");
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("");
        }
    }
}

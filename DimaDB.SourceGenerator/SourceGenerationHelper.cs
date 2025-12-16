
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
        public string? BaseClassName { get; }

        public AstNodeAttribute(string className, string arguments)
        {
            ClassName = className;
            Arguments = arguments;
        }

        public AstNodeAttribute(string className, string arguments, string baseClassName)
        {
            ClassName = className;
            Arguments = arguments;
            BaseClassName = baseClassName;
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

        sb.AppendLine("        abstract public T Accept<T>(IVisitor<T> visitor);");
        sb.AppendLine("        abstract public void Accept(IVisitor visitor);");
        sb.AppendLine("");

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
        var variants = new (string, string)[]
        {
            ("IVisitor<T>", "T"),
            ("IVisitor", "void")
        };

        foreach(var (interfaceName, returnType) in variants)
        {
            sb.AppendLine($"        public interface {interfaceName}");
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

    private static void GenerateAstNodes(StringBuilder sb, AstNodesToGenerate node, string baseClassName)
    {
        var variants = new (string, string, string)[]
        {
            ("IVisitor<T>", "T", "Accept<T>"),
            ("IVisitor", "void", "Accept")
        };

        foreach (var astNode in node.AstNodes)
        {
            var astNodeBaseClassName = (string.IsNullOrEmpty(astNode.BaseClassName) ? baseClassName : astNode.BaseClassName);
            sb.AppendLine($"        public record {astNode.Name}({astNode.Arguments}) : {astNodeBaseClassName} ");
            sb.AppendLine("        {");

            foreach (var (interfaceName, returnType, acceptMethodName) in variants)
            {
                var acceptMethodSuffix = astNode.Name.EndsWith(baseClassName) ? "" : baseClassName;
                sb.AppendLine($"            override public {returnType} {acceptMethodName}({interfaceName} visitor) => visitor.Visit{astNode.Name}{acceptMethodSuffix}(this);");
            }

            sb.AppendLine("        }");
            sb.AppendLine("");
        }
    }
}

﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Reflection;

namespace SimpleGrasshopper.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class TypePropertyComponentGenerator : TypeComponentGenerator
{
    protected override string AttrName => "PropertyComponent";

    protected override string GetComponentName(TypeDeclarationSyntax syntax, SemanticModel model)
    {
        return "TypePropertyComponent<{0}>()";
    }
}

[Generator(LanguageNames.CSharp)]
public class TypeMethodComponentGenerator : TypeComponentGenerator
{
    protected override string AttrName => "TypeComponent";

    protected override string GetComponentName(TypeDeclarationSyntax syntax, SemanticModel model)
    {
        var name = "TypeMethodComponent";
        foreach (var attrs in syntax.AttributeLists)
        {
            foreach (var a in attrs.Attributes)
            {
                var attrSymbol = model.GetSymbolInfo(a).Symbol;
                if (attrSymbol?.GetFullMetadataName() != "SimpleGrasshopper.Attributes.BaseComponentAttribute") continue;

                var strs = a.ToString().Split('"');
                if (strs.Length > 3) continue;

                name = strs[1];
                break;
            }
        }
        return $"{name}(typeof({{0}}))";
    }
}

public abstract class TypeComponentGenerator : IIncrementalGenerator
{
    protected abstract string AttrName { get; }

    protected abstract string GetComponentName(TypeDeclarationSyntax syntax, SemanticModel model);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName($"SimpleGrasshopper.Attributes.{AttrName}Attribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (n, ct) => ((TypeDeclarationSyntax)n.TargetNode, n.SemanticModel))
            .Where(m => m.Item1 is not null);

        context.RegisterSourceOutput(provider.Collect(), Execute);
    }

    private void Execute(SourceProductionContext context, ImmutableArray<(TypeDeclarationSyntax, SemanticModel)> syntaxes)
    {
        foreach (var (syntax, model) in syntaxes)
        {
            var nameSpace = AssemblyPriorityGenerator.GetParent<BaseNamespaceDeclarationSyntax>(syntax)?.Name.ToString() ?? "Null";

            var className = syntax.Identifier.Text;

            string guidStr = Utils.GetGuid(nameSpace, className, AttrName);

            var codeClassName = $"{className}_{AttrName}";

            //Obsolete
            if (syntax.IsObsolete())
            {
                codeClassName += "_Obsolete";
            }

            var attr = "";
            if (this is TypeMethodComponentGenerator)
            {
                foreach (var attrs in syntax.AttributeLists)
                {
                    foreach (var a in attrs.Attributes)
                    {
                        var attrSymbol = model.GetSymbolInfo(a).Symbol;

                        switch (attrSymbol?.GetFullMetadataName())
                        {
                            case "SimpleGrasshopper.Attributes.DocObjAttrAttribute":
                                var strs = a.ToString().Split('"');
                                if (strs.Length > 3) continue;

                                attr = strs[1];

                                if (string.IsNullOrEmpty(attr)) continue;

                                attr = $"public override Grasshopper.Kernel.IGH_Attributes CreateAttribute() => new {attr}(this);";
                                break;
                        }
                    }
                }
            }

            var code = $$"""
             using SimpleGrasshopper.DocumentObjects;
             using System;

             namespace {{nameSpace}}
             {
                public partial class {{codeClassName}}()
                    : {{string.Format(GetComponentName(syntax, model), className)}}
                {
                    public override Guid ComponentGuid => new ("{{guidStr}}");

                    {{attr}}
                }
             }
             """;

            context.AddSource($"{codeClassName}.g.cs", code);
        }
    }
}

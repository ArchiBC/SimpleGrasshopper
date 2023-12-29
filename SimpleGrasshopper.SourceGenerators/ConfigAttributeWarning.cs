﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimpleGrasshopper.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class ConfigAttributeWarning : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        InitOneAttribute(context, "Config", null, null);
        InitOneAttribute(context, "Range", null, "Config");

        InitOneAttribute(context, "ToolButton",
            [
                "bool",
                "Boolean",
                "System.Boolean",
            ], null);
    }

    private static void ValidTypes(SourceProductionContext spc, string attributeName, string[]? validTypes, TypeSyntax type, SemanticModel model)
    {
        if (validTypes == null) return;

        var typeName = model.GetTypeInfo(type).Type!.GetFullMetadataName();

        var rightType = false;
        foreach (var validType in validTypes)
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            if (typeName == validType)
            {
                rightType = true;
                break;
            }
        }

        if (!rightType)
        {
            spc.DiagnosticWrongType(type.GetLocation(), $"This type can't be tagged with {attributeName}!");
        }
    }

    private static void InitOneAttribute(IncrementalGeneratorInitializationContext context, string attributeName, string[]? validTypes, string? parent)
    {
        attributeName = $"SimpleGrasshopper.Attributes.{attributeName}Attribute";
        parent = parent == null ? null : $"SimpleGrasshopper.Attributes.{parent}Attribute";

        var provider = context.SyntaxProvider.ForAttributeWithMetadataName
            (attributeName,
                static (node, _) => node is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax { Parent: TypeDeclarationSyntax } } },
                static (n, ct) => ((VariableDeclaratorSyntax)n.TargetNode, n.SemanticModel))
                .Where(m => m.Item1 is not null);

        context.RegisterSourceOutput(provider.Collect(), (spc, array) =>
        {
            foreach (var (variableInfo, model) in array)
            {
                var field = (FieldDeclarationSyntax)variableInfo.Parent!.Parent!;

                var loc = variableInfo.Identifier.GetLocation();

                ValidTypes(spc, attributeName, validTypes, field.Declaration.Type, model);

                var hasSetting = false;
                var hasParent = false;
                foreach (var attrs in field.AttributeLists)
                {
                    foreach (var attr in attrs.Attributes)
                    {
                        var symbol = model.GetSymbolInfo(attr).Symbol;

                        var symbolName = symbol?.GetFullMetadataName();

                        if (symbolName == "SimpleGrasshopper.Attributes.SettingAttribute")
                        {
                            hasSetting = true;
                        }
                        if (symbolName == parent)
                        {
                            hasParent = true;
                        }
                        else if (symbolName == attributeName)
                        {
                            loc = attr.Name.GetLocation();
                        }
                    }
                }

                if (!hasParent && parent != null)
                {
                    spc.DiagnosticAttributeUsing(loc, $"The attribute {attributeName} must be used with the attribute {parent}!");
                }

                if (!hasSetting)
                {
                    spc.DiagnosticAttributeUsing(loc, $"The attribute {attributeName} must be used with the attribute SimpleGrasshopper.Attributes.SettingAttribute!");
                }
            }
        });

        var provider2 = context.SyntaxProvider.ForAttributeWithMetadataName
            (attributeName,
                static (node, _) => node is PropertyDeclarationSyntax { Parent: TypeDeclarationSyntax },
                static (n, ct) => ((PropertyDeclarationSyntax)n.TargetNode, n.SemanticModel))
                .Where(m => m.Item1 is not null);

        context.RegisterSourceOutput(provider2.Collect(), (spc, array) =>
        {
            foreach (var (property, model) in array)
            {
                if (!property.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    spc.DiagnosticWrongKeyword(property.Identifier.GetLocation(),
                        "The property should be a static method!");
                }

                if (property.AccessorList?.Accessors is SyntaxList<AccessorDeclarationSyntax> accessor
                    && (!accessor.Any(x => x.IsKind(SyntaxKind.GetAccessorDeclaration))
                    || !accessor.Any(x => x.IsKind(SyntaxKind.SetAccessorDeclaration))))
                {
                    spc.DiagnosticPropertyGetSet(property.Identifier.GetLocation(), "The property should has a getter and a setter!");
                }

                if (parent != null && !property.AttributeLists.Any(m => m.Attributes.Any(a => model.GetSymbolInfo(a).Symbol!.GetFullMetadataName() == parent)))
                {
                    spc.DiagnosticAttributeUsing(property.Identifier.GetLocation(),
                        $"The attribute {attributeName} must be used with the {parent}!");
                }

                ValidTypes(spc, attributeName, validTypes, property.Type, model);
            }
        });
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ReactiveProperty
{
    [Generator]
    public class ReactivePropertyGenerator : ISourceGenerator
    {
        private const string ReactivePropertyAttribute = "ReactivePropertyAttribute";

        private const string _reactivePropertyAttributeText = @"
using System;


    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal class ReactivePropertyAttribute : Attribute
    {
        public string PropertyName { get; set; }
        
        public ReactivePropertyAttribute()
        {
        }
    }

";

        private const string ReactivePropertyClassSource = @"
public class ReactiveProperty<T>
    {
        public event Action FieldChanged;

        public int Field
        {
            get { return _field; }
            set
            {
                if (_field != value)
                {
                    _field = value;
                    if (FieldChanged != null)
                    {
                        FieldChanged.Invoke();
                    }
                }
            }
        }




     
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(PostInit);
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private void PostInit(GeneratorPostInitializationContext i)
        {
            i.AddSource("ReactivePropertyAttribute_g.cs", _reactivePropertyAttributeText);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName(ReactivePropertyAttribute);

            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> classFieldsGroup in receiver.ReactiveFields
                         .GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType,
                             SymbolEqualityComparer.Default))
            {

                string source = GenerateClass(classFieldsGroup.Key, classFieldsGroup);
                source = FormatCode(source);
                
                context.AddSource($"{classFieldsGroup.Key.Name}_Classes_g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }

        private string GenerateClass(INamedTypeSymbol @class, IEnumerable<IFieldSymbol> fields)
        {
            SymbolDisplayFormat symbolDisplayFormat =
                new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly);
            string className = @class.ToDisplayString(symbolDisplayFormat);
            string classNamespace = @class.ContainingNamespace.IsGlobalNamespace
                ? null
                : @class.ContainingNamespace.ToDisplayString();

            StringBuilder sourceBuilder = new StringBuilder($@"");
            sourceBuilder.AppendLine($@"
using System;
");
            if (!string.IsNullOrEmpty(classNamespace))
            {
                sourceBuilder.AppendLine($@"
namespace {classNamespace}
{{
");
            }

            sourceBuilder.AppendLine($@"
public partial class {className}
{{

");
            
            foreach (IFieldSymbol field in fields)
            {
                string fieldName = field.Name;
                string propertyName = char.ToUpper(fieldName[1]) + fieldName.Substring(2);;
                sourceBuilder.AppendLine($@"

        public event Action {propertyName}Changed;

        public {field.Type} {propertyName}
        {{
                get {{ return {fieldName}; }}
                set
                {{
                    if ({fieldName} != value)
                    {{
                        {fieldName} = value;
                        if ({propertyName}Changed != null)
                        {{
                            {propertyName}Changed.Invoke();
                        }}
                    }}
                }}
        }}
");
                
            }
            
            
            sourceBuilder.AppendLine($@"
}}
");
            if (!string.IsNullOrEmpty(classNamespace))
            {
                sourceBuilder.AppendLine($@"
}}
");
            }

            return sourceBuilder.ToString();
        }

        private string FormatCode(string code, CancellationToken cancelToken = default)
        {
            return CSharpSyntaxTree.ParseText(code, cancellationToken: cancelToken)
                .GetRoot(cancelToken)
                .NormalizeWhitespace()
                .SyntaxTree
                .GetText(cancelToken)
                .ToString();
        }

        private void ProcessField(StringBuilder source, IFieldSymbol field)
        {
            source.Append($"public {field.Type.ToDisplayString()} {field.Name} {{get; set;}}");
        }

        private class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<IFieldSymbol> ReactiveFields { get; } = new List<IFieldSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is FieldDeclarationSyntax fieldDeclarationSyntax &&
                    fieldDeclarationSyntax.AttributeLists.Count > 0)
                {
                    foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
                    {
                        IFieldSymbol fieldSymbol = ModelExtensions.GetDeclaredSymbol(context.SemanticModel, variable) as IFieldSymbol;
                        
                        if(fieldSymbol.DeclaredAccessibility != Accessibility.Private)
                            continue;

                        if (fieldSymbol.GetAttributes()
                            .Any(ad => ad.AttributeClass.ToDisplayString() == ReactivePropertyAttribute))
                        {
                            ReactiveFields.Add(fieldSymbol);
                        }
                    }
                }
            }
        }
    }
}
﻿using Microsoft.AspNetCore.Razor.Language;
using RazorLight.Compilation;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RazorLight
{
    public class TemplateFactoryProvider : ITemplateFactoryProvider
    {
        private readonly RazorSourceGenerator sourceGenerator;
        private readonly RoslynCompilationService templateCompiler;

        public TemplateFactoryProvider(
            RazorSourceGenerator generator,
            RoslynCompilationService compiler
            )
        {
            sourceGenerator = generator;
            templateCompiler = compiler;
        }

        public RazorSourceGenerator SourceGenerator => sourceGenerator;
        public RoslynCompilationService Compiler => templateCompiler;

        public async Task<TemplateFactoryResult> CreateFactoryAsync(string templateKey)
        {
            if(templateKey == null)
            {
                throw new ArgumentNullException(nameof(templateKey));
            }

            GeneratedRazorTemplate generatedRazorTemplate = await sourceGenerator.GenerateCodeAsync(templateKey).ConfigureAwait(false);

            if (generatedRazorTemplate.CSharpDocument.Diagnostics.Count > 0)
            {
                var builder = new StringBuilder();
                builder.AppendLine("Failed to generate Razor template. See \"Diagnostics\" property for more details");

                foreach (RazorDiagnostic d in generatedRazorTemplate.CSharpDocument.Diagnostics)
                {
                    builder.AppendLine($"- {d.GetMessage()}");
                }

                throw new TemplateGenerationException(builder.ToString(), generatedRazorTemplate.CSharpDocument.Diagnostics);
            }

            CompiledTemplateDescriptor templateDescriptor = templateCompiler.CompileAndEmit(generatedRazorTemplate);

            if(templateDescriptor.TemplateAttribute != null)
            {
                Type compiledType = templateDescriptor.TemplateAttribute.TemplateType;

                var newExpression = Expression.New(compiledType);
                var keyProperty = compiledType.GetTypeInfo().GetProperty(nameof(ITemplatePage.Key));
                var propertyBindExpression = Expression.Bind(keyProperty, Expression.Constant(templateKey));
                var objectInitializeExpression = Expression.MemberInit(newExpression, propertyBindExpression);

                var pageFactory = Expression
                        .Lambda<Func<ITemplatePage>>(objectInitializeExpression)
                        .Compile();
                return new TemplateFactoryResult(templateDescriptor, pageFactory);
            }
            else
            {
                return new TemplateFactoryResult(templateDescriptor, null);
            }
        }
    }
}

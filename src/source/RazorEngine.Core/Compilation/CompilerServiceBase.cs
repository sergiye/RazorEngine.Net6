namespace RazorEngine.Compilation
{
    using System;
    using System.CodeDom;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using Microsoft.AspNetCore.Razor;
    using Microsoft.AspNetCore.Razor.Language;
    using Templating;
    using RazorEngine.Compilation.ReferenceResolver;
    using System.Security;
    using Microsoft.AspNetCore.Mvc.Razor.Extensions;
    using Microsoft.AspNetCore.Razor.Language.Extensions;

    /// <summary>
    /// Provides a base implementation of a compiler service.
    /// </summary>
    public abstract class CompilerServiceBase : ICompilerService
    {
        /// <summary>
        /// The namespace for dynamic templates.
        /// </summary>
        protected internal const string DynamicTemplateNamespace = "CompiledRazorTemplates.Dynamic";
        /// <summary>
        /// A prefix for all dynamically created classes.
        /// </summary>
        protected internal const string ClassNamePrefix = "RazorEngine_";

        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="CompilerServiceBase"/>
        /// </summary>
        /// <param name="codeLanguage">The code language.</param>
        /// <param name="markupParserFactory">The markup parser factory.</param>
        [SecurityCritical]
        protected CompilerServiceBase()
        {
            ReferenceResolver = new UseCurrentAssembliesReferenceResolver();
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the assembly resolver.
        /// </summary>
        public IReferenceResolver ReferenceResolver { get; set; }

        /// <summary>
        /// Gets or sets whether the compiler service is operating in debug mode.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets whether the compiler should load assemblies with Assembly.Load(byte[])
        /// to prevent files from being locked.
        /// </summary>
        public bool DisableTempFileLocking { get; set; }

        /// <summary>
        /// Extension of a source file without dot ("cs" for C# files or "vb" for VB.NET files).
        /// </summary>
        public abstract string SourceFileExtension { get; }

        private bool _disposed;

        #endregion

        #region Methods

        /// <summary>
        /// Tries to create and return a unique temporary directory.
        /// </summary>
        /// <returns>the (already created) temporary directory</returns>
        protected static string GetDefaultTemporaryDirectory()
        {
            var created = false;
            var tried = 0;
            string tempDirectory = "";
            while (!created && tried < 10)
            {
                tried++;
                try
                {
                    tempDirectory = Path.Combine(Path.GetTempPath(), "RazorEngine_" + Path.GetRandomFileName());
                    if (!Directory.Exists(tempDirectory))
                    {
                        Directory.CreateDirectory(tempDirectory);
                        created = Directory.Exists(tempDirectory);
                    }
                }
                catch (IOException)
                {
                    if (tried > 8)
                    {
                        throw;
                    }
                }
            }
            if (!created)
            {
                throw new Exception("Could not create a temporary directory! Maybe all names are already used?");
            }
            return tempDirectory;
        }

        /// <summary>
        /// Returns a new temporary directory ready to be used.
        /// This can be overwritten in subclases to change the created directories.
        /// </summary>
        /// <returns></returns>
        protected virtual string GetTemporaryDirectory()
        {
            return GetDefaultTemporaryDirectory();
        }

        /// <summary>
        /// Builds a type name for the specified template type.
        /// </summary>
        /// <param name="templateType">The template type.</param>
        /// <param name="modelType">The model type.</param>
        /// <returns>The string type name (including namespace).</returns>
        [Pure]
        public abstract string BuildTypeName(Type templateType, Type modelType);

        /// <summary>
        /// Compiles the type defined in the specified type context.
        /// </summary>
        /// <param name="context">The type context which defines the type to compile.</param>
        /// <returns>The compiled type.</returns>
        [SecurityCritical]
        public abstract Tuple<Type, CompilationData> CompileType(TypeContext context);

        /// <summary>
        /// Creates a <see cref="RazorEngineHost"/> used for class generation.
        /// </summary>
        /// <param name="templateType">The template base type.</param>
        /// <param name="modelType">The model type.</param>
        /// <param name="className">The class name.</param>
        /// <returns>An instance of <see cref="RazorEngineHost"/>.</returns>

        /// <summary>
        /// Gets the source code from Razor for the given template.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="template">The template to compile.</param>
        /// <param name="namespaceImports">The set of namespace imports.</param>
        /// <param name="templateType">The template type.</param>
        /// <param name="modelType">The model type.</param>
        /// <returns></returns>
        [Pure]
        [SecurityCritical]
        public string GetCodeCompileUnit(string className, ITemplateSource template, ISet<string> namespaceImports, Type templateType, Type modelType)
        {
            var typeContext =
                new TypeContext(className, namespaceImports)
                {
                    TemplateContent = template,
                    TemplateType = templateType,
                    ModelType = modelType
                };
            return GetCodeCompileUnit(typeContext);
        }

        /// <summary>
        /// Helper method to generate the prefered assembly name.
        /// </summary>
        /// <param name="context">the context of the current compilation.</param>
        /// <returns></returns>
        protected string GetAssemblyName(TypeContext context)
        {
            return String.Format("{0}.{1}", DynamicTemplateNamespace, context.ClassName);
        }

        /// <summary>
        /// Inspects the source and returns the source code.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [SecurityCritical]
        public virtual string InspectSource(RazorPageGeneratorResult results, TypeContext context)
        {
            return results.GeneratedCode;
        }

        /// <summary>
        /// Gets the code compile unit used to compile a type.
        /// </summary>
        /// <param name="context"></param>
        /// <returns>A <see cref="CodeCompileUnit"/> used to compile a type.</returns>
        [Pure]
        [SecurityCritical]
        public string GetCodeCompileUnit(TypeContext context)
        {
            string className = context.ClassName;
            ITemplateSource template = context.TemplateContent;
            ISet<string> namespaceImports = context.Namespaces;
            Type templateType = context.TemplateType;
            Type modelType = context.ModelType;

            if (string.IsNullOrEmpty(className))
                throw new ArgumentException("Class name is required.");

            if (template == null)
                throw new ArgumentException("Template is required.");

            namespaceImports = namespaceImports ?? new HashSet<string>();

            // Gets the generator result.
            return GetGeneratorResult(GetNamespaces(templateType, namespaceImports), context);
        }

        /// <summary>
        /// Gets the generator result.
        /// </summary>
        /// <param name="host">The razor engine host.</param>
        /// <param name="context">The compile context.</param>
        /// <returns>The generator result.</returns>
        [SecurityCritical]
        private string GetGeneratorResult(IEnumerable<string> namespaces, TypeContext context)
        {
            string importString = @"
@using System
@using System.Threading.Tasks
";
            importString += string.Join("\r\n", namespaces.Select(n => "@using " + n.Trim())) + "\r\n";

            RazorProjectEngine projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, new EmptyProjectFileSystem(), builder =>
            {
                builder
                    .SetNamespace(DynamicTemplateNamespace)
                    //.SetBaseType("Microsoft.Extensions.RazorViews.BaseView")
                    .SetBaseType(BuildTypeName(context.TemplateType, context.ModelType))
                    .ConfigureClass((document, @class) =>
                    {
                        @class.ClassName = context.ClassName;
                        //if (!string.IsNullOrWhiteSpace(document.Source.FilePath))
                        //{
                        //    @class.ClassName = Path.GetFileNameWithoutExtension(document.Source.FilePath);
                        //}
                        @class.Modifiers.Clear();
                        @class.Modifiers.Add("internal");
                    });

                InjectDirective.Register(builder);
                ModelDirective.Register(builder);
                //NamespaceDirective.Register(builder);
                //PageDirective.Register(builder);

                FunctionsDirective.Register(builder);
                InheritsDirective.Register(builder);
                SectionDirective.Register(builder);

                //builder.Features.Add(new ModelExpressionPass());
                //builder.Features.Add(new Microsoft.CodeAnalysis.Razor.DefaultTagHelperDescriptorProvider());
                //builder.Features.Add(new InstrumentationPass());

                //https://github.com/aspnet/Razor/blob/d2d84d00f52e2b6785c5644cb457984a42675013/src/RazorPageGenerator/Program.cs#L68
                builder.Features.Add(new SuppressChecksumOptionsFeature());
                builder.Features.Add(new SuppressMetadataAttributesFeature());

                builder.AddDefaultImports(importString);
            });

            using (var reader = context.TemplateContent.GetTemplateReader())
            {
                string path = null;
                if (string.IsNullOrWhiteSpace(context.TemplateContent.TemplateFile))
                {
                    path = Directory.GetCurrentDirectory();
                }
                else
                {
                    path = Path.GetDirectoryName(context.TemplateContent.TemplateFile);
                }
                var razorProject = RazorProjectFileSystem.Create(path);
                var templateEngine = new RazorTemplateEngine(projectEngine.Engine, razorProject);
                templateEngine.Options.DefaultImports = RazorSourceDocument.Create(importString, fileName: null);
                RazorPageGeneratorResult result;
                //RazorPageGeneratorResult result1;
                if (string.IsNullOrWhiteSpace(context.TemplateContent.TemplateFile))
                {
                    //var item = RazorSourceDocument.Create(context.TemplateContent.Template, "temp");
                    //var imports = new List<RazorSourceDocument>();
                    //imports.Add(templateEngine.Options.DefaultImports);
                    //var doc1 = RazorCodeDocument.Create(item, imports);
                    var doc = projectEngine.Process(new InMemoryRazorProjectItemWrapper(context.TemplateContent.Template));
                    result = GenerateCodeFile(doc);
                    //result1 = GenerateCodeFile(templateEngine, doc1);
                }
                else
                {
                    var item = razorProject.GetItem(context.TemplateContent.TemplateFile);
                    var doc = projectEngine.Process(new FileSystemRazorProjectItemWrapper(item));
                    result = GenerateCodeFile(doc);
                    //result1 = GenerateCodeFile(templateEngine, item);
                }
                return InspectSource(result, context);
            }
        }

        //private static RazorPageGeneratorResult GenerateCodeFile(RazorTemplateEngine templateEngine, RazorProjectItem projectItem)
        //{
        //    var projectItemWrapper = new FileSystemRazorProjectItemWrapper(projectItem);
        //    var cSharpDocument = templateEngine.GenerateCode(projectItemWrapper);
        //    if (cSharpDocument.Diagnostics.Any())
        //    {
        //        var diagnostics = string.Join(Environment.NewLine, cSharpDocument.Diagnostics);
        //        Console.WriteLine($"One or more parse errors encountered. This will not prevent the generator from continuing: {Environment.NewLine}{diagnostics}.");
        //    }

        //    var generatedCodeFilePath = Path.ChangeExtension(projectItem.PhysicalPath, ".Designer.cs");
        //    return new RazorPageGeneratorResult
        //    {
        //        FilePath = generatedCodeFilePath,
        //        GeneratedCode = cSharpDocument.GeneratedCode,
        //    };
        //}
        //private static RazorPageGeneratorResult GenerateCodeFile(RazorTemplateEngine templateEngine, RazorCodeDocument document)
        //{
        //    var cSharpDocument = templateEngine.GenerateCode(document);
        //    if (cSharpDocument.Diagnostics.Any())
        //    {
        //        var diagnostics = string.Join(Environment.NewLine, cSharpDocument.Diagnostics);
        //        Console.WriteLine($"One or more parse errors encountered. This will not prevent the generator from continuing: {Environment.NewLine}{diagnostics}.");
        //    }

        //    return new RazorPageGeneratorResult
        //    {
        //        FilePath = null,
        //        GeneratedCode = cSharpDocument.GeneratedCode,
        //    };
        //}
        private static RazorPageGeneratorResult GenerateCodeFile(RazorCodeDocument document)
        {
            var cSharpDocument = document.GetCSharpDocument();
            if (cSharpDocument.Diagnostics.Any())
            {
                var diagnostics = string.Join(Environment.NewLine, cSharpDocument.Diagnostics);
                if (cSharpDocument.Diagnostics.Where(d => d.Severity == RazorDiagnosticSeverity.Error).Any())
                {
                    var err = cSharpDocument.Diagnostics.First(d => d.Severity == RazorDiagnosticSeverity.Error);
                    throw new TemplateParsingException($"One or more parse errors encountered: {Environment.NewLine}{diagnostics}.",
                        err.Span.CharacterIndex, err.Span.LineIndex);
                }
                Console.WriteLine($"One or more parse errors encountered. This will not prevent the generator from continuing: {Environment.NewLine}{diagnostics}.");
            }

            return new RazorPageGeneratorResult
            {
                FilePath = null,
                GeneratedCode = cSharpDocument.GeneratedCode,
            };
        }

        /// <summary>
        /// Gets any required namespace imports.
        /// </summary>
        /// <param name="templateType">The template type.</param>
        /// <param name="otherNamespaces">The requested set of namespace imports.</param>
        /// <returns>A set of namespace imports.</returns>
        private static IEnumerable<string> GetNamespaces(Type templateType, IEnumerable<string> otherNamespaces)
        {
            var templateNamespaces = templateType.GetCustomAttributes(typeof(RequireNamespacesAttribute), true)
                .Cast<RequireNamespacesAttribute>()
                .SelectMany(a => a.Namespaces)
                .Concat(otherNamespaces)
                .Distinct();

            return templateNamespaces;
        }

        /// <summary>
        /// Returns a set of assemblies that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of assemblies.</returns>
        [Obsolete("Use IncludeReferences instead")]
        public virtual IEnumerable<string> IncludeAssemblies()
        {
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Returns a set of references that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of references.</returns>
        public virtual IEnumerable<CompilerReference> IncludeReferences()
        {
            return Enumerable.Empty<CompilerReference>();
        }

        /// <summary>
        /// Helper method to get all references for the given compilation.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected IEnumerable<CompilerReference> GetAllReferences(TypeContext context)
        {
#pragma warning disable 0618 // Backwards Compat.
            var references =
                ReferenceResolver.GetReferences(
                    context,
                    IncludeAssemblies()
                        .Select(CompilerReference.From)
                        .Concat(IncludeReferences()))
#pragma warning restore 0618 // Backwards Compat.
                .ToList();
            context.AddReferences(references);
            return references;
        }

        /// <summary>
        /// Disposes the current instance.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Disposes the current instance via the disposable pattern.
        /// </summary>
        /// <param name="disposing">true when Dispose() was called manually.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        #endregion

        //https://github.com/aspnet/Razor/blob/b70815e317298c3078fff7ed6e21fa9b5738949f/src/RazorPageGenerator/Program.cs
        private class SuppressChecksumOptionsFeature : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
        {
            public int Order { get; set; }

            public void Configure(RazorCodeGenerationOptionsBuilder options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                options.SuppressChecksum = true;
            }
        }

        private class SuppressMetadataAttributesFeature : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
        {
            public int Order { get; set; }

            public void Configure(RazorCodeGenerationOptionsBuilder options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                options.SuppressMetadataAttributes = true;
            }
        }

        private class InMemoryRazorProjectItemWrapper : RazorProjectItem
        {
            private readonly string _template;
            public InMemoryRazorProjectItemWrapper(string template)
            {
                _template = template;
            }

            public override string BasePath { get; }

            /// <inheritdoc />
            public override string FilePath => "temp";

            /// <inheritdoc />
            public override bool Exists => true;

            /// <inheritdoc />
            public override string PhysicalPath => "";

            public override Stream Read()
            {
                var processedContent = ProcessFileIncludes();
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(processedContent));
            }

            private string ProcessFileIncludes()
            {
                return _template;
            }
        }

        private class FileSystemRazorProjectItemWrapper : RazorProjectItem
        {
            private readonly RazorProjectItem _source;

            public FileSystemRazorProjectItemWrapper(RazorProjectItem item)
            {
                _source = item;
            }

            public override string BasePath => _source.BasePath;

            public override string FilePath => _source.FilePath;

            // Mask the full name since we don't want a developer's local file paths to be commited.
            public override string PhysicalPath => _source.PhysicalPath;

            public override bool Exists => _source.Exists;

            public override Stream Read()
            {
                var processedContent = ProcessFileIncludes();
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(processedContent));
            }

            private string ProcessFileIncludes()
            {
                var basePath = Path.GetDirectoryName(_source.PhysicalPath);
                var cshtmlContent = File.ReadAllText(_source.PhysicalPath);

                var startMatch = "<%$ include: ";
                var endMatch = " %>";
                var startIndex = 0;
                while (startIndex < cshtmlContent.Length)
                {
                    startIndex = cshtmlContent.IndexOf(startMatch, startIndex);
                    if (startIndex == -1)
                    {
                        break;
                    }
                    var endIndex = cshtmlContent.IndexOf(endMatch, startIndex);
                    if (endIndex == -1)
                    {
                        throw new InvalidOperationException($"Invalid include file format in {_source.PhysicalPath}. Usage example: <%$ include: ErrorPage.js %>");
                    }
                    var includeFileName = cshtmlContent.Substring(startIndex + startMatch.Length, endIndex - (startIndex + startMatch.Length));
                    Console.WriteLine("      Inlining file {0}", includeFileName);
                    var includeFileContent = File.ReadAllText(System.IO.Path.Combine(basePath, includeFileName));
                    cshtmlContent = cshtmlContent.Substring(0, startIndex) + includeFileContent + cshtmlContent.Substring(endIndex + endMatch.Length);
                    startIndex = startIndex + includeFileContent.Length;
                }
                return cshtmlContent;
            }
        }

        internal class EmptyProjectFileSystem : RazorProjectFileSystem
        {
            public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
            {
                NormalizeAndEnsureValidPath(basePath);
                return Enumerable.Empty<RazorProjectItem>();
            }

            public override RazorProjectItem GetItem(string path)
            {
                NormalizeAndEnsureValidPath(path);
                return new NotFoundProjectItem(string.Empty, path);
            }
        }

        /// <summary>
        /// A <see cref="RazorProjectItem"/> that does not exist.
        /// </summary>
        internal class NotFoundProjectItem : RazorProjectItem
        {
            /// <summary>
            /// Initializes a new instance of <see cref="NotFoundProjectItem"/>.
            /// </summary>
            /// <param name="basePath">The base path.</param>
            /// <param name="path">The path.</param>
            public NotFoundProjectItem(string basePath, string path)
            {
                BasePath = basePath;
                FilePath = path;
            }

            /// <inheritdoc />
            public override string BasePath { get; }

            /// <inheritdoc />
            public override string FilePath { get; }

            /// <inheritdoc />
            public override bool Exists => false;

            /// <inheritdoc />
            public override string PhysicalPath => throw new NotSupportedException();

            /// <inheritdoc />
            public override Stream Read() => throw new NotSupportedException();
        }
    }
}
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Extensions;
using Bicep.Core.Json;
using Bicep.Core.Semantics;
using Bicep.Core.TypeSystem;
using Bicep.Core.Workspaces;
using Bicep.RegistryModuleTool.Extensions;
using Bicep.RegistryModuleTool.ModuleValidators;
using Bicep.RegistryModuleTool.Proxies;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;

namespace Bicep.RegistryModuleTool.ModuleFiles
{
    public record MainArmTemplateParameter(string Name, string Type, bool Required, string? Description);

    public record MainArmTemplateOutput(string Name, string Type, string? Description);

    public sealed class MainArmTemplateFile : ModuleFile
    {
        public const string FileName = "main.json";

        private readonly Lazy<JsonElement> lazyRootElement;

        private readonly Lazy<IEnumerable<MainArmTemplateParameter>> lazyParameters;

        private readonly Lazy<IEnumerable<MainArmTemplateOutput>> lazyOutputs;

        private readonly Lazy<string> lazyTemplateHash;

        public MainArmTemplateFile(string path, string content)
            : base(path)
        {
            this.Content = content;

            this.lazyRootElement = new(() => JsonElementFactory.CreateElement(content));
            // this.lazyParameters = new(() => !lazyRootElement.Value.TryGetProperty("parameters", out var parametersElement)
            //     ? Enumerable.Empty<MainArmTemplateParameter>()
            //     : parametersElement.EnumerateObject().Select(ToParameter));
            // this.lazyOutputs = new(() => !lazyRootElement.Value.TryGetProperty("outputs", out var outputsElement)
            //         ? Enumerable.Empty<MainArmTemplateOutput>()
            //         : outputsElement.EnumerateObject().Select(ToOutput));
            this.lazyTemplateHash = new(() => lazyRootElement.Value.GetPropertyByPath("metadata._generator.templateHash").ToNonNullString());

            var armTemplate = new ArmTemplateSemanticModel(SourceFileFactory.CreateArmTemplateFile(new Uri(System.IO.Path.GetFullPath(path)), content));

            this.lazyParameters = new Lazy<IEnumerable<Bicep.RegistryModuleTool.ModuleFiles.MainArmTemplateParameter>>(() =>
            {
                return armTemplate.Parameters.Select(kv =>
                    new MainArmTemplateParameter(kv.Value.Name, GetPrimitiveTypeName(kv.Value.TypeReference), kv.Value.IsRequired, kv.Value.Description));
            });
            this.lazyOutputs = new Lazy<IEnumerable<Bicep.RegistryModuleTool.ModuleFiles.MainArmTemplateOutput>>(() =>
            {
                return armTemplate.Outputs.Select(kv =>
                    new MainArmTemplateOutput(kv.Name, GetPrimitiveTypeName(kv.TypeReference), kv.Description));
            });
        }

        private static string GetPrimitiveTypeName(ITypeReference typeRef) => typeRef.Type switch {
            StringType or StringLiteralType => "string",
            UnionType unionOfStrings when unionOfStrings.Members.All(m => m.Type is StringLiteralType || m.Type is StringType)
                => "string",
            IntegerType or IntegerLiteralType => "int",
            UnionType unionOfInts when unionOfInts.Members.All(m => m.Type is IntegerLiteralType || m.Type is IntegerType)
                => "int",
            BooleanType or BooleanLiteralType => "bool",
            UnionType unionOfBools when unionOfBools.Members.All(m => m.Type is BooleanLiteralType || m.Type is BooleanType)
                => "bool",
            ObjectType => "object",
            ArrayType => "array",
            TypeSymbol otherwise => throw new InvalidOperationException($"Unable to determine primitive type of {otherwise.Name}"),
        };

        public string Content { get; }

        public JsonElement RootElement => this.lazyRootElement.Value;

        public IEnumerable<MainArmTemplateParameter> Parameters => this.lazyParameters.Value;

        public IEnumerable<MainArmTemplateOutput> Outputs => this.lazyOutputs.Value;

        public string TemplateHash => this.lazyTemplateHash.Value;

        public static MainArmTemplateFile Generate(IFileSystem fileSystem, BicepCliProxy bicepCliProxy, MainBicepFile mainBicepFile)
        {
            var tempFilePath = fileSystem.Path.GetTempFileName();

            try
            {
                bicepCliProxy.Build(mainBicepFile.Path, tempFilePath);
            }
            catch (Exception)
            {
                fileSystem.File.Delete(tempFilePath);

                throw;
            }

            using var tempFileStream = fileSystem.FileStream.CreateDeleteOnCloseStream(tempFilePath);
            using var streamReader = new StreamReader(tempFileStream);

            var path = fileSystem.Path.GetFullPath(FileName);
            var content = streamReader.ReadToEnd();

            return new(path, content);
        }

        public static MainArmTemplateFile ReadFromFileSystem(IFileSystem fileSystem)
        {
            var path = fileSystem.Path.GetFullPath(FileName);
            var content = fileSystem.File.ReadAllText(FileName);

            return new(path, content);
        }

        public MainArmTemplateFile WriteToFileSystem(IFileSystem fileSystem)
        {
            fileSystem.File.WriteAllText(this.Path, this.Content);

            return this;
        }

        private string GetTypeFromDefinition(JsonElement element)
        {
            return element.TryGetProperty("type", out var typeElement)
            ? typeElement.ToNonNullString()
            : GetTypeFromDefinition(LookupRef(element));
        }

        private string? TryGetDescription(JsonElement element)
        {
            if (element.TryGetProperty("metadata", out var metdataElement) &&
                metdataElement.TryGetProperty("description", out var descriptionElement))
            {
                return descriptionElement.ToNonNullString();
            }

            // The order of the checks allow the user to optionally override the default description for a user defined type
            if (element.TryGetProperty("$ref", out var _))
            {
                return TryGetDescription(LookupRef(element));
            }

            return null;
        }

        private JsonElement LookupRef(JsonElement element)
        {
            return this.RootElement.GetProperty("definitions").GetProperty(element.GetProperty("$ref").ToNonNullString().Split('/')[2]);
        }


        protected override void ValidatedBy(IModuleFileValidator validator) => validator.Validate(this);
    }
}

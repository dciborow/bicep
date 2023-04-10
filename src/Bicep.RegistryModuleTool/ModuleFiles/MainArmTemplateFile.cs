// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Extensions;
using Bicep.Core.Json;
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
            this.lazyParameters = new(() => !lazyRootElement.Value.TryGetProperty("parameters", out var parametersElement)
                ? Enumerable.Empty<MainArmTemplateParameter>()
                : parametersElement.EnumerateObject().Select(ToParameter));
            this.lazyOutputs = new(() => !lazyRootElement.Value.TryGetProperty("outputs", out var outputsElement)
                    ? Enumerable.Empty<MainArmTemplateOutput>()
                    : outputsElement.EnumerateObject().Select(ToOutput));
            this.lazyTemplateHash = new(() => lazyRootElement.Value.GetPropertyByPath("metadata._generator.templateHash").ToNonNullString());
        }

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

        private MainArmTemplateParameter ToParameter(JsonProperty parameterProperty)
        {
            string name = parameterProperty.Name;
            string type = GetTypeFromDefinition(parameterProperty.Value);
            bool required = !parameterProperty.Value.TryGetProperty("defaultValue", out _);
            string? description = TryGetDescription(parameterProperty.Value);
            return new(name, type, required, description);
        }

        private MainArmTemplateOutput ToOutput(JsonProperty outputProperty)
        {
            string name = outputProperty.Name;
            string type = GetTypeFromDefinition(outputProperty.Value);
            string? description = TryGetDescription(outputProperty.Value);

            return new(name, type, description);
        }

        private string GetTypeFromDefinition(JsonElement element)
        {
            return element.TryGetProperty("type", out _)
            ? element.GetProperty("type").ToNonNullString()
            : this.lazyRootElement.Value.GetPropertyByPath("definitions." + element.GetProperty("$ref").ToNonNullString().Split('/')[2] + ".type").ToNonNullString();
        }

        private string? TryGetDescription(JsonElement element)
        {
            if(element.TryGetProperty("metadata", out _) && element.GetProperty("metadata").TryGetProperty("description", out var descriptionElement)){
                return descriptionElement.ToNonNullString();
            }
            if(element.TryGetProperty("$ref", out var refElement)){
              return this.lazyRootElement.Value.GetPropertyByPath("definitions." + refElement.ToNonNullString().Split('/')[2] + ".type").ToNonNullString();
            }
            return null;
        }

        protected override void ValidatedBy(IModuleFileValidator validator) => validator.Validate(this);
    }
}

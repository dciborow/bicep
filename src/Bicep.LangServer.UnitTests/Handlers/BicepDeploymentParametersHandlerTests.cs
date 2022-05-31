// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bicep.Core.Syntax;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.Mock;
using Bicep.Core.UnitTests.Utils;
using Bicep.LanguageServer.Deploy;
using Bicep.LanguageServer.Handlers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Bicep.LangServer.UnitTests.Handlers
{
    [TestClass]
    public class BicepDeploymentParametersHandlerTests
    {
        [NotNull]
        public TestContext? TestContext { get; set; }

        private readonly ISerializer Serializer = StrictMock.Of<ISerializer>().Object;

        [TestMethod]
        public async Task Handle_WithNoParamsInSourceFile_ShouldReturnEmptyListOfUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"var test = 'abc'";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.20.27533"",
      ""templateHash"": ""6882627226792194393""
    }
  },
  ""variables"": {
    ""test"": ""abc""
  },
  ""resources"": []
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().BeEmpty();
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithUnusedParamInSourceFile_ShouldReturnEmptyListOfUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"param test string = 'abc'";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.18.56646"",
      ""templateHash"": ""2028391450931931217""
    }
  },
  ""parameters"": {
    ""test"": {
      ""type"": ""string"",
      ""defaultValue"": ""abc""
    }
  },
  ""resources"": []
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().BeEmpty();
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithOnlyDefaultValues_ShouldReturnUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"param name string = 'test'
param location string = 'global
resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: name
  location: location
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.18.56646"",
      ""templateHash"": ""3422964353444461889""
    }
  },
  ""parameters"": {
    ""name"": {
      ""type"": ""string"",
      ""defaultValue"": ""test""
    },
    ""location"": {
      ""type"": ""string"",
      ""defaultValue"": ""global""
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Network/dnsZones"",
      ""apiVersion"": ""2018-05-01"",
      ""name"": ""[parameters('name')]"",
      ""location"": ""[parameters('location')]""
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().SatisfyRespectively(
                updatedParam =>
                {
                    updatedParam.name.Should().Be("name");
                    updatedParam.value.Should().Be("test");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeFalse();
                },
                updatedParam =>
                {
                    updatedParam.name.Should().Be("location");
                    updatedParam.value.Should().Be("global");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeFalse();
                });
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithDefaultValuesAndParametersFile_ShouldReturnUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"param name string = 'test'
param location string
resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: name
  location: location
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.18.56646"",
      ""templateHash"": ""3422964353444461889""
    }
  },
  ""parameters"": {
    ""name"": {
      ""type"": ""string"",
      ""defaultValue"": ""test""
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Network/dnsZones"",
      ""apiVersion"": ""2018-05-01"",
      ""name"": ""[parameters('name')]"",
      ""location"": ""[parameters('location')]""
    }
  ]
}";
            var parametersFileContents = @"{
    ""location"": {
      ""value"": ""westus""
    }
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var parametersFilePath = FileHelper.SaveResultFile(TestContext, "parameters.json", parametersFileContents);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, parametersFilePath, template, CancellationToken.None);

            result.deploymentParameters.Should().SatisfyRespectively(
                updatedParam =>
                {
                    updatedParam.name.Should().Be("name");
                    updatedParam.value.Should().Be("test");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeFalse();
                });
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithMissingParametersFileAndDefaultValue_ShouldReturnUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"param name string = 'test'
param location string
resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: name
  location: location
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.18.56646"",
      ""templateHash"": ""3422964353444461889""
    }
  },
  ""parameters"": {
    ""name"": {
      ""type"": ""string"",
      ""defaultValue"": ""test""
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Network/dnsZones"",
      ""apiVersion"": ""2018-05-01"",
      ""name"": ""[parameters('name')]"",
      ""location"": ""[parameters('location')]""
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().SatisfyRespectively(
                updatedParam =>
                {
                    updatedParam.name.Should().Be("name");
                    updatedParam.value.Should().Be("test");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeFalse();
                },
                updatedParam =>
                {
                    updatedParam.name.Should().Be("location");
                    updatedParam.value.Should().BeNull();
                    updatedParam.isMissingParam.Should().BeTrue();
                    updatedParam.isExpression.Should().BeFalse();
                });
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_ParameterWithDefaultValueAndEntryInParametersFile_ShouldIgnoreParameter()
        {
            var bicepFileContents = @"param name string = 'test'
param location string = 'eastus'
resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: name
  location: location
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.18.56646"",
      ""templateHash"": ""3422964353444461889""
    }
  },
  ""parameters"": {
    ""name"": {
      ""type"": ""string"",
      ""defaultValue"": ""test""
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Network/dnsZones"",
      ""apiVersion"": ""2018-05-01"",
      ""name"": ""[parameters('name')]"",
      ""location"": ""[parameters('location')]""
    }
  ]
}";
            var parametersFileContents = @"{
    ""location"": {
      ""value"": ""westus""
    }
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var parametersFilePath = FileHelper.SaveResultFile(TestContext, "parameters.json", parametersFileContents);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, parametersFilePath, template, CancellationToken.None);

            result.deploymentParameters.Should().SatisfyRespectively(
                updatedParam =>
                {
                    updatedParam.name.Should().Be("name");
                    updatedParam.value.Should().Be("test");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeFalse();
                });
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithParameterOfTypeObjectAndDefaultValue_ShouldReturnEmptyListOfUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"resource blueprintName_policyArtifact 'Microsoft.Blueprint/blueprints/artifacts@2018-11-01-preview' = {
  name: 'name/policyArtifact'
  kind: 'policyAssignment'
  properties: testProperties
}
param testProperties object = {
  displayName: 'Blocked Resource Types policy definition'
  description: 'Block certain resource types'
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.44.5715"",
      ""templateHash"": ""15862569082920623108""
    }
  },
  ""parameters"": {
    ""testProperties"": {
      ""type"": ""object"",
      ""defaultValue"": {
        ""displayName"": ""Blocked Resource Types policy definition"",
        ""description"": ""Block certain resource types""
      }
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Blueprint/blueprints/artifacts"",
      ""apiVersion"": ""2018-11-01-preview"",
      ""name"": ""name/policyArtifact"",
      ""kind"": ""policyAssignment"",
      ""properties"": ""[parameters('testProperties')]""
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().BeEmpty();
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithParameterOfTypeArrayAndDefaultValue_ShouldReturnEmptyListOfUpdatedDeploymentParameters()
        {
            var bicepFileContents = @"resource blueprintName_policyArtifact 'Microsoft.Blueprint/blueprints/artifacts@2018-11-01-preview' = {
  name: 'name/policyArtifact'
  kind: 'policyAssignment'
  allowedOrigins: allowedOrigins
}
param allowedOrigins array = [
  'https://foo.com'
  'https://bar.com'
]";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.44.5715"",
      ""templateHash"": ""15862569082920623108""
    }
  },
  ""parameters"": {
    ""testProperties"": {
      ""type"": ""object"",
      ""defaultValue"": {
        ""displayName"": ""Blocked Resource Types policy definition"",
        ""description"": ""Block certain resource types""
      }
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Blueprint/blueprints/artifacts"",
      ""apiVersion"": ""2018-11-01-preview"",
      ""name"": ""name/policyArtifact"",
      ""kind"": ""policyAssignment"",
      ""properties"": ""[parameters('testProperties')]""
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().BeEmpty();
            result.errorMessage.Should().BeNull();
        }

        [TestMethod]
        public async Task Handle_WithParameterOfTypeObjectAndNoDefaultValue_ShouldReturnUpdatedDeploymentParameterWithShowDefaultSetToFalse()
        {
            var bicepFileContents = @"resource blueprintName_policyArtifact 'Microsoft.Blueprint/blueprints/artifacts@2018-11-01-preview' = {
  name: 'name/policyArtifact'
  kind: 'policyAssignment'
  properties: testProperties
}
param testProperties object";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.44.5715"",
      ""templateHash"": ""15862569082920623108""
    }
  },
  ""parameters"": {
    ""testProperties"": {
      ""type"": ""object"",
      ""defaultValue"": {
        ""displayName"": ""Blocked Resource Types policy definition"",
        ""description"": ""Block certain resource types""
      }
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Blueprint/blueprints/artifacts"",
      ""apiVersion"": ""2018-11-01-preview"",
      ""name"": ""name/policyArtifact"",
      ""kind"": ""policyAssignment"",
      ""properties"": ""[parameters('testProperties')]""
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().BeEmpty();
            result.errorMessage.Should().BeEquivalentToIgnoringNewlines("Parameters of type array or object should either contain a default value or must be specified in parameters.json file. Please update the value for following parameters: testProperties");
        }

        [TestMethod]
        public async Task Handle_ParameterWithDefaultValuesOfTypeExpression_ShouldReturnUpdatedDeploymentParametersWithIsExpressionSetToTrue()
        {
            var bicepFileContents = @"param location string = resourceGroup().location
param policyDefinitionId string = resourceId('Microsoft.Network/virtualNetworks/subnets', 'virtualNetworkName_var', 'subnet1Name')
resource blueprintName_policyArtifact 'Microsoft.Blueprint/blueprints/artifacts@2018-11-01-preview' = {
  name: 'name/policyArtifact'
  kind: 'policyAssignment'
  location: location
  properties: {
    policyDefinitionId: policyDefinitionId
  }
}";
            var template = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""metadata"": {
    ""_generator"": {
      ""name"": ""bicep"",
      ""version"": ""0.6.46.5435"",
      ""templateHash"": ""8792531277105895125""
    }
  },
  ""parameters"": {
    ""location"": {
      ""type"": ""string"",
      ""defaultValue"": ""[resourceGroup().location]""
    },
    ""policyDefinitionId"": {
      ""type"": ""string"",
      ""defaultValue"": ""[resourceId('Microsoft.Network/virtualNetworks/subnets', 'virtualNetworkName_var', 'subnet1Name')]""
    }
  },
  ""resources"": [
    {
      ""type"": ""Microsoft.Blueprint/blueprints/artifacts"",
      ""apiVersion"": ""2018-11-01-preview"",
      ""name"": ""name/policyArtifact"",
      ""kind"": ""policyAssignment"",
      ""location"": ""[parameters('location')]"",
      ""properties"": {
        ""policyDefinitionId"": ""[parameters('policyDefinitionId')]""
      }
    }
  ]
}";
            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = await bicepDeploymentParametersHandler.Handle(bicepFilePath, string.Empty, template, CancellationToken.None);

            result.deploymentParameters.Should().SatisfyRespectively(
                updatedParam =>
                {
                    updatedParam.name.Should().Be("location");
                    updatedParam.value.Should().Be("resourceGroup().location");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeTrue();
                },
                updatedParam =>
                {
                    updatedParam.name.Should().Be("policyDefinitionId");
                    updatedParam.value.Should().Be("resourceId('Microsoft.Network/virtualNetworks/subnets', 'virtualNetworkName_var', 'subnet1Name')");
                    updatedParam.isMissingParam.Should().BeFalse();
                    updatedParam.isExpression.Should().BeTrue();
                });
            result.errorMessage.Should().BeNull();
        }

        [DataTestMethod]
        [DataRow("param test string = 'test'", ParameterType.String)]
        [DataRow("param test int = 1", ParameterType.Int)]
        [DataRow("param test bool = true", ParameterType.Bool)]
        [DataRow(@"param test array = [
  1
  2
]", ParameterType.Array)]
        [DataRow(@"param test object = {
  displayName: 'Blocked Resource Types policy definition'
  description: 'Block certain resource types'
}", ParameterType.Object)]
        [DataRow("param test ", null)]
        public void VerifyParameterType(string bicepFileContents, ParameterType? expected)
        {
            var programSyntax = ParserHelper.Parse(bicepFileContents);
            programSyntax.Should().NotBeNull();

            var parameterDeclarationSyntax = programSyntax.Declarations.First() as ParameterDeclarationSyntax;
            parameterDeclarationSyntax.Should().NotBeNull();

            var bicepFilePath = FileHelper.SaveResultFile(TestContext, "input.bicep", bicepFileContents);
            var documentUri = DocumentUri.FromFileSystemPath(bicepFilePath);
            var bicepCompilationManager = BicepCompilationManagerHelper.CreateCompilationManager(documentUri, bicepFileContents, true);
            var bicepDeploymentParametersHandler = new BicepDeploymentParametersHandler(bicepCompilationManager, Serializer);

            var result = bicepDeploymentParametersHandler.GetParameterType(parameterDeclarationSyntax!);

            result.Should().Be(expected);
        }
    }
}
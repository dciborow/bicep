// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Extensions;
using Bicep.Core.FileSystem;
using Bicep.Core.Navigation;
using Bicep.Core.Parsing;
using Bicep.Core.Samples;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;
using Bicep.Core.Syntax.Visitors;
using Bicep.Core.Text;
using Bicep.Core.UnitTests;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.FileSystem;
using Bicep.Core.UnitTests.Utils;
using Bicep.Core.Workspaces;
using Bicep.LangServer.IntegrationTests.Assertions;
using Bicep.LangServer.IntegrationTests.Extensions;
using Bicep.LangServer.IntegrationTests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SymbolKind = Bicep.Core.Semantics.SymbolKind;

namespace Bicep.LangServer.IntegrationTests
{
    [TestClass]
    public class FoldingTests
    {
        private static readonly SharedLanguageHelperManager DefaultServer = new();
        private static readonly SharedLanguageHelperManager ServerWithBuiltInTypes = new();
        private static readonly SharedLanguageHelperManager ServerWithTestNamespaceProvider = new();

        [NotNull]
        public TestContext? TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            DefaultServer.Initialize(async () => await MultiFileLanguageServerHelper.StartLanguageServer(testContext));
            ServerWithBuiltInTypes.Initialize(async () => await MultiFileLanguageServerHelper.StartLanguageServer(testContext, services => services.WithNamespaceProvider(BuiltInTestTypes.Create())));
            ServerWithTestNamespaceProvider.Initialize(async () => await MultiFileLanguageServerHelper.StartLanguageServer(testContext));
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            await DefaultServer.DisposeAsync();
            await ServerWithBuiltInTypes.DisposeAsync();
            await ServerWithTestNamespaceProvider.DisposeAsync();
        }

        [TestMethod]
        public async Task Hovers_are_displayed_on_discription_decorator_objects_across_bicep_modules()
        {
            var inputFile = @"
@description('this is param1')
param param1 string

@description('this is param2')
@allowed([ 'new', 'existing', 'none' ])
param param2 string

var var2 = param2

@description('this is out1')
output out1 string = '${param1}-out1'
",
                '|');

            var folded = @"
@... param param1 string
@... param param2 string

var var2 = param2

@... output out1 string = '${param1}-out1'
",
                '|');

            var bicepFile = SourceFileFactory.CreateBicepFile(new Uri("file:///path/to/main.bicep"), file);
            var foldedFile = SourceFileFactory.CreateBicepFile(new Uri("file:///path/to/main.bicep"), folded);

            var files = new Dictionary<Uri, string>
            {
                [bicepFile.FileUri] = file,
                [foldedFile.FileUri] = folded
            };

            using var helper = await LanguageServerHelper.StartServerWithText(this.TestContext, files, bicepFile.FileUri, services => services.WithNamespaceProvider(BuiltInTestTypes.Create()));
            var client = helper.Client;

            var hovers = await RequestFolding(client, bicepFile, cursors);

            hovers.Should().SatisfyRespectively(
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\n@... param param1 stringn"),
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\n@... param param2 string\n"),
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\n\n"),
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\nvar var2 = param2\n"),
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\n\n"),
                h => h!.Contents.MarkupContent!.Value.Should().EndWith("```\n@... output out1 string = '${param1}-out1'\n"));
        }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Threading;
using System.Threading.Tasks;
using Bicep.LanguageServer.CompilationManager;
using Bicep.LanguageServer.Utils;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Bicep.LanguageServer.Handlers
{
    public class BicepFoldingHandler : FoldingRangeHandlerBase
    {
        private readonly ILogger<BicepFoldingHandler> logger;
        private readonly ICompilationManager compilationManager;

        // TODO: Not sure if this needs to be shared.
        private readonly FoldingLegend legend = new();

        public BicepFoldingHandler(ILogger<BicepFoldingHandler> logger, ICompilationManager compilationManager)
        {
            this.logger = logger;
            this.compilationManager = compilationManager;
        }

        protected override Task<FoldingDocument> GetFoldingDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FoldingDocument(this.legend));
        }

        protected override Task Fold(FoldingBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            var compilationContext = this.compilationManager.GetCompilation(identifier.TextDocument.Uri);

            if (compilationContext != null)
            {
                FoldingVisitor.BuildFolding(builder, compilationContext.Compilation.SourceFileGrouping.EntryPoint);
            }

            return Task.CompletedTask;
        }

        protected override FoldingRegistrationOptions CreateRegistrationOptions(FoldingCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = DocumentSelectorFactory.Create(),
            Legend = this.legend,
            Full = new FoldingCapabilityRequestFull
            {
                Delta = true
            },
            Range = true
        };
    }
}

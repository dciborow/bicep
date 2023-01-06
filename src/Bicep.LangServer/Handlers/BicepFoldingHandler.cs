// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
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
        readonly TextDocumentManager _documentManager;

        public BicepFoldingHandler(TextDocumentManager textDocManager)
        {
            _documentManager = textDocManager;
        }

        public override Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
        {
            var foldingRanges = new List<FoldingRange>();

            var doc = _documentManager.GetDocument(request.TextDocument.Uri.ToString());
            if (doc != null)
            {
                var regions = GetRegions(doc);
                foreach (var region in regions)
                {
                    foldingRanges.Add(new FoldingRange
                    {
                        StartLine = region.StartLineNo - 1,
                        StartCharacter = region.StartCharPos,
                        EndLine = region.EndLineNo - 1,
                        EndCharacter = region.EndCharPos,
                        Kind = FoldingRangeKind.Region
                    }); ;
                }
            }
            return Task.FromResult<Container<FoldingRange>?>(new Container<FoldingRange>(foldingRanges.ToArray()));
        }

        private static Object GetRegions(Object doc)
        {
            //TODO: Implement custom Regions for Bicep
            var regions = doc.Proc.Regions;

            return regions;
        }

        protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities)
        {
            return new FoldingRangeRegistrationOptions
            {
                DocumentSelector = DocumentSelector.ForPattern(@"**/*.bicep")
            };
        }

    }
}

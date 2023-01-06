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
    }
}

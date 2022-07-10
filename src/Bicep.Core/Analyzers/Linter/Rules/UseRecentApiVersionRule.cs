// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bicep.Core.ApiVersion;
using Bicep.Core.CodeAction;
using Bicep.Core.Configuration;
using Bicep.Core.Diagnostics;
using Bicep.Core.Parsing;
using Bicep.Core.Resources;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;

namespace Bicep.Core.Analyzers.Linter.Rules
{
    //asdfg prefix -> suffix

    //    //asdfg
    //      if ($FullResourceTypes -like '*/providers/*') {
    //        # If we have a provider resources
    //        $FullResourceTypes = @($FullResourceTypes -split '/')
    //        if ($av.Name -match "'/{0,}(?<ResourceType>\w+\.\w+)/{0,}'") {
    //            $FullResourceTypes = @($matches.ResourceType)
    //        }
    //        else
    //{
    //    Write - Warning "Could not identify provider resource for $($FullResourceTypes -join '/')"
    //            continue
    //        }
    //    }

    //asdfg update
    // Adds linter rule to flag an issue when api version used in resource is not recent
    // 1. Any GA version is allowed as long as it's less than years old, even if there is a more recent GA version
    // 2. If there is no GA apiVersion less than 2 years old, then accept only the latest one GA version available
    // 3. A non-stable version (api version with any -* suffix, such as -preview) is accepted only if it is latest and there is no later GA version
    // 4. For non preview versions(e.g. alpha, beta, privatepreview and rc), order of preference is latest GA -> Preview -> Non Preview   asdf????
    public sealed class UseRecentApiVersionRule : LinterRuleBase
    {
        public new const string Code = "use-recent-api-version";
        public const int MaxAllowedAgeInDays = 365 * 2;

        private DateTime today = DateTime.Today;

        public UseRecentApiVersionRule() : base(
            code: Code,
            description: CoreResources.UseRecentApiVersionRuleDescription,
            docUri: new Uri($"https://aka.ms/bicep/linter/{Code}"),
            diagnosticStyling: DiagnosticStyling.Default)
        {
        }

        public override void Configure(AnalyzersConfiguration config)
        {
            base.Configure(config);

            // Today's date can be changed to enable testing/debug scenarios
            string? debugToday = this.GetConfigurationValue<string?>("debug-today", null);
            if (debugToday is not null)
            {
                this.today = ApiVersionHelper.ParseDate(debugToday);
            }
        }

        override public IEnumerable<IDiagnostic> AnalyzeInternal(SemanticModel model)
        {
            var visitor = new Visitor(model, today, UseRecentApiVersionRule.MaxAllowedAgeInDays);
            visitor.Visit(model.SourceFile.ProgramSyntax);

            return visitor.Fixes.Select(fix => CreateFixableDiagnosticForSpan(fix.Span, fix.Fix));
        }

        public sealed class Visitor : SyntaxVisitor
        {
            internal readonly List<(TextSpan Span, CodeFix Fix)> Fixes = new();

            private readonly IApiVersionProvider apiVersionProvider;
            private readonly SemanticModel model;
            private readonly DateTime today;
            private readonly int maxAllowedAgeInDays;


            public Visitor(SemanticModel model, DateTime today, int maxAllowedAgeInDays)
            {
                this.apiVersionProvider = model.ApiVersionProvider ?? new ApiVersionProvider();
                this.model = model;
                this.today = today;
                this.maxAllowedAgeInDays = maxAllowedAgeInDays;
            }

            public override void VisitResourceDeclarationSyntax(ResourceDeclarationSyntax resourceDeclarationSyntax)
            {
                ResourceSymbol resourceSymbol = new ResourceSymbol(model.SymbolContext, resourceDeclarationSyntax.Name.IdentifierName, resourceDeclarationSyntax);

                if (resourceSymbol.TryGetResourceTypeReference() is ResourceTypeReference resourceTypeReference &&
                    resourceTypeReference.ApiVersion is string apiVersion &&
                    GetReplacementSpan(resourceSymbol, apiVersion) is TextSpan replacementSpan)
                {
                    string fullyQualifiedResourceType = resourceTypeReference.FormatType();
                    var fix = CreateFixIfFails(replacementSpan, fullyQualifiedResourceType, apiVersion);

                    if (fix is not null)
                    {
                        Fixes.Add(fix.Value);
                    }
                }

                base.VisitResourceDeclarationSyntax(resourceDeclarationSyntax);
            }

            public (TextSpan span, CodeFix fix)? CreateFixIfFails(TextSpan replacementSpan, string fullyQualifiedResourceType, string actualApiVersion)
            {
                (string? currentApiDate, string? actualApiSuffix) = ApiVersionHelper.TryParse(actualApiVersion);
                if (currentApiDate is null)
                {//asdfg testpoint
                    // The API version is not valid. Bicep will show an error, so we don't want to show anything else
                    return null;
                }

                var (allApiVersions, acceptableApiVersions) = GetAcceptableApiVersions(apiVersionProvider, today, maxAllowedAgeInDays, fullyQualifiedResourceType);
                if (!allApiVersions.Any())
                {
                    // Resource type not recognized. Bicep will show a warning, so we don't want to show anything else
                    return null;//asdfg testpoint
                }

                Debug.Assert(acceptableApiVersions.Any(), $"There should always be at least one acceptable version for a valid resource type: {fullyQualifiedResourceType}");
                if (acceptableApiVersions.Contains(actualApiVersion)) //asdfg case insensitive
                {//asdfg testpoint
                    // Passed - version is acceptable
                    return null;
                }

                if (!allApiVersions.Contains(actualApiVersion))
                {//asdfg testpoint
                    // apiVersion for resource type not recognized. Bicep will show a warning, so we don't want to show anything else
                    return null;
                }

                int ageInDays = today.Subtract(ApiVersionHelper.ParseDate(actualApiVersion)).Days;
                return CreateCodeFix(
                    replacementSpan,
                    fullyQualifiedResourceType,
                    actualApiVersion,
                    $"'{actualApiVersion}' is {ageInDays} days old, should be no more than {maxAllowedAgeInDays} days old.",
                    acceptableApiVersions);
            }

            //asdfg handle when there's a newer non-preview version
            public static (string[] allApiVersions, string[] acceptableVersions) GetAcceptableApiVersions(IApiVersionProvider apiVersionProvider, DateTime today, int maxAllowedAgeInDays, string fullyQualifiedResourceType)
            {
                var allVersionsSorted = apiVersionProvider.GetSortedValidApiVersions(fullyQualifiedResourceType).ToArray();
                if (!allVersionsSorted.Any())
                {
                    // The resource type is not recognized.
                    return (allVersionsSorted, Array.Empty<string>());
                }

                var oldestAcceptableDate = ApiVersionHelper.Format(today.AddDays(-maxAllowedAgeInDays), null);

                var stableVersionsSorted = allVersionsSorted.Where(v => !ApiVersionHelper.IsPreviewVersion(v)).ToArray();
                var previewVersionsSorted = allVersionsSorted.Where(v => ApiVersionHelper.IsPreviewVersion(v)).ToArray();

                var recentStableVersionsSorted = FilterRecentVersions(stableVersionsSorted, oldestAcceptableDate).ToArray();
                var recentPreviewVersionsSorted = FilterRecentVersions(previewVersionsSorted, oldestAcceptableDate).ToArray();

                // Start with all recent stable versions
                List<string> acceptableVersions = recentStableVersionsSorted.ToList();

                // If no recent stable versions, add the most recent stable version, if any
                if (!acceptableVersions.Any())
                {
                    acceptableVersions.AddRange(FilterMostRecentApiVersion(stableVersionsSorted));
                }

                // Add all recent (not old) preview versions that are newer than the newest stable version, if any
                var mostRecentStableDate = GetNewestDateInApiVersions(stableVersionsSorted);
                if (mostRecentStableDate is not null)
                {
                    Debug.Assert(stableVersionsSorted.Any(), "There should have been at least one stable version since mostRecentStableDate != null");
                    acceptableVersions.AddRange(FilterApiVersionsNewerThanDate(recentPreviewVersionsSorted, mostRecentStableDate));
                }
                else
                {
                    // There are no stable versions available at all - add all preview versions that are recent enough
                    acceptableVersions.AddRange(recentPreviewVersionsSorted);

                    // If there are no recent preview versions, add the newest preview only
                    if (!acceptableVersions.Any())
                    {
                        acceptableVersions.AddRange(FilterMostRecentApiVersion(previewVersionsSorted));
                        Debug.Assert(acceptableVersions.Any(), "There should have been at least one preview version available to add");
                    }
                }

                acceptableVersions.Sort((v1, v2) => //asdfg test
                {
                    // Sort by date descending, then stable first, then others alphabetically ascending
                    var dateCompare = ApiVersionHelper.CompareApiVersionDates(v1, v2);
                    if (dateCompare != 0)
                    {
                        return -dateCompare;
                    }

                    var v1IsStable = !ApiVersionHelper.IsPreviewVersion(v1);
                    var v2IsStable = !ApiVersionHelper.IsPreviewVersion(v2);
                    if (v1IsStable && !v2IsStable)
                    {
                        return -1;
                    }
                    else if (v2IsStable && !v2IsStable)
                    {
                        return 1;
                    }

                    return string.CompareOrdinal(v1, v2);
                });

                Debug.Assert(acceptableVersions.Any(), $"Didn't find any acceptable API versions for {fullyQualifiedResourceType}");
                return (allVersionsSorted, acceptableVersions.ToArray());
            }

            private TextSpan? GetReplacementSpan(ResourceSymbol resourceSymbol, string apiVersion)
            {
                if (resourceSymbol.DeclaringResource.TypeString is StringSyntax typeString &&
                    typeString.StringTokens.First() is Token token)
                {
                    int replacementSpanStart = token.Span.Position + token.Text.IndexOf(apiVersion);

                    return new TextSpan(replacementSpanStart, apiVersion.Length);
                }

                return null;
            }

            private (TextSpan Span, CodeFix Fix) CreateCodeFix(TextSpan span, string fullyQualifiedResourceType, string actualApiVersion, string reason, string[] acceptableApiVersions)
            {
                var preferredVersion = acceptableApiVersions[0];
                var codeReplacement = new CodeReplacement(span, preferredVersion);

                var acceptableVersionsString = string.Join(", ", acceptableApiVersions);

                string description = string.Format(CoreResources.UseRecentApiVersionRuleMessageFormat, fullyQualifiedResourceType, reason, acceptableVersionsString);
                var fix = new CodeFix(description, true, CodeFixKind.QuickFix, codeReplacement);

                return (span, fix);
            }

            // Returns just the date string, not an entire apiVersion
            private static string? GetNewestDateInApiVersions(string[] apiVersions) //asdfg test
            {
                // We're safe to use Max on the apiVersion date strings since they're in the form yyyy-MM-dd, will give most recently since they're sorted ascending
                return apiVersions.Max(v => ApiVersionHelper.TryParse(v).date);
            }

            // Retrieves the most recent API version (this could be more than one if there are multiple apiVersions
            //   with the same, most recent date)
            private static IEnumerable<string> FilterMostRecentApiVersion(string[] apiVersions)
            {
                var mostRecentDate = GetNewestDateInApiVersions(apiVersions);
                if (mostRecentDate is not null)
                {
                    return FilterApiVersionsWithDate(apiVersions, mostRecentDate);
                }

                return Array.Empty<string>();
            }

            // Returns just the date string, not an entire apiVersion
            private static IEnumerable<string> FilterApiVersionsNewerThanDate(string[] apiVersions, string date)
            {
                return apiVersions.Where(v => ApiVersionHelper.CompareApiVersionDates(v, date) > 0);
            }

            private static IEnumerable<string> FilterApiVersionsNewerOrEqualToDate(string[] apiVersions, string date)
            {
                return apiVersions.Where(v => ApiVersionHelper.CompareApiVersionDates(v, date) >= 0);
            }

            // Returns just the date string, not an entire apiVersion
            private static IEnumerable<string> FilterApiVersionsWithDate(IEnumerable<string> apiVersions, string date)
            {
                return apiVersions.Where(v => ApiVersionHelper.CompareApiVersionDates(v, date) == 0);
            }

            private static IEnumerable<string> FilterRecentVersions(string[] apiVersions, string lastAcceptableRecentDate)
            {
                return FilterApiVersionsNewerOrEqualToDate(apiVersions, lastAcceptableRecentDate);
            }
        }
    }
}

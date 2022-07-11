// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Analyzers.Linter.Rules;
using Bicep.Core.ApiVersion;
using Bicep.Core.Configuration;
using Bicep.Core.Json;
using Bicep.Core.Parsing;
using Bicep.Core.Semantics;
using Bicep.Core.TypeSystem;
using Bicep.Core.UnitTests.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// asdfg test with case sensitivity
// asdfg test with different scopes
// asdfg does it need to understand imported types?
// asdfg test two years
//asdfg deployment scopes
namespace Bicep.Core.UnitTests.Diagnostics.LinterRuleTests
{
    [TestClass]
    public class UseRecentApiVersionRuleTests : LinterRuleTestsBase
    {
        public UseRecentApiVersionRuleTests()
        {

        }

        // Welcome to the 25th century!
        // All tests using CompileAndTest set this value as "today" as far as the rule is concerned
        //private const string FAKE_TODAY_DATE = "2422-07-04";

        //private void CompileAndTest(string bicep, OnCompileErrors onCompileErrors, params string[] expectedMessagesForCode)
        //{
        //    // Test with today's date and real types.
        //    AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
        //       bicep,
        //       expectedMessagesForCode,
        //       onCompileErrors,
        //       IncludePosition.LineNumber);
        //}

        //private void CompileAndTestWithFakeDateAndTypes(string bicep, string fakeToday, string[] expectedMessagesForCode)
        //{
        //    // Test with the linter thinking today's date is fakeToday and also using fake resource types from FakeResourceTypes
        //    // Note: The compiler does not know about these fake types, only the linter.

        //    RootConfiguration configurationWithFakeTodayDate = CreateConfigurationWithFakeToday(fakeToday);
        //    AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
        //        bicep,
        //        expectedMessagesForCode,
        //        OnCompileErrors.IncludeErrors,
        //        IncludePosition.LineNumber,
        //        configuration: configurationWithFakeTodayDate,
        //        apiVersionProvider: FakeApiVersionProviderResourceScope);
        //}

        private void CompileAndTestWithFakeDateAndTypes(string bicep, ResourceScope scope, string[] resourceTypes, string fakeToday, params string[] expectedMessagesForCode)
        {
            // Test with the linter thinking today's date is fakeToday and also fake resource types from FakeResourceTypes
            // Note: The compiler does not know about these fake types, only the linter.
            var apiProvider = new ApiVersionProvider();
            apiProvider.InjectTypeReferences(scope, FakeResourceTypes.GetFakeResourceTypeReferences(resourceTypes));

            AssertLinterRuleDiagnostics(UseRecentApiVersionRule.Code,
                bicep,
                expectedMessagesForCode,
                OnCompileErrors.IncludeErrors,
                IncludePosition.LineNumber,
                configuration: CreateConfigurationWithFakeToday(fakeToday),
                apiVersionProvider: apiProvider);
        }

        // Uses fake resource types from FakeResourceTypes
        //asdfg private readonly IApiVersionProvider FakeApiVersionProviderResourceScope = new ApiVersionProvider(FakeResourceTypes.GetFakeResourceTypeReferences(FakeResourceTypes.ResourceScope));

        //asdfg private static RootConfiguration ConfigurationWithFakeTodayDate = CreateConfigurationWithFakeToday(FAKE_TODAY_DATE);

        private static SemanticModel SemanticModel(RootConfiguration configuration, IApiVersionProvider apiVersionProvider)
            => new Compilation(
                   BicepTestConstants.Features,
                   TestTypeHelper.CreateEmptyProvider(),
                   SourceFileGroupingFactory.CreateFromText(string.Empty, BicepTestConstants.FileResolver),
                   configuration,
                   apiVersionProvider,
                   new LinterAnalyzer(configuration))
                .GetEntrypointSemanticModel();

        private static RootConfiguration CreateConfigurationWithFakeToday(string today)
        {
            return new RootConfiguration(
                BicepTestConstants.BuiltInConfiguration.Cloud,
                BicepTestConstants.BuiltInConfiguration.ModuleAliases,
                    new AnalyzersConfiguration(
                         JsonElementFactory.CreateElement(@"
                            {
                              ""core"": {
                                ""enabled"": true,
                                ""rules"": {
                                  ""use-recent-api-version"": {
                                      ""level"": ""warning"",
                                      ""debug-today"": ""<TESTING_TODAY_DATE>""
                                  }
                                }
                              }
                            }".Replace("<TESTING_TODAY_DATE>", today))),
                null);
        }

        [TestClass]
        public class GetAcceptableApiVersionsTests
        {
            private void TestGetAcceptableApiVersions(string fullyQualifiedResourceType, ResourceScope scope, string resourceTypes, string today, string[] expectedApiVersions, int maxAllowedAgeInDays = UseRecentApiVersionRule.MaxAllowedAgeInDays)
            {
                var apiVersionProvider = new ApiVersionProvider();
                apiVersionProvider.InjectTypeReferences(scope, FakeResourceTypes.GetFakeResourceTypeReferences(resourceTypes));
                var (allVersions, allowedVersions) = UseRecentApiVersionRule.Visitor.GetAcceptableApiVersions(apiVersionProvider, ApiVersionHelper.ParseDate(today), maxAllowedAgeInDays, scope, fullyQualifiedResourceType);
                allowedVersions.Should().BeEquivalentTo(expectedApiVersions, options => options.WithStrictOrdering());
            }


            [TestMethod]
            public void CaseInsensitiveResourceType()
            {
                TestGetAcceptableApiVersions(
                    "Fake.KUSTO/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2418-09-07-preview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2418-09-07-preview",
                    });
            }

            [TestMethod]
            public void CaseInsensitiveApiSuffix()
            {
                TestGetAcceptableApiVersions(
                    "Fake.KUSTO/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2418-09-07-PREVIEW
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2418-09-07-preview",
                    });
            }

            [TestMethod]
            public void ResourceTypeNotRecognized_ReturnNone()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kisto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2421-01-01
                    ",
                    "2421-07-07",
                    new string[]
                    {
                    });
            }

            [TestMethod]
            public void NoStable_OldPreview_PickOnlyMostRecentPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15-beta
                        Fake.Kusto/clusters@2413-09-07-alpha
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07-alpha",
                    });
            }

            [TestMethod]
            public void NoStable_OldPreview_PickOnlyMostRecentPreview_MultiplePreviewWithSameDate()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15-beta
                        Fake.Kusto/clusters@2413-09-07-alpha
                        Fake.Kusto/clusters@2413-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07-alpha",
                        "2413-09-07-beta",
                    });
            }

            [TestMethod]
            public void NoStable_NewPreview_PickAllNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-07-21-preview
                        Fake.Kusto/clusters@2419-08-15-beta
                        Fake.Kusto/clusters@2419-09-07-alpha
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-07-alpha",
                        "2419-08-15-beta",
                        "2419-07-21-preview",
                    });
            }

            [TestMethod]
            public void GNoStable_NewPreview_PickNewPreview_MultiplePreviewHaveSameDate()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-07-21-preview
                        Fake.Kusto/clusters@2419-08-15-beta
                        Fake.Kusto/clusters@2419-09-07-alpha
                        Fake.Kusto/clusters@2419-07-21-beta
                        Fake.Kusto/clusters@2419-08-15-privatepreview
                        Fake.Kusto/clusters@2419-09-07-beta
                        Fake.Kusto/clusters@2419-09-07-privatepreview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-07-alpha",
                        "2419-09-07-beta",
                        "2419-09-07-privatepreview",
                        "2419-08-15-beta",
                        "2419-08-15-privatepreview",
                        "2419-07-21-beta",
                        "2419-07-21-preview",
                    });
            }


            [TestMethod]
            public void GNoStable_OldAndNewPreview_PickNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-07-15-privatepreview
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2414-05-15-beta
                        Fake.Kusto/clusters@2415-09-07-alpha
                        Fake.Kusto/clusters@2419-08-21-beta
                        Fake.Kusto/clusters@2419-09-07-beta
                    ",

                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-07-beta",
                        "2419-08-21-beta",
                        "2419-07-15-privatepreview",
                    });
            }

            [TestMethod]
            public void OldStable_NoPreview_PickOnlyMostRecentStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-01-21
                        Fake.Kusto/clusters@2419-05-15
                        Fake.Kusto/clusters@2419-09-07
                        Fake.Kusto/clusters@2419-11-09
                        Fake.Kusto/clusters@2420-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2500-07-07",
                    new string[]
                    {
                        "2420-09-18",
                    });
            }

            [TestMethod]
            public void OldStable_OldPreview_NewestPreviewIsOlderThanNewestStable_PickOnlyNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-preview
                        Fake.Kusto/clusters@2413-09-07
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-09-07",
                    });
            }

            [TestMethod]
            public void OldStable_OldPreview_NewestPreviewIsSameAgeAsNewestStable_PickOnlyNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-preview
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2413-06-15-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-06-15",
                    });
            }

            [TestMethod]
            public void OldStable_OldPreview_NewestPreviewIsNewThanNewestStable_PickJustNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2413-09-07-preview
                        Fake.Kusto/clusters@2413-09-07-beta
                        Fake.Kusto/clusters@2413-09-08-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2413-06-15",
                    });
            }

            [TestMethod]
            public void OldStable_NewPreview_PickNewestStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2419-09-07-beta
                        Fake.Kusto/clusters@2419-09-08-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-08-beta",
                        "2419-09-07-beta",
                        "2419-09-07-preview",
                        "2413-06-15",
                    });
            }

            [TestMethod]
            public void OldStable_OldAndNewPreview_PickNewestStableAndNewPreview()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21
                        Fake.Kusto/clusters@2413-05-15
                        Fake.Kusto/clusters@2413-06-15-beta
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2419-09-07-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2419-09-07-beta",
                        "2419-09-07-preview",
                        "2413-05-15",
                    });
            }

            [TestMethod]
            public void NewStable_NoPreview_PickNewStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-08-15
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-09-18",
                        "2419-08-15",
                        "2419-07-21",
                    });
            }

            //asdfg should this be a separate rule?
            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_NoPreviewAreNewer()
            {
                TestGetAcceptableApiVersions(
                   "Fake.Kusto/clusters",
                   ResourceScope.ResourceGroup,
                   @"
                        Fake.Kusto/clusters@2413-07-21
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2419-07-16-beta
                        Fake.Kusto/clusters@2420-09-18
                    ",
                   "2421-07-07",
                   new string[]
                   {
                        "2420-09-18",
                        "2419-07-21",
                   });
            }

            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_OnePreviewIsOlder()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2420-09-18
                        Fake.Kusto/clusters@2421-07-16-beta
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2421-07-16-beta",
                        "2420-09-18",
                        "2419-07-21",
                    });
            }

            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_MultiplePreviewsAreNewer() //asdfg should this be a separate rule?
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-07-21
                        Fake.Kusto/clusters@2419-07-21
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2420-09-18
                        Fake.Kusto/clusters@2421-07-16-beta
                        Fake.Kusto/clusters@2421-07-17-preview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2421-07-17-preview",
                        "2421-07-16-beta",
                        "2420-09-18",
                        "2419-07-21",
                    });
            }

            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_MultiplePreviewsAreNewer_AllAreOld()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2415-07-21
                        Fake.Kusto/clusters@2415-07-15-alpha
                        Fake.Kusto/clusters@2416-09-18
                        Fake.Kusto/clusters@2417-07-16-beta
                        Fake.Kusto/clusters@2417-07-17-preview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2416-09-18",
                    });
            }

            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_MultiplePreviewsAreNewer_AllStableAreOld()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2415-07-21
                        Fake.Kusto/clusters@2415-07-15-alpha
                        Fake.Kusto/clusters@2416-09-18
                        Fake.Kusto/clusters@2421-07-16-beta
                        Fake.Kusto/clusters@2421-07-17-preview
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2421-07-17-preview",
                        "2421-07-16-beta",
                        "2416-09-18",
                    });
            }

            [TestMethod]
            public void OnlyPickPreviewThatAreNewerThanNewestStable_AllAreNew_NoPreviewAreNewerThanStable() //asdfg should this be a separate rule?
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-01-21-preview
                        Fake.Kusto/clusters@2419-07-11
                        Fake.Kusto/clusters@2419-07-15-alpha
                        Fake.Kusto/clusters@2419-07-16-beta
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-09-18",
                        "2419-07-11",
                    });
            }

            [TestMethod]
            public void OldAndNewStable_NoPreview_PickNewStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-01-21
                        Fake.Kusto/clusters@2419-05-15
                        Fake.Kusto/clusters@2419-09-07
                        Fake.Kusto/clusters@2421-01-01
                        Fake.Kusto/clusters@2425-01-01
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2425-01-01",
                        "2421-01-01",
                        "2419-09-07",
                    });
            }

            [TestMethod]
            public void OldAndNewStable_OldPreview_PickNewStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-09-07-privatepreview
                        Fake.Kusto/clusters@2413-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-09-18",
                        "2420-06-14",
                    });
            }

            [TestMethod]
            public void OldAndNewStable_NewPreviewButOlderThanNewestStable_PickNewStableOnly()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2419-09-07-privatepreview
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2420-09-18",
                        "2420-06-14",
                    });
            }

            [TestMethod]
            public void OldAndNewStable_OldAndNewPreview_PickNewStableAndPreviewNewestThanNewestStable()
            {
                TestGetAcceptableApiVersions(
                    "Fake.Kusto/clusters",
                    ResourceScope.ResourceGroup,
                    @"
                        Fake.Kusto/clusters@2413-09-07-privatepreview
                        Fake.Kusto/clusters@2421-09-07-privatepreview
                        Fake.Kusto/clusters@2419-09-07-preview
                        Fake.Kusto/clusters@2417-09-07-preview
                        Fake.Kusto/clusters@2414-01-21
                        Fake.Kusto/clusters@2414-05-15
                        Fake.Kusto/clusters@2414-09-07
                        Fake.Kusto/clusters@2415-11-09
                        Fake.Kusto/clusters@2415-02-15
                        Fake.Kusto/clusters@2420-06-14
                        Fake.Kusto/clusters@2420-09-18
                    ",
                    "2421-07-07",
                    new string[]
                    {
                        "2421-09-07-privatepreview",
                        "2420-09-18",
                        "2420-06-14",
                    });
            }



            //asdf newer preview




















        }

        [TestClass]
        public class AnalyzeApiVersionTests
        {
            private void Test(
                DateTime currentVersionDate,
                string currentVersionSuffix,
                DateTime[] gaVersionDates,
                DateTime[] previewVersionDates,
                (string reason, string acceptableVersions, string replacement)? expectedFix)
            {
                string currentVersion = ApiVersionHelper.Format(currentVersionDate) + currentVersionSuffix;
                string[] gaVersions = gaVersionDates.Select(d => "Whoever.whatever/whichever@" + ApiVersionHelper.Format(d)).ToArray();
                string[] previewVersions = previewVersionDates.Select(d => "Whoever.whatever/whichever@" + ApiVersionHelper.Format(d) + "-preview").ToArray();
                var apiVersionProvider = new ApiVersionProvider();
                apiVersionProvider.InjectTypeReferences(ResourceScope.ResourceGroup, FakeResourceTypes.GetFakeResourceTypeReferences(gaVersions.Concat(previewVersions)));
                var semanticModel = SemanticModel(BicepTestConstants.BuiltInConfiguration, apiVersionProvider);
                var visitor = new UseRecentApiVersionRule.Visitor(semanticModel, DateTime.Today, UseRecentApiVersionRule.MaxAllowedAgeInDays);

                var result = visitor.AnalyzeApiVersion(new TextSpan(17, 47), ResourceScope.ResourceGroup, "Whoever.whatever/whichever", currentVersion);

                if (expectedFix == null)
                {
                    result.Should().BeNull();
                }
                else
                {
                    result.Should().NotBeNull();
                    result!.Value.span.Should().Be(new TextSpan(17, 47));
                    result.Value.resourceType.Should().Be("Whoever.whatever/whichever");
                    result.Value.reason.Should().Be(expectedFix.Value.reason);
                    (string.Join(", ", result.Value.acceptableVersions)).Should().Be(expectedFix.Value.acceptableVersions);
                    result.Value.fixes.Should().HaveCount(1); // Right now we only create one fix
                    result.Value.fixes[0].Replacements.Should().SatisfyRespectively(r => r.Span.Should().Be(new TextSpan(17, 47)));
                    result.Value.fixes[0].Replacements.Select(r => r.Text).Should().BeEquivalentTo(new string[] { expectedFix.Value.replacement });
                }
            }

            [TestMethod]
            public void WithCurrentVersionLessThanTwoYearsOld_ShouldNotAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-1 * 365);
                DateTime recentGAVersionDate = DateTime.Today.AddDays(-5 * 31);

                Test(currentVersionDate, "", new DateTime[] { currentVersionDate, recentGAVersionDate }, new DateTime[] { },
                    null);
            }

            [TestMethod]
            public void WithCurrentVersionMoreThanTwoYearsOldAndRecentApiVersionIsAvailable_ShouldAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-3 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);
                DateTime recentGAVersionDate = DateTime.Today.AddDays(-5 * 30);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                Test(currentVersionDate, "", new DateTime[] { currentVersionDate, recentGAVersionDate }, new DateTime[] { },
                    (
                        $"'{currentVersion}' is {3 * 365} days old, should be no more than 730 days old.",
                        recentGAVersion,
                        recentGAVersion
                    ));
            }

            [TestMethod]
            public void WithCurrentAndRecentApiVersionsMoreThanTwoYearsOld_ShouldAddDiagnosticsToUseRecentApiVersion()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-4 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);

                DateTime recentGAVersionDate = DateTime.Today.AddDays(-3 * 365);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                Test(currentVersionDate, "", new DateTime[] { currentVersionDate, recentGAVersionDate }, new DateTime[] { },
                     (
                        $"'{currentVersion}' is {4 * 365} days old, should be no more than 730 days old.",
                        recentGAVersion,
                        recentGAVersion
                     ));
            }

            [TestMethod]
            public void WhenCurrentAndRecentApiVersionsAreSameAndMoreThanTwoYearsOld_ShouldNotAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-3 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);

                DateTime recentGAVersionDate = currentVersionDate;
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                Test(currentVersionDate, "", new DateTime[] { currentVersionDate }, new DateTime[] { },
                    null);
            }

            [TestMethod]
            public void WithPreviewVersion_WhenCurrentPreviewVersionIsLatest_ShouldNotAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);

                DateTime recentGAVersionDate = DateTime.Today.AddDays(-3 * 365);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                DateTime recentPreviewVersionDate = currentVersionDate;
                string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);


                Test(currentVersionDate, "", new DateTime[] { currentVersionDate, recentGAVersionDate }, new DateTime[] { recentPreviewVersionDate },
                     null);
            }

            [TestMethod]
            public void WithOldPreviewVersion_WhenRecentPreviewVersionIsAvailable_ButIsOlderThanGAVersion_ShouldAddDiagnosticsAboutBeingOld()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-5 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate, "-preview");

                DateTime recentGAVersionDate = DateTime.Today.AddDays(-1 * 365);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                DateTime recentPreviewVersionDate = DateTime.Today.AddDays(-2 * 365);
                string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate, "-preview");

                Test(currentVersionDate, "-preview", new DateTime[] { recentGAVersionDate }, new DateTime[] { currentVersionDate, recentPreviewVersionDate },
                    (
                       //asdfg ask brian: show by date or ga first?
                       $"'{currentVersion}' is {5 * 365} days old, should be no more than 730 days old.",
                       recentGAVersion,
                       recentGAVersion //ASDFG??? or preview version?  ask brian
                    ));
            }

            //[TestMethod]
            //public void WithPreviewVersion_WhenRecentPreviewVersionIsAvailable_AndIfNewerGAVersion_ShouldAddDiagnosticsToUseGA()
            //{
            //    DateTime currentVersionDate = DateTime.Today.AddDays(-5 * 365);
            //    string currentVersion = ApiVersionHelper.Format(currentVersionDate, "-preview");

            //    DateTime recentGAVersionDate = DateTime.Today.AddDays(-3 * 365);
            //    string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

            //    DateTime recentPreviewVersionDate = DateTime.Today.AddDays(-2 * 365);
            //    string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate, "-preview");

            //    TestCreateFixIfFails(currentVersionDate, "-preview", new DateTime[] { recentGAVersionDate }, new DateTime[] { currentVersionDate, recentPreviewVersionDate },
            //        (
            //           //asdfg ask brian: show by date or ga first?
            //           $"'{currentVersion}' is a preview version and there is a more recent non-preview version available.",recentPreviewVersion}, {recentGAVersion}",
            //           recentGAVersion //ASDFG??? or preview version?  ask brian
            //        ));
            //}

            [TestMethod]
            public void WithOldPreviewVersion_WhenRecentGAVersionIsAvailable_ShouldAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-5 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);

                DateTime recentGAVersionDate = DateTime.Today.AddDays(-2 * 365);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                DateTime recentPreviewVersionDate = DateTime.Today.AddDays(-3 * 365);
                string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate);

                Test(currentVersionDate, "-preview", new DateTime[] { recentGAVersionDate }, new DateTime[] { currentVersionDate, recentPreviewVersionDate },
                  (
                     $"'{currentVersion}-preview' is {5 * 365} days old, should be no more than 730 days old.",
                     recentGAVersion,
                     recentGAVersion
                  ));
            }


            [TestMethod]
            public void WithRecentPreviewVersion_WhenRecentGAVersionIsAvailable_ShouldAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-2 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate);

                DateTime recentGAVersionDate = DateTime.Today.AddDays(-1 * 365);
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                Test(currentVersionDate, "-preview", new DateTime[] { recentGAVersionDate }, new DateTime[] { currentVersionDate },
                  (
                     $"'{currentVersion}-preview' is a preview version and there is a more recent non-preview version available.",
                     recentGAVersion,
                     recentGAVersion
                  ));
            }

            [TestMethod]
            public void WithRecentPreviewVersion_WhenRecentGAVersionIsSameAsPreviewVersion_ShouldAddDiagnosticsUsingGAVersion()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-2 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate, "-preview");

                DateTime recentGAVersionDate = currentVersionDate;
                string recentGAVersion = ApiVersionHelper.Format(recentGAVersionDate);

                Test(currentVersionDate, "-preview", new DateTime[] { recentGAVersionDate }, new DateTime[] { currentVersionDate },
                 (
                    $"'{currentVersion}' is a preview version and there is a non-preview version available with the same date.",
                    recentGAVersion,
                    recentGAVersion
                 ));
            }

            [TestMethod]
            public void WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsNotRecent_ShouldAddDiagnosticsUsingRecentPreviewVersion()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-3 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate, "-preview");

                DateTime recentPreviewVersionDate = DateTime.Today.AddDays(-2 * 365);
                string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate, "-preview");

                Test(currentVersionDate, "-preview", new DateTime[] { }, new DateTime[] { recentPreviewVersionDate, currentVersionDate },
                    (
                       $"'{currentVersion}' is {3 * 365} days old, should be no more than 730 days old.",
                       recentPreviewVersion,
                       recentPreviewVersion
                    ));
            }

            [TestMethod]
            public void WithPreviewVersion_WhenGAVersionisNull_AndCurrentVersionIsRecent_ShouldNotAddDiagnostics()
            {
                DateTime currentVersionDate = DateTime.Today.AddDays(-2 * 365);
                string currentVersion = ApiVersionHelper.Format(currentVersionDate, "-preview");

                DateTime recentPreviewVersionDate = currentVersionDate;
                string recentPreviewVersion = ApiVersionHelper.Format(recentPreviewVersionDate, "-preview");

                Test(currentVersionDate, "-preview", new DateTime[] { }, new DateTime[] { recentPreviewVersionDate, currentVersionDate },
                    null);
            }
        }

        [TestMethod]
        public void ArmTtk_ApiVersionIsNotAnExpression_Error()
        {
            string bicep = @"
                resource publicIPAddress1 'fake.Network/publicIPAddresses@[concat(\'2020\', \'01-01\')]' = {
                  name: 'publicIPAddress1'
                  #disable-next-line no-loc-expr-outside-params
                  location: resourceGroup().location
                  tags: {
                    displayName: 'publicIPAddress1'
                  }
                  properties: {
                    publicIPAllocationMethod: 'Dynamic'
                  }
                }";
            CompileAndTestWithFakeDateAndTypes(bicep,
                ResourceScope.ResourceGroup,
                FakeResourceTypes.ResourceScopeTypes,
                "2422-07-04",
                new string[] { "[1] The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\"." });
        }

        ////asdfg
        //[TestMethod]
        //public void NestedResources1_Fail()
        //{
        //    string bicep = @"
        //        param location string

        //        resource namespace1 'fake.ServiceBus/namespaces@2418-01-01-preview' = {
        //          name: 'namespace1'
        //          location: location
        //          properties: {
        //          }
        //        }

        //        // Using 'parent'
        //        resource namespace1_queue1 'fake.ServiceBus/namespaces/queues@2417-04-01' = {
        //          parent: namespace1
        //          name: 'queue1'
        //        }

        //        // Using 'parent'
        //        resource namespace1_queue1_rule1 'fake.ServiceBus/namespaces/queues/authorizationRules@2415-08-01' = {
        //          parent: namespace1_queue1
        //          name: 'rule1'
        //        }

        //        // Using nested name
        //        resource namespace1_queue2 'fake.ServiceBus/namespaces/queues@2417-04-01' = {
        //          name: 'namespace1/queue1'
        //        }

        //        // Using 'parent'
        //        resource namespace1_queue2_rule2 'fake.ServiceBus/namespaces/queues/authorizationRules@2418-01-01-preview' = {
        //          parent: namespace1_queue2
        //          name: 'rule2'
        //        }

        //        // Using nested name
        //        resource namespace1_queue2_rule3 'fake.ServiceBus/namespaces/queues/authorizationRules@4017-04-01' = {
        //          name: 'namespace1/queue2/rule3'
        //        }";

        //    CompileAndTestWithFakeDateAndTypes(bicep,
        //        "2422-07-04",
        //        new string[] {
        //            "[3] Use recent API versions",
        //            "[11] Use recent API versions",
        //            "[17] Use recent API versions",
        //            "[23] Use recent API versions",
        //            "[28] Use recent API versions",
        //            "[34] Use recent API versions"
        //        });
        //}

        //asdfg
        //[TestMethod]
        //public void NestedResources2_Fail()
        //{
        //    string bicep = @"
        //        param location string

        //        // Using resource nesting
        //        resource namespace2 'Microsoft.ServiceBus/namespaces@2018-01-01-preview' = {
        //          name: 'namespace2'
        //          location: location

        //          resource queue1 'queues@2015-08-01' = {
        //            name: 'queue1'
        //            location: location

        //            resource rule1 'authorizationRules@2018-01-01-preview' = {
        //              name: 'rule1'
        //            }
        //          }
        //        }

        //        // Using nested name (parent is a nested resource)
        //        resource namespace2_queue1_rule2 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
        //          name: 'namespace2/queue1/rule2'
        //        }

        //        // Using parent (parent is a nested resource)
        //        resource namespace2_queue1_rule3 'Microsoft.ServiceBus/namespaces/queues/authorizationRules@2017-04-01' = {
        //          parent: namespace2::queue1
        //          name: 'rule3'
        //        }";

        //    CompileAndTestWithFakeDateAndTypes(bicep,
        // "2422-07-04",
        //        "[4] Use recent API versions",
        //        "[8] Use recent API versions",
        //        "[12] Use recent API versions",
        //        "[19] Use recent API versions",
        //        "[24] Use recent API versions"
        //        );
        //}

        [TestMethod]
        public void ArmTtk_NotAString_Error()
        {
            string bicep = @"
                resource publicIPAddress1 'fake.Network/publicIPAddresses@True' = {
                name: 'publicIPAddress1'
#disable-next-line no-loc-expr-outside-params no-hardcoded-location
                location: 'westus'
                tags: {
                    displayName: 'publicIPAddress1'
                }
                properties: {
                    publicIPAllocationMethod: 'Dynamic'
                }
            }
            ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                FakeResourceTypes.ResourceScopeTypes,
                "2422-07-04",
                new string[] {
                  "[1] The resource type is not valid. Specify a valid resource type of format \"<types>@<apiVersion>\"."
                });
        }






        //asdfg test with non-preview versions at least as recent (plus when only older)
        // current algorithm:
        //   complain if there's a stable with same date as the preview being used (but only if apiversion not found)
        //   comaplin if there's a more recent stable than the preview being used (but only if old)
        // e.g.
        /* 
          "Microsoft.SecurityInsights/threatIntelligence": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2022-07-01-preview",
        "2022-06-01-preview",
        "2022-05-01-preview",
        "2022-04-01-preview",
        "2022-01-01-preview",
        "2021-10-01-preview",
        "2021-10-01",
        "2021-09-01-preview",
        "2021-04-01",
        "2019-01-01-preview"
      ],
        */


        //asdfg
        /*
           "Microsoft.DBforMySQL/operations": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2021-12-01-preview", pass
        "2021-05-01-preview", pass
        "2021-05-01", pass
        "2017-12-01-preview", fail (newer stable lavailabe)
        "2017-12-01"  fail
      ],
        */


        /*asdfg

        "Microsoft.DataProtection/ResourceGuards": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2022-05-01",
        "2022-04-01",
        "2022-03-01",
        "2022-02-01-preview",
        "2022-01-01",
        "2021-12-01-preview",
        "2021-10-01-preview",
        "2021-07-01",
        "2021-02-01-preview"
      ],


        2022-02-01-preview:

            [-] apiVersions Should Be Recent (14 ms)                                                                            
        Microsoft.DataProtection/ResourceGuards uses a preview version ( 2022-02-01-preview ) and there are more recent versions available. Line: 6, Column: 8
        Valid Api Versions:                                                                                             
        2022-05-01                                                                                                      
        2022-05-01                                                                                                      
        2022-04-01                                                                                                      
        2022-03-01                                                                                                      
        2022-02-01-preview                                                                                              
        2022-01-01                                                                                                      
        2021-12-01-preview                                                                                              
        2021-10-01-preview                                                                                              
        2021-07-01                                                                                                      
        2021-02-01-preview  
        */



        //asdfg
        /* "Microsoft.Sql/servers/databases/geoBackupPolicies": [
    {
      "aliases": null,
      "apiProfiles": null,
      "apiVersions": [
        "2015-05-01-preview",
        "2014-04-01-preview",
        "2014-04-01"
      ],*/



        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithSameDateAsStable_Fail()
        {
            string bicep = @"
                    resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                      name: 'db]'
                    #disable-next-line no-hardcoded-location
                      location: 'westeurope'
                      properties: {
                        administratorLogin: 'sa'
                        administratorLoginPassword: 'don\'t put passwords in plain text'
                        createMode: 'Default'
                        sslEnforcement: 'Disabled'
                      }
                    }
                ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                new string[]
                {
                       "Fake.DBforMySQL/servers@2417-12-01",
                       "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04",
                //asdfg wich? "[1] Use recent apiVersions. There is a non-preview version of Fake.DBforMySQL/servers available. Acceptable API versions: 2417-12-01");
                "[1] Use recent API version for 'fake.DBforMySQL/servers'. '2417-12-01-preview' is 1676 days old, should be no more than 730 days old. Acceptable versions: 2417-12-01");
        }

        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithLaterDateThanStable_Fail()
        {
            string bicep = @"
                    resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                      name: 'db]'
                    #disable-next-line no-hardcoded-location
                      location: 'westeurope'
                      properties: {
                        administratorLogin: 'sa'
                        administratorLoginPassword: 'don\'t put passwords in plain text'
                        createMode: 'Default'
                        sslEnforcement: 'Disabled'
                      }
                    }
                ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                new string[]
                {
                       "Fake.DBforMySQL/servers@2417-12-02",
                       "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04",
                //asdfg which? "[1] Use recent apiVersions. There is a non-preview version of Fake.DBforMySQL/servers available. Acceptable API versions: 2417-12-01");
                "[1] Use recent API version for 'fake.DBforMySQL/servers'. '2417-12-01-preview' is 1676 days old, should be no more than 730 days old. Acceptable versions: 2417-12-02");
        }

        [TestMethod]
        public void ArmTtk_PreviewWhenNonPreviewIsAvailable_WithEarlierDateThanStable_Pass()
        {
            string bicep = @"
                resource db 'fake.DBforMySQL/servers@2417-12-01-preview' = {
                    name: 'db]'
                #disable-next-line no-hardcoded-location
                    location: 'westeurope'
                    properties: {
                    administratorLogin: 'sa'
                    administratorLoginPassword: 'don\'t put passwords in plain text'
                    createMode: 'Default'
                    sslEnforcement: 'Disabled'
                    }
                }
            ";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                new string[]
                {
                    "Fake.DBforMySQL/servers@2417-11-31",
                    "Fake.DBforMySQL/servers@2417-12-01-preview",
                },
                fakeToday: "2422-07-04",
                "[1] Use recent API version for 'fake.DBforMySQL/servers'. '2417-12-01-preview' is 1676 days old, should be no more than 730 days old. Acceptable versions: 2417-11-31");
        }

        //asdfg what if only beta/etc?
        [TestMethod]
        public void ArmTtk_OnlyPreviewAvailable_EvenIfOld_Pass()
        {
            string bicep = @"
                   resource namespace 'Microsoft.DevTestLab/schedules@2417-08-01-preview' = {
                      name: 'namespace'
                      location: 'global'
                      properties: {
                      }
                   }";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                new string[]
                {
                       "Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview",
                       "Fake.MachineLearningCompute/operationalizationClusters@2417-08-01-preview",
                },
                fakeToday: "2422-07-04");
        }

        [TestMethod]
        public void NewerPreviewAvailable_Fail()
        {
            string bicep = @"
                   resource namespace 'Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview' = {
                      name: 'clusters'
                      location: 'global'
                   }";

            CompileAndTestWithFakeDateAndTypes(
                bicep,
                ResourceScope.ResourceGroup,
                new string[]
                {
                       "Fake.MachineLearningCompute/operationalizationClusters@2417-06-01-preview",
                       "Fake.MachineLearningCompute/operationalizationClusters@2417-08-01-preview",
                },
                fakeToday: "2422-07-04",
                "[1] Use recent API version for 'Fake.MachineLearningCompute/operationalizationClusters'. '2417-06-01-preview' is 1859 days old, should be no more than 730 days old. Acceptable versions: 2417-08-01-preview");
        }

        [TestMethod]
        public void ExtensionResources_RoleAssignment_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            /*
                [-] apiVersions Should Be Recent (15 ms)                                                                            
                Api versions must be the latest or under 2 years old (730 days) - API version 2020-04-01-preview of Microsoft.Authorization/roleAssignments is 830 days old Line: 40, Column: 8
                Valid Api Versions:                                                                                             
                2018-07-01                                                                                                      
                2022-01-01-preview                                                                                              
                2021-04-01-preview                                                                                              
                2020-10-01-preview                                                                                              
                2020-08-01-preview  
            */
            CompileAndTestWithFakeDateAndTypes(@"
                    targetScope = 'subscription'

                    @description('The principal to assign the role to')
                    param principalId string

                    @allowed([
                      'Owner'
                      'Contributor'
                      'Reader'
                    ])
                    @description('Built-in role to assign')
                    param builtInRoleType string

                    var role = {
                      Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                      Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                      Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                    }

                    resource roleAssignSub 'fake.Authorization/roleAssignments@2420-04-01-preview' = {
                      name: guid(subscription().id, principalId, role[builtInRoleType])
                      properties: {
                        roleDefinitionId: role[builtInRoleType]
                        principalId: principalId
                      }
                    }",
                    ResourceScope.Subscription, //asdfg 
                    FakeResourceTypes.SubscriptionScopeTypes,
                   fakeToday: "2422-07-04",
                   new String[] {
                       "[20] Use recent API version for 'fake.Authorization/roleAssignments'. '2420-04-01-preview' is 824 days old, should be no more than 730 days old. Acceptable versions: 2420-10-01-preview, 2420-08-01-preview, 2417-09-01"
                   });
        }

        [TestMethod]
        public void ExtensionResources_Lock_Pass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                   resource createRgLock 'Microsoft.Authorization/locks@2016-09-01' = {
                      name: 'rgLock'
                      properties: {
                        level: 'CanNotDelete'
                        notes: 'Resource group should not be deleted.'
                      }
                    }",
                ResourceScope.ResourceGroup,
                FakeResourceTypes.ResourceScopeTypes,
                "2422-07-04");
        }

        [TestMethod]
        public void ExtensionResources_SubscriptionRole()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                    targetScope = 'subscription'

                    @description('The principal to assign the role to')
                    param principalId string

                    @allowed([
                      'Owner'
                      'Contributor'
                      'Reader'
                    ])
                    @description('Built-in role to assign')
                    param builtInRoleType string

                    var role = {
                      Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                      Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                      Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                    }

                    resource roleAssignSub 'fake.Authorization/roleAssignments@2020-04-01-preview' = {
                      name: guid(subscription().id, principalId, role[builtInRoleType])
                      properties: {
                        roleDefinitionId: role[builtInRoleType]
                        principalId: principalId
                      }
                    }",
                    ResourceScope.Subscription,
                    FakeResourceTypes.ResourceScopeTypes, //asdfg should fail
                    "2422-07-04"
                );
        }

        [TestMethod]
        public void ExtensionResources_ScopeProperty()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                    @description('The principal to assign the role to')
                    param principalId string

                    @allowed([
                      'Owner'
                      'Contributor'
                      'Reader'
                    ])
                    @description('Built-in role to assign')
                    param builtInRoleType string

                    param location string = resourceGroup().location

                    var role = {
                      Owner: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
                      Contributor: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c'
                      Reader: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7'
                    }
                    #disable-next-line no-loc-expr-outside-params
                    var uniqueStorageName = 'storage${uniqueString(resourceGroup().id)}'

                    // newer stable available
                    resource demoStorageAcct 'fake.Storage/storageAccounts@2420-08-01-preview' = {
                      name: uniqueStorageName
                      location: location
                      sku: {
                        name: 'Standard_LRS'
                      }
                      kind: 'Storage'
                      properties: {}
                    }

                    // old
                    resource roleAssignStorage 'fake.Authorization/roleAssignments@2420-04-01-preview' = {
                      name: guid(demoStorageAcct.id, principalId, role[builtInRoleType])
                      properties: {
                        roleDefinitionId: role[builtInRoleType]
                        principalId: principalId
                      }
                      scope: demoStorageAcct
                    }",
                ResourceScope.ResourceGroup,
                FakeResourceTypes.ResourceScopeTypes,
                "2422-07-04",
                "[23] Use recent API version for 'fake.Storage/storageAccounts'. '2420-08-01-preview' is a preview version and there is a more recent non-preview version available. Acceptable versions: 2421-06-01, 2421-04-01, 2421-02-01, 2421-01-01",
                "[34] Use recent API version for 'fake.Authorization/roleAssignments'. '2420-04-01-preview' is 824 days old, should be no more than 730 days old. Acceptable versions: 2420-10-01-preview, 2420-08-01-preview, 2415-07-01");
        }

        [TestMethod]
        public void ExtensionResources_ScopeProperty_ExistingResource_PartiallyPass()
        {
            // https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/scope-extension-resources#apply-to-resource
            CompileAndTestWithFakeDateAndTypes(@"
                    resource demoStorageAcct 'fake.Storage/storageAccounts@2421-04-01' existing = {
                      name: 'examplestore'
                    }

                    resource createStorageLock 'fake.Authorization/locks@2416-09-01' = {
                      name: 'storeLock'
                      scope: demoStorageAcct
                      properties: {
                        level: 'CanNotDelete'
                        notes: 'Storage account should not be deleted.'
                      }
                    }",
                 ResourceScope.ResourceGroup,
                 FakeResourceTypes.ResourceScopeTypes,
                 "2422-07-04",
                 "[5] Use recent API version for 'fake.Authorization/locks'. '2416-09-01' is 2132 days old, should be no more than 730 days old. Acceptable versions: 2420-05-01");
        }

        //asdfg
        //[TestMethod]
        //public void TenantDeployment_OldApiVersion_Fail()
        //{
        //    CompileAndTestWithFakeDateAndTypes(@"
        //            targetScope = 'tenant'

        //            resource mgName_resource 'fake.Management/managementGroups@2420-02-01' = {
        //              name: 'mg1'
        //            }",
        //        FakeResourceTypes.SubscriptionScopeTypes, //asdfg test for real
        //        "2422-07-04",
        //        "asdfg todo");
        //}

        //[TestMethod]
        //public void TenantDeployment_Pass()
        //{
        //    CompileAndTestWithFakeDateAndTypes(@"
        //            targetScope = 'tenant'

        //            resource mgName_resource 'fake.Management/managementGroups@2420-02-01' = {
        //              name: 'mg1'
        //            }",
        // ResourceScope.Tenant,
        //        FakeResourceTypes.SubscriptionScopeTypes, //asdfg test for real
        //        "2422-07-04",
        //        "asdfg todo");
        //}

        [TestMethod]
        public void SubscriptionDeployment_OldApiVersion_Fail()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                    targetScope='subscription'

                    param resourceGroupName string
                    param resourceGroupLocation string

                    resource newRG 'fake.Resources/resourceGroups@2419-05-10' = {
                      name: resourceGroupName
                      location: resourceGroupLocation
                    }",
                ResourceScope.Subscription,
                FakeResourceTypes.SubscriptionScopeTypes, //asdfg test for real
                "2422-07-04",
                "[6] Use recent API version for 'fake.Resources/resourceGroups'. '2419-05-10' is 1151 days old, should be no more than 730 days old. Acceptable versions: 2421-05-01, 2421-04-01, 2421-01-01, 2420-10-01, 2420-08-01");
        }

        [TestMethod]
        public void SubscriptionDeployment_Pass()
        {
            CompileAndTestWithFakeDateAndTypes(@"
                    targetScope='subscription'

                    param resourceGroupName string
                    param resourceGroupLocation string

                    resource newRG 'fake.Resources/resourceGroups@2421-01-01' = {
                      name: resourceGroupName
                      location: resourceGroupLocation
                    }",
                ResourceScope.Subscription,
                FakeResourceTypes.SubscriptionScopeTypes, //asdfg test for real
                "2422-07-04");
        }

        //asdfg management group
        //    [TestMethod]
        //    public void ManagementGroupDeployment_OldApiVersion_Fail()
        //    {
        //        CompileAndTestWithFakeDateAndTypes(@"
        //            targetScope = 'managementGroup'

        //            param mgName string = 'mg-${uniqueString(newGuid())}'

        //            resource newMG 'Microsoft.Management/managementGroups@2020-05-01' = {
        //              scope: tenant()
        //              name: mgName
        //              properties: {}
        //            }

        //            output newManagementGroup string = mgName",
        // ResourceScope.ManagementGroupt,
        //            "asdfg todo");
        //    }

        //    [TestMethod]
        //    public void ArmTtk_LatestStableApiOlderThan2Years_Pass()
        //    {
        //        CompileAndTestWithFakeDateAndTypes(@"
        //            @description('The name for the Slack connection.')
        //            param slackConnectionName string = 'SlackConnection'

        //            @description('Location for all resources.')
        //            param location string

        //            // The only available API versions are:
        //            //    fake.Web/connections@2015-08-01-preview
        //            //    fake.Web/connections@2016-06-01
        //            // So this passes even though it's older than 2 years
        //            resource slackConnectionName_resource 'Fke.Web/connections@2016-06-01' = {
        //              location: location
        //              name: slackConnectionName
        //              properties: {
        //                api: {
        //                  id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
        //                }
        //                displayName: 'slack'
        //              }
        //            }");
        //    }

        //    [TestMethod]
        //    public void UnrecognizedVersionApiOlderThan2Years_Pass_ButGetCompilerWarning()
        //    {
        //        CompileAndTest(@"
        //            @description('The name for the Slack connection.')
        //            param slackConnectionName string = 'SlackConnection'

        //            @description('Location for all resources.')
        //            param location string

        //            resource slackConnectionName_resource 'Microsoft.Web/connections@2015-06-01' = { // Known type, unknown api version
        //              location: location
        //              name: slackConnectionName
        //              properties: {
        //                api: {
        //                  id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
        //                }
        //                displayName: 'slack'
        //              }
        //            }",
        //            OnCompileErrors.IncludeErrorsAndWarnings,
        //            "[7] Resource type \"Microsoft.Web/connections@2015-06-01");
        //    }

        //    [TestMethod]
        //    public void UnrecognizedResourceType_WithApiOlderThan2Years_Pass_ButGetCompilerWarning()
        //    {
        //        CompileAndTest(@"
        //            @description('The name for the Slack connection.')
        //            param slackConnectionName string = 'SlackConnection'

        //            @description('Location for all resources.')
        //            param location string

        //            resource slackConnectionName_resource 'Microsoft.Unknown/connections@2015-06-01' = { // unknown resource type
        //              location: location
        //              name: slackConnectionName
        //              properties: {
        //                api: {
        //                  id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
        //                }
        //                displayName: 'slack'
        //              }
        //            }",
        //             OnCompileErrors.IncludeErrorsAndWarnings,
        //            "[7] Resource type \"Fake.Unknown/connections@2015-06-01\" does not have types available.");
        //    }

        //    [TestMethod]
        //    public void ArmTtk_ProviderResource()
        //    {

        //        CompileAndTestWithFakeDateAndTypes(@"
        //            @description('The name for the Slack connection.')
        //            param slackConnectionName string = 'SlackConnection'

        //            @description('Location for all resources.')
        //            param location string

        //            resource slackConnectionName_resource 'Microsoft.Unknown/connections@2015-06-01' = { // unknown resource type
        //              location: location
        //              name: slackConnectionName
        //              properties: {
        //                api: {
        //                  id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'slack')
        //                }
        //                displayName: 'slack'
        //              }
        //            }",
        //            new string[] {
        //            "asdf??"
        //            },
        //            "2022-07-07",
        //            "asdf??");
        //    }

        //    [TestMethod]
        //    public void LotsOfNonStableVersions()
        //    {
        //        //asdfg?
        //        /*
        //         {
        //        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
        //        "contentVersion": "1.0.0.0",
        //        "parameters": {},
        //        "functions": [],
        //        "resources": [
        //        {
        //        // pass
        //         "name": "res1",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2020-05-26-privatepreview"
        //        },
        //        {
        //        // apiVersions Should Be Recent
        //        // Api versions must be the latest or under 2 years old (730 days) - API version 2020-05-26-preview of Microsoft.VSOnline/registeredSubscriptions is 772 days old Line: 14, Column: 14
        //        // Valid Api Versions:                                                                                             
        //        //    2020-05-26-beta                                                                                                 
        //        //    2020-05-26-privatepreview   
        //         "name": "res2",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2020-05-26-preview"
        //        },
        //        {
        //        // pass
        //         "name": "res3",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2020-05-26-beta"
        //        },
        //        {
        //        // pass
        //         "name": "res4",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2020-05-26-alpha"
        //        },
        //        {
        //        // pass
        //         "name": "res5",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2019-07-01-privatepreview"
        //        },
        //        {
        //        // pass
        //         "name": "res6",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2019-07-01-preview"
        //        },
        //        {
        //        // pass
        //         "name": "res7",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2019-07-01-beta"
        //        },
        //        {
        //        // pass
        //         "name": "res8",
        //        "type": "Microsoft.VSOnline/registeredSubscriptions",
        //        "apiVersion": "2019-07-01-alpha"
        //        }
        //        ],
        //        "outputs": {}
        //        }
        //        */

        //        CompileAndTestWithFakeDateAndTypes(@"
        //// Pass - old but no more recent stable version
        //resource res1 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-privatepreview' = {
        //  name: 'res1'
        //}

        //// asdfg?
        //// Pass - old but no more recent stable version
        //resource res2 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-preview' = {
        //  name: 'res2'
        //}

        //// asdfg?
        //// Pass - old but no more recent stable version
        //resource res3 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-beta' = {
        //  name: 'res3'
        //}

        //// asdfg?
        //// Pass - old but no more recent stable version 
        //resource res4 'Microsoft.VSOnline/registeredSubscriptions@2020-05-26-alpha' = {
        //  name: 'res4'
        //}

        //// Fail
        //resource res5 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-privatepreview' = {
        //  name: 'res5'
        //}

        //// Fail
        //resource res6 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-preview' = {
        //  name: 'res6'
        //}

        //// Fail
        //resource res7 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-beta' = {
        //  name: 'res7'
        //}

        //// Fail
        //resource res8 'Microsoft.VSOnline/registeredSubscriptions@2019-07-01-alpha' = {
        //  name: 'res8'
        //}",
        //                new string[] {
        //    "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-privatepreview",
        //    "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-preview",
        //    "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-beta",
        //    "Microsoft.VSOnline/registeredSubscriptions@2020-05-26-alpha",
        //    "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-privatepreview",
        //    "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-preview",
        //    "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-beta",
        //    "Microsoft.VSOnline/registeredSubscriptions@2019-07-01-alpha"
        //                },
        //                "2022-07-07",
        //                "asdf??");
        //    }


        // asdfg ask brian
        //     {
        //  /*  Api versions must be the latest or under 2 years old (730 days) - API version 2014-04-01-preview of Microsoft.Web/sites/eventGridFilters is 3022 days old Line: 19, Column: 8
        //       Valid Api Versions:                                                                                             
        //    2022-03-01                                                                                                      
        //    2022-03-01                                                                                                      
        //    2021-03-01                                                                                                      
        //    2021-02-01                                                                                                      
        //    2021-01-15                                                                                                      
        //    2021-01-01                                                                                                      
        //    2020-12-01                                                                                                      
        //    2020-10-01                                                                                                      
        //    2020-09-01  
        //    */
        //  "type": "Microsoft.Web/sites/eventGridFilters",
        //  "apiVersion": "2014-04-01-preview"
        //},
        //  {
        //  /*  
        //          Microsoft.Web/sites/eventGridFilters uses a preview version ( 2015-08-01-preview ) and there are more recent versions available. Line: 37, Column: 8
        //    Valid Api Versions:                                                                                             
        //    2022-03-01                                                                                                      
        //    2022-03-01                                                                                                      
        //    2021-03-01                                                                                                      
        //    2021-02-01                                                                                                      
        //    2021-01-15                                                                                                      
        //    2021-01-01                                                                                                      
        //    2020-12-01                                                                                                      
        //    2020-10-01                                                                                                      
        //    2020-09-01  
        //    */
        //  "type": "Microsoft.Web/sites/eventGridFilters",
        //  "apiVersion": "2015-08-01-preview"
        //},
    }
}

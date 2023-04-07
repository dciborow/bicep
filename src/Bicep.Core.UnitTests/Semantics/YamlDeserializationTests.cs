// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.RegularExpressions;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.UnitTests.Assertions;

namespace Bicep.Core.UnitTests.Semantics
{

    [TestClass]
    public class YamlDeserializationTests
    {
        private const string SIMPLE_JSON = """
            {
              "string": "someVal",
              "int": 123,
              /*
              this is a
              multi line
              comment
              */
              "array": [//comment
                1,
                2
              ],
              //comment
              # comment
              "object": { #comment
                "nestedString": "someVal"
              }
            }
            """;

        private const string COMPLEX_JSON = """
            {
              "label": "dateTimeFromEpoch",
              "kind": "function",
              "value": "```bicep\ndateTimeFromEpoch([epochTime: int]): string\n\n```\nConverts an epoch time integer value to an [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) dateTime string.\n",
              "documentation": {
                "kind": "markdown",
                "value": "```bicep\ndateTimeFromEpoch([epochTime: int]): string\n\n```\nConverts an epoch time integer value to an [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) dateTime string.\n"
              },
              "deprecated": false,
              "preselect": false,
              "sortText": "3_dateTimeFromEpoch",
              "insertTextFormat": "snippet",
              "insertTextMode": "adjustIndentation",
              "textEdit": {
                "range": {},
                "newText": "dateTimeFromEpoch($0)"
              },
              "command": {
                "title": "signature help",
                "command": "editor.action.triggerParameterHints"
              }
            }
            """;

        [TestMethod]
        public void Commented_YAML_file_content_gets_deserialized_into_JSON()
        {
            var yml = @"
                string: someVal 
                int: 123
                /*
                this is a
                multi line
                comment
                */
                array:
                - 1
                # comment
                - 2
                //comment
                #comment
                object: #more comments
                    nestedString: someVal";

            CompareSimpleJSON(yml);
        }

        [TestMethod]
        public void JSON_file_content_gets_deserialized_into_JSON()
        {
            var json = SIMPLE_JSON;

            CompareSimpleJSON(json);

        }

        private static void CompareSimpleJSON(string json)
        {

            // Manual fixes to allow extended json comment syntax
            //json = json.Replace("//", "#");
            // Manually fix multi-line comment with regex by appending # and manually fix first line
            //json = Regex.Replace(json, @"(/\*.+?\*/)", m => m.Value.Replace("\n", "\n#"), RegexOptions.Singleline).Replace("/*", "# /*");

            /*var deserializer = new DeserializerBuilder().Build();
            var p = deserializer.Deserialize<Dictionary<string, object>>(json);
            var jToken = JToken.FromObject(p);*/

            var jToken = SystemNamespaceType.ExtractTokenFromObject(json);
            var correctList = new List<int> { 1, 2 };
            var correctObject = new Dictionary<string, string> { { "nestedString", "someVal" } };

            Assert.AreEqual("someVal", jToken["string"]);
            Assert.AreEqual(123, jToken["int"]?.ToObject<int>());

            CollectionAssert.AreEqual(correctList, jToken["array"]?.ToObject<List<int>>());
            Assert.AreEqual(1, jToken["array"]?.ToObject<List<int>>()?[0]);
            Assert.AreEqual(2, jToken["array"]?.ToObject<List<int>>()?[1]);

            CollectionAssert.AreEqual(correctObject, jToken["object"]?.ToObject<Dictionary<string, string>>());
            Assert.AreEqual("someVal", jToken["object"]?.ToObject<Dictionary<string, string>>()?["nestedString"]);
        }

        [TestMethod]
        public void Complex_JSON_gets_deserialized_into_JSON()
        {
            var json = COMPLEX_JSON;
            
            var jToken = SystemNamespaceType.ExtractTokenFromObject(json);
            Assert.AreEqual("```bicep\ndateTimeFromEpoch([epochTime: int]): string\n\n```\nConverts an epoch time integer value to an [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) dateTime string.\n", jToken["documentation"]?["value"]);

        }


        [TestMethod]
        public void Complex_JSON_gets_deserialized_into_JSON_by_old_method()
        {
            var json = COMPLEX_JSON;

            #pragma warning disable CS0618 // Disable warning for obsolete method to verify functionality
            var jTokenOld = SystemNamespaceType.OldExtractTokenFromObject(json);
            #pragma warning restore CS0618

            var exptectedValue = "```bicep\ndateTimeFromEpoch([epochTime: int]): string\n\n```\nConverts an epoch time integer value to an [ISO 8601](https://en.wikipedia.org/wiki/ISO_8601) dateTime string.\n";

            Assert.AreEqual(exptectedValue, jTokenOld["documentation"]?["value"]);

        }

        [DataTestMethod]
        [DataRow(SIMPLE_JSON)]
        [DataRow(COMPLEX_JSON)]
        public void Compare_new_and_old_JSON_parsing(string json)
        {
            var jTokenNew = SystemNamespaceType.ExtractTokenFromObject(json);

            #pragma warning disable CS0618 // Disable warning for obsolete method to verify functionality
            var jTokenOld = SystemNamespaceType.OldExtractTokenFromObject(json);
#pragma warning restore CS0618

            var comparer = new JTokenEqualityComparer();
            var hashCode1 = comparer.GetHashCode(jTokenNew);
            var hashCode2 = comparer.GetHashCode(jTokenOld);
            Assert.AreEqual(hashCode1.ToString(), hashCode2.ToString());

        }

        //[TestMethod]
        /*public void Simple_YAML_file_content_gets_deserialized_into_JSON()
        {
            var yml = @"
                 name: George Washington
                 age: 89
                 height_in_inches: 5.75
                 addresses:
                   home:
                     street: 400 Mockingbird Lane
                     city: Louaryland
                     state: Hawidaho #if //comment then, {[state, Hawidaho //comment]}
                     zip: 99970";

            CompareSimpleJSON(yml);

        }*/
    }

}
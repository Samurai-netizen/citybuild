#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Core
{
    /// <summary>
    /// Keeps the logging policy of M0-57 true as the code grows: the runtime code logs through <c>ClientLog</c> (so the level of the build and
    /// the rule "no token, no body" apply), and only <c>UnityLogSink</c> calls <c>UnityEngine.Debug</c> to write a line.
    /// </summary>
    public sealed class LoggingPolicyTests
    {
        /// <summary>The runtime folders of the client; the Editor folder and the tests may use Unity's log directly.</summary>
        private static readonly string[] RuntimeFolders = { "Core", "Net", "Presentation" };

        /// <summary>The one runtime file that may call <c>Debug.Log*</c>.</summary>
        private const string SinkFile = "UnityLogSink.cs";

        /// <summary>A direct call to Unity's log, with or without the <c>UnityEngine.</c> prefix.</summary>
        private static readonly Regex DirectCall = new Regex(@"\bDebug\.(Log|LogWarning|LogError|LogException|LogFormat|LogAssertion)\b");

        /// <summary>No runtime file except the Unity sink calls <c>Debug.Log*</c>; a new log line goes through <c>ClientLog</c>.</summary>
        [Test]
        public void RuntimeCode_LogsOnlyThroughClientLog()
        {
            List<string> offenders = new List<string>();
            foreach (string folder in RuntimeFolders)
            {
                string path = Path.Combine(Application.dataPath, "_Project", folder);
                foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file) == SinkFile)
                    {
                        continue;
                    }

                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (DirectCall.IsMatch(lines[i]))
                        {
                            offenders.Add(Path.GetFileName(file) + ":" + (i + 1));
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty, "Use ClientLog instead of UnityEngine.Debug.Log*: " + string.Join(", ", offenders));
        }
    }
}

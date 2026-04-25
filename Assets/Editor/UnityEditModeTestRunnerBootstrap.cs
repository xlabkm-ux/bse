using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BreachScenarioEngine.Mcp.Editor
{
    public static class UnityEditModeTestRunnerBootstrap
    {
        public static void RunEditModeTests()
        {
            var resultsPath = ResolveResultsPath();
            var assemblyNames = ResolveAssemblyNames();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new TestRunCallback(resultsPath);

            try
            {
                api.RegisterCallbacks(callback);
                var settings = new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = assemblyNames
                })
                {
                    runSynchronously = true
                };

                api.Execute(settings);

                if (!callback.Completed)
                {
                    Debug.LogError("EditMode test run did not complete before the runner returned.");
                    EditorApplication.Exit(1);
                    return;
                }

                var exitCode = callback.Success ? 0 : 1;
                Debug.LogFormat("EditMode test run completed with success={0}. Results: {1}", callback.Success, resultsPath);
                EditorApplication.Exit(exitCode);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
            finally
            {
                api.UnregisterCallbacks(callback);
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        private static string ResolveResultsPath()
        {
            var args = Environment.GetCommandLineArgs();
            var value = ReadCommandLineValue(args, "-testResults");
            if (string.IsNullOrWhiteSpace(value))
            {
                value = ReadCommandLineValue(args, "-results");
            }

            return string.IsNullOrWhiteSpace(value) ? "Temp/unity-editmode-tests.xml" : value;
        }

        private static string[] ResolveAssemblyNames()
        {
            var args = Environment.GetCommandLineArgs();
            var value = ReadCommandLineValue(args, "-testAssemblies");
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parsed = value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();
                if (parsed.Length > 0)
                {
                    return parsed;
                }
            }

            return new[] { "BreachScenarioEngine.Mcp.Editor.Tests" };
        }

        private static string ReadCommandLineValue(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return "";
        }

        private sealed class TestRunCallback : ICallbacks
        {
            private readonly string _resultsPath;

            public bool Completed { get; private set; }
            public bool Success { get; private set; }

            public TestRunCallback(string resultsPath)
            {
                _resultsPath = resultsPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, _resultsPath);
                    Success = result.FailCount == 0;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Success = false;
                }
                finally
                {
                    Completed = true;
                }
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}

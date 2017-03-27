namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Checkers;
    using Common;
    using Executors;

    using OJS.Common.Extensions;

    public class NodeJsES6PreprocessExecuteAndCheckExecutionStrategy : ExecutionStrategy
    {
        private readonly string nodeJsExecutablePath;

        private readonly string vm2ModulePath;

        public NodeJsES6PreprocessExecuteAndCheckExecutionStrategy(string nodeJsExecutablePath, string vm2ModulePath)
        {
            if (!File.Exists(nodeJsExecutablePath))
            {
                throw new ArgumentException(
                    $"NodeJS not found in: {nodeJsExecutablePath}", nameof(nodeJsExecutablePath));
            }

            if (!File.Exists(vm2ModulePath))
            {
                throw new ArgumentException(
                    $"VM2 lib not found in: {vm2ModulePath}", nameof(vm2ModulePath));
            }

            this.nodeJsExecutablePath = new FileInfo(nodeJsExecutablePath).FullName;
            this.vm2ModulePath = this.FixStringPath(new FileInfo(vm2ModulePath).FullName);
        }

        protected string NodeJsExecutablePath => this.nodeJsExecutablePath;

        protected string Vm2ModulePath => this.vm2ModulePath;

        protected virtual string JsCodeRequiredModules => @"
const { VM } = require(""" + this.Vm2ModulePath + @""");
";

        protected virtual string JsSandboxItems => @"
            console: {
                log(...msgs) {
                    result.push(msgs);
                }
            }
";

        protected virtual string JsHiddenItems => string.Empty;

        protected virtual string JsSolveFunctionName => "solve";

        public override ExecutionResult Execute(ExecutionContext executionContext)
        {
            var result = new ExecutionResult();

            // setting the IsCompiledSuccessfully variable to true as in the NodeJS
            // execution strategy there is no compilation
            result.IsCompiledSuccessfully = true;

            // Save the preprocessed submission which is ready for execution

            // Process the submission and check each test
            IExecutor executor = new StandardProcessExecutor();
            IChecker checker = Checker.CreateChecker(executionContext.CheckerAssemblyName, executionContext.CheckerTypeName, executionContext.CheckerParameter);

            result.TestResults = this.ProcessTests(executionContext, executor, checker);

            return result;
        }

        protected virtual List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker)
        {
            var testResults = new List<TestResult>();

            foreach (var test in executionContext.Tests)
            {
                var codeToExecute = this.PreprocessJsSolution(executionContext.Code, executionContext.TimeLimit * 2, test.Input);
                var pathToSolutionFile = FileHelpers.SaveStringToTempFile(codeToExecute);

                var processExecutionResult = executor.Execute(
                    this.NodeJsExecutablePath,
                    string.Empty,
                    executionContext.TimeLimit,
                    executionContext.MemoryLimit,
                    new[] { pathToSolutionFile });
                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, processExecutionResult.ReceivedOutput);
                testResults.Add(testResult);

                // Clean up
                File.Delete(pathToSolutionFile);
            }

            return testResults;
        }

        protected virtual List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker, string codeSavePath)
        {
            return null;
        }

        protected virtual string GetJsCodeTemplate(string userCode, int timeLimit, string arguments) {
            return this.JsCodeRequiredModules + @"
function getSandboxFunction(codeToExecute, test) {
    const code = `
        const " + this.JsSolveFunctionName + @" = (function(" + this.JsHiddenItems + @") {
            return (${codeToExecute}.bind({}));
        }).call({});
        /* */
        ${test}
    `;
    const timeout = " + timeLimit + @";

    return function() {
        const result = [];
        const sandbox = {
" + this.JsSandboxItems + @"
        };

        const vm = new VM({ timeout, sandbox });
        const returnValue = vm.run(code);
        if(typeof returnValue !== ""undefined"") {
            result.push([returnValue]);
        }
        return result;
    }
};

const code = """ + userCode + @""";
const args = """ + arguments + @""";
const func = getSandboxFunction(code, args);

const result = func();
result.forEach(line => console.log(...line));
";
        }

        protected string EscapeJsString(string code)
        {
            return code
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        protected string FixStringPath(string path)
        {
            return path.Replace('\\', '/')
                    .Replace(" ", "\\ ");
        }

        protected string FixArgumentPath(string path)
        {
            return "\"" + path + "\"";
        }

        private string PreprocessJsSolution(string code, int timeLimit, string input)
        {
            var escapedCode = this.EscapeJsString(code.Trim().Trim(';'));

            char[] splitters = { '\n', '\r' };

            var argsString = input.Split(splitters, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => "\"" + this.EscapeJsString(x) + "\"");
            var args = this.EscapeJsString(this.JsSolveFunctionName + "([" + string.Join(", ", argsString) + "]);");

            return this.GetJsCodeTemplate(escapedCode, timeLimit, args);
        }
    }
}

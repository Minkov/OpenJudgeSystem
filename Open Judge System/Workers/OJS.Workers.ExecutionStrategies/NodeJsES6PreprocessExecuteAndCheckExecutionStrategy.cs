namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using OJS.Common.Extensions;
    using OJS.Workers.Checkers;
    using OJS.Workers.Common;
    using OJS.Workers.Executors;

    public class NodeJsES6PreprocessExecuteAndCheckExecutionStrategy : ExecutionStrategy
    {
        protected static readonly Random Rand = new Random();

        protected readonly string userCodePlaceholderName = $"#userCodePlaceholder-{Rand.Next()}#";

        protected readonly string argumentsPlaceholderName = $"#argsPlaceholder-{Rand.Next()}#";

        protected readonly string timeLimitPlaceholderName = $"#timeLimitPlaceholder-{Rand.Next()}#";

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

            this.NodeJsExecutablePath = nodeJsExecutablePath;
            this.Vm2ModulePath = this.FixPath(new FileInfo(vm2ModulePath).FullName);
        }

        protected string NodeJsExecutablePath { get; private set; }
        protected string Vm2ModulePath { get; private set; }

        protected virtual string JsCodeTemplate => @"
const { VM } = require(""" + this.Vm2ModulePath + @""");

function getSandboxFunction(codeToExecute) {
    const code = `
		(function() {
			return (${codeToExecute}.bind({}));
		}).call({})(args);
    `;
    const timeout = " + this.timeLimitPlaceholderName + @";

    return function(args) {
		const result = [];
        const sandbox = {
            console: {
                log(...msgs) {
					result.push(msgs);
                }
            },
            args
        };

        const vm = new VM({ timeout, sandbox })
        const returnValue = vm.run(code);
        if(typeof returnValue !== 'undefined') {
            result.push([returnValue]);
        }
        return result;
    }
};

const code = " + this.userCodePlaceholderName + @"

const func = getSandboxFunction(code);

const args = [" + this.argumentsPlaceholderName + @"];
const result = func(args);
result.forEach(line => console.log(...line));
";

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

            var solutionCodeTemplate =
                    this.PreprocessJsSubmission(this.JsCodeTemplate, executionContext.Code.Trim(';'), executionContext.TimeLimit * 2);

            foreach (var test in executionContext.Tests)
            {
                var codeToExecute =
                        this.PreprocessJsSolution(solutionCodeTemplate, executionContext.Code.Trim(), test.Input);
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

        private string PreprocessJsSolution(string template, string code, string input)
        {
            var fixedInput = input.Trim()
                    .Replace(@"\", @"\\")
                    .Replace(@"""", @"\""")
                    .Replace("'", "\\'");

            char[] splitters = { '\n', '\r' };

            var argsString = fixedInput.Split(splitters, StringSplitOptions.RemoveEmptyEntries)
                    .Select(arg => $"\"{arg}\"");

            var args = string.Join(", ", argsString);

            return template
                    .Replace(this.argumentsPlaceholderName, args);
        }

        protected string PreprocessJsSubmission(string template, string code, int timeLimit)
        {
            var processedCode = template
                .Replace(this.userCodePlaceholderName, code)
                .Replace(this.timeLimitPlaceholderName, timeLimit.ToString());

            return processedCode;
        }

        protected string FixPath(string path)
        {
            return path.Replace('\\', '/')
                    .Replace(" ", "\\ ");
        }
    }
}

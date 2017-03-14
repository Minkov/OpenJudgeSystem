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

    public class NodeJSES6PreprocessAndRunMochaTestsExecutionStrategy : NodeJsES6PreprocessExecuteAndCheckExecutionStrategy
    {
		protected readonly string testIndexPlaceholder = $"#testIndexPlaceholder-{Rand.Next()}#";

        public NodeJSES6PreprocessAndRunMochaTestsExecutionStrategy(string nodeJsExecutablePath, string vm2ModulePath, string mochaModulePath, string chaiModulePath)
			: base(nodeJsExecutablePath, vm2ModulePath)
        {
            if (!File.Exists(mochaModulePath))
            {
                throw new ArgumentException(
                    $"Mocha not found in: {mochaModulePath}", nameof(mochaModulePath));
            }

            if (!File.Exists(chaiModulePath))
            {
                throw new ArgumentException(
                    $"Chai not found in: {chaiModulePath}", nameof(chaiModulePath));
            }

            this.MochaModulePath = new FileInfo(mochaModulePath).FullName.Replace(" ", "\" \"");
            this.ChaiModulePath = this.FixPath(new FileInfo(chaiModulePath).FullName);
        }

        protected string MochaModulePath { get; private set; }
        protected string ChaiModulePath { get; private set; }

        protected override string JsCodeTemplate => @"
const { VM } = require(""" + this.Vm2ModulePath + @""");
const { expect } = require(""" + this.ChaiModulePath + @""");

function getSandboxFunction(codeToExecute) {
    const code = `
		const result = (function() {
			return (${codeToExecute}.bind({}));
		}).call({})();

		it('Test # " + this.testIndexPlaceholder + @"', () => {" + this.argumentsPlaceholderName + @"});
    `;
    const timeout = " + this.timeLimitPlaceholderName + @";

    return function() {
        const sandbox = {
			it, expect
        };

        const vm = new VM({ timeout, sandbox });
        const returnValue = vm.run(code);
    }
};

const code = " + this.userCodePlaceholderName + @"
getSandboxFunction(code)();
";

        protected override List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker)
        {
            var testResults = new List<TestResult>();

            var solutionCodeTemplate = this.PreprocessJsSubmission(this.JsCodeTemplate, executionContext.Code.Trim(';'), executionContext.TimeLimit * 2);

            var indexRegular = 1;
            var indexTrial = 1;
            var tests = executionContext.Tests.ToList();
            for (var i = 0; i < tests.Count; i++)
            {
                var test = tests[i];
                var index = test.IsTrialTest ? indexTrial : indexRegular;
                if (test.IsTrialTest)
                {
                    ++indexTrial;
                }
                else
                {
                    ++indexRegular;
                }

                var codeToExecute = this.PreprocessJsSolution(solutionCodeTemplate, executionContext.Code.Trim(), test.Input, index);

                var pathToSolutionFile = FileHelpers.SaveStringToTempFile(codeToExecute);

                var processExecutionResult = executor.Execute(
                    this.NodeJsExecutablePath,
                    string.Empty,
                    executionContext.TimeLimit,
                    executionContext.MemoryLimit,
                        new string[] { this.MochaModulePath, this.FixPath(pathToSolutionFile) });

                var receivedOutput = "yes";

                if (processExecutionResult.ReceivedOutput.IndexOf("failing") >= 0)
                {
                    receivedOutput = processExecutionResult.ReceivedOutput;
                }

                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, receivedOutput);
                testResults.Add(testResult);

                // Clean up the files
                File.Delete(pathToSolutionFile);
            }

            return testResults;
        }

        private string PreprocessJsSolution(string template, string code, string input, int index)
        {
            return template
                    .Replace(this.argumentsPlaceholderName, input.Trim())
                    .Replace(this.testIndexPlaceholder, index.ToString());
        }
    }
}

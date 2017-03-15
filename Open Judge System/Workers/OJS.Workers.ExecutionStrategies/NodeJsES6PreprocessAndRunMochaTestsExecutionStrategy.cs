namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Common;

    using OJS.Common.Extensions;

    public class NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy : NodeJsES6PreprocessExecuteAndCheckExecutionStrategy
    {
		private readonly string mochaModulePath;

		private readonly string chaiModulePath;

        public NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy(string nodeJsExecutablePath, string vm2ModulePath, string mochaModulePath, string chaiModulePath)
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

            this.mochaModulePath = new FileInfo(mochaModulePath).FullName.Replace(" ", "\" \"");
            this.chaiModulePath = this.FixPath(new FileInfo(chaiModulePath).FullName);
        }

        protected string MochaModulePath
		{
			get
			{
				return this.mochaModulePath;
			}
		}

        protected string ChaiModulePath
		{
			get
			{
				return this.chaiModulePath;
			}
		}


		protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const chai = require(""" + this.ChaiModulePath + @"""),
	{ expect } = chai;
";

        protected virtual string GetJsCodeTemplate(string userCode, int timeLimit, string arguments, int index)
		{
			return this.JsCodeRequiredModules + @"
function getSandboxFunction(codeToExecute) {
    const code = `
		const result = (function() {
			return (${codeToExecute}.bind({}));
		}).call({})();

		it('Test # " + 42 + @"', () => {
" + arguments + @"
		});
    `;
    const timeout = " + timeLimit + @";

    return function() {
        const sandbox = {
			it, expect
        };

        const vm = new VM({ timeout, sandbox });
        const returnValue = vm.run(code);
    }
};

const code = " + userCode + @"
getSandboxFunction(code)();
";
		}

        protected override List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker)
        {
            var testResults = new List<TestResult>();

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

                var codeToExecute = this.PreprocessJsSolution(executionContext.Code.Trim(), executionContext.TimeLimit * 2, test.Input, index);

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

        protected string PreprocessJsSolution(string code, int timeLimit, string input, int index)
        {
            return this.GetJsCodeTemplate(code, timeLimit, input.Trim(), index);
        }
    }
}

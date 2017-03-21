namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Common;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using OJS.Common.Extensions;

    public class NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy : NodeJsES6PreprocessExecuteAndCheckExecutionStrategy
    {
        private readonly string mochaModulePath;

        private readonly string chaiModulePath;

        private readonly string sinonModulePath;

        private readonly string sinonChaiModulePath;

        public NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy(string nodeJsExecutablePath, string vm2ModulePath, string mochaModulePath, string chaiModulePath, string sinonModulePath, string sinonChaiModulePath)
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

            if (!File.Exists(sinonModulePath))
            {
                throw new ArgumentException(
                    $"Sinon not found in: {sinonModulePath}", nameof(sinonModulePath));
            }

            if (!File.Exists(sinonChaiModulePath))
            {
                throw new ArgumentException(
                    $"SinonChai not found in: {sinonChaiModulePath}", nameof(sinonChaiModulePath));
            }

            this.mochaModulePath = this.FixArgumentPath(new FileInfo(mochaModulePath).FullName);
            this.chaiModulePath = this.FixStringPath(new FileInfo(chaiModulePath).FullName);
            this.sinonModulePath = this.FixStringPath(new FileInfo(sinonModulePath).FullName);
            this.sinonChaiModulePath = this.FixStringPath(new FileInfo(sinonChaiModulePath).FullName);
        }

        protected string MochaModulePath => this.mochaModulePath;

        protected string ChaiModulePath => this.chaiModulePath;

        protected string SinonModulePath => this.sinonModulePath;

        protected string SinonChaiModulePath => this.sinonChaiModulePath;

        protected virtual string JsResultObjectName => "result";

        protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const chai = require(""" + this.ChaiModulePath + @"""),
    { expect } = chai;

const sinon = require(""" + this.SinonModulePath + @""");
const sinonChai = require(""" + this.SinonChaiModulePath + @""");
chai.use(sinonChai);
";

        protected override string JsSandboxItems => @"it, beforeEach, expect";

        protected override string JsHiddenItems => "describe, it, before, beforeEach, after, afterEach, chai, expect, sinon, sinonChai";

        protected override List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker)
        {
            var testResults = new List<TestResult>();

            var testStrings = executionContext.Tests
                .Select(test => @"
it(""Test"", () => {
" + test.Input + @"
});
");

            var tests = @"let " + this.JsResultObjectName + @";
beforeEach(() => {
    " + this.JsResultObjectName + @" = " + this.JsSolveFunctionName + @"();
});
" + string.Join(string.Empty, testStrings) + @"
(function(){})();";

            var codeToExecute = this.PreprocessJsSolution(executionContext.Code, executionContext.TimeLimit * 2, tests);

            var pathToSolutionFile = FileHelpers.SaveStringToTempFile(codeToExecute);

            var processExecutionResult = executor.Execute(
                this.NodeJsExecutablePath,
                string.Empty,
                executionContext.TimeLimit,
                executionContext.MemoryLimit,
                new string[] { this.MochaModulePath, this.FixArgumentPath(pathToSolutionFile), "-R", "json" });

            var testJsonResults = JsonConvert.DeserializeObject<JObject>(processExecutionResult.ReceivedOutput)["tests"];

            var testsList = executionContext.Tests.ToList();

            for (int i = 0; i < testsList.Count; ++i)
            {
                var test = testsList[i];

                var receivedOutput = testJsonResults[i]["err"]["message"];
                if (receivedOutput != null)
                {
                    receivedOutput = "Mocha error: " + receivedOutput;
                }
                else
                {
                    receivedOutput = "yes";
                }

                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, receivedOutput.ToString());
                testResults.Add(testResult);
            }

            // Clean up the files
            File.Delete(pathToSolutionFile);

            return testResults;
        }

        protected string PreprocessJsSolution(string code, int timeLimit, string input)
        {
            var escapedCode = this.EscapeJsString(code.Trim().Trim(';'));
            var escapedInput = this.EscapeJsString(input);

            return this.GetJsCodeTemplate(escapedCode, timeLimit, escapedInput);
        }
    }
}

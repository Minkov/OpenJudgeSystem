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

        protected string MochaModulePath => this.mochaModulePath;

        protected string ChaiModulePath => this.chaiModulePath;

        protected virtual string JsResultObjectName => "result";

        protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const chai = require(""" + this.ChaiModulePath + @"""),
    { expect } = chai;
";

        protected override string JsSandboxItems => @"it, beforeEach, expect";

        protected override string JsHiddenItems => "describe, it, before, beforeEach after, afterEach, chai, expect, sinon, sinonChai";

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
                new string[] { this.MochaModulePath, this.FixPath(pathToSolutionFile), "-R", "json" });

            var testJsonResults = JsonConvert.DeserializeObject<JObject>(processExecutionResult.ReceivedOutput)["tests"];

            var testsList = executionContext.Tests.ToList();

            for (int i = 0; i < testsList.Count; ++i)
            {
                var test = testsList[i];

                var receivedOutput = testJsonResults[i]["err"]["message"];
                if(receivedOutput != null)
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

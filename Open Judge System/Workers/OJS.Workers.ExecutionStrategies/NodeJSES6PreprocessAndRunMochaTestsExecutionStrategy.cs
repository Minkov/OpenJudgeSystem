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

    public class NodeJSES6PreprocessAndRunMochaTestsExecutionStrategy : ExecutionStrategy
    {
        protected const string UserInputPlaceholder = "#userInput";

        protected const string TestArgsPlaceholder = "#testArguments";

        protected const string TestIndexPlaceholder = "#testIndex";

        public NodeJSES6PreprocessAndRunMochaTestsExecutionStrategy(string mochaModulePath, string chaiModulePath)
        {
            if (!File.Exists(mochaModulePath))
            {
                throw new ArgumentException(
                    $"Mocha not found in: {mochaModulePath}", nameof(mochaModulePath));
            }

            this.MochaModulePath = new FileInfo(mochaModulePath).FullName;

            if (!File.Exists(chaiModulePath))
            {
                throw new ArgumentException(
                    $"Chai not found in: {chaiModulePath}", nameof(chaiModulePath));
            }

            this.ChaiModulePath = this.FixPath(new FileInfo(chaiModulePath).FullName);
        }

        protected string MochaModulePath { get; private set; }

        protected string ChaiModulePath { get; private set; }

        protected virtual string JsCodeTemplate => @"
let util = require('util');
const expect = require('" + this.ChaiModulePath + @"').expect;
let vm = require('vm'),
    sandbox,
    consoleFake = {
        'logs': [],
        'log': function(text){
            this.logs.push(text.toString());
        }
    },
    userCode = `_____thisIsTheResultHidden = " + UserInputPlaceholder + @".bind({})()`;

sandbox = {
    'console': consoleFake,
    '_____thisIsTheResultHidden': undefined
};

sandbox.console.logs =  [];
vm.createContext(sandbox);

vm.runInNewContext(userCode, sandbox);

var result = sandbox['_____thisIsTheResultHidden'];

it('Test # " + TestIndexPlaceholder + @"', () => {
    " + TestArgsPlaceholder + @"
});
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

            var solutionCodeTemplate = this.PreprocessJsSubmission(this.JsCodeTemplate, executionContext.Code.Trim(';'));

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

                var pathToSolutionFile = FileHelpers.GetTempPath();
                File.WriteAllText(pathToSolutionFile, codeToExecute);

                var processExecutionResult = executor.Execute(
                    this.MochaModulePath,
                    string.Empty,
                    executionContext.TimeLimit,
                    executionContext.MemoryLimit,
                        new string[] { this.FixPath(pathToSolutionFile) });

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
                    .Replace(TestArgsPlaceholder, input.Trim())
                    .Replace(TestIndexPlaceholder, index.ToString());
        }

        private string PreprocessJsSubmission(string template, string code)
        {
            string replacePlaceholder = "--__pl4c3h0ld3r__;--__pl4c3h0ld3r__;--__pl4c3h0ld3r__;--__pl4c3h0ld3r__;--__pl4c3h0ld3r__;--__pl4c3h0ld3r__;" + Guid.NewGuid();
            code = code.Replace("\\", "\\\\")
                    .Replace("\\\'", replacePlaceholder)
                        .Replace("'", "\"")
                        .Replace("`", "\\`")
                        .Replace(replacePlaceholder, "\\\'");

            var processedCode = template
                .Replace(UserInputPlaceholder, code);

            return processedCode;
        }

        private string FixPath(string path)
        {
            return path.Replace('\\', '/')
                    .Replace(" ", "\\ ");
        }
    }
}

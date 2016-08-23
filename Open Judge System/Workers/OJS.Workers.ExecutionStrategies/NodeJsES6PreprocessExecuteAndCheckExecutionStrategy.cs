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
        protected const string UserInputPlaceholder = "#userInput#";

        protected const string TestArgsPlaceholder = "#testArguments";

        public NodeJsES6PreprocessExecuteAndCheckExecutionStrategy(string nodeJsExecutablePath)
        {
            if (!File.Exists(nodeJsExecutablePath))
            {
                throw new ArgumentException(
                    $"NodeJS not found in: {nodeJsExecutablePath}", nameof(nodeJsExecutablePath));
            }

            this.NodeJsExecutablePath = nodeJsExecutablePath;
        }

        protected string NodeJsExecutablePath { get; private set; }

        protected virtual string JsCodeTemplate => @"
var util = require('util');

let vm = require('vm'),
    sandbox,
    consoleFake = {
        'logs': [],
        'log': function(text){
            this.logs.push(text.toString());
        }
    },
    userCode = " + UserInputPlaceholder + @";

sandbox = {
    'console': consoleFake,
    '_____thisIsTheResultHidden': undefined
};

sandbox.console.logs =  [];
vm.createContext(sandbox);

userCode = `_____thisIsTheResultHidden = (${userCode}([" + TestArgsPlaceholder + @"]))`

vm.runInNewContext(userCode, sandbox);

if(sandbox['_____thisIsTheResultHidden']) {
    console.log(sandbox['_____thisIsTheResultHidden']);
} else {
    consoleFake.logs.forEach(log => console.log(log));
}
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

            foreach (var test in executionContext.Tests)
            {
                var codeToExecute = this.PreprocessJsSolution(solutionCodeTemplate, executionContext.Code.Trim(), test.Input);
                var pathToSolutionFile = FileHelpers.SaveStringToTempFile(codeToExecute);

                var processExecutionResult = executor.Execute(this.NodeJsExecutablePath, string.Empty, executionContext.TimeLimit, executionContext.MemoryLimit, new[] { pathToSolutionFile });
                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, processExecutionResult.ReceivedOutput);
                testResults.Add(testResult);

                // Clean up the files
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
            input = input
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");
            var argsString =
                    string.Join(", ", input.Trim()
                                           .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(arg => string.Format("\"{0}\"", arg)));
            return template
                    .Replace(TestArgsPlaceholder, argsString);
        }

        private string PreprocessJsSubmission(string template, string code)
        {
            var processedCode = template
                .Replace(UserInputPlaceholder, code);

            return processedCode;
        }
    }
}

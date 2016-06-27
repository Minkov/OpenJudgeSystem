namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using OJS.Common.Extensions;
    using OJS.Workers.Checkers;
    using OJS.Workers.Common;
    using OJS.Workers.Executors;

    public class NodeJsPreprocessExecuteAndCheckExecutionStrategy : ExecutionStrategy
    {
        private const string UserInputPlaceholder = "#userInput#";
        private const string RequiredModules = "#requiredModule#";
        private const string PreevaluationPlaceholder = "#preevaluationCode#";
        private const string PostevaluationPlaceholder = "#postevaluationCode#";
        private const string EvaluationPlaceholder = "#evaluationCode#";

        public NodeJsPreprocessExecuteAndCheckExecutionStrategy(string nodeJsExecutablePath, string sandboxModulePath)
        {
            if (!File.Exists(nodeJsExecutablePath))
            {
                throw new ArgumentException(
                    $"NodeJS not found in: {nodeJsExecutablePath}", nameof(nodeJsExecutablePath));
            }

            if (!File.Exists(sandboxModulePath) && !Directory.Exists(sandboxModulePath))
            {
                throw new ArgumentException(
                    $"Sandbox lib not found in: {sandboxModulePath}", nameof(sandboxModulePath));
            }

            this.NodeJsExecutablePath = nodeJsExecutablePath;
            this.SandboxModulePath = this.ProcessModulePath(sandboxModulePath);
        }

        protected string NodeJsExecutablePath { get; private set; }

        protected string SandboxModulePath { get; private set; }

        protected virtual string JsCodeRequiredModules => @"
var EOL = require('os').EOL;
var Sandbox = require(""" + this.SandboxModulePath + @""");";

        protected virtual string JsCodePreevaulationCode => @"
var content = ''";

        protected virtual string JsCodePostevaulationCode => string.Empty;

        protected virtual string JsCodeEvaluation => @"
var inputData = content.trim().split(EOL);
var sandbox = new Sandbox();
code += `; 
solve(inputdata);
`;

sandbox.run(code, function(output) {    
    console.log(output);    
});";

        protected virtual string JsCodeTemplate => RequiredModules + @"

var code = `var solve = " + UserInputPlaceholder + @"`;
" + PreevaluationPlaceholder + @"
" + EvaluationPlaceholder + @";
" + PostevaluationPlaceholder;

        public override ExecutionResult Execute(ExecutionContext executionContext)
        {
            var result = new ExecutionResult();

            // setting the IsCompiledSuccessfully variable to true as in the NodeJS
            // execution strategy there is no compilation
            result.IsCompiledSuccessfully = true;

            // Preprocess the user submission
            var codeToExecute = this.PreprocessJsSubmission(this.JsCodeTemplate, executionContext.Code.Trim(';'));

            // Save the preprocessed submission which is ready for execution

            // Process the submission and check each test
            IExecutor executor = new RestrictedProcessExecutor();
            IChecker checker = Checker.CreateChecker(executionContext.CheckerAssemblyName, executionContext.CheckerTypeName, executionContext.CheckerParameter);

            result.TestResults = this.ProcessTests(executionContext, executor, checker, codeToExecute);

            // Clean up
            //File.Delete(codeSavePath);

            return result;
        }

        protected virtual List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker, string codeToExecute)
        {
            var testResults = new List<TestResult>();

            foreach (var test in executionContext.Tests)
            {
                codeToExecute = codeToExecute.Replace("var content = ''", "var content = `" + test.Input + "`");
                var codeSavePath = FileHelpers.SaveStringToTempFile(codeToExecute);

                var processExecutionResult = executor.Execute(this.NodeJsExecutablePath, string.Empty, executionContext.TimeLimit, executionContext.MemoryLimit, new[] { codeSavePath });
                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, processExecutionResult.ReceivedOutput);
                testResults.Add(testResult);
            }

            return testResults;
        }

        protected string ProcessModulePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private string PreprocessJsSubmission(string template, string code)
        {
            var processedCode = template
                .Replace(RequiredModules, this.JsCodeRequiredModules)
                .Replace(PreevaluationPlaceholder, this.JsCodePreevaulationCode)
                .Replace(EvaluationPlaceholder, this.JsCodeEvaluation)
                .Replace(PostevaluationPlaceholder, this.JsCodePostevaulationCode)
                .Replace(UserInputPlaceholder, code);

            //throw new Exception(processedCode);

            return processedCode;
        }
    }
}

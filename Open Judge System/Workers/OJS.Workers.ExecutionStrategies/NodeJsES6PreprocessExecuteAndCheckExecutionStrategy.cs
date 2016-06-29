namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;

    using OJS.Common.Extensions;
    using OJS.Workers.Checkers;
    using OJS.Workers.Common;
    using OJS.Workers.Executors;

    public class NodeJsES6PreprocessExecuteAndCheckExecutionStrategy : ExecutionStrategy
    {
        private const string UserInputPlaceholder = "#userInput#";

        private const string RequiredModules = "#requiredModule#";
        private const string PreevaluationPlaceholder = "#preevaluationCode#";
        private const string PostevaluationPlaceholder = "#postevaluationCode#";
        private const string EvaluationPlaceholder = "#evaluationCode#";

        private const string PathToCodePlaceholder = "#pathToCode#";
        private const string TestArgsPlaceholder = "#testArguments";

        public NodeJsES6PreprocessExecuteAndCheckExecutionStrategy(string nodeJsExecutablePath, string sandboxModulePath)
        {
            if (!File.Exists(nodeJsExecutablePath))
            {
                throw new ArgumentException(
                    $"NodeJS not found in: {nodeJsExecutablePath}", nameof(nodeJsExecutablePath));
            }

            //if (!File.Exists(sandboxModulePath) && !Directory.Exists(sandboxModulePath))
            //{
            //    throw new ArgumentException(
            //        $"Sandbox lib not found in: {sandboxModulePath}", nameof(sandboxModulePath));
            //}

            this.NodeJsExecutablePath = nodeJsExecutablePath;
            this.SandboxModulePath = this.ProcessModulePath(sandboxModulePath);
        }

        protected string NodeJsExecutablePath { get; private set; }

        protected string SandboxModulePath { get; private set; }

        protected virtual string JsCodeRequiredModules => @"
var EOL = require('os').EOL;
var Sandbox = require(""" + this.ProcessModulePath(this.SandboxModulePath) + @""");";

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

        protected virtual string JsSandboxWrapper => @"
var EOL = require('os').EOL;
var Sandbox = require(""" + this.SandboxModulePath + @""");
var pathToCode = """ + PathToCodePlaceholder + @""";
var s = new Sandbox();
s.run(pathToCode, function(output){
    //var lines = [...output.console];
    var lines = [];
    if(output.console && Array.isArray(output.console)){
       lines.push(...output.console);
    }

    if(output.result && output.result !== 'null') {
        lines.push(output.result.substr(1, output.result.length - 2));
    }

    console.log(lines.join(EOL).trim());
});
";

        //        protected virtual string JsSolutionTemplate => @"
        //var solutionFunc = " + UserInputPlaceholder + @"
        //var args = [" + TestArgsPlaceholder + @"];
        //solutionFunc(args);
        //";
        protected virtual string JsSolutionTemplate => @"
(" + UserInputPlaceholder + @"
)([" + TestArgsPlaceholder + "]);";

        public override ExecutionResult Execute(ExecutionContext executionContext)
        {
            var result = new ExecutionResult();

            // setting the IsCompiledSuccessfully variable to true as in the NodeJS
            // execution strategy there is no compilation
            result.IsCompiledSuccessfully = true;

            // Preprocess the user submission
            //var codeToExecute = this.PreprocessJsSubmission(this.JsCodeTemplate, executionContext.Code.Trim(';'));

            // Save the preprocessed submission which is ready for execution

            // Process the submission and check each test
            //IExecutor executor = new RestrictedProcessExecutor();
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
                var solutionCodeToExecute = this.PreprocessJsSolution(this.JsSolutionTemplate, executionContext.Code.Trim(), test.Input);
                var pathToSolutionFile = FileHelpers.SaveStringToTempFile(solutionCodeToExecute);
                string wrapperCode = this.ProcessSandboxWrapper(pathToSolutionFile);
                var pathToWrapperCode = FileHelpers.SaveStringToTempFile(wrapperCode);

                var processExecutionResult = executor.Execute(this.NodeJsExecutablePath, string.Empty, executionContext.TimeLimit, executionContext.MemoryLimit, new[] { pathToWrapperCode });
                var testResult = this.ExecuteAndCheckTest(test, processExecutionResult, checker, processExecutionResult.ReceivedOutput);
                testResults.Add(testResult);

                // Clean up the files
                //File.Delete(pathToSolutionFile);
                //File.Delete(pathToWrapperCode);
            }

            return testResults;
        }

        protected virtual List<TestResult> ProcessTests(ExecutionContext executionContext, IExecutor executor, IChecker checker, string codeSavePath)
        {
            return null;
        }

        protected string ProcessModulePath(string path)
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), path);
            return path.Replace('\\', '/');
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
                    .Replace(TestArgsPlaceholder, argsString)
                    .Replace(UserInputPlaceholder, code);
        }

        private string ProcessSandboxWrapper(string pathToSolutionFile)
        {
            return this.JsSandboxWrapper.Replace(PathToCodePlaceholder, this.ProcessModulePath(pathToSolutionFile));
        }

        private string PreprocessJsSubmission(string template, string code)
        {
            var processedCode = template
                .Replace(RequiredModules, this.JsCodeRequiredModules)
                .Replace(PreevaluationPlaceholder, this.JsCodePreevaulationCode)
                .Replace(EvaluationPlaceholder, this.JsCodeEvaluation)
                .Replace(PostevaluationPlaceholder, this.JsCodePostevaulationCode)
                .Replace(UserInputPlaceholder, code);

            return processedCode;
        }
    }
}

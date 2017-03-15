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

    public class NodeJsES6PreprocessAndRunDomTestsExecutionStrategy : NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy
    {
        public NodeJsES6PreprocessAndRunDomTestsExecutionStrategy(
			string nodeJsExecutablePath,
			string vm2ModulePath,
			string mochaModulePath,
			string chaiModulePath,
			string jsDomModulePath,
			string jQueryModulePath,
			string handlebarsModulePath,
			string sinonModulePath,
			string sinonChaiModulePath,
			string underscoreModulePath)
			: base(nodeJsExecutablePath, vm2ModulePath, mochaModulePath, chaiModulePath)
        {
			if (!File.Exists(jsDomModulePath))
			{
				throw new ArgumentException(
					$"JsDom not found in: {jsDomModulePath}", nameof(jsDomModulePath));
			}

			if (!File.Exists(jQueryModulePath))
			{
				throw new ArgumentException(
					$"jQuery not found in: {jQueryModulePath}", nameof(jQueryModulePath));
			}

			if (!File.Exists(handlebarsModulePath))
			{
				throw new ArgumentException(
					$"Handlebars not found in: {handlebarsModulePath}", nameof(handlebarsModulePath));
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
			if (!File.Exists(underscoreModulePath))
			{
				throw new ArgumentException(
					$"Underscore not found in: {underscoreModulePath}", nameof(underscoreModulePath));
			}

			this.JsDomModulePath = this.FixPath(new FileInfo(jsDomModulePath).FullName);
			this.JQueryModulePath = this.FixPath(new FileInfo(jQueryModulePath).FullName);
			this.HandlebarsModulePath = this.FixPath(new FileInfo(handlebarsModulePath).FullName);
			this.SinonModulePath = this.FixPath(new FileInfo(sinonModulePath).FullName);
			this.SinonChaiModulePath = this.FixPath(new FileInfo(sinonChaiModulePath).FullName);
			this.UnderscoreModulePath = this.FixPath(new FileInfo(underscoreModulePath).FullName);
        }

		protected string JsDomModulePath { get; private set; }
		protected string JQueryModulePath { get; private set; }
		protected string HandlebarsModulePath { get; private set; }
		protected string SinonModulePath { get; private set; }
		protected string SinonChaiModulePath { get; private set; }
		protected string UnderscoreModulePath { get; private set; }

		protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const { jsdom } = require(""" + this.JsDomModulePath + @"""),
	document = jsdom('<html></html>', {}),
	window = document.defaultView;
const $ = require(""" + this.JQueryModulePath + @""")(window);

const sinon = require(""" + this.SinonModulePath + @""");
const sinonChai = require(""" + this.SinonChaiModulePath + @""");
chai.use(sinonChai);

const _ = require(""" + this.UnderscoreModulePath + @""");
const handlebars = require(""" + this.HandlebarsModulePath + @""");
";

        protected override string JsCodeTemplate => this.JsCodeRequiredModules + @"
function getSandboxFunction(codeToExecute) {
    const code = `
		const result = (function() {
			return (${codeToExecute}.bind({}));
		}).call({})();

		it('Test # " + this.testIndexPlaceholder + @"', () => {
" + this.argumentsPlaceholderName + @"
		});
    `;
    const timeout = " + this.timeLimitPlaceholderName + @";

    return function() {
        const sandbox = {
			it, expect,
			document, window, $,
			sinon, handlebars, _
        };

        const vm = new VM({ timeout, sandbox });
        const returnValue = vm.run(code);
    }
};

const code = " + this.userCodePlaceholderName + @"
getSandboxFunction(code)();
";
    }
}

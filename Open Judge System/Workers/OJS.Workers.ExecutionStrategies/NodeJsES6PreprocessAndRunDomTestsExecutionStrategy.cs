namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.IO;

    public class NodeJsES6PreprocessAndRunDomTestsExecutionStrategy : NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy
    {
        private readonly string jsDomModulePath;

        private readonly string jQueryModulePath;

        private readonly string handlebarsModulePath;

        private readonly string underscoreModulePath;

        public NodeJsES6PreprocessAndRunDomTestsExecutionStrategy(
            string nodeJsExecutablePath,
            string vm2ModulePath,
            string mochaModulePath,
            string chaiModulePath,
            string sinonModulePath,
            string sinonChaiModulePath,
            string jsDomModulePath,
            string jQueryModulePath,
            string handlebarsModulePath,
            string underscoreModulePath)
            : base(nodeJsExecutablePath, vm2ModulePath, mochaModulePath, chaiModulePath, sinonModulePath, sinonChaiModulePath)
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

            if (!File.Exists(underscoreModulePath))
            {
                throw new ArgumentException(
                    $"Underscore not found in: {underscoreModulePath}", nameof(underscoreModulePath));
            }

            this.jsDomModulePath = this.FixStringPath(new FileInfo(jsDomModulePath).FullName);
            this.jQueryModulePath = this.FixStringPath(new FileInfo(jQueryModulePath).FullName);
            this.handlebarsModulePath = this.FixStringPath(new FileInfo(handlebarsModulePath).FullName);
            this.underscoreModulePath = this.FixStringPath(new FileInfo(underscoreModulePath).FullName);
        }

        protected string JsDomModulePath => this.jsDomModulePath;

        protected string JQueryModulePath => this.jQueryModulePath;

        protected string HandlebarsModulePath => this.handlebarsModulePath;

        protected string UnderscoreModulePath => this.underscoreModulePath;

        protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const { jsdom } = require(""" + this.JsDomModulePath + @"""),
    document = jsdom(""<html></html>"", {}),
    window = document.defaultView;
const $ = require(""" + this.JQueryModulePath + @""")(window);

const handlebars = require(""" + this.HandlebarsModulePath + @""");

const _ = require(""" + this.UnderscoreModulePath + @""");
";

        protected override string JsSandboxItems => base.JsSandboxItems + @",
document, window, $,
sinon, handlebars, _
";

        protected override string JsHiddenItems => base.JsHiddenItems + ", jsdom, handlebars";
    }
}

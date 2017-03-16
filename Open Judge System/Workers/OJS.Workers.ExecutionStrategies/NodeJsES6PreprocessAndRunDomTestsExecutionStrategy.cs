﻿namespace OJS.Workers.ExecutionStrategies
{
    using System;
    using System.IO;

    public class NodeJsES6PreprocessAndRunDomTestsExecutionStrategy : NodeJsES6PreprocessAndRunMochaTestsExecutionStrategy
    {
        private readonly string jsDomModulePath;

        private readonly string jQueryModulePath;

        private readonly string handlebarsModulePath;

        private readonly string sinonModulePath;

        private readonly string sinonChaiModulePath;

        private readonly string underscoreModulePath;

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

            this.jsDomModulePath = this.FixPath(new FileInfo(jsDomModulePath).FullName);
            this.jQueryModulePath = this.FixPath(new FileInfo(jQueryModulePath).FullName);
            this.handlebarsModulePath = this.FixPath(new FileInfo(handlebarsModulePath).FullName);
            this.sinonModulePath = this.FixPath(new FileInfo(sinonModulePath).FullName);
            this.sinonChaiModulePath = this.FixPath(new FileInfo(sinonChaiModulePath).FullName);
            this.underscoreModulePath = this.FixPath(new FileInfo(underscoreModulePath).FullName);
        }

        protected string JsDomModulePath => this.jsDomModulePath;

        protected string JQueryModulePath => this.jQueryModulePath;

        protected string HandlebarsModulePath => this.handlebarsModulePath;

        protected string SinonModulePath => this.sinonModulePath;

        protected string SinonChaiModulePath => this.sinonChaiModulePath;

        protected string UnderscoreModulePath => this.underscoreModulePath;

        protected override string JsCodeRequiredModules => base.JsCodeRequiredModules + @"
const { jsdom } = require(""" + this.JsDomModulePath + @"""),
    document = jsdom('<html></html>', {}),
    window = document.defaultView;
const $ = require(""" + this.JQueryModulePath + @""")(window);

const handlebars = require(""" + this.HandlebarsModulePath + @""");

const sinon = require(""" + this.SinonModulePath + @""");
const sinonChai = require(""" + this.SinonChaiModulePath + @""");
chai.use(sinonChai);

const _ = require(""" + this.UnderscoreModulePath + @""");
";

        protected override string JsSandboxItems => base.JsSandboxItems + @",
document, window, $,
sinon, handlebars, _
";
    }
}

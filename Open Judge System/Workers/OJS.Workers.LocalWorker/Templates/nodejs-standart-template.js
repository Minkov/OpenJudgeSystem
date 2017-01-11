const fs = require("fs");
const { VM } = require("vm2");

function execute(codeToExecute) {
    let funcName = `func${Date.now()}`;

    let code = `
        let scope = {
            ${funcName}: (function(){
                return ${codeToExecute}.bind({});
            }).call({})
        };
        scope.${funcName}(args);
    `;

    let sandbox = {
        console: {
            logs: [],
            log(msg) {
                this.logs.push(msg);
            }
        }
    };

    let timeout = 1000;

    return function(args) {
        sandbox.args = args;
        const vm = new VM({ timeout, sandbox })
        let result = vm.run(code);

        return [...sandbox.console.logs, result];
    }
};

let code = THIS_IS_THE_PLACE_FOR_THE_CODE;
let args = THIS_IS_THE_PLACE_FOR_THE_ARGS;

let func = execute(code);

let result = func(args);
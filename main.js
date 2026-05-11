// This script initializes the .NET runtime and starts Avalonia
import { dotnet } from './_framework/dotnet.js'

window.onerror = function (msg, url, line, col, error) {
    if (msg && msg.toString().includes("already exited")) {
        return true;
    }
};

window.onunhandledrejection = function (event) {
    if (event.reason && event.reason.message && event.reason.message.includes("already exited")) {
        event.preventDefault();
    }
};

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

try {
    await dotnet.run();
} catch (err) {
    if (err.message.includes("already exited")) {
        console.error("./.NET Runtime crashed. Stopping execution to prevent console flood.");
    } else {
        throw err;
    }
}

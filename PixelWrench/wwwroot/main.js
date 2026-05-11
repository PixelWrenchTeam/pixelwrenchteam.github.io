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

const runtime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = runtime.getConfig();

try {
    await runtime.runMain();
} catch (err) {
    if (err.message && err.message.includes("already exited")) {
        console.warn("Runtime exited gracefully or via handled crash.");
    } else {
        throw err;
    }
}

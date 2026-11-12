import { dotnet } from './_framework/dotnet.js';

const splash = document.getElementById('splash');
const progress = document.getElementById('progress');

try {
    const runtime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .withResourceLoader((type, name, url) => url)
        .withModuleConfig({
            onDownloadResourceProgress: (loaded, total) => {
                if (total > 0) progress.style.width = `${Math.round(100 * loaded / total)}%`;
            },
        })
        .create();
    const config = runtime.getConfig();
    const started = runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    const hide = () => splash.classList.add('done');
    new MutationObserver((_, observer) => {
        if (document.querySelector('#out canvas')) { hide(); observer.disconnect(); }
    }).observe(document.getElementById('out'), { childList: true, subtree: true });
    await started;
} catch (error) {
    splash.classList.add('failed');
    document.getElementById('message').textContent = `The math tools could not start: ${error}`;
    throw error;
}

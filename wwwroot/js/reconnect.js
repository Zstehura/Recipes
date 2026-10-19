// Behavior for the custom reconnection banner in _Layout.cshtml.
// Blazor dispatches "components-reconnect-state-changed" on the banner element as the
// connection state changes (and sets matching CSS classes, which the styles use).
(() => {
    const banner = document.getElementById('components-reconnect-modal');
    if (!banner) {
        return;
    }

    // "failed": the server couldn't be reached; let the user try again
    document.getElementById('reconnect-retry')?.addEventListener('click', () => Blazor.reconnect());

    banner.addEventListener('components-reconnect-state-changed', (event) => {
        // "rejected": the server was reached but no longer has this session. Reload to start a
        // new one; unsaved form changes come back from the browser drafts.
        if (event.detail.state === 'rejected') {
            location.reload();
        }
    });
})();

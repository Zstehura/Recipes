// Copy and share for the grocery list (Pages/GroceryList.razor). The Clipboard and Web Share
// APIs only exist on HTTPS (or localhost), so on a plain-HTTP home server copying falls back to
// the older select-and-copy, and sharing isn't offered.
window.appShare = {
    canShare: () => window.isSecureContext && typeof navigator.share === 'function',

    // Returns false if the user closed the share sheet without sharing
    share: async (title, text) => {
        try {
            await navigator.share({ title, text });
            return true;
        } catch (e) {
            if (e.name === 'AbortError') return false;
            throw e;
        }
    },

    copy: async (text) => {
        if (window.isSecureContext && navigator.clipboard) {
            await navigator.clipboard.writeText(text);
            return;
        }
        const area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        try {
            if (!document.execCommand('copy')) throw new Error('Copy was refused');
        } finally {
            area.remove();
        }
    },
};

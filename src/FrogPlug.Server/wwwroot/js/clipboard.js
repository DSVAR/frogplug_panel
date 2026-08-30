window.fluxClipboard = window.fluxClipboard || {
    copy: async function (value) {
        await navigator.clipboard.writeText(value || "");
    }
};

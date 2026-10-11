window.fluxClipboard = window.fluxClipboard || {
    copy: async function (value) {
        const text = value || "";
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch {
                // Fall back to the selection-based copy below.
            }
        }

        return window.fluxClipboard.copyBySelection(text);
    },

    copyBySelection: function (text) {
        const area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        const copied = document.execCommand("copy");
        area.remove();
        return copied;
    }
};

document.addEventListener("click", async function (event) {
    const button = event.target.closest("[data-copy-target]");
    if (!button) {
        return;
    }

    const input = document.getElementById(button.dataset.copyTarget);
    if (!input) {
        return;
    }

    const copied = await window.fluxClipboard.copy(input.value);
    const original = button.dataset.copyLabel || button.textContent;
    button.dataset.copyLabel = original;
    button.textContent = copied ? "Скопировано" : "Не удалось";
    setTimeout(function () {
        button.textContent = original;
    }, 2000);
});

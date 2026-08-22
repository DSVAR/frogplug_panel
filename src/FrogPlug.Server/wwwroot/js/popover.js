window.fluxPopover = {
    watchers: new WeakMap(),
    getPosition(anchorId, menuId) {
        const button = document.getElementById(anchorId);
        const menu = document.getElementById(menuId);
        if (!button || !menu) {
            return { left: 8, top: 8 };
        }

        const rect = button.getBoundingClientRect();
        const menuRect = menu.getBoundingClientRect();
        const width = menuRect.width;
        const height = menuRect.height;
        const left = Math.min(
            Math.max(8, rect.right - width),
            window.innerWidth - width - 8);
        const top = rect.bottom + 6 + height <= window.innerHeight
            ? rect.bottom + 6
            : Math.max(8, rect.top - height - 6);
        return { left, top };
    },
    watch(dotnet) {
        const close = () => dotnet.invokeMethodAsync("CloseMenuFromViewport");
        const previous = this.watchers.get(dotnet);
        if (previous) {
            window.removeEventListener("scroll", previous, true);
            window.removeEventListener("resize", previous);
        }

        this.watchers.set(dotnet, close);
        window.addEventListener("scroll", close, true);
        window.addEventListener("resize", close);
    },
    unwatch(dotnet) {
        const close = this.watchers.get(dotnet);
        if (!close) {
            return;
        }

        window.removeEventListener("scroll", close, true);
        window.removeEventListener("resize", close);
        this.watchers.delete(dotnet);
    },
    focus(id) {
        document.getElementById(id)?.focus();
    }
};

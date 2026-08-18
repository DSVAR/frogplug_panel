document.addEventListener("click", (event) => {
    const button = event.target.closest("[data-password-toggle]");
    if (!button) {
        return;
    }

    const input = document.getElementById(button.dataset.passwordToggle);
    if (!input) {
        return;
    }

    const isPassword = input.type === "password";
    input.type = isPassword ? "text" : "password";
    button.setAttribute("aria-label", isPassword ? "Скрыть пароль" : "Показать пароль");
});

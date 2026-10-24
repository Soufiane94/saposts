// Confirmation for destructive actions (buttons with data-confirm). Without JavaScript the action runs directly.
document.addEventListener("submit", (event) => {
  const message = event.submitter?.dataset.confirm;
  if (message && !window.confirm(message)) {
    event.preventDefault();
  }
});

// Copy buttons next to read-only fields (shown only when the clipboard is available).
for (const button of document.querySelectorAll("[data-copy-button]")) {
  const source = button.parentElement.querySelector("[data-copy-source]");
  if (!source || !navigator.clipboard) {
    continue;
  }
  button.hidden = false;
  button.addEventListener("click", async () => {
    await navigator.clipboard.writeText(source.value);
    button.textContent = "Copié";
    setTimeout(() => { button.textContent = "Copier"; }, 2000);
  });
}

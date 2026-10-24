// Confirmation for destructive actions (buttons with data-confirm). Without JavaScript the action runs directly.
document.addEventListener("submit", (event) => {
  const message = event.submitter?.dataset.confirm;
  if (message && !window.confirm(message)) {
    event.preventDefault();
  }
});

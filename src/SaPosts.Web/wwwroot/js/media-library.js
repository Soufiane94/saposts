// Media library: drop zone and file picker. Reloads the list when every file was uploaded cleanly;
// otherwise the upload list stays, so errors and warnings can be read.
(() => {
  "use strict";

  const zone = document.querySelector("[data-dropzone]");
  const input = document.querySelector("[data-dropzone-input]");
  if (!zone || !input || !window.SaPostsMedia) {
    return;
  }

  const upload = async (files) => {
    if (files.length === 0) {
      return;
    }
    const succeeded = await window.SaPostsMedia.upload(files);
    const clean = !document.querySelector('.upload[data-state="error"], .upload[data-state="warning"]');
    if (succeeded > 0 && clean) {
      window.location.reload();
    }
  };

  zone.addEventListener("dragover", (event) => {
    event.preventDefault();
    zone.dataset.active = "true";
  });
  zone.addEventListener("dragleave", () => delete zone.dataset.active);
  zone.addEventListener("drop", (event) => {
    event.preventDefault();
    delete zone.dataset.active;
    upload([...event.dataTransfer.files]);
  });
  input.addEventListener("change", () => {
    upload([...input.files]);
    input.value = "";
  });
})();

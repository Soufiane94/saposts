// Media uploads for the post editor and the media library. Asks for the alt text of each image,
// reads video dimensions (and whether this browser can play the file), then uploads one file at a
// time with progress. Exposes window.SaPostsMedia.upload(files, onUploaded).
(() => {
  "use strict";

  const root = document.querySelector("[data-media-upload]");
  const list = document.querySelector("[data-upload-list]");
  if (!root || !list) {
    return;
  }

  const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
  const dialog = root.querySelector("[data-alt-dialog]");
  const accepted = /^(image\/(jpeg|png|webp|avif|gif)|video\/(mp4|webm))$/;

  const askAltText = (file) => new Promise((resolve) => {
    const preview = dialog.querySelector("[data-alt-preview]");
    const input = dialog.querySelector("[data-alt-input]");
    const url = URL.createObjectURL(file);
    preview.src = url;
    dialog.querySelector("[data-alt-file]").textContent = file.name;
    input.value = "";
    dialog.returnValue = "";
    dialog.addEventListener("close", () => {
      URL.revokeObjectURL(url);
      resolve(dialog.returnValue === "confirm" && input.value.trim() ? input.value.trim() : null);
    }, { once: true });
    dialog.showModal();
  });

  // Dimensions avoid a layout shift when the video loads; a browser that cannot read the file
  // probably means a codec (such as HEVC) that readers' browsers cannot play either.
  const probeVideo = (file) => new Promise((resolve) => {
    const video = document.createElement("video");
    const url = URL.createObjectURL(file);
    const finish = (result) => { URL.revokeObjectURL(url); resolve(result); };
    video.preload = "metadata";
    video.muted = true;
    video.addEventListener("loadedmetadata", () => finish({ width: video.videoWidth, height: video.videoHeight }), { once: true });
    video.addEventListener("error", () => finish({ width: 0, height: 0 }), { once: true });
    video.src = url;
  });

  const send = (file, fields, onProgress) => new Promise((resolve, reject) => {
    const body = new FormData();
    for (const [name, value] of Object.entries(fields)) {
      body.append(name, value);
    }
    body.append("file", file); // last: the server reads the fields before streaming the file

    const request = new XMLHttpRequest(); // fetch() has no upload progress
    request.open("POST", root.dataset.uploadUrl);
    request.setRequestHeader("RequestVerificationToken", token);
    request.responseType = "json";
    request.upload.addEventListener("progress", (event) => {
      if (event.lengthComputable) {
        onProgress(event.loaded / event.total);
      }
    });
    request.addEventListener("load", () => {
      if (request.status === 200) {
        resolve(request.response);
      } else {
        reject(new Error(request.response?.detail ?? `Erreur ${request.status}.`));
      }
    });
    request.addEventListener("error", () => reject(new Error("Erreur réseau : l’envoi a été interrompu.")));
    request.send(body);
  });

  const addItem = (file) => {
    const item = document.createElement("li");
    item.className = "upload";
    const name = document.createElement("span");
    name.className = "upload__name";
    name.textContent = file.name;
    const progress = document.createElement("progress");
    progress.max = 100;
    progress.value = 0;
    progress.setAttribute("aria-label", `Envoi de ${file.name}`);
    const status = document.createElement("span");
    status.className = "upload__status";
    item.append(name, progress, status);
    list.append(item);

    const setStatus = (text, kind) => {
      status.textContent = text;
      item.dataset.state = kind;
    };
    return {
      progress: (ratio) => {
        progress.value = Math.round(ratio * 100);
        if (ratio >= 1) {
          setStatus("Traitement…", "processing");
        }
      },
      waiting: () => setStatus("En attente…", "waiting"),
      sending: () => setStatus("Envoi…", "sending"),
      done: (warning) => { progress.value = 100; setStatus(warning ? `Envoyé. ${warning}` : "Envoyé.", warning ? "warning" : "done"); },
      fail: (message) => { progress.remove(); setStatus(message, "error"); },
      remove: () => item.remove(),
    };
  };

  async function uploadOne(file, onUploaded) {
    const item = addItem(file);
    if (!accepted.test(file.type)) {
      item.fail("Format non accepté. Images : JPEG, PNG, WebP, AVIF, GIF. Vidéos : MP4 ou WebM.");
      return false;
    }

    const fields = {};
    let warning = null;
    if (file.type.startsWith("image/")) {
      item.waiting();
      const alt = await askAltText(file);
      if (alt === null) {
        item.remove();
        return false;
      }
      fields.alt = alt;
    } else {
      const { width, height } = await probeVideo(file);
      if (width > 0 && height > 0) {
        fields.width = width;
        fields.height = height;
      } else {
        warning = "Ce navigateur ne peut pas lire cette vidéo : vos lecteurs non plus, probablement. Préférez H.264/AAC (MP4) ou VP9 (WebM).";
      }
    }

    item.sending();
    try {
      const media = await send(file, fields, item.progress);
      item.done([warning, media.warning].filter(Boolean).join(" "));
      onUploaded?.(media);
      return true;
    } catch (error) {
      item.fail(error.message);
      return false;
    }
  }

  window.SaPostsMedia = {
    /** Uploads files one by one; resolves with the number of successful uploads. */
    async upload(files, onUploaded) {
      let succeeded = 0;
      for (const file of files) {
        if (await uploadOne(file, onUploaded)) {
          succeeded++;
        }
      }
      return succeeded;
    },
  };
})();

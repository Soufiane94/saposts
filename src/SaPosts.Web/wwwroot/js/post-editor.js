// Post editor: slug suggestion, TinyMDE on the body, a toolbar of real buttons, and a live preview
// rendered by the server (same pipeline as on save). Without JavaScript the form still works.
(() => {
  "use strict";

  const form = document.querySelector("[data-post-editor]");
  if (!form) {
    return;
  }

  const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
  const textarea = form.querySelector(".editor__source");
  const toolbar = form.querySelector(".editor__toolbar");
  const preview = form.querySelector("[data-preview]");

  // Slug: while the field is empty, show the slug the server will generate (approximately) as a placeholder.
  const title = form.querySelector("[data-slug-source]");
  const slug = form.querySelector("[data-slug-target]");
  const slugify = (text) =>
    text.normalize("NFD").replace(/\p{Mn}/gu, "").toLowerCase()
      .replace(/œ/g, "oe").replace(/æ/g, "ae").replace(/ß/g, "ss")
      .replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "") || "article";
  const suggestSlug = () => { slug.placeholder = slugify(title.value); };
  title.addEventListener("input", suggestSlug);
  suggestSlug();

  let timer;
  let pending;
  const refreshPreview = (markdown) => {
    clearTimeout(timer);
    timer = setTimeout(async () => {
      pending?.abort();
      pending = new AbortController();
      const body = new FormData();
      body.append("markdown", markdown);
      body.append("__RequestVerificationToken", token);
      try {
        const response = await fetch(form.dataset.previewUrl, { method: "POST", body, signal: pending.signal });
        if (response.ok) {
          preview.innerHTML = await response.text(); // sanitized server-side
        }
      } catch (error) {
        if (error.name !== "AbortError") {
          console.warn("Preview failed", error);
        }
      }
    }, 400);
  };

  if (!window.TinyMDE) {
    textarea.addEventListener("input", () => refreshPreview(textarea.value));
    return;
  }

  const editor = new window.TinyMDE.Editor({ textarea });

  // TinyMDE hides the labelled textarea behind a bare contenteditable element:
  // give that element the semantics and the label of the field it replaces.
  const surface = editor.e;
  surface.id = "body-editor";
  surface.classList.add("editor__surface");
  surface.setAttribute("role", "textbox");
  surface.setAttribute("aria-multiline", "true");
  surface.setAttribute("aria-labelledby", "body-label");
  surface.setAttribute("aria-describedby", "body-hint body-error");
  surface.setAttribute("spellcheck", "true");
  toolbar.setAttribute("aria-controls", surface.id);

  editor.addEventListener("change", (event) => refreshPreview(event.content));

  // TinyMDE's toggleCommandState() relies on a state cached on selection events, which can be stale:
  // read the state at call time instead.
  const toggle = (command) => {
    const wasActive = Boolean(editor.getCommandState()[command]);
    editor.setCommandState(command, !wasActive);

    // On an empty line, a line command inserts its marker ("- ", "> ", "## ") but leaves the caret before it.
    const focus = editor.getSelection();
    if (!wasActive && focus) {
      const line = editor.getContent().split("\n")[focus.row] ?? "";
      const marker = line.match(/^\s*(?:#{1,6}|[-*+]|\d+[.)]|>)\s+/)?.[0].length ?? 0;
      if (focus.col < marker) {
        editor.setSelection({ row: focus.row, col: marker });
      }
    }
  };

  const commands = {
    heading: () => toggle("h2"),
    bold: () => toggle("bold"),
    italic: () => toggle("italic"),
    code: () => toggle("code"),
    list: () => toggle("ul"),
    quote: () => toggle("blockquote"),
    link: () => {
      const url = window.prompt("Adresse du lien (https://…)");
      if (url) {
        editor.wrapSelection("[", `](${url.trim()})`);
      }
    },
  };

  // Keep the editor's selection when a button is clicked with the mouse.
  toolbar.addEventListener("mousedown", (event) => {
    if (event.target.closest("[data-command]")) {
      event.preventDefault();
    }
  });
  toolbar.addEventListener("click", (event) => {
    const button = event.target.closest("[data-command]");
    if (button) {
      commands[button.dataset.command]?.();
    }
  });

  // Media: dropped on the editor or chosen with the toolbar button; each upload is inserted where
  // the caret was, as its own paragraph. Uploaded images also become available as cover.
  const media = window.SaPostsMedia;
  const fileInput = toolbar.querySelector("[data-media-input]");
  const coverSelect = form.querySelector("[data-cover-select]");
  const coverPreview = form.querySelector("[data-cover-preview]");

  // A media reference must be a paragraph of its own (blank lines around it) to render full width.
  const insertMedia = (item, at) => {
    const lines = editor.getContent().split("\n");
    const position = at ?? { row: lines.length - 1, col: lines.at(-1).length };
    const hasText = (row) => (lines[row] ?? "").trim().length > 0;
    const before = hasText(position.row) ? "\n\n" : position.row > 0 && hasText(position.row - 1) ? "\n" : "";
    const after = hasText(position.row + 1) ? "\n\n" : "\n";
    editor.paste(`${before}${item.markdown}${after}`, position, position);

    if (item.kind === "image" && coverSelect) {
      const option = new Option(item.label, item.id);
      option.dataset.thumbnail = item.thumbnailUrl;
      coverSelect.add(option, 1);
    }
  };

  const uploadAndInsert = async (files) => {
    if (!media || files.length === 0) {
      return;
    }
    // Uploads keep their order; each one goes after the previous.
    let at = editor.getSelection();
    await media.upload(files, (item) => {
      insertMedia(item, at);
      at = editor.getSelection();
    });
  };

  editor.addEventListener("drop", (event) => uploadAndInsert([...event.dataTransfer.files]));
  commands.media = () => fileInput.click();
  fileInput.addEventListener("change", () => {
    uploadAndInsert([...fileInput.files]);
    fileInput.value = "";
  });

  coverSelect?.addEventListener("change", () => {
    const thumbnail = coverSelect.selectedOptions[0]?.dataset.thumbnail;
    coverPreview.hidden = !thumbnail;
    if (thumbnail) {
      coverPreview.src = thumbnail;
    }
  });

  // Ctrl/Cmd+B and +I would otherwise insert <b>/<i> elements into the contenteditable.
  surface.addEventListener("keydown", (event) => {
    if (!(event.ctrlKey || event.metaKey) || event.altKey) {
      return;
    }
    const command = { b: "bold", i: "italic" }[event.key.toLowerCase()];
    if (command) {
      event.preventDefault();
      commands[command]();
    }
  });

  toolbar.hidden = false;
})();

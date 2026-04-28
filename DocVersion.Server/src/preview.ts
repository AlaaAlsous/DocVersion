import { dom, state } from "./state";
import { getResponseContentType, isTextContentType } from "./utils";

export function revokePreviewObjectUrl(): void {
  if (!state.activePreviewObjectUrl) return;
  URL.revokeObjectURL(state.activePreviewObjectUrl);
  state.activePreviewObjectUrl = null;
}

export function resetPreviewSurface(): void {
  revokePreviewObjectUrl();
  dom.fileContentBody.innerHTML = "";
  dom.fileContentBody.classList.remove(
    "media-preview",
    "binary-preview",
    "word-preview",
  );
}

export function updateEditorActions() {
  dom.editBtn.style.display = "inline-block";
  dom.editBtn.style.visibility = state.currentFileIsEditable
    ? "visible"
    : "hidden";
  dom.saveBtn.style.display = "none";
  dom.cancelBtn.style.display = "none";
}

export function setFileContentHeader(fileName = "", contextLabel = "") {
  dom.fileContentTitle.textContent = "File Content";

  if (!fileName) {
    dom.fileContentPath.textContent = "";
    dom.fileContentPath.style.display = "none";
    return;
  }

  const fullPath = state.currentPath
    ? `${state.currentPath}/${fileName}`
    : fileName;
  dom.fileContentPath.textContent = contextLabel
    ? `${fullPath} (${contextLabel})`
    : fullPath;
  dom.fileContentPath.style.display = "inline-block";
}

export function showTextPreview(
  text: string,
  { editable = true }: { editable?: boolean } = {},
) {
  resetPreviewSurface();

  dom.fileContentBody.textContent = text || "This file is empty.";
  dom.fileContentTextarea.value = text || "";
  state.currentFileIsEditable = editable;
  state.isEditMode = false;

  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";
  updateEditorActions();

  if (text && text.length > 0) {
    const lines = text.split("\n");
    const container = document.createElement("div");
    container.className = "file-preview-lines";
    lines.forEach((line, idx) => {
      const row = document.createElement("div");
      row.className = "file-preview-line";
      const num = document.createElement("span");
      num.className = "file-preview-linenum";
      num.textContent = (idx + 1).toString();
      const code = document.createElement("span");
      code.className = "file-preview-code";
      code.textContent = line || "\u200B";
      row.appendChild(num);
      row.appendChild(code);
      container.appendChild(row);
    });
    dom.fileContentBody.innerHTML = "";
    dom.fileContentBody.appendChild(container);
  } else {
    dom.fileContentBody.textContent = text || "This file is empty.";
  }
}

export function showBinaryPreviewMessage(message: string) {
  resetPreviewSurface();

  dom.fileContentBody.classList.add("binary-preview");
  dom.fileContentBody.textContent = message;
  dom.fileContentTextarea.value = "";
  state.currentFileIsEditable = false;
  state.isEditMode = false;

  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";
  updateEditorActions();
}

export function showMediaPreview(blob: Blob, tagName: string) {
  resetPreviewSurface();

  state.activePreviewObjectUrl = URL.createObjectURL(blob);
  let mediaElement: HTMLImageElement | HTMLVideoElement | HTMLAudioElement;
  if (tagName === "img") {
    mediaElement = document.createElement("img");
    mediaElement.src = state.activePreviewObjectUrl!;
    mediaElement.className = "file-preview-media";
    mediaElement.alt = state.currentFileName || "Image preview";
  } else if (tagName === "video") {
    mediaElement = document.createElement("video");
    mediaElement.src = state.activePreviewObjectUrl!;
    mediaElement.className = "file-preview-media";
    mediaElement.controls = true;
    mediaElement.preload = "metadata";
  } else if (tagName === "audio") {
    mediaElement = document.createElement("audio");
    mediaElement.src = state.activePreviewObjectUrl!;
    mediaElement.className = "file-preview-media";
    mediaElement.controls = true;
    mediaElement.preload = "metadata";
  } else {
    mediaElement = document.createElement(tagName) as any;
    (mediaElement as any).src = state.activePreviewObjectUrl!;
    mediaElement.className = "file-preview-media";
  }

  dom.fileContentBody.classList.add("media-preview");
  dom.fileContentBody.appendChild(mediaElement);
  dom.fileContentTextarea.value = "";
  state.currentFileIsEditable = false;
  state.isEditMode = false;

  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";
  updateEditorActions();
}

export async function showWordPreview(blob: Blob) {
  resetPreviewSurface();

  dom.fileContentBody.classList.add("word-preview");
  dom.fileContentTextarea.value = "";
  state.currentFileIsEditable = false;
  state.isEditMode = false;
  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";
  updateEditorActions();

  try {
    const arrayBuffer = await blob.arrayBuffer();
    const result = await (window as any).mammoth.convertToHtml({ arrayBuffer });
    const container = document.createElement("div");
    container.className = "word-preview-content";
    container.innerHTML = result.value;
    dom.fileContentBody.appendChild(container);
  } catch {
    dom.fileContentBody.classList.remove("word-preview");
    dom.fileContentBody.classList.add("binary-preview");
    dom.fileContentBody.textContent =
      "Could not render this Word document. Use Download instead.";
  }
}

export function showPdfPreview(blob: Blob) {
  resetPreviewSurface();

  state.activePreviewObjectUrl = URL.createObjectURL(blob);
  const embed = document.createElement("embed");
  embed.src = state.activePreviewObjectUrl;
  embed.type = "application/pdf";
  embed.className = "file-preview-pdf";

  dom.fileContentBody.classList.add("media-preview");
  dom.fileContentBody.appendChild(embed);
  dom.fileContentTextarea.value = "";
  state.currentFileIsEditable = false;
  state.isEditMode = false;

  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";
  updateEditorActions();
}

export async function renderFilePreview(
  response: Response,
  fileName: string,
  contextLabel = "",
) {
  state.currentFileName = fileName;
  setFileContentHeader(fileName, contextLabel);

  const contentType = getResponseContentType(response);

  if (contentType.startsWith("image/")) {
    const blob = await response.blob();
    showMediaPreview(blob, "img");
    return;
  }

  if (contentType.startsWith("video/")) {
    const blob = await response.blob();
    showMediaPreview(blob, "video");
    return;
  }

  if (contentType.startsWith("audio/")) {
    const blob = await response.blob();
    showMediaPreview(blob, "audio");
    return;
  }

  if (contentType === "application/pdf") {
    const blob = await response.blob();
    showPdfPreview(blob);
    return;
  }

  const wordTypes = [
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/msword",
  ];
  if ((wordTypes as string[]).includes(contentType)) {
    const blob = await response.blob();
    await showWordPreview(blob);
    return;
  }

  if (isTextContentType(contentType)) {
    const text = await response.text();
    showTextPreview(text);
    return;
  }

  showBinaryPreviewMessage(
    contentType
      ? `Preview is not available for this file type (${contentType}). Use Download instead.`
      : "Preview is not available for this file type. Use Download instead.",
  );
}

import { dom, state } from "./state";
import { toApiPath, formatBytes } from "./utils";
import {
  clearErrorMessage,
  showErrorMessage,
  showSuccessMessage,
} from "./messages";
import { renderFilePreview, showTextPreview } from "./preview";
import {
  handleUnauthorizedResponse,
  logout,
  setExplorerPath,
  resetDetailsPanels,
} from "./auth";
import { getFilesHistory } from "./history";
import { displayFiles } from "./display";

export async function getFiles(path = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  state.currentPath = path || "";
  setExplorerPath(state.currentPath);
  dom.logoutBtn.style.display = "inline-block";

  try {
    const encodedPath = toApiPath(path);
    const url = encodedPath ? `/api/files/${encodedPath}` : "/api/files";
    const response = await fetch(url, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to fetch files");
      return;
    }

    const files = await response.json();
    clearErrorMessage();
    displayFiles(files);
  } catch (error) {
    showErrorMessage("Error fetching files");
  }
}

export async function showFileContent(fileName: string, contextLabel = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = state.currentPath
    ? `${state.currentPath}/${fileName}`
    : fileName;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to load file content");
      return;
    }

    await renderFilePreview(response, fileName, contextLabel);

    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error loading file content");
  }
}

export async function saveFile() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  if (!state.currentFileName) return;

  const filePath = state.currentPath
    ? `${state.currentPath}/${state.currentFileName}`
    : state.currentFileName;
  const encodedFilePath = toApiPath(filePath);
  const content = dom.fileContentTextarea.value;

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "text/plain",
      },
      body: content,
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to save file");
      return;
    }

    showTextPreview(content);

    if (state.activeHistoryFileName === state.currentFileName) {
      await getFilesHistory(state.currentFileName);
    }

    showSuccessMessage("File saved successfully");
  } catch (error) {
    showErrorMessage("Error saving file");
  }
}

export async function uploadFile() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }
  const file = dom.fileInput.files[0];

  if (!file) {
    showErrorMessage("No file selected");
    return;
  }

  const path = state.currentPath
    ? `${state.currentPath}/${file.name}`
    : file.name;
  const encodedPath = toApiPath(path);

  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Type": "file",
      },
      body: file,
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      if (response.status === 409) {
        showErrorMessage("A file with that name already exists");
      } else {
        showErrorMessage("Failed to upload file");
      }
      return;
    }

    dom.fileInput.value = "";
    dom.fileInputName.textContent = "";
    await getFiles(state.currentPath);
    showSuccessMessage(`File uploaded: ${file.name}`);
  } catch (error) {
    console.error("Error uploading file:", error);
    showErrorMessage("Error uploading file");
  }
}

export async function downloadFile(file: string) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = state.currentPath ? `${state.currentPath}/${file}` : file;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to download file");
      return;
    }

    const blob = await response.blob();
    const downloadUrl = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = file;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(downloadUrl);

    showSuccessMessage(`Download started: ${file}`);
  } catch (error) {
    showErrorMessage("Error downloading file");
  }
}

export async function createFolder() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }
  const folderName = dom.folderNameInput.value.trim();

  if (!folderName) {
    showErrorMessage("Folder name cannot be empty");
    return;
  }

  const path = state.currentPath
    ? `${state.currentPath}/${folderName}`
    : folderName;
  const encodedPath = toApiPath(path);

  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Type": "folder",
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      if (response.status === 409) {
        showErrorMessage("A folder with that name already exists");
      } else {
        showErrorMessage("Failed to create folder");
      }
      return;
    }

    dom.folderNameInput.value = "";
    await getFiles(state.currentPath);
    showSuccessMessage(`Folder created: ${folderName}`);
  } catch (error) {
    console.error("Error creating folder:", error);
    showErrorMessage("Error creating folder");
  }
}

export async function deleteItem(item: string) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const encodedPath = toApiPath(item);

  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage(`Failed to delete item (${response.status})`);
      return;
    }

    resetDetailsPanels();
    await getFiles(state.currentPath);
    showSuccessMessage(`Deleted: ${item.split("/").pop()}`);
  } catch (error) {
    console.error("Error deleting item:", error);
    showErrorMessage("Error deleting item");
  }
}

export async function showItemMetadata(itemName: string) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const itemPath = state.currentPath
    ? `${state.currentPath}/${itemName}`
    : itemName;
  const encodedItemPath = toApiPath(itemPath);

  try {
    const response = await fetch(`/api/files/${encodedItemPath}`, {
      method: "HEAD",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to fetch metadata");
      return;
    }

    const itemType = response.headers.get("X-Type") ?? "item";
    const bytesHeader = response.headers.get("X-Bytes");
    const metadata = {
      bytes: bytesHeader ? Number(bytesHeader) : "Unknown",
      created: response.headers.get("X-Created-At") ?? "Unknown",
      changed: response.headers.get("X-Changed-At") ?? "Unknown",
      extension: response.headers.get("X-Extension") ?? "",
      type: itemType,
    };

    showMetadata(itemName, metadata);
    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error fetching metadata");
  }
}

export function showDeleteConfirmation(itemName: string, itemPath: string) {
  clearErrorMessage();

  dom.errorMessage.style.display = "block";
  dom.errorMessage.style.color = "var(--gh-text)";
  dom.errorMessage.style.backgroundColor = "var(--gh-canvas)";
  dom.errorMessage.style.borderColor = "var(--gh-border)";
  dom.errorMessage.textContent = `Delete \"${itemName}\"?`;

  const actions = document.createElement("div");
  actions.className = "confirm-actions";

  const yesBtn = document.createElement("button");
  yesBtn.type = "button";
  yesBtn.className = "confirm-yes-btn";
  yesBtn.textContent = "Yes";
  yesBtn.addEventListener("click", async () => {
    await deleteItem(itemPath);
  });

  const noBtn = document.createElement("button");
  noBtn.type = "button";
  noBtn.className = "confirm-no-btn";
  noBtn.textContent = "No";
  noBtn.addEventListener("click", () => {
    clearErrorMessage();
  });

  actions.append(yesBtn, noBtn);
  dom.errorMessage.appendChild(actions);
}

export function showMetadata(
  file: string,
  metadata: {
    type: string;
    bytes: number | string;
    created: string;
    changed: string;
    extension?: string;
  },
) {
  dom.metadataBox.style.display = "block";
  dom.metadataBox.innerHTML = `
    <div class="metadata-header">
      <h3>Metadata</h3>
      <button id="closeMetadataBtn" class="metadata-close-btn" type="button" aria-label="Close metadata">X</button>
    </div>
    <p><strong>Name:</strong> ${file}</p>
    <p><strong>Type:</strong> ${metadata.type}</p>
    <p><strong>Size:</strong> ${formatBytes(typeof metadata.bytes === "number" ? metadata.bytes : Number(metadata.bytes))}</p>
    <p><strong>Created:</strong> ${metadata.created}</p>
    <p><strong>Modified:</strong> ${metadata.changed}</p>
    <p><strong>Extension:</strong> ${metadata.extension || "-"}</p>
  `;

  const closeBtn = dom.metadataBox.querySelector("#closeMetadataBtn");
  if (closeBtn) closeBtn.addEventListener("click", closeMetadata);
}

export function closeMetadata() {
  dom.metadataBox.style.display = "none";
}

export function editFile() {
  if (!state.currentFileName || !state.currentFileIsEditable) return;

  state.isEditMode = true;
  dom.fileContentBody.style.display = "none";
  dom.fileContentTextarea.style.display = "block";

  dom.editBtn.style.display = "none";
  dom.saveBtn.style.display = "inline-block";
  dom.cancelBtn.style.display = "inline-block";

  dom.fileContentTextarea.focus();
}

export function cancelEdit() {
  state.isEditMode = false;

  dom.fileContentBody.style.display = "block";
  dom.fileContentTextarea.style.display = "none";

  dom.editBtn.style.display = "inline-block";
  dom.saveBtn.style.display = "none";
  dom.cancelBtn.style.display = "none";
}

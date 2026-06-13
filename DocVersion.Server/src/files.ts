import { dom, state } from "./state";
import { toApiPath, formatBytes } from "./utils";
import {
  clearErrorMessage,
  showErrorMessage,
  showSuccessMessage,
} from "./messages";
import { renderFilePreview, showTextPreview } from "./preview";
import { editFile, cancelEdit } from "./preview";
export { editFile, cancelEdit };
import {
  fetchWithAuth,
  handleUnauthorizedResponse,
  setExplorerPath,
  resetDetailsPanels,
} from "./auth";
import { getFilesHistory } from "./history";
import { displayFiles } from "./display";
import { showSpinner, hideSpinner } from "./index";

export async function getFiles(path = "") {
  state.currentPath = path || "";
  setExplorerPath(state.currentPath);
  dom.logoutBtn.style.display = "inline-flex";

  showSpinner();
  try {
    const encodedPath = toApiPath(path);
    const url = encodedPath ? `/api/files/${encodedPath}` : "/api/files";
    const response = await fetchWithAuth(url);

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to fetch files");
      return;
    }

    const files = await response.json();
    clearErrorMessage();
    displayFiles(files);
    hideSpinner();
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error fetching files");
  }
}

export async function showFileContent(fileName: string, contextLabel = "") {
  const filePath = state.currentPath
    ? `${state.currentPath}/${fileName}`
    : fileName;
  const encodedFilePath = toApiPath(filePath);

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedFilePath}`);

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to load file content");
      return;
    }

    await renderFilePreview(response, fileName, contextLabel);

    hideSpinner();
    clearErrorMessage();
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error loading file content");
  }
}

export async function saveFile() {
  if (!state.currentFileName) return;

  const filePath = state.currentPath
    ? `${state.currentPath}/${state.currentFileName}`
    : state.currentFileName;
  const encodedFilePath = toApiPath(filePath);
  let content = "";
  const monacoDiv = document.getElementById("monacoEditor");
  if (
    monacoDiv &&
    monacoDiv.style.display !== "none" &&
    window.monacoEditorInstance
  ) {
    content = window.monacoEditorInstance.getValue();
  } else {
    content = dom.fileContentTextarea.value;
  }

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedFilePath}`, {
      method: "PUT",
      headers: {
        "Content-Type": "text/plain",
        "X-Type": "file",
      },
      body: content,
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to save file");
      return;
    }

    showTextPreview(content);

    if (state.activeHistoryFileName === state.currentFileName) {
      await getFilesHistory(state.currentFileName);
    }

    hideSpinner();
    setTimeout(() => {
      showSuccessMessage("File saved successfully");
    }, 350);
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error saving file");
  }
}

export async function uploadFile() {
  const file = dom.fileInput.files[0];

  if (!file) {
    showErrorMessage("No file selected");
    return;
  }

  const path = state.currentPath
    ? `${state.currentPath}/${file.name}`
    : file.name;
  const encodedPath = toApiPath(path);

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedPath}`, {
      method: "PUT",
      headers: {
        "X-Type": "file",
      },
      body: file,
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      if (response.status === 409) {
        showErrorMessage("A file or folder with that name already exists");
      } else {
        showErrorMessage("Failed to upload file");
      }
      return;
    }

    dom.fileInput.value = "";
    dom.fileInputName.textContent = "";
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage(`File uploaded: ${file.name}`);
  } catch (error) {
    hideSpinner();
    console.error("Error uploading file:", error);
    showErrorMessage("Error uploading file");
  }
}

export async function downloadFile(file: string) {
  const filePath = state.currentPath ? `${state.currentPath}/${file}` : file;
  const encodedFilePath = toApiPath(filePath);
  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedFilePath}`);

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
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

    hideSpinner();
    showSuccessMessage(`Download started: ${file}`);
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error downloading file");
  }
}

export async function createFolder() {
  const folderName = dom.folderNameInput.value.trim();

  if (!folderName) {
    showErrorMessage("Folder name cannot be empty");
    return;
  }

  const path = state.currentPath
    ? `${state.currentPath}/${folderName}`
    : folderName;
  const encodedPath = toApiPath(path);

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        "X-Type": "folder",
      },
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      if (response.status === 409) {
        showErrorMessage("A folder or file with that name already exists");
      } else {
        showErrorMessage("Failed to create folder");
      }
      return;
    }

    dom.folderNameInput.value = "";
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage(`Folder created: ${folderName}`);
  } catch (error) {
    hideSpinner();
    console.error("Error creating folder:", error);
    showErrorMessage("Error creating folder");
  }
}

export async function uploadFolder() {
  const files = dom.folderInput.files;
  if (!files || files.length === 0) {
    showErrorMessage("No folder selected");
    return;
  }

  const first = files[0].webkitRelativePath.split("/")[0];
  let exists = false;
  try {
    const encodedPath = toApiPath(
      state.currentPath ? `${state.currentPath}/${first}` : first,
    );
    const res = await fetchWithAuth(`/api/files/${encodedPath}`, {
      method: "HEAD",
    });
    if (res.ok && res.headers.get("X-Type") === "folder") {
      exists = true;
    }
  } catch {}
  if (exists) {
    showErrorMessage("A folder or file with that name already exists");
    return;
  }

  const formData = new FormData();
  for (let i = 0; i < files.length; i++) {
    const f = files[i];
    let relPath = f.webkitRelativePath;
    if (state.currentPath) {
      relPath = state.currentPath.replace(/^\/+|\/+$/g, "") + "/" + relPath;
    }
    formData.append("files", f, relPath);
  }
  showSpinner();
  try {
    const response = await fetchWithAuth("/api/files/upload-folder", {
      method: "POST",
      body: formData,
    });
    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }
    if (!response.ok) {
      hideSpinner();
      const data = await response.json().catch(() => ({}));
      showErrorMessage(data?.Message || "Failed to upload folder");
      return;
    }
    dom.folderInput.value = "";
    dom.folderInputName.textContent = "";
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage("Folder uploaded!");
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error uploading folder");
  }
}

export async function downloadFolderAsZip(folder: string) {
  const folderPath = state.currentPath
    ? `${state.currentPath}/${folder}`
    : folder;
  const encodedFolderPath = toApiPath(folderPath);
  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/zip/${encodedFolderPath}`);
    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }
    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to download folder");
      return;
    }
    const blob = await response.blob();
    const downloadUrl = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = downloadUrl;

    let filename = folder + ".zip";
    const disposition = response.headers.get("Content-Disposition");
    if (disposition) {
      const match = disposition.match(/filename="?([^";]+)"?/);
      if (match && match[1]) filename = match[1];
    }
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(downloadUrl);
    hideSpinner();
    showSuccessMessage(`Download started: ${filename}`);
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error downloading folder");
  }
}

if (
  dom.folderInput &&
  dom.folderInputLabel &&
  dom.uploadFolderBtn &&
  dom.folderInputName
) {
  dom.folderInputLabel.addEventListener("click", (e: MouseEvent) => {
    e.preventDefault();
    dom.folderInput.click();
  });

  dom.folderInput.addEventListener("change", () => {
    const files = dom.folderInput.files;
    if (files && files.length > 0) {
      const first = files[0].webkitRelativePath.split("/")[0];
      dom.folderInputName.textContent = first;
    } else {
      dom.folderInputName.textContent = "";
    }
  });

  dom.uploadFolderBtn.addEventListener("click", async () => {
    if (!dom.folderInput.files || dom.folderInput.files.length === 0) {
      showErrorMessage("No folder selected  to upload");
      return;
    }
    await uploadFolder();
    dom.folderInput.value = "";
    dom.folderInputName.textContent = "";
  });
}

export async function createFile() {
  const fileName = dom.fileNameInput.value.trim();
  if (!fileName) {
    showErrorMessage("File name cannot be empty");
    return;
  }
  const path = state.currentPath
    ? `${state.currentPath}/${fileName}`
    : fileName;
  const encodedPath = toApiPath(path);
  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        "X-Type": "file",
      },
      body: "",
    });
    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }
    if (!response.ok) {
      hideSpinner();
      if (response.status === 409) {
        showErrorMessage("A file or folder with that name already exists");
      } else {
        showErrorMessage("Failed to create file");
      }
      return;
    }
    dom.fileNameInput.value = "";
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage(`File created: ${fileName}`);
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error creating file");
  }
}

export async function deleteItem(item: string) {
  const encodedPath = toApiPath(item);

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedPath}`, {
      method: "DELETE",
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage(`Failed to delete item (${response.status})`);
      return;
    }

    resetDetailsPanels();
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage(`Deleted: ${item.split("/").pop()}`);
  } catch (error) {
    hideSpinner();
    console.error("Error deleting item:", error);
    showErrorMessage("Error deleting item");
  }
}

export async function showItemMetadata(itemName: string) {
  const itemPath = state.currentPath
    ? `${state.currentPath}/${itemName}`
    : itemName;
  const encodedItemPath = toApiPath(itemPath);

  showSpinner();
  try {
    const response = await fetchWithAuth(`/api/files/${encodedItemPath}`, {
      method: "HEAD",
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
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
    hideSpinner();
    clearErrorMessage();
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error fetching metadata");
  }
}

export function showDeleteConfirmation(itemName: string, itemPath: string) {
  clearErrorMessage();
  dom.errorMessage.classList.remove("hidden");
  dom.errorMessage.textContent = "";
  dom.errorMessage.style.color = "#b38600";
  dom.errorMessage.style.backgroundColor = "#fef3ba44";
  dom.errorMessage.style.borderColor = "#b38600";

  const question = document.createElement("span");
  question.textContent = `Delete \"${itemName}\"?`;
  dom.errorMessage.appendChild(question);

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
  dom.metadataBox.style.display = "flex";
  dom.metadataBox.innerHTML = "";

  const header = document.createElement("div");
  header.className = "metadata-header";

  const title = document.createElement("h3");
  title.textContent = "Metadata";

  const closeBtn = document.createElement("button");
  closeBtn.id = "closeMetadataBtn";
  closeBtn.className = "metadata-close-btn";
  closeBtn.type = "button";
  closeBtn.setAttribute("aria-label", "Close metadata");
  closeBtn.textContent = "X";

  header.append(title, closeBtn);
  dom.metadataBox.appendChild(header);

  const content = document.createElement("div");
  content.className = "metadata-content";

  const addRow = (label: string, value: string) => {
    const row = document.createElement("p");
    const strong = document.createElement("strong");
    strong.textContent = `${label}:`;
    row.appendChild(strong);
    row.append(` ${value}`);
    content.appendChild(row);
  };

  addRow("Name", file);
  addRow("Type", metadata.type);
  addRow(
    "Size",
    formatBytes(
      typeof metadata.bytes === "number"
        ? metadata.bytes
        : Number(metadata.bytes),
    ),
  );
  addRow("Created", metadata.created);
  addRow("Modified", metadata.changed);
  addRow("Extension", metadata.extension || "-");

  dom.metadataBox.appendChild(content);
  closeBtn.addEventListener("click", closeMetadata);
}

export function closeMetadata() {
  dom.metadataBox.style.display = "none";
}

export async function renameItem(
  oldName: string,
  newName: string,
  isFolder: boolean,
  prevName?: string,
  nextName?: string,
) {
  showSpinner();
  try {
    const response = await fetchWithAuth("/api/files/rename", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        OldName: oldName,
        NewName: newName,
        IsFolder: isFolder,
      }),
    });
    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }
    if (!response.ok) {
      hideSpinner();
      const data = await response.json().catch(() => ({}));
      if (data?.Message && prevName && nextName) {
        showErrorMessage(`${data.Message} (${prevName} → ${nextName})`);
      } else {
        await getFiles(state.currentPath);
        showErrorMessage(
          data?.Message ||
            "Rename failed: A file or folder with this name already exists locally or in the database.",
        );
      }
      return;
    }
    await getFiles(state.currentPath);
    if (!isFolder && newName) {
      await showItemMetadata(newName);
      await showFileContent(newName);
      await getFilesHistory(newName);
    }
    hideSpinner();
    if (prevName && nextName) {
      showSuccessMessage(`Renamed: ${prevName} → ${nextName}`);
    } else {
      showSuccessMessage(`Renamed to: ${newName}`);
    }
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error renaming item");
  }
}

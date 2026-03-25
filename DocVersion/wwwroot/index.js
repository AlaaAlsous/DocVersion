"use strict";
const loginModal = document.getElementById("loginModal");
const modalUserName = document.getElementById("modalUserName");
const modalPassword = document.getElementById("modalPassword");
const modalSubmit = document.getElementById("modalSubmit");
const logoutBtn = document.getElementById("logoutBtn");
const currentUser = document.getElementById("currentUser");
const modalError = document.getElementById("modalError");

const createFolderBtn = document.getElementById("createFolderBtn");
const folderNameInput = document.getElementById("folderName");
const uploadBtn = document.getElementById("uploadBtn");
const fileInput = document.getElementById("fileInput");

const errorMessage = document.getElementById("errorMessage");
const explorerPath = document.getElementById("explorerPath");
const fileList = document.getElementById("file-list");

const fileContentTitle = document.getElementById("fileContentTitle");
const fileContentBody = document.getElementById("fileContentBody");
const fileContentPath = document.getElementById("fileContentPath");
const fileContentTextarea = document.getElementById("fileContentTextarea");
const editBtn = document.getElementById("editBtn");
const saveBtn = document.getElementById("saveBtn");
const cancelBtn = document.getElementById("cancelBtn");

const metadataBox = document.getElementById("file-info-box");
const historyBox = document.getElementById("file-history-box");

let connection;
let currentPath = "";
let currentFileName = "";
let activeHistoryFileName = "";
let activeHistoryEntries = [];
let historyCursor = -1;
let isEditMode = false;
let errorMessageTimeoutId = null;
let modalErrorTimeoutId = null;

const MESSAGE_TIMEOUT_MS = 5000;
const DEFAULT_PREVIEW_TEXT = "Select a file to preview its content.";

function toApiPath(path = "") {
  if (!path) return "";
  return path
    .split("/")
    .filter((segment) => segment.length > 0)
    .map((segment) => encodeURIComponent(segment))
    .join("/");
}

function setCurrentUser(username) {
  if (!username) {
    currentUser.textContent = "";
    currentUser.style.display = "none";
    return;
  }

  currentUser.textContent = username;
  currentUser.style.display = "inline-block";
}

function setExplorerPath(path = "") {
  if (!explorerPath) return;
  explorerPath.textContent = path ? `/${path}` : "/";
}

function setFileContentHeader(fileName = "", contextLabel = "") {
  fileContentTitle.textContent = "File Content";

  if (!fileName) {
    fileContentPath.textContent = "";
    fileContentPath.style.display = "none";
    return;
  }

  const fullPath = currentPath ? `${currentPath}/${fileName}` : fileName;
  fileContentPath.textContent = contextLabel
    ? `${fullPath} (${contextLabel})`
    : fullPath;
  fileContentPath.style.display = "inline-block";
}

function clearModalError() {
  if (modalErrorTimeoutId) {
    clearTimeout(modalErrorTimeoutId);
    modalErrorTimeoutId = null;
  }
  modalError.textContent = "";
  modalError.style.display = "none";
}

function showModalError(message) {
  clearModalError();
  modalError.textContent = message;
  modalError.style.display = "block";
  modalErrorTimeoutId = setTimeout(() => {
    clearModalError();
  }, MESSAGE_TIMEOUT_MS);
}

function clearErrorMessage() {
  if (errorMessageTimeoutId) {
    clearTimeout(errorMessageTimeoutId);
    errorMessageTimeoutId = null;
  }

  errorMessage.textContent = "";
  errorMessage.style.display = "none";
  errorMessage.style.color = "";
  errorMessage.style.backgroundColor = "";
  errorMessage.style.borderColor = "";
}

function showErrorMessage(message) {
  clearErrorMessage();
  errorMessage.textContent = message;
  errorMessage.style.display = "block";
  errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

function showSuccessMessage(message) {
  clearErrorMessage();
  errorMessage.textContent = message;
  errorMessage.style.color = "var(--gh-success-text)";
  errorMessage.style.backgroundColor = "var(--gh-success-bg)";
  errorMessage.style.borderColor = "var(--gh-success-border)";
  errorMessage.style.display = "block";
  errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

function showDeleteConfirmation(itemName, itemPath) {
  clearErrorMessage();

  errorMessage.style.display = "block";
  errorMessage.style.color = "var(--gh-text)";
  errorMessage.style.backgroundColor = "var(--gh-canvas)";
  errorMessage.style.borderColor = "var(--gh-border)";
  errorMessage.textContent = `Delete \"${itemName}\"?`;

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
  errorMessage.appendChild(actions);
}

function closeMetadata() {
  metadataBox.style.display = "none";
}

function closeFileHistory() {
  activeHistoryFileName = "";
  activeHistoryEntries = [];
  historyCursor = -1;
  historyBox.style.display = "none";
}

function updateHistoryNavigationUi() {
  const statusEl = historyBox.querySelector("#historyNavStatus");
  const backBtn = historyBox.querySelector("#historyBackBtn");
  const forwardBtn = historyBox.querySelector("#historyForwardBtn");

  if (!statusEl || !backBtn || !forwardBtn) return;

  const canGoOlder = historyCursor + 1 < activeHistoryEntries.length;
  const canGoNewer = historyCursor >= 0;

  statusEl.textContent =
    historyCursor === -1
      ? "Nuvarande version"
      : `Version ${activeHistoryEntries[historyCursor].version}`;

  backBtn.disabled = !canGoOlder;
  forwardBtn.disabled = !canGoNewer;
}

async function showHistoryVersionContent(fileName, version) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = currentPath ? `${currentPath}/${fileName}` : fileName;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(
      `/api/files/history/${version}/${encodedFilePath}`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    if (!response.ok) {
      showErrorMessage("Failed to load history version");
      return;
    }

    const text = await response.text();
    currentFileName = fileName;

    setFileContentHeader(fileName, `History v${version}`);
    fileContentBody.textContent = text || "This file is empty.";
    fileContentTextarea.value = text || "";

    editBtn.style.display = "inline-block";
    saveBtn.style.display = "none";
    cancelBtn.style.display = "none";

    isEditMode = false;
    fileContentBody.style.display = "block";
    fileContentTextarea.style.display = "none";

    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error loading history version");
  }
}

async function navigateHistory(direction) {
  if (!activeHistoryFileName) return;
  if (isEditMode) return;

  if (direction < 0) {
    if (historyCursor + 1 >= activeHistoryEntries.length) return;
    historyCursor += 1;
    await showHistoryVersionContent(
      activeHistoryFileName,
      activeHistoryEntries[historyCursor].version,
    );
  } else if (direction > 0) {
    if (historyCursor < 0) return;

    if (historyCursor === 0) {
      historyCursor = -1;
      await showFileContent(activeHistoryFileName);
    } else {
      historyCursor -= 1;
      await showHistoryVersionContent(
        activeHistoryFileName,
        activeHistoryEntries[historyCursor].version,
      );
    }
  }

  updateHistoryNavigationUi();
}

function showMetadata(file, metadata) {
  metadataBox.style.display = "block";
  metadataBox.innerHTML = `
    <div class="metadata-header">
      <h3>Metadata</h3>
      <button class="metadata-close-btn" type="button" onclick="closeMetadata()" aria-label="Close metadata">X</button>
    </div>
    <p><strong>Name:</strong> ${file}</p>
    <p><strong>Type:</strong> ${metadata.type}</p>
    <p><strong>Size:</strong> ${metadata.bytes} bytes</p>
    <p><strong>Created:</strong> ${metadata.created}</p>
    <p><strong>Modified:</strong> ${metadata.changed}</p>
    <p><strong>Extension:</strong> ${metadata.extension || "-"}</p>
  `;
}

function displayFileHistory(filename, history) {
  activeHistoryFileName = filename;
  activeHistoryEntries = history;
  historyCursor = -1;
  historyBox.innerHTML = `
    <div class="history-header">
      <h3>History: ${filename}</h3>
      <button class="metadata-close-btn" type="button" onclick="closeFileHistory()" aria-label="Close history">X</button>
    </div>
    <div class="history-nav" role="group" aria-label="History navigation">
      <button id="historyBackBtn" class="history-nav-btn" type="button" onclick="navigateHistory(-1)">← Older</button>
      <span id="historyNavStatus" class="history-nav-status">Nuvarande version</span>
      <button id="historyForwardBtn" class="history-nav-btn" type="button" onclick="navigateHistory(1)">Newer →</button>
    </div>
    <ul class="history-list">
      ${history
        .map(
          (h) => `
        <li class="history-item">
          <span class="history-version">V.${h.version}</span>
          <span class="history-date">${new Date(h.createdAt).toLocaleString()}</span>
          <button class="history-restore-btn" onclick="restoreFileVersion('${filename}', ${h.version})">Restore</button>
        </li>
      `,
        )
        .join("")}
    </ul>
  `;
  historyBox.style.display = "block";
  updateHistoryNavigationUi();
}

function editFile() {
  if (!currentFileName) return;

  isEditMode = true;
  fileContentBody.style.display = "none";
  fileContentTextarea.style.display = "block";

  editBtn.style.display = "none";
  saveBtn.style.display = "inline-block";
  cancelBtn.style.display = "inline-block";

  fileContentTextarea.focus();
}

function cancelEdit() {
  isEditMode = false;

  fileContentBody.style.display = "block";
  fileContentTextarea.style.display = "none";

  editBtn.style.display = "inline-block";
  saveBtn.style.display = "none";
  cancelBtn.style.display = "none";
}

function resetDetailsPanels() {
  historyBox.style.display = "none";
  metadataBox.style.display = "none";

  setFileContentHeader();
  fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
  fileContentBody.style.display = "block";

  fileContentTextarea.value = "";
  fileContentTextarea.style.display = "none";

  editBtn.style.display = "none";
  saveBtn.style.display = "none";
  cancelBtn.style.display = "none";

  currentFileName = "";
  activeHistoryFileName = "";
  activeHistoryEntries = [];
  historyCursor = -1;
  isEditMode = false;
}

async function startSignalR() {
  const token = localStorage.getItem("jwt");
  if (!token) return false;

  const shouldRefreshCurrentPath = (path) => {
    if (!path) return false;
    if (!currentPath) return !path.includes("/");
    return path === currentPath || path.startsWith(`${currentPath}/`);
  };

  if (connection) {
    try {
      await connection.stop();
    } catch (error) {
      console.error("SignalR reconnect cleanup error:", error);
    }
  }

  const nextConnection = new signalR.HubConnectionBuilder()
    .withUrl("api/events/signalr", { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .build();

  nextConnection.on("Event", (type, path) => {
    switch (type) {
      case 0:
      case 1:
      case 2:
      case 5:
      case 7:
        if (shouldRefreshCurrentPath(path)) {
          void getFiles(currentPath);
        }
        break;
      default:
        console.log("Unknown event type:", type);
    }
  });

  try {
    await nextConnection.start();
    connection = nextConnection;
    return true;
  } catch (error) {
    console.error("SignalR connection error:", error);
    showErrorMessage("Real-time updates are unavailable");
    connection = null;
    return false;
  }
}

async function login() {
  const username = modalUserName.value.trim();
  const password = modalPassword.value.trim();

  if (!username || !password) {
    showModalError("Username and password are required");
    return;
  }

  let response;
  try {
    response = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        user: username,
        password: password,
      }),
    });

    if (!response.ok) {
      showModalError("Wrong username or password");
      return;
    }
  } catch (error) {
    console.error("Login error:", error);
    showModalError("Could not reach server");
    return;
  }

  const data = await response.json();
  const token = data.token ?? data.Token;
  if (!token) return;

  localStorage.setItem("jwt", token);
  localStorage.setItem("username", username);
  setCurrentUser(username);

  loginModal.style.display = "none";
  clearModalError();
  await startSignalR();
  await getFiles();
}

function logout() {
  clearErrorMessage();
  clearModalError();
  resetDetailsPanels();

  localStorage.removeItem("jwt");
  localStorage.removeItem("username");

  currentPath = "";
  currentFileName = "";
  activeHistoryFileName = "";

  if (connection) {
    connection.stop().catch((error) => {
      console.error("SignalR disconnect error:", error);
    });
    connection = null;
  }

  setExplorerPath();
  folderNameInput.value = "";
  fileInput.value = "";
  fileList.innerHTML = "";

  logoutBtn.style.display = "none";
  setCurrentUser("");

  loginModal.style.display = "flex";
  modalPassword.value = "";
  modalUserName.focus();
}

async function getFiles(path = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  currentPath = path || "";
  setExplorerPath(currentPath);
  logoutBtn.style.display = "inline-block";

  try {
    const encodedPath = toApiPath(path);
    const url = encodedPath ? `/api/files/${encodedPath}` : "/api/files";
    const response = await fetch(url, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

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

async function getFilesHistory(filename) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = currentPath ? `${currentPath}/${filename}` : filename;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/history/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      showErrorMessage("Failed to fetch file history");
      return;
    }

    const history = await response.json();
    activeHistoryEntries = history;
    historyCursor = -1;
    displayFileHistory(filename, history);
    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error fetching file history");
  }
}

async function restoreFileVersion(filename, version) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = currentPath ? `${currentPath}/${filename}` : filename;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(
      `/api/files/restore/${encodedFilePath}?version=${version}`,
      {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    if (!response.ok) {
      showErrorMessage("Failed to restore file version");
      return;
    }

    await showFileContent(filename, `Restored v${version}`);
    await getFiles(currentPath);
    await getFilesHistory(filename);
    showSuccessMessage(`File restored successfully (v${version})`);
  } catch (error) {
    showErrorMessage("Error restoring file version");
  }
}

async function showFileContent(fileName, contextLabel = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = currentPath ? `${currentPath}/${fileName}` : fileName;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      showErrorMessage("Failed to load file content");
      return;
    }

    const text = await response.text();
    currentFileName = fileName;

    setFileContentHeader(fileName, contextLabel);
    fileContentBody.textContent = text || "This file is empty.";
    fileContentTextarea.value = text || "";

    editBtn.style.display = "inline-block";
    saveBtn.style.display = "none";
    cancelBtn.style.display = "none";

    isEditMode = false;
    fileContentBody.style.display = "block";
    fileContentTextarea.style.display = "none";

    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error loading file content");
  }
}

async function saveFile() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  if (!currentFileName) return;

  const filePath = currentPath
    ? `${currentPath}/${currentFileName}`
    : currentFileName;
  const encodedFilePath = toApiPath(filePath);
  const content = fileContentTextarea.value;

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "text/plain",
      },
      body: content,
    });

    if (!response.ok) {
      showErrorMessage("Failed to save file");
      return;
    }

    fileContentBody.textContent = content || "This file is empty.";
    cancelEdit();

    if (activeHistoryFileName === currentFileName) {
      await getFilesHistory(currentFileName);
    }

    showSuccessMessage("File saved successfully");
  } catch (error) {
    showErrorMessage("Error saving file");
  }
}

async function showItemMetadata(itemName) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const itemPath = currentPath ? `${currentPath}/${itemName}` : itemName;
  const encodedItemPath = toApiPath(itemPath);

  try {
    const response = await fetch(`/api/files/${encodedItemPath}`, {
      method: "HEAD",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      showErrorMessage("Failed to fetch metadata");
      return;
    }

    const itemType = response.headers.get("X-Type") ?? "item";
    const metadata = {
      bytes: response.headers.get("X-Bytes") ?? "Unknown",
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

async function createFolder() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }
  const folderName = folderNameInput.value.trim();

  if (!folderName) {
    showErrorMessage("Folder name cannot be empty");
    return;
  }

  const path = currentPath ? `${currentPath}/${folderName}` : folderName;
  const encodedPath = toApiPath(path);

  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Type": "folder",
      },
    });

    if (!response.ok) {
      if (response.status === 409) {
        showErrorMessage("A folder with that name already exists");
      } else {
        showErrorMessage("Failed to create folder");
      }
      return;
    }

    folderNameInput.value = "";
    await getFiles(currentPath);
    showSuccessMessage(`Folder created: ${folderName}`);
  } catch (error) {
    console.error("Error creating folder:", error);
    showErrorMessage("Error creating folder");
  }
}

async function uploadFile() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }
  const file = fileInput.files[0];

  if (!file) {
    showErrorMessage("No file selected");
    return;
  }

  const path = currentPath ? `${currentPath}/${file.name}` : file.name;
  const encodedPath = toApiPath(path);

  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Type": "file",
      },
      body: file,
    });

    if (!response.ok) {
      if (response.status === 409) {
        showErrorMessage("A file with that name already exists");
      } else {
        showErrorMessage("Failed to upload file");
      }
      return;
    }

    fileInput.value = "";
    await getFiles(currentPath);
    showSuccessMessage(`File uploaded: ${file.name}`);
  } catch (error) {
    console.error("Error uploading file:", error);
    showErrorMessage("Error uploading file");
  }
}

async function downloadFile(file) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = currentPath ? `${currentPath}/${file}` : file;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

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

async function deleteItem(item) {
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

    if (!response.ok) {
      showErrorMessage(`Failed to delete item (${response.status})`);
      return;
    }

    resetDetailsPanels();
    await getFiles(currentPath);
    showSuccessMessage(`Deleted: ${item.split("/").pop()}`);
  } catch (error) {
    console.error("Error deleting item:", error);
    showErrorMessage("Error deleting item");
  }
}

function displayFiles(files) {
  fileList.innerHTML = "";

  if (currentPath) {
    const backItem = document.createElement("li");
    backItem.classList.add("folder");

    const backLabel = document.createElement("span");
    backLabel.classList.add("item-label");
    backLabel.textContent = "..";

    backItem.append(backLabel);
    backItem.style.cursor = "pointer";
    backItem.addEventListener("click", async () => {
      const pathParts = currentPath.split("/");
      if (pathParts.length > 0) pathParts.pop();

      currentPath = pathParts.join("/");
      await getFiles(currentPath);
      setFileContentHeader();
      fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
    });

    fileList.appendChild(backItem);
  }

  if (Object.keys(files).length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.textContent = "No files found";
    fileList.appendChild(emptyItem);
    return;
  }

  const sortedItems = Object.entries(files).sort((a, b) => {
    const aIsFile = a[1].file;
    const bIsFile = b[1].file;
    if (aIsFile !== bIsFile) return aIsFile ? 1 : -1;
    return a[0].localeCompare(b[0], "sv");
  });

  sortedItems.forEach(([name, metadata]) => {
    const listItem = document.createElement("li");
    const itemLabel = document.createElement("span");
    const buttonGroup = document.createElement("div");
    const downloadBtn = document.createElement("button");
    const delBtn = document.createElement("button");
    const historyBtn = document.createElement("button");
    const itemPath = currentPath ? `${currentPath}/${name}` : name;

    itemLabel.classList.add("item-label");
    buttonGroup.classList.add("item-actions");

    delBtn.textContent = "";
    delBtn.classList.add("icon-btn", "icon-btn-delete");
    delBtn.title = "Delete";
    delBtn.setAttribute("aria-label", "Delete");
    delBtn.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      showDeleteConfirmation(name, itemPath);
    });

    downloadBtn.textContent = "";
    downloadBtn.classList.add("icon-btn", "icon-btn-download");
    downloadBtn.title = "Download";
    downloadBtn.setAttribute("aria-label", "Download");
    downloadBtn.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();
      await downloadFile(name);
    });

    historyBtn.textContent = "";
    historyBtn.classList.add("icon-btn", "icon-btn-history");
    historyBtn.title = "View history";
    historyBtn.setAttribute("aria-label", "View file history");
    historyBtn.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();

      if (metadata.file) {
        await showItemMetadata(name);
        await showFileContent(name);
      }

      await getFilesHistory(name);
    });

    if (metadata.file) {
      listItem.classList.add("file");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        await showItemMetadata(name);
        await showFileContent(name);
      });
      buttonGroup.append(downloadBtn, delBtn, historyBtn);
    } else {
      listItem.classList.add("folder");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        await showItemMetadata(name);
        currentPath = currentPath ? `${currentPath}/${name}` : name;
        await getFiles(currentPath);
        setFileContentHeader();
        fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
      });
      buttonGroup.append(delBtn);
    }

    listItem.append(itemLabel, buttonGroup);
    fileList.appendChild(listItem);
  });
}

uploadBtn.addEventListener("click", uploadFile);
createFolderBtn.addEventListener("click", createFolder);
modalSubmit.addEventListener("click", login);
logoutBtn.addEventListener("click", logout);

modalUserName.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

modalPassword.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

setCurrentUser(localStorage.getItem("username") ?? "");
getFiles();

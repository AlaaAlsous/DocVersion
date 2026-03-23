"use strict";
const loginModal = document.getElementById("loginModal");
const modalUserName = document.getElementById("modalUserName");
const modalPassword = document.getElementById("modalPassword");
const modalSubmit = document.getElementById("modalSubmit");
const logoutBtn = document.getElementById("logoutBtn");
const modalError = document.getElementById("modalError");
const createFolderBtn = document.getElementById("createFolderBtn");
const folderNameInput = document.getElementById("folderName");
const uploadBtn = document.getElementById("uploadBtn");
const fileInput = document.getElementById("fileInput");
const errorMessage = document.getElementById("errorMessage");

let connection;
let currentPath = "";

function startSignalR() {
  const token = localStorage.getItem("jwt");
  if (!token) return;

  connection = new signalR.HubConnectionBuilder()
    .withUrl("api/events/signalr", { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .build();

  connection.on("Event", (type, path) => {
    switch (type) {
      case 0:
      case 1:
      case 2:
      case 5:
      case 7:
        if (path.startsWith(currentPath)) {
          getFiles(currentPath);
        }
        break;
      default:
        console.log("Unknown event type:", type);
    }
  });

  connection.start();
}

async function login() {
  const username = modalUserName.value.trim();
  const password = modalPassword.value.trim();

  if (!username || !password) {
    modalError.textContent = "Username and password are required";
    modalError.style.display = "block";
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
      modalError.textContent = "Wrong username or password";
      modalError.style.display = "block";
      return;
    }
  } catch (error) {
    console.error("Login error:", error);
    modalError.textContent = "Could not reach server";
    modalError.style.display = "block";
    return;
  }

  const data = await response.json();
  const token = data.token ?? data.Token;
  if (!token) {
    return;
  }
  localStorage.setItem("jwt", token);

  loginModal.style.display = "none";
  modalError.style.display = "none";
  startSignalR();
  await getFiles();
}

function toApiPath(path = "") {
  if (!path) return "";
  return path
    .split("/")
    .filter((segment) => segment.length > 0)
    .map((segment) => encodeURIComponent(segment))
    .join("/");
}

async function getFiles(path = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

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
      if (response.status === 401) {
        localStorage.removeItem("jwt");
        logout();
        return;
      }
      throw new Error("Failed to fetch files");
    }

    const files = await response.json();
    clearErrorMessage();
    displayFiles(files);
  } catch (error) {
    errorMessage.textContent = "Error fetching files";
    errorMessage.style.display = "block";
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
      if (response.status === 401) {
        localStorage.removeItem("jwt");
        logout();
        return;
      }
      throw new Error("Failed to download file");
    }

    const blob = await response.blob();
    const downloadUrl = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = file;
    document.body.appendChild(link);
    link.click();
    clearErrorMessage();
    link.remove();
    URL.revokeObjectURL(downloadUrl);
  } catch (error) {
    errorMessage.textContent = "Error downloading file";
    errorMessage.style.display = "block";
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
      if (response.status === 401) {
        localStorage.removeItem("jwt");
        logout();
        return;
      }
      throw new Error("Failed to fetch metadata");
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
    errorMessage.textContent = "Error fetching metadata";
    errorMessage.style.display = "block";
  }
}

function displayFiles(files) {
  const fileList = document.getElementById("file-list");
  fileList.innerHTML = "";

  if (currentPath) {
    const backItem = document.createElement("li");
    backItem.textContent = "🔙 Back";
    backItem.style.cursor = "pointer";
    backItem.addEventListener("click", async () => {
      const pathParts = currentPath.split("/");
      if (pathParts.length > 0) pathParts.pop();
      currentPath = pathParts.join("/");
      await getFiles(currentPath);
    });
    fileList.appendChild(backItem);
  }

  if (Object.keys(files).length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.textContent = "No files found";
    fileList.appendChild(emptyItem);
    return;
  }

  Object.entries(files).forEach(([name, metadata]) => {
    const listItem = document.createElement("li");
    const itemLabel = document.createElement("span");
    const buttonGroup = document.createElement("div");
    const metadataBtn = document.createElement("button");
    const delBtn = document.createElement("button");

    metadataBtn.textContent = "Info";
    metadataBtn.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      showItemMetadata(name);
    });

    delBtn.textContent = "Delete";
    delBtn.addEventListener("click", (event) => {
      const path = currentPath ? `${currentPath}/${name}` : name;
      event.preventDefault();
      event.stopPropagation();
      deleteItem(path);
    });

    if (metadata.file) {
      itemLabel.textContent = `📄 ${name} (${metadata.bytes} bytes)`;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", () => downloadFile(name));
    } else {
      itemLabel.textContent = `📁 ${name}`;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        currentPath = currentPath ? `${currentPath}/${name}` : name;
        await getFiles(currentPath);
      });
    }

    buttonGroup.append(metadataBtn, delBtn);
    listItem.append(itemLabel, buttonGroup);
    fileList.appendChild(listItem);
  });
}

function showMetadata(file, metadata) {
  let fileInfoBox = document.getElementById("file-info-box");
  if (!fileInfoBox) {
    fileInfoBox = document.createElement("div");
    fileInfoBox.id = "file-info-box";
    document.body.appendChild(fileInfoBox);
  }

  fileInfoBox.innerHTML = `
    <h3>Metadata</h3>
    <p><strong>Name:</strong> ${file}</p>
    <p><strong>Type:</strong> ${metadata.type}</p>
    <p><strong>Size:</strong> ${metadata.bytes} bytes</p>
    <p><strong>Created:</strong> ${metadata.created}</p>
    <p><strong>Modified:</strong> ${metadata.changed}</p>
    <p><strong>Extension:</strong> ${metadata.extension || "-"}</p>
    <button id="closeMetadataBtn">Close</button>
  `;
  fileInfoBox.style.display = "block";

  const closeBtn = document.getElementById("closeMetadataBtn");
  if (closeBtn) {
    closeBtn.addEventListener("click", () => {
      fileInfoBox.style.display = "none";
    });
  }
}

function logout() {
  localStorage.removeItem("jwt");
  currentPath = "";
  document.getElementById("file-list").innerHTML = "";
  logoutBtn.style.display = "none";
  loginModal.style.display = "flex";
  modalPassword.value = "";
  modalUserName.focus();
}

async function createFolder() {
  const token = localStorage.getItem("jwt");
  const folderName = folderNameInput.value.trim();
  if (!folderName) {
    errorMessage.textContent = "Folder name cannot be empty";
    errorMessage.style.display = "block";
    return;
  }
  const path = currentPath ? `${currentPath}/${folderName}` : folderName;
  const encodedPath = toApiPath(path);
  try {
    const respone = await fetch(`/api/files/${encodedPath}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Type": "folder",
      },
    });
    if (!respone.ok) {
      errorMessage.textContent = "Failed to create folder";
      errorMessage.style.display = "block";
      return;
    }
    folderNameInput.value = "";
    clearErrorMessage();
    await getFiles(currentPath);
  } catch (error) {
    console.error("Error creating folder:", error);
    errorMessage.textContent = "Error creating folder";
    errorMessage.style.display = "block";
  }
}

async function uploadFile() {
  const token = localStorage.getItem("jwt");
  const file = fileInput.files[0];
  if (!file) {
    errorMessage.textContent = "No file selected";
    errorMessage.style.display = "block";
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
      errorMessage.textContent = "Failed to upload file";
      errorMessage.style.display = "block";
      return;
    }
    fileInput.value = "";
    clearErrorMessage();
    await getFiles(currentPath);
  } catch (error) {
    console.error("Error uploading file:", error);
    errorMessage.textContent = "Error uploading file";
    errorMessage.style.display = "block";
  }
}

async function deleteItem(item) {
  const token = localStorage.getItem("jwt");
  if (!token) return;
  const encodedPath = toApiPath(item);
  try {
    const response = await fetch(`/api/files/${encodedPath}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });
    if (!response.ok) {
      errorMessage.textContent = `Failed to delete item (${response.status})`;
      errorMessage.style.display = "block";
      return;
    }
    await getFiles(currentPath);
    errorMessage.style.display = "none";
  } catch (error) {
    console.error("Error deleting item:", error);
    errorMessage.textContent = "Error deleting item";
    errorMessage.style.display = "block";
  }
}

function clearErrorMessage() {
  errorMessage.textContent = "";
  errorMessage.style.display = "none";
}

uploadBtn.addEventListener("click", uploadFile);

createFolderBtn.addEventListener("click", createFolder);

modalSubmit.addEventListener("click", login);

modalUserName.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

modalPassword.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

logoutBtn.addEventListener("click", logout);

getFiles();

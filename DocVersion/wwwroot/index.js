"use strict";
const loginModal = document.getElementById("loginModal");
const modalUserName = document.getElementById("modalUserName");
const modalPassword = document.getElementById("modalPassword");
const modalSubmit = document.getElementById("modalSubmit");
const logoutBtn = document.getElementById("logoutBtn");
const modalError = document.getElementById("modalError");

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
  await getFiles();
}

let currentPath = "";

async function getFiles(path = "") {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  logoutBtn.style.display = "inline-block";

  try {
    const url = path ? `/api/files/${path}` : "/api/files";
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
    displayFiles(files);
  } catch (error) {
    console.error("Error fetching files:", error);
  }
}

function displayFiles(files) {
  const fileList = document.getElementById("file-list");
  fileList.innerHTML = "";

  if (Object.keys(files).length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.textContent = "No files yet for this user.";
    fileList.appendChild(emptyItem);
    return;
  }

  Object.entries(files).forEach(([name, metadata]) => {
    const listItem = document.createElement("li");

    if (metadata.file) {
      listItem.textContent = `📄 ${name} (${metadata.bytes} bytes)`;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", () => downloadFile(name));
    } else {
      listItem.textContent = `📁 ${name}`;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        currentPath = currentPath ? `${currentPath}/${name}` : name;
        await getFiles(currentPath);
      });
    }
    fileList.appendChild(listItem);
  });
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
    fileList.insertBefore(backItem, fileList.firstChild);
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

modalSubmit.addEventListener("click", login);

modalUserName.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

modalPassword.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

logoutBtn.addEventListener("click", logout);

getFiles();

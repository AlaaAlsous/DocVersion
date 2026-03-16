"use strict";
const loginModal = document.getElementById("loginModal");
const modalUserName = document.getElementById("modalUserName");
const modalPassword = document.getElementById("modalPassword");
const modalSubmit = document.getElementById("modalSubmit");
const logoutBtn = document.getElementById("logoutBtn");

async function login() {
  const username = modalUserName.value.trim();
  const password = modalPassword.value.trim();

  if (!username || !password) {
    alert("Username and password are required");
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
      alert("Wrong username or password");
      return;
    }
  } catch (error) {
    console.error("Login error:", error);
    alert("Could not reach server");
    return;
  }

  const data = await response.json();
  const token = data.token ?? data.Token;
  if (!token) {
    alert("Login succeeded but no token was returned");
    return;
  }
  localStorage.setItem("jwt", token);

  loginModal.style.display = "none";
  await getFiles();
}

async function getFiles() {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logoutBtn.style.display = "none";
    loginModal.style.display = "block";
    return;
  }

  logoutBtn.style.display = "inline-block";

  try {
    const response = await fetch("/api/files", {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      if (response.status === 401) {
        localStorage.removeItem("jwt");
        logoutBtn.style.display = "none";
        loginModal.style.display = "block";
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
    listItem.textContent = `${name} (${metadata.bytes} bytes)`;
    fileList.appendChild(listItem);
  });
}

function logout() {
  localStorage.removeItem("jwt");
  document.getElementById("file-list").innerHTML = "";
  logoutBtn.style.display = "none";
  loginModal.style.display = "block";
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

import { dom, state, DEFAULT_PREVIEW_TEXT } from "./state";
import { clearErrorMessage, clearModalError, showModalError } from "./messages";
import {
  resetPreviewSurface,
  updateEditorActions,
  setFileContentHeader,
} from "./preview";
import { startSignalR } from "./signalr";
import { getFiles } from "./files";

export function handleUnauthorizedResponse(response: Response): boolean {
  if (response.status !== 401) return false;
  logout();
  return true;
}

export function setCurrentUser(username: string) {
  if (!username) {
    dom.currentUser.textContent = "";
    dom.currentUser.style.display = "none";
    return;
  }

  dom.currentUser.textContent = username;
  dom.currentUser.style.display = "inline-block";
}

export function setExplorerPath(path = "") {
  if (!dom.explorerPath) return;
  dom.explorerPath.textContent = path ? `/${path}` : "/";
}

export function resetDetailsPanels() {
  dom.historyBox.style.display = "none";
  dom.metadataBox.style.display = "none";

  setFileContentHeader();
  resetPreviewSurface();
  dom.fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
  dom.fileContentBody.style.display = "block";

  dom.fileContentTextarea.value = "";
  dom.fileContentTextarea.style.display = "none";

  state.currentFileIsEditable = false;
  updateEditorActions();

  state.currentFileName = "";
  state.activeHistoryFileName = "";
  state.activeHistoryEntries = [];
  state.historyCursor = -1;
  state.isEditMode = false;
}

export async function login() {
  const username = dom.modalUserName.value.trim();
  const password = dom.modalPassword.value.trim();

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

  dom.loginModal.style.display = "none";
  clearModalError();
  await startSignalR();
  await getFiles();
}

export function logout() {
  clearErrorMessage();
  clearModalError();
  resetDetailsPanels();

  localStorage.removeItem("jwt");
  localStorage.removeItem("username");

  state.currentPath = "";
  state.currentFileName = "";
  state.activeHistoryFileName = "";

  if (state.connection) {
    state.connection.stop().catch((error: any) => {
      console.error("SignalR disconnect error:", error);
    });
    state.connection = null;
  }

  setExplorerPath();
  dom.folderNameInput.value = "";
  dom.fileInput.value = "";
  dom.fileList.innerHTML = "";

  dom.logoutBtn.style.display = "none";
  setCurrentUser("");

  dom.loginModal.style.display = "flex";
  dom.modalPassword.value = "";
  dom.modalUserName.focus();
}

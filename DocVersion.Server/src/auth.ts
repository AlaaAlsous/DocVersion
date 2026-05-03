import { dom, state, DEFAULT_PREVIEW_TEXT } from "./state";
import { clearErrorMessage, clearModalError, showModalError } from "./messages";
import {
  resetPreviewSurface,
  updateEditorActions,
  setFileContentHeader,
} from "./preview";
import { startSignalR } from "./signalr";
import { getFiles } from "./files";

let refreshInFlight: Promise<string | null> | null = null;

function getTokenFromResponse(data: any): string | null {
  return data?.token ?? data?.Token ?? null;
}

function storeTokens(accessToken: string) {
  localStorage.setItem("jwt", accessToken);
}

function clearTokens() {
  localStorage.removeItem("jwt");
}

async function requestTokenRefresh(): Promise<string | null> {
  try {
    const response = await fetch("/api/login/refresh", {
      method: "POST",
      credentials: "same-origin",
    });

    if (!response.ok) {
      return null;
    }

    const data = await response.json();
    const nextAccessToken = getTokenFromResponse(data);
    if (!nextAccessToken) {
      return null;
    }

    storeTokens(nextAccessToken);
    return nextAccessToken;
  } catch (error) {
    console.error("Refresh token error:", error);
    return null;
  }
}

async function refreshAccessToken(): Promise<string | null> {
  if (refreshInFlight) {
    return refreshInFlight;
  }

  refreshInFlight = requestTokenRefresh().finally(() => {
    refreshInFlight = null;
  });

  return refreshInFlight;
}

function withBearerHeader(init: RequestInit, token: string): RequestInit {
  const headers = new Headers(init.headers ?? {});
  headers.set("Authorization", `Bearer ${token}`);
  return { ...init, headers };
}

export async function fetchWithAuth(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return new Response(null, { status: 401 });
  }

  const firstResponse = await fetch(input, withBearerHeader(init, token));
  if (firstResponse.status !== 401) {
    return firstResponse;
  }

  const refreshedToken = await refreshAccessToken();
  if (!refreshedToken) {
    logout();
    return firstResponse;
  }

  return fetch(input, withBearerHeader(init, refreshedToken));
}

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
  const email = dom.modalUserName.value.trim();
  const password = dom.modalPassword.value.trim();

  if (!email || !password) {
    showModalError("Email and password are required");
    return;
  }

  let response;
  try {
    response = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email,
        password: password,
      }),
    });

    if (!response.ok) {
      showModalError("Wrong email or password");
      return;
    }
  } catch (error) {
    console.error("Login error:", error);
    showModalError("Could not reach server");
    return;
  }

  const data = await response.json();
  const token = getTokenFromResponse(data);
  if (!token) {
    showModalError("Invalid login response");
    return;
  }

  storeTokens(token);
  localStorage.setItem("username", email);
  setCurrentUser(email);

  dom.loginModal.style.display = "none";
  clearModalError();
  await startSignalR();
  await getFiles();
}

export async function register() {
  const email = dom.modalUserName.value.trim();
  const password = dom.modalPassword.value.trim();

  if (!email || !password) {
    showModalError("Email and password are required");
    return;
  }

  let response: Response;
  try {
    response = await fetch("/api/login/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
  } catch (error) {
    console.error("Register error:", error);
    showModalError("Could not reach server");
    return;
  }

  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    showModalError(data.message ?? "Could not create account");
    return;
  }

  const data = await response.json();
  const token = getTokenFromResponse(data);
  if (!token) {
    showModalError("Invalid register response");
    return;
  }

  storeTokens(token);
  localStorage.setItem("username", email);
  setCurrentUser(email);

  dom.loginModal.style.display = "none";
  clearModalError();
  await startSignalR();
  await getFiles();
}

export function logout() {
  const token = localStorage.getItem("jwt");
  if (token) {
    void fetch("/api/login/logout", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      credentials: "same-origin",
    }).catch(() => {});
  }

  clearErrorMessage();
  clearModalError();
  resetDetailsPanels();

  clearTokens();
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

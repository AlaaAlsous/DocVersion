import { dom, state } from "./state";
import { toApiPath } from "./utils";
import {
  clearErrorMessage,
  showErrorMessage,
  showSuccessMessage,
} from "./messages";
import { renderFilePreview } from "./preview";
import { handleUnauthorizedResponse, logout } from "./auth";
import { showFileContent, getFiles } from "./files";

export function closeFileHistory() {
  state.activeHistoryFileName = "";
  state.activeHistoryEntries = [];
  state.historyCursor = -1;
  dom.historyBox.style.display = "none";
}

export function updateHistoryNavigationUi() {
  const statusEl = dom.historyBox.querySelector("#historyNavStatus");
  const backBtn = dom.historyBox.querySelector("#historyBackBtn");
  const forwardBtn = dom.historyBox.querySelector("#historyForwardBtn");

  if (!statusEl || !backBtn || !forwardBtn) return;

  const canGoOlder =
    state.historyCursor + 1 < state.activeHistoryEntries.length;
  const canGoNewer = state.historyCursor >= 0;

  statusEl.textContent =
    state.historyCursor === -1
      ? "Current version"
      : `Version ${state.activeHistoryEntries[state.historyCursor].version}`;

  (backBtn as any).disabled = !canGoOlder;
  (forwardBtn as any).disabled = !canGoNewer;
}

export async function showHistoryVersionContent(
  fileName: string,
  version: number,
) {
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
    const response = await fetch(
      `/api/files/history/${encodedFilePath}?version=${version}`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to load history version");
      return;
    }

    await renderFilePreview(response, fileName, `History v${version}`);

    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error loading history version");
  }
}

export async function navigateHistory(direction: number) {
  if (!state.activeHistoryFileName) return;
  if (state.isEditMode) return;

  if (direction < 0) {
    if (state.historyCursor + 1 >= state.activeHistoryEntries.length) return;
    state.historyCursor += 1;
    await showHistoryVersionContent(
      state.activeHistoryFileName,
      state.activeHistoryEntries[state.historyCursor].version,
    );
  } else if (direction > 0) {
    if (state.historyCursor < 0) return;

    if (state.historyCursor === 0) {
      state.historyCursor = -1;
      await showFileContent(state.activeHistoryFileName);
    } else {
      state.historyCursor -= 1;
      await showHistoryVersionContent(
        state.activeHistoryFileName,
        state.activeHistoryEntries[state.historyCursor].version,
      );
    }
  }

  updateHistoryNavigationUi();
}

export async function getFilesHistory(filename: string) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = state.currentPath
    ? `${state.currentPath}/${filename}`
    : filename;
  const encodedFilePath = toApiPath(filePath);

  try {
    const response = await fetch(`/api/files/history/${encodedFilePath}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      if (response.status === 404) {
        displayFileHistory(filename, []);
        clearErrorMessage();
        return;
      }
      showErrorMessage("Failed to fetch file history");
      return;
    }

    const history = await response.json();
    state.activeHistoryEntries = history;
    state.historyCursor = -1;
    displayFileHistory(filename, history);
    clearErrorMessage();
  } catch (error) {
    showErrorMessage("Error fetching file history");
  }
}

export async function restoreFileVersion(filename: string, version: number) {
  const token = localStorage.getItem("jwt");
  if (!token) {
    logout();
    return;
  }

  const filePath = state.currentPath
    ? `${state.currentPath}/${filename}`
    : filename;
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

    if (handleUnauthorizedResponse(response)) {
      return;
    }

    if (!response.ok) {
      showErrorMessage("Failed to restore file version");
      return;
    }

    await showFileContent(filename, `Restored v${version}`);
    await getFiles(state.currentPath);
    await getFilesHistory(filename);
    showSuccessMessage(`File restored successfully (v${version})`);
  } catch (error) {
    showErrorMessage("Error restoring file version");
  }
}

export function displayFileHistory(
  filename: string,
  history: { version: number; createdAt: string }[],
) {
  state.activeHistoryFileName = filename;
  state.activeHistoryEntries = history;
  state.historyCursor = -1;
  dom.historyBox.innerHTML = `
    <div class="history-header">
      <h3>History: ${filename}</h3>
      <button id="closeHistoryBtn" class="metadata-close-btn" type="button" aria-label="Close history">X</button>
    </div>
    <div class="history-nav" role="group" aria-label="History navigation">
      <button id="historyBackBtn" class="history-nav-btn" type="button">← Older</button>
      <span id="historyNavStatus" class="history-nav-status">Nuvarande version</span>
      <button id="historyForwardBtn" class="history-nav-btn" type="button">Newer →</button>
    </div>
    <ul class="history-list"></ul>
  `;

  const closeBtn = dom.historyBox.querySelector("#closeHistoryBtn");
  if (closeBtn) closeBtn.addEventListener("click", closeFileHistory);
  const backBtn = dom.historyBox.querySelector("#historyBackBtn");
  if (backBtn) backBtn.addEventListener("click", () => navigateHistory(-1));
  const forwardBtn = dom.historyBox.querySelector("#historyForwardBtn");
  if (forwardBtn)
    forwardBtn.addEventListener("click", () => navigateHistory(1));

  const list = dom.historyBox.querySelector(".history-list");
  if (list) {
    if (history.length === 0) {
      const li = document.createElement("li");
      li.className = "history-item";
      li.textContent = "No previous versions";
      list.appendChild(li);
    }
    history.forEach((h) => {
      const li = document.createElement("li");
      li.className = "history-item";

      const versionSpan = document.createElement("span");
      versionSpan.className = "history-version";
      versionSpan.textContent = `V.${h.version}`;

      const dateSpan = document.createElement("span");
      dateSpan.className = "history-date";
      dateSpan.textContent = new Date(h.createdAt).toLocaleString();

      const restoreBtn = document.createElement("button");
      restoreBtn.className = "history-restore-btn";
      restoreBtn.textContent = "Restore";
      restoreBtn.addEventListener("click", () =>
        restoreFileVersion(filename, h.version),
      );

      li.append(versionSpan, dateSpan, restoreBtn);
      list.appendChild(li);
    });
  }

  dom.historyBox.style.display = "block";
  updateHistoryNavigationUi();
}

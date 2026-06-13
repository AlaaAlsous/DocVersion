import { createFile } from "./files";
dom.createFileBtn.addEventListener("click", createFile);
import { dom, state, MESSAGE_TIMEOUT_MS, DEFAULT_PREVIEW_TEXT } from "./state";
import { openBinPanel, closeBinPanel } from "./bin";
import { toApiPath, getResponseContentType, isTextContentType } from "./utils";
import {
  clearErrorMessage,
  showErrorMessage,
  showSuccessMessage,
  clearModalError,
  showModalError,
} from "./messages";
import {
  revokePreviewObjectUrl,
  resetPreviewSurface,
  updateEditorActions,
  setFileContentHeader,
  showTextPreview,
  showBinaryPreviewMessage,
  showMediaPreview,
  showWordPreview,
  showPdfPreview,
  renderFilePreview,
} from "./preview";
import {
  fetchWithAuth,
  handleUnauthorizedResponse,
  submitAuthForm,
  toggleAuthMode,
  setCurrentUser,
  setExplorerPath,
  resetDetailsPanels,
  login,
  register,
  logout,
} from "./auth";
import {
  getFiles,
  showFileContent,
  saveFile,
  uploadFile,
  downloadFile,
  createFolder,
  deleteItem,
  showItemMetadata,
  showDeleteConfirmation,
  showMetadata,
  closeMetadata,
  editFile,
  cancelEdit,
} from "./files";
import {
  closeFileHistory,
  updateHistoryNavigationUi,
  showHistoryVersionContent,
  navigateHistory,
  getFilesHistory,
  restoreFileVersion,
  displayFileHistory,
} from "./history";
import { startSignalR } from "./signalr";
import { displayFiles } from "./display";
import { renderFileList } from "./display";

dom.uploadBtn.addEventListener("click", uploadFile);
dom.createFolderBtn.addEventListener("click", createFolder);
dom.modalSubmit.addEventListener("click", submitAuthForm);
dom.modalModeToggle.addEventListener("click", toggleAuthMode);
dom.logoutBtn.addEventListener("click", logout);

dom.editBtn.addEventListener("click", editFile);
dom.shareBtn.addEventListener("click", shareFile);
dom.saveBtn.addEventListener("click", saveFile);
dom.cancelBtn.addEventListener("click", cancelEdit);

dom.binBtn.addEventListener("click", openBinPanel);
dom.binCloseBtn.addEventListener("click", closeBinPanel);

dom.explorerSearchToggle.addEventListener("click", () => {
  state.searchVisible = !state.searchVisible;
  dom.explorerSearchWrap.style.display = state.searchVisible ? "block" : "none";
  dom.explorerSearchToggle.classList.toggle("active", state.searchVisible);
  if (state.searchVisible) {
    dom.explorerSearchInput.focus();
  } else {
    state.searchQuery = "";
    dom.explorerSearchInput.value = "";
    renderFileList();
  }
});

dom.explorerSearchInput.addEventListener("input", () => {
  state.searchQuery = dom.explorerSearchInput.value;
  renderFileList();
});

dom.fileInput.addEventListener("change", function () {
  if (dom.fileInput.files && dom.fileInput.files.length > 0) {
    dom.fileInputName.textContent = dom.fileInput.files[0].name;
  } else {
    dom.fileInputName.textContent = "";
  }
});

if (dom.folderNameInput && dom.createFolderBtn) {
  dom.folderNameInput.addEventListener("keydown", (event: KeyboardEvent) => {
    if (event.key === "Enter") {
      event.preventDefault();
      dom.createFolderBtn.click();
    }
  });
}

if (dom.fileNameInput && dom.createFileBtn) {
  dom.fileNameInput.addEventListener("keydown", (event: KeyboardEvent) => {
    if (event.key === "Enter") {
      event.preventDefault();
      dom.createFileBtn.click();
    }
  });
}

document.addEventListener("keydown", (event) => {
  if (
    state.isEditMode &&
    (event.ctrlKey || event.metaKey) &&
    event.key.toLowerCase() === "s"
  ) {
    event.preventDefault();
    dom.saveBtn.click();
    return;
  }

  if (!state.activeHistoryFileName || dom.historyBox.style.display === "none")
    return;
  if (state.isEditMode) return;

  const key = event.key.toLowerCase();
  const isOlderShortcut = event.ctrlKey && !event.shiftKey && key === "z";
  const isNewerShortcut =
    (event.ctrlKey && !event.shiftKey && key === "y") ||
    (event.ctrlKey && event.shiftKey && key === "z");

  if (isOlderShortcut) {
    event.preventDefault();
    void navigateHistory(-1);
    return;
  }

  if (isNewerShortcut) {
    event.preventDefault();
    void navigateHistory(1);
  }
});

dom.modalUserName.addEventListener("keydown", (event: KeyboardEvent) => {
  if (event.key === "Enter") submitAuthForm();
});

dom.modalPassword.addEventListener("keydown", (event: KeyboardEvent) => {
  if (event.key === "Enter") submitAuthForm();
});

window.addEventListener("storage", (event) => {
  if (event.key === "jwt") {
    if (!event.newValue) {
      logout();
    } else {
      startSignalR();
    }
  }
  if (event.key === "username") {
    setCurrentUser(event.newValue ?? "");
  }
});

export async function shareFile() {
  if (!state.currentFileName) return;

  showSpinner();

  try {
    const filePath = state.currentPath
      ? `${state.currentPath}/${state.currentFileName}`
      : state.currentFileName;

    const res = await fetchWithAuth("/api/files/share", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ filePath }),
    });

    if (!res.ok) {
      showErrorMessage("Could not create share link");
      hideSpinner();
      return;
    }

    const { url } = await res.json();

    if (navigator.share) {
      try {
        await navigator.share({
          title: state.currentFileName,
          text: "Shared file",
          url,
        });
        hideSpinner();
        return;
      } catch (err: any) {
        if (err.name === "AbortError") {
          hideSpinner();
          return;
        }
      }
    }

    await navigator.clipboard.writeText(url);
    showSuccessMessage("Share link copied to clipboard");
    hideSpinner();

  } catch (error) {
    hideSpinner();
    showErrorMessage("Error sharing file");
  }
}

export function showSpinner() {
  const spinner = document.getElementById("spinner-overlay");
  if (spinner) spinner.style.display = "flex";
}

export function hideSpinner() {
  const spinner = document.getElementById("spinner-overlay");
  if (spinner) spinner.style.display = "none";
}

setCurrentUser(localStorage.getItem("username") ?? "");
startSignalR();
getFiles();

export {
  editFile,
  saveFile,
  cancelEdit,
  restoreFileVersion,
  closeMetadata,
  closeFileHistory,
  showItemMetadata,
  deleteItem,
  showFileContent,
  getFiles,
  getFilesHistory,
  createFolder,
  uploadFile,
  downloadFile,
  displayFiles,
  setCurrentUser,
  submitAuthForm,
  toggleAuthMode,
  login,
  register,
  logout,
  startSignalR,
  showSuccessMessage,
  showErrorMessage,
  showModalError,
  clearErrorMessage,
  clearModalError,
  setExplorerPath,
  setFileContentHeader,
  resetDetailsPanels,
  revokePreviewObjectUrl,
  updateEditorActions,
  showTextPreview,
  showBinaryPreviewMessage,
  showMediaPreview,
  showWordPreview,
  showPdfPreview,
  getResponseContentType,
  isTextContentType,
  renderFilePreview,
  toApiPath,
  handleUnauthorizedResponse,
  showDeleteConfirmation,
  showMetadata,
  displayFileHistory,
  updateHistoryNavigationUi,
  showHistoryVersionContent,
  MESSAGE_TIMEOUT_MS,
  DEFAULT_PREVIEW_TEXT,
};
export { state, dom };
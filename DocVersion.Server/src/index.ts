import { createFile } from "./files";
dom.createFileBtn.addEventListener("click", createFile);
import { dom, state, MESSAGE_TIMEOUT_MS, DEFAULT_PREVIEW_TEXT } from "./state";
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
  handleUnauthorizedResponse,
  setCurrentUser,
  setExplorerPath,
  resetDetailsPanels,
  login,
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

dom.uploadBtn.addEventListener("click", uploadFile);
dom.createFolderBtn.addEventListener("click", createFolder);
dom.modalSubmit.addEventListener("click", login);
dom.logoutBtn.addEventListener("click", logout);

dom.editBtn.addEventListener("click", editFile);
dom.saveBtn.addEventListener("click", saveFile);
dom.cancelBtn.addEventListener("click", cancelEdit);

dom.fileInput.addEventListener("change", function () {
  if (dom.fileInput.files && dom.fileInput.files.length > 0) {
    dom.fileInputName.textContent = dom.fileInput.files[0].name;
  } else {
    dom.fileInputName.textContent = "";
  }
});

document.addEventListener("keydown", (event) => {
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
  if (event.key === "Enter") login();
});

dom.modalPassword.addEventListener("keydown", (event: KeyboardEvent) => {
  if (event.key === "Enter") login();
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
  login,
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

export const dom = {
  loginModal: document.getElementById("loginModal") as any,
  modalUserName: document.getElementById("modalUserName") as any,
  modalPassword: document.getElementById("modalPassword") as any,
  modalSubmit: document.getElementById("modalSubmit") as any,
  logoutBtn: document.getElementById("logoutBtn") as any,
  currentUser: document.getElementById("currentUser") as any,
  modalError: document.getElementById("modalError") as any,
  createFolderBtn: document.getElementById("createFolderBtn") as any,
  folderNameInput: document.getElementById("folderName") as any,
  uploadBtn: document.getElementById("uploadBtn") as any,
  fileInput: document.getElementById("fileInput") as any,
  fileInputName: document.getElementById("fileInputName") as any,
  errorMessage: document.getElementById("errorMessage") as any,
  explorerPath: document.getElementById("explorerPath") as any,
  fileList: document.getElementById("file-list") as any,
  fileContentTitle: document.getElementById("fileContentTitle") as any,
  fileContentBody: document.getElementById("fileContentBody") as any,
  fileContentPath: document.getElementById("fileContentPath") as any,
  fileContentTextarea: document.getElementById("fileContentTextarea") as any,
  editBtn: document.getElementById("editBtn") as any,
  saveBtn: document.getElementById("saveBtn") as any,
  cancelBtn: document.getElementById("cancelBtn") as any,
  metadataBox: document.getElementById("file-info-box") as any,
  historyBox: document.getElementById("file-history-box") as any,
};

export const state = {
  connection: null as any,
  currentPath: "",
  currentFileName: "",
  activeHistoryFileName: "",
  activeHistoryEntries: [] as any[],
  historyCursor: -1,
  isEditMode: false,
  currentFileIsEditable: false,
  errorMessageTimeoutId: null as any,
  modalErrorTimeoutId: null as any,
  activePreviewObjectUrl: null as string | null,
};

export const MESSAGE_TIMEOUT_MS = 5000;
export const DEFAULT_PREVIEW_TEXT = "Select a file to preview its content.";

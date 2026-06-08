import { dom, state, DEFAULT_PREVIEW_TEXT } from "./state";
import {
  setFileContentHeader,
  resetPreviewSurface,
  updateEditorActions,
} from "./preview";
import {
  getFiles,
  showFileContent,
  showItemMetadata,
  downloadFile,
  downloadFolderAsZip,
  showDeleteConfirmation,
} from "./files";
import { getFilesHistory } from "./history";
import { showSpinner, hideSpinner } from "./index";

export function displayFiles(files: Record<string, { file: boolean }>) {
  state.allFiles = files;
  renderFileList();
}

function getSearchQuery(): string {
  return state.searchQuery.toLowerCase().trim();
}

function matchesQuery(name: string, query: string): boolean {
  if (!query) return true;
  return name.toLowerCase().includes(query);
}

export function renderFileList() {
  dom.fileList.innerHTML = "";

  const files = state.allFiles;
  const query = getSearchQuery();

  const fileNames = Object.keys(files);
  const currentFilePath =
    state.currentPath && state.currentFileName
      ? `${state.currentPath}/${state.currentFileName}`
      : state.currentFileName;
  const currentHistoryPath =
    state.currentPath && state.activeHistoryFileName
      ? `${state.currentPath}/${state.activeHistoryFileName}`
      : state.activeHistoryFileName;
  if (state.currentFileName && !fileNames.includes(state.currentFileName)) {
    if (dom.fileContentTitle) dom.fileContentTitle.textContent = "";
    if (dom.fileContentBody) dom.fileContentBody.textContent = "";
    if (dom.fileContentTextarea) dom.fileContentTextarea.value = "";
    state.currentFileName = "";
  }
  if (
    state.activeHistoryFileName &&
    !fileNames.includes(state.activeHistoryFileName)
  ) {
    if (dom.historyBox) dom.historyBox.style.display = "none";
    state.activeHistoryFileName = "";
    state.activeHistoryEntries = [];
    state.historyCursor = -1;
  }
  if (dom.metadataBox && state.currentFileName === "") {
    dom.metadataBox.style.display = "none";
  }

  function setActiveItem(li: HTMLElement) {
    dom.fileList
      .querySelectorAll("li.active")
      .forEach((el: HTMLElement) => el.classList.remove("active"));
    li.classList.add("active");
  }

  if (state.currentPath) {
    const backItem = document.createElement("li");
    backItem.classList.add("folder");

    const backLabel = document.createElement("span");
    backLabel.classList.add("item-label");
    backLabel.textContent = "..";

    backItem.append(backLabel);
    backItem.style.cursor = "pointer";
    backItem.addEventListener("click", async () => {
      showSpinner();
      const pathParts = state.currentPath.split("/");
      if (pathParts.length > 0) pathParts.pop();

      state.currentPath = pathParts.join("/");
      await getFiles(state.currentPath);
      setFileContentHeader();
      resetPreviewSurface();
      dom.fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
      dom.fileContentTextarea.style.display = "none";
      state.currentFileName = "";
      state.currentFileIsEditable = false;
      updateEditorActions();
      hideSpinner();
    });

    dom.fileList.appendChild(backItem);
  }

  const filteredEntries = Object.entries(files).filter(([name]) =>
    matchesQuery(name, query),
  );

  if (filteredEntries.length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.textContent = query ? "No results found" : "No files found";
    dom.fileList.appendChild(emptyItem);
    return;
  }

  const sortedItems = filteredEntries.sort(
    (a: [string, { file: boolean }], b: [string, { file: boolean }]) => {
      const aIsFile = a[1].file;
      const bIsFile = b[1].file;
      if (aIsFile !== bIsFile) return aIsFile ? 1 : -1;
      return a[0].localeCompare(b[0], "sv");
    },
  );

  sortedItems.forEach(([name, metadata]: [string, { file: boolean }]) => {
    const listItem = document.createElement("li");
    const itemLabel = document.createElement("span");
    const buttonGroup = document.createElement("div");
    const downloadBtn = document.createElement("button");
    const delBtn = document.createElement("button");
    const historyBtn = document.createElement("button");
    const renameBtn = document.createElement("button");
    const metaBtn = document.createElement("button");
    const itemPath = state.currentPath ? `${state.currentPath}/${name}` : name;

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

    historyBtn.textContent = "";
    historyBtn.classList.add("icon-btn", "icon-btn-history");
    historyBtn.title = "View history";
    historyBtn.setAttribute("aria-label", "View file history");
    historyBtn.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();
      showSpinner();
      if (metadata.file) {
        await showItemMetadata(name);
        await showFileContent(name);
      }
      await getFilesHistory(name);
      hideSpinner();
    });

    if (metadata.file) {
      downloadBtn.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        showSpinner();
        await downloadFile(name);
        hideSpinner();
      });
    } else {
      downloadBtn.addEventListener("click", async (event) => {
        event.preventDefault();
        event.stopPropagation();
        showSpinner();
        await downloadFolderAsZip(name);
        hideSpinner();
      });
    }

    renameBtn.textContent = "";
    renameBtn.classList.add("icon-btn", "icon-btn-rename");
    renameBtn.title = "Rename";
    renameBtn.setAttribute("aria-label", "Rename");
    renameBtn.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();

      if (listItem.querySelector("input.rename-input")) return;
      const currentName = name;
      const isFolder = !metadata.file;
      const input = document.createElement("input");
      input.type = "text";
      input.value = currentName;
      input.className = "rename-input";
      input.setAttribute("maxlength", "255");
      itemLabel.textContent = "";
      itemLabel.appendChild(input);
      input.focus();
      input.select();
      input.addEventListener("click", (e) => e.stopPropagation());
      input.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
          input.blur();
        } else if (e.key === "Escape") {
          itemLabel.textContent = currentName;
        }
      });
      input.addEventListener("blur", () => {
        const newName = input.value.trim();
        if (newName && newName !== currentName) {
          const oldPath = state.currentPath
            ? `${state.currentPath}/${currentName}`
            : currentName;
          const newPath = state.currentPath
            ? `${state.currentPath}/${newName}`
            : newName;
          import("./files").then((m) =>
            m.renameItem(oldPath, newPath, isFolder, currentName, newName),
          );
        }
        itemLabel.textContent =
          newName && newName !== currentName ? newName : currentName;
      });
    });

    metaBtn.textContent = "";
    metaBtn.classList.add("icon-btn", "icon-btn-metadata");
    metaBtn.title = "Show metadata";
    metaBtn.setAttribute("aria-label", "Show metadata");
    metaBtn.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      showItemMetadata(name);
    });

    if (metadata.file) {
      listItem.classList.add("file");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        showSpinner();
        setActiveItem(listItem);
        await showItemMetadata(name);
        await showFileContent(name);
        if (dom.historyBox.style.display !== "none") {
          await getFilesHistory(name);
        }
        hideSpinner();
      });
      buttonGroup.append(downloadBtn, historyBtn, renameBtn, delBtn);
    } else {
      listItem.classList.add("folder");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.style.background = "rgba(188, 153, 56, 0.10)";
      listItem.addEventListener("click", async () => {
        showSpinner();
        setActiveItem(listItem);
        await showItemMetadata(name);
        state.currentPath = state.currentPath
          ? `${state.currentPath}/${name}`
          : name;
        await getFiles(state.currentPath);
        setFileContentHeader();
        resetPreviewSurface();
        dom.fileContentBody.textContent = DEFAULT_PREVIEW_TEXT;
        dom.fileContentTextarea.style.display = "none";
        state.currentFileName = "";
        state.currentFileIsEditable = false;
        updateEditorActions();
        hideSpinner();
      });
      buttonGroup.append(downloadBtn, metaBtn, renameBtn, delBtn);
    }

    listItem.append(itemLabel, buttonGroup);
    dom.fileList.appendChild(listItem);
  });
}

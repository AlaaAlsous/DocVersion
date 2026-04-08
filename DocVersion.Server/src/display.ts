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
  showDeleteConfirmation,
} from "./files";
import { getFilesHistory } from "./history";

export function displayFiles(files: Record<string, { file: boolean }>) {
  dom.fileList.innerHTML = "";

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
    });

    dom.fileList.appendChild(backItem);
  }

  if (Object.keys(files).length === 0) {
    const emptyItem = document.createElement("li");
    emptyItem.textContent = "No files found";
    dom.fileList.appendChild(emptyItem);
    return;
  }

  const sortedItems = Object.entries(files).sort(
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
    downloadBtn.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();
      await downloadFile(name);
    });

    historyBtn.textContent = "";
    historyBtn.classList.add("icon-btn", "icon-btn-history");
    historyBtn.title = "View history";
    historyBtn.setAttribute("aria-label", "View file history");
    historyBtn.addEventListener("click", async (event) => {
      event.preventDefault();
      event.stopPropagation();

      if (metadata.file) {
        await showItemMetadata(name);
        await showFileContent(name);
      }

      await getFilesHistory(name);
    });

    if (metadata.file) {
      listItem.classList.add("file");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
        setActiveItem(listItem);
        await showItemMetadata(name);
        await showFileContent(name);
        if (dom.historyBox.style.display !== "none") {
          await getFilesHistory(name);
        }
      });
      buttonGroup.append(downloadBtn, delBtn, historyBtn);
    } else {
      listItem.classList.add("folder");
      itemLabel.textContent = name;
      listItem.style.cursor = "pointer";
      listItem.addEventListener("click", async () => {
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
      });
      buttonGroup.append(delBtn);
    }

    listItem.append(itemLabel, buttonGroup);
    dom.fileList.appendChild(listItem);
  });
}

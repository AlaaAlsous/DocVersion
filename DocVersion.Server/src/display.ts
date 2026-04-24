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
    const renameBtn = document.createElement("button");
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
      input.style.width = "100%";
      input.style.minWidth = "0";
      input.style.maxWidth = "100%";
      input.style.boxSizing = "border-box";
      input.style.overflow = "hidden";
      input.style.textOverflow = "ellipsis";
      input.style.marginLeft = "4px";
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
      buttonGroup.append(downloadBtn, delBtn, historyBtn, renameBtn);
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
      buttonGroup.append(delBtn, renameBtn);
    }

    listItem.append(itemLabel, buttonGroup);
    dom.fileList.appendChild(listItem);
  });
}

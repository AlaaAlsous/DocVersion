import { dom, state } from "./state";
import {
  clearErrorMessage,
  showErrorMessage,
  showSuccessMessage,
  showConfirmationPrompt,
} from "./messages";
import { fetchWithAuth, handleUnauthorizedResponse } from "./auth";
import { getFiles } from "./files";
import { showSpinner, hideSpinner } from "./index";
import { formatBytes } from "./utils";

export interface BinItemData {
  id: number;
  username: string;
  originalPath: string;
  storagePath: string;
  isFile: boolean;
  sizeBytes: number;
  deletedAt: string;
  expiresAt: string;
}

export function closeBinPanel() {
  dom.binPanel.style.display = "none";
}

export async function openBinPanel() {
  showSpinner();
  try {
    const response = await fetchWithAuth("/api/files/bin");

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to fetch bin items");
      return;
    }

    const items: BinItemData[] = await response.json();
    displayBinItems(items);
    dom.binPanel.style.display = "block";
    clearErrorMessage();
    hideSpinner();
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error fetching bin items");
  }
}

function displayBinItems(items: BinItemData[]) {
  dom.binItemsList.innerHTML = "";

  if (items.length === 0) {
    dom.binItemsList.innerHTML =
      '<li class="bin-empty">No items in the bin</li>';
    hideEmptyBinButton();
    return;
  }

  showEmptyBinButton();

  items.forEach((item) => {
    const li = document.createElement("li");
    li.className = "bin-item";
    li.setAttribute("data-id", item.id.toString());

    const nameSpan = document.createElement("span");
    nameSpan.className = "bin-item-name";
    nameSpan.textContent = item.isFile
      ? `📄 ${item.originalPath}`
      : `📁 ${item.originalPath}`;

    const sizeSpan = document.createElement("span");
    sizeSpan.className = "bin-item-size";
    sizeSpan.textContent = formatBytes(item.sizeBytes);

    const dateSpan = document.createElement("span");
    dateSpan.className = "bin-item-date";
    const deletedDate = new Date(item.deletedAt);
    const expiresDate = new Date(item.expiresAt);
    const daysLeft = Math.max(
      0,
      Math.ceil((expiresDate.getTime() - Date.now()) / (1000 * 60 * 60 * 24)),
    );
    dateSpan.textContent = `Deleted ${deletedDate.toLocaleDateString()} (${daysLeft}d left)`;

    const actions = document.createElement("div");
    actions.className = "bin-item-actions";

    const restoreBtn = document.createElement("button");
    restoreBtn.className = "bin-restore-btn icon-btn";
    restoreBtn.setAttribute("aria-label", "Restore");
    restoreBtn.title = "Restore to original location";
    restoreBtn.addEventListener("click", async (e) => {
      e.stopPropagation();
      await restoreBinItem(item.id);
    });

    const deleteBtn = document.createElement("button");
    deleteBtn.className = "bin-delete-btn icon-btn";
    deleteBtn.setAttribute("aria-label", "Delete permanently");
    deleteBtn.title = "Permanently delete";
    deleteBtn.addEventListener("click", (e) => {
      e.stopPropagation();
      showConfirmationPrompt(
        `Permanently delete "${item.originalPath}"? This cannot be undone.`,
        async () => {
          await permanentlyDeleteBinItem(item.id);
        },
      );
    });

    actions.append(restoreBtn, deleteBtn);
    li.append(nameSpan, sizeSpan, dateSpan, actions);
    dom.binItemsList.appendChild(li);
  });
}

export async function restoreBinItem(binItemId: number) {
  showSpinner();
  try {
    const response = await fetchWithAuth(
      `/api/files/bin/restore/${binItemId}`,
      {
        method: "POST",
      },
    );

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      const data = await response.json().catch(() => ({}));
      showErrorMessage(data?.message || "Could not restore item");
      return;
    }

    await openBinPanel();
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage("Item restored from bin");
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error restoring item");
  }
}

export async function emptyBin() {
  showSpinner();
  try {
    const response = await fetchWithAuth("/api/files/bin/empty", {
      method: "DELETE",
    });

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to empty bin");
      return;
    }

    dom.binItemsList.innerHTML =
      '<li class="bin-empty">No items in the bin</li>';
    hideEmptyBinButton();
    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage("Bin emptied");
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error emptying bin");
  }
}

function showEmptyBinButton() {
  const existing = document.getElementById("emptyBinBtn");
  if (existing) return;
  const btn = document.createElement("button");
  btn.id = "emptyBinBtn";
  btn.className = "bin-empty-btn";
  btn.textContent = "Empty Bin";
  btn.addEventListener("click", () => {
    showConfirmationPrompt(
      "Empty the entire bin? This cannot be undone.",
      async () => {
        await emptyBin();
      },
    );
  });
  const closeBtn = dom.binPanel.querySelector(".bin-close-btn");
  if (closeBtn && closeBtn.parentNode) {
    closeBtn.parentNode.insertBefore(btn, closeBtn);
  } else {
    dom.binPanel.querySelector(".bin-header")?.appendChild(btn);
  }
}

function hideEmptyBinButton() {
  const btn = document.getElementById("emptyBinBtn");
  if (btn) btn.remove();
}

export async function permanentlyDeleteBinItem(binItemId: number) {
  showSpinner();
  try {
    const response = await fetchWithAuth(
      `/api/files/bin/permanent/${binItemId}`,
      {
        method: "DELETE",
      },
    );

    if (handleUnauthorizedResponse(response)) {
      hideSpinner();
      return;
    }

    if (!response.ok) {
      hideSpinner();
      showErrorMessage("Failed to permanently delete item");
      return;
    }

    const binItemLi = dom.binItemsList.querySelector(
      `li.bin-item[data-id="${binItemId}"]`,
    );
    if (binItemLi) {
      binItemLi.remove();
    }

    if (dom.binItemsList.children.length === 0) {
      dom.binItemsList.innerHTML =
        '<li class="bin-empty">No items in the bin</li>';
      hideEmptyBinButton();
    }

    await getFiles(state.currentPath);
    hideSpinner();
    showSuccessMessage("Item permanently deleted");
  } catch (error) {
    hideSpinner();
    showErrorMessage("Error deleting item");
  }
}

import { dom, state, MESSAGE_TIMEOUT_MS } from "./state";

export function clearModalError() {
  if (state.modalErrorTimeoutId) {
    clearTimeout(state.modalErrorTimeoutId);
    state.modalErrorTimeoutId = null;
  }
  dom.modalError.textContent = "";
  dom.modalError.classList.add("hidden");
}

export function showModalError(message: string) {
  clearModalError();
  dom.modalError.textContent = message;
  dom.modalError.classList.remove("hidden");
  state.modalErrorTimeoutId = setTimeout(() => {
    clearModalError();
  }, MESSAGE_TIMEOUT_MS);
}

export function clearErrorMessage() {
  if (state.errorMessageTimeoutId) {
    clearTimeout(state.errorMessageTimeoutId);
    state.errorMessageTimeoutId = null;
  }
  dom.errorMessage.innerHTML = "";
  dom.errorMessage.classList.add("hidden");
  dom.errorMessage.style.color = "";
  dom.errorMessage.style.backgroundColor = "";
  dom.errorMessage.style.borderColor = "";
}

export function showErrorMessage(message: string) {
  if (state.errorMessageTimeoutId) {
    clearTimeout(state.errorMessageTimeoutId);
    state.errorMessageTimeoutId = null;
  }
  dom.errorMessage.innerHTML = "";
  const span = document.createElement("span");
  span.textContent = message;
  dom.errorMessage.appendChild(span);
  dom.errorMessage.style.color = "var(--gh-danger)";
  dom.errorMessage.style.backgroundColor = "var(--gh-danger-bg)";
  dom.errorMessage.style.borderColor = "var(--gh-danger-border)";
  dom.errorMessage.classList.remove("hidden");
  state.errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

export function showConfirmationPrompt(
  message: string,
  onConfirm: () => Promise<void>
) {
  clearErrorMessage();
  dom.errorMessage.classList.remove("hidden");
  dom.errorMessage.textContent = "";
  dom.errorMessage.style.color = "#b38600";
  dom.errorMessage.style.backgroundColor = "#fef3ba44";
  dom.errorMessage.style.borderColor = "#b38600";

  const question = document.createElement("span");
  question.textContent = message;
  dom.errorMessage.appendChild(question);

  const actions = document.createElement("div");
  actions.className = "confirm-actions";

  const yesBtn = document.createElement("button");
  yesBtn.type = "button";
  yesBtn.className = "confirm-yes-btn";
  yesBtn.textContent = "Yes";
  yesBtn.addEventListener("click", async () => {
    clearErrorMessage();
    await onConfirm();
  });

  const noBtn = document.createElement("button");
  noBtn.type = "button";
  noBtn.className = "confirm-no-btn";
  noBtn.textContent = "No";
  noBtn.addEventListener("click", () => {
    clearErrorMessage();
  });

  actions.append(yesBtn, noBtn);
  dom.errorMessage.appendChild(actions);
}

export function showSuccessMessage(message: string) {
  if (state.errorMessageTimeoutId) {
    clearTimeout(state.errorMessageTimeoutId);
    state.errorMessageTimeoutId = null;
  }
  dom.errorMessage.innerHTML = "";
  const span = document.createElement("span");
  span.textContent = message;
  dom.errorMessage.appendChild(span);
  dom.errorMessage.style.color = "var(--gh-success-text)";
  dom.errorMessage.style.backgroundColor = "var(--gh-success-bg)";
  dom.errorMessage.style.borderColor = "var(--gh-success-border)";
  dom.errorMessage.classList.remove("hidden");
  state.errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

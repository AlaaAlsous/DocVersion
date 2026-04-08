import { dom, state, MESSAGE_TIMEOUT_MS } from "./state";

export function clearModalError() {
  if (state.modalErrorTimeoutId) {
    clearTimeout(state.modalErrorTimeoutId);
    state.modalErrorTimeoutId = null;
  }
  dom.modalError.textContent = "";
  dom.modalError.style.display = "none";
}

export function showModalError(message: string) {
  clearModalError();
  dom.modalError.textContent = message;
  dom.modalError.style.display = "block";
  state.modalErrorTimeoutId = setTimeout(() => {
    clearModalError();
  }, MESSAGE_TIMEOUT_MS);
}

export function clearErrorMessage() {
  if (state.errorMessageTimeoutId) {
    clearTimeout(state.errorMessageTimeoutId);
    state.errorMessageTimeoutId = null;
  }

  dom.errorMessage.textContent = "";
  dom.errorMessage.style.display = "none";
  dom.errorMessage.style.color = "";
  dom.errorMessage.style.backgroundColor = "";
  dom.errorMessage.style.borderColor = "";
}

export function showErrorMessage(message: string) {
  clearErrorMessage();
  dom.errorMessage.textContent = message;
  dom.errorMessage.style.display = "block";
  state.errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

export function showSuccessMessage(message: string) {
  clearErrorMessage();
  dom.errorMessage.textContent = message;
  dom.errorMessage.style.color = "var(--gh-success-text)";
  dom.errorMessage.style.backgroundColor = "var(--gh-success-bg)";
  dom.errorMessage.style.borderColor = "var(--gh-success-border)";
  dom.errorMessage.style.display = "block";
  state.errorMessageTimeoutId = setTimeout(() => {
    clearErrorMessage();
  }, MESSAGE_TIMEOUT_MS);
}

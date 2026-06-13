import { state, dom } from "./state";
import { showErrorMessage } from "./messages";
import { getFiles, showFileContent } from "./files";
import { getFilesHistory } from "./history";

export async function startSignalR() {
  const token = localStorage.getItem("jwt");
  if (!token) return false;

  const shouldRefreshCurrentPath = (path: string) => {
    if (!path) return false;
    if (!state.currentPath) return !path.includes("/");
    return (
      path === state.currentPath || path.startsWith(`${state.currentPath}/`)
    );
  };

  if (state.connection) {
    try {
      await state.connection.stop();
    } catch (error) {
      console.error("SignalR reconnect cleanup error:", error);
    }
  }

  const nextConnection = new (window as any).signalR.HubConnectionBuilder()
    .withUrl("/api/events/signalr", {
      accessTokenFactory: () => localStorage.getItem("jwt") ?? "",
    })
    .withAutomaticReconnect()
    .build();

  nextConnection.on("Event", (type: number, path: string) => {
    const getFileNameFromPath = (p: string) => {
      const parts = p.split("/");
      return parts[parts.length - 1];
    };
    const getParentPath = (p: string) => {
      const parts = p.split("/");
      parts.pop();
      return parts.join("/");
    };

    switch (type) {
      case 0:
      case 1:
        if (shouldRefreshCurrentPath(path)) {
          void getFiles(state.currentPath);
        }
        {
          const fileName = getFileNameFromPath(path);
          const parentPath = getParentPath(path);
          if (
            parentPath === state.currentPath &&
            fileName === state.currentFileName
          ) {
            void showFileContent(fileName);
          }
          if (
            state.activeHistoryFileName &&
            parentPath === state.currentPath &&
            fileName === state.activeHistoryFileName
          ) {
            void getFilesHistory(state.activeHistoryFileName);
          }
        }
        break;
      case 2:
      case 5:
      case 7:
      case 8:
      case 9:
      case 10:
      case 11:
        if (shouldRefreshCurrentPath(path)) {
          void getFiles(state.currentPath);
        }
        if (dom.binPanel && dom.binPanel.style.display !== "none") {
          import("./bin").then((m) => m.openBinPanel());
        }
        break;
      default:
        console.log("Unknown event type:", type);
    }
  });

  try {
    await nextConnection.start();
    state.connection = nextConnection;
    return true;
  } catch (error) {
    console.error("SignalR connection error:", error);
    showErrorMessage("Real-time updates are unavailable");
    state.connection = null;
    return false;
  }
}

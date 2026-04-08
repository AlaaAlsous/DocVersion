export function formatBytes(bytes: number): string {
  if (bytes < 1024) return bytes + " B";
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + " KB";
  if (bytes < 1024 * 1024 * 1024)
    return (bytes / (1024 * 1024)).toFixed(2) + " MB";
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + " GB";
}

export function toApiPath(path = "") {
  if (!path) return "";
  return path
    .split("/")
    .filter((segment) => segment.length > 0)
    .map((segment) => encodeURIComponent(segment))
    .join("/");
}

export function getResponseContentType(response: Response): string {
  const contentType: string | null = response.headers.get("Content-Type");

  if (contentType === null) {
    return "";
  }

  const index = contentType.indexOf(";");
  const mainType = index === -1 ? contentType : contentType.substring(0, index);
  return mainType.trim().toLowerCase();
}

export function isTextContentType(contentType: string): boolean {
  if (!contentType) return false;
  if (contentType.startsWith("text/")) return true;

  return (
    contentType === "application/json" ||
    contentType === "application/xml" ||
    contentType === "application/javascript" ||
    contentType === "application/x-javascript" ||
    contentType.endsWith("+json") ||
    contentType.endsWith("+xml")
  );
}

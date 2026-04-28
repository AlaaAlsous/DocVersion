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

  const textTypes = [
    "application/json",
    "application/xml",
    "application/javascript",
    "application/x-javascript",
    "application/x-www-form-urlencoded",
    "application/x-sh",
    "application/x-csh",
    "application/x-python",
    "application/x-perl",
    "application/x-php",
    "application/x-ruby",
    "application/x-shellscript",
    "application/x-sql",
    "application/x-yaml",
    "application/x-markdown",
    "application/x-latex",
    "application/x-tex",
    "application/x-troff",
    "application/x-c",
    "application/x-c++",
    "application/x-java",
    "application/x-go",
    "application/x-typescript",
    "application/x-scss",
    "application/x-css",
    "application/x-sass",
    "application/x-less",
    "application/x-sql",
    "application/x-csv",
    "application/csv",
    "application/x-bash",
    "application/x-powershell",
    "application/x-ini",
    "application/x-toml",
    "application/x-properties",
    "application/x-log",
    "application/x-config",
    "application/x-env",
    "application/x-groovy",
    "application/x-kotlin",
    "application/x-swift",
    "application/x-rust",
    "application/x-haskell",
    "application/x-erlang",
    "application/x-elixir",
    "application/x-scala",
    "application/x-clojure",
    "application/x-fsharp",
    "application/x-vbscript",
    "application/x-vba",
    "application/x-pascal",
    "application/x-fortran",
    "application/x-assembly",
    "application/x-matlab",
    "application/x-octave",
    "application/x-r",
    "application/x-stata",
    "application/x-sas",
    "application/x-dockerfile",
    "application/x-batch",
    "application/x-cmake",
    "application/x-makefile",
    "application/x-git",
    "application/x-diff",
    "application/x-patch",
    "application/x-yaml",
    "application/x-toml",
    "application/x-ini",
    "application/x-properties",
    "application/x-md",
    "application/x-markdown",
    "application/x-log",
    "application/x-config",
    "application/x-env",
    "application/x-csv",
    "application/csv",
    "application/x-sql",
    "application/x-lua",
    "application/x-sh",
    "application/x-shellscript",
    "application/x-bash",
    "application/x-powershell",
    "application/x-perl",
    "application/x-python",
    "application/x-php",
    "application/x-ruby",
    "application/x-csharp",
    "application/x-cs",
    "application/x-typescript",
    "application/x-javascript",
    "application/x-json",
    "application/x-yaml",
    "application/x-toml",
    "application/x-ini",
    "application/x-properties",
    "application/x-md",
    "application/x-markdown",
    "application/x-log",
    "application/x-config",
    "application/x-env",
    "application/x-csv",
    "application/csv",
    "application/x-sql",
  ];
  if (textTypes.includes(contentType)) return true;
  if (contentType.endsWith("+json") || contentType.endsWith("+xml"))
    return true;

  return false;
}

# Installation

## 1. Installera Node-beroenden (TypeScript, esbuild, Sass)

```bash
npm install
```

## 2. Bygg JavaScript (bundlar alla TS-filer till en index.js)

```bash
npm run build
```

## 3. Bygg CSS

```bash
npx sass DocVersion.Server/src/styles.scss DocVersion.Server/wwwroot/css/styles.css --no-source-map
```

## 4. Starta servern

```bash
cd DocVersion.Server
dotnet run
```

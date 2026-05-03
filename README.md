# DocVersion

![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-5C2D91?logo=dotnet&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/EF%20Core-68217A?logo=dotnet&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?logo=typescript&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?logo=dotnet&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-000000?logo=jsonwebtokens&logoColor=white)
![Sass](https://img.shields.io/badge/Sass-CC6699?logo=sass&logoColor=white)
![Monaco Editor](https://img.shields.io/badge/Monaco%20Editor-007ACC?logo=visualstudiocode&logoColor=white)
![esbuild](https://img.shields.io/badge/esbuild-FFCF00?logo=esbuild&logoColor=black)
![Node.js](https://img.shields.io/badge/Node.js-43853D?logo=node.js&logoColor=white)
![NPM](https://img.shields.io/badge/NPM-CB3837?logo=npm&logoColor=white)
![PowerShell](https://img.shields.io/badge/PowerShell-5391FE?logo=powershell&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

<p align="center">
	<img src="DocVersion.Server/wwwroot/Assets/Favicon.png" alt="DocVersion" width="90"/>
</p>

<p align="center">
	<img src="DocVersion.Server/wwwroot/Assets/History.png" alt="History" width="180"/>
	<img src="DocVersion.Server/wwwroot/Assets/MetaData.png" alt="Metadata" width="180"/>
</p>

## Beskrivning

DocVersion är en fullstack-applikation för säker dokument- och filhantering med versionshistorik per användare.

Backend är byggd i ASP.NET Core med EF Core och SQLite. Frontend är en TypeScript-klient som bundlas med esbuild och körs som statiska filer från serverns wwwroot.

Systemet innehåller:

- Inloggning och registrering med e-post och hashade lösenord
- JWT access token i klienten och refresh token i HttpOnly-cookie
- Automatisk token-refresh i klienten vid 401
- Fil- och mapphantering med upload, download, rename, delete och zip
- Versionshistorik med visning och återställning av tidigare versioner
- Preview för text, bild, video, ljud, PDF och Word
- Realtidsuppdateringar via SignalR
- Lokal Monaco Editor för edit-läge utan CDN

## Arkitektur

Projektet består av tre delar:

- DocVersion.Server: API, auth, SignalR, statiska filer och körning
- DocVersion.Core: delade modeller och helpers
- DocVersion.Client: CLI-klient för pull, push och sync mot servern

### Server

- ASP.NET Core controllers för auth och filer
- EF Core DbContext med UserAccounts och FileHistories
- JWT-baserad autentisering och authorization
- Rate limiter för auth endpoints
- SignalR-hub för användarspecifika event

### Web-frontend

- TypeScript-moduler under DocVersion.Server/src
- Bundling till DocVersion.Server/wwwroot/js/index.js med esbuild
- SCSS till DocVersion.Server/wwwroot/css/styles.css
- Monaco laddas lokalt från DocVersion.Server/wwwroot/js/vendor/monaco/vs

### Lagring och versionering

- Filinnehåll per användare i DocVersion.Server/Storage
- Historikfiler i DocVersion.Server/Storage/.history
- Metadata om versioner i tabellen FileHistories

## Komplett funktionell översikt

### Auth-funktioner

- Login med e-post och lösenord
- Register med e-postvalidering och minimilängd för lösenord
- Refresh endpoint med token type-kontroll och refresh-version-kontroll
- Logout som revokerar refresh genom versioninkrement
- Modal i UI med växling mellan Sign In och Create Account

### Filfunktioner

- Lista filer och mappar
- Visa filinnehåll eller mappinnehåll
- Hämta metadata via HEAD
- Skapa fil
- Skapa mapp
- Spara fil (inklusive tom fil)
- Ladda upp fil
- Ladda upp mapp
- Ladda ner fil
- Ladda ner mapp som zip
- Byta namn på fil eller mapp
- Ta bort fil eller mapp

### Versionsfunktioner

- Skapa ny version när filinnehåll ändras
- Lista versioner
- Öppna specifik historikversion
- Navigera historik med tangentbord
- Återställ vald version

### Preview-funktioner

- Textpreview med radnummer
- Bildpreview
- Videopreview
- Ljudpreview
- PDF-preview
- Word-preview
- Binary fallback-meddelande för ej previewbara typer

### Realtidsfunktioner

- SignalR-anslutning med access token
- Automatiskt återanslutningsstöd
- Eventhantering per användare
- UI-uppdatering av explorer, preview och history efter events

## API-endpoints

### Auth: api/login

- POST /api/login
- POST /api/login/register
- POST /api/login/refresh
- POST /api/login/logout

### Filer: api/files

- GET /api/files
- GET /api/files/{path}
- HEAD /api/files/{path}
- POST /api/files/{path}
- PUT /api/files/{path}
- DELETE /api/files/{path}
- POST /api/files/rename
- GET /api/files/zip/{folder}
- POST /api/files/upload-folder

### Historik

- GET /api/files/history/{path}
- GET /api/files/history/{path}?version={n}
- POST /api/files/restore/{path}?version={n}

### SignalR

- Hub: /api/events/signalr

## DocVersion.Client: användning och sync-flöde

DocVersion.Client är ett kommandoradsverktyg för synk mellan lokal mapp och servern.

Kommandoformat:

- DocVersion.Client pull <serverUrl> [email] [password]
- DocVersion.Client push <serverUrl> [email] [password]
- DocVersion.Client sync <serverUrl> [email] [password]

Beteende:

- pull: laddar ner serverns filer/mappar och speglar lokalt
- push: laddar upp lokala filer/mappar till servern
- sync: kombinerar pull och push-flöden med event-baserad synklogik
- login sker om email och password skickas med
- X-Type headers används för korrekt fil eller mapphantering

Exempel:

```powershell
cd DocVersion.Client
dotnet run -- pull http://localhost:3000 user@example.com MyPass123
dotnet run -- push http://localhost:3000 user@example.com MyPass123
dotnet run -- sync http://localhost:3000 user@example.com MyPass123
```

## Frontend build och sync-funktioner

Projektet använder en separat sync-funktion för Monaco-filer.

NPM-scripts:

- npm run sync:monaco
- npm run build
- npm run build:check
- npm run dev-css

Vad sync:monaco gör:

- Källa: node_modules/monaco-editor/min/vs
- Mål: DocVersion.Server/wwwroot/js/vendor/monaco/vs
- Tar bort tidigare mål, skapar mapp och kopierar om allt

Byggordning i npm run build:

1. npm run sync:monaco
2. esbuild bundlar TypeScript till index.js

Detta säkerställer att Monaco alltid finns lokalt och att ingen CDN krävs.

## Projektstruktur

```text
DocVersion/
├─ DocVersion.sln
├─ package.json
├─ package-lock.json
├─ README.md
├─ scripts/
│  └─ sync-monaco.mjs
├─ DocVersion.Core/
│  ├─ Helpers/
│  └─ Models/
├─ DocVersion.Client/
│  └─ Program.cs
└─ DocVersion.Server/
	 ├─ Program.cs
	 ├─ appsettings.json
	 ├─ appsettings.Development.json
	 ├─ Controllers/
	 │  ├─ LoginController.cs
	 │  └─ FilesController.cs
	 ├─ Data/
	 │  └─ AppDbContext.cs
	 ├─ Hubs/
	 │  └─ EventHub.cs
	 ├─ Models/
	 │  ├─ UserAccount.cs
	 │  └─ FileHistory.cs
	 ├─ Security/
	 │  ├─ JwtOptions.cs
	 │  └─ JwtService.cs
	 ├─ Services/
	 │  └─ FileService.cs
	 ├─ src/
	 │  ├─ auth.ts
	 │  ├─ display.ts
	 │  ├─ files.ts
	 │  ├─ history.ts
	 │  ├─ index.ts
	 │  ├─ messages.ts
	 │  ├─ preview.ts
	 │  ├─ signalr.ts
	 │  ├─ state.ts
	 │  ├─ styles.scss
	 │  └─ utils.ts
	 └─ wwwroot/
			├─ index.html
			├─ css/
			├─ js/
			└─ Assets/
```

## Lokal utveckling

Krav:

- .NET SDK 10
- Node.js och npm

Installera beroenden:

```powershell
npm install
```

Bygg frontend:

```powershell
npm run build
```

Bygg CSS:

```powershell
npx sass DocVersion.Server/src/styles.scss DocVersion.Server/wwwroot/css/styles.css --no-source-map
```

Bygg backend:

```powershell
dotnet build DocVersion.Server/DocVersion.Server.csproj -c Release
```

Kör server:

```powershell
cd DocVersion.Server
dotnet run
```

Server URL:

- http://localhost:3000/

## Konfiguration

JWT-nyckel hämtas i denna ordning:

1. Miljövariabel JWT_KEY
2. Jwt:Key i appsettings

Rekommendation:

- Sätt JWT_KEY i miljön i stället för att använda dev-nycklar i appsettings.

## Säkerhet

- PasswordHasher för hash och verify
- JWT med validering av issuer, audience, lifetime och signing key
- Refresh token i HttpOnly-cookie med SameSite=Strict
- RefreshTokenVersion för revocation vid logout
- Rate limiter för auth
- Eventflöde scoped per användare i SignalR

## English Summary

DocVersion is a .NET 10 and TypeScript fullstack file management system with JWT auth, refresh cookies, file version history, local Monaco integration, SignalR real-time updates, and a CLI client for pull, push, and sync.

## Licens

MIT

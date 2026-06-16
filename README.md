# DocVersion

![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-5C2D91?logo=dotnet&logoColor=white)
![Azure](https://img.shields.io/badge/Azure_App_Service-0078D4?logo=microsoftazure&logoColor=white)
![Blob Storage](https://img.shields.io/badge/Azure%20Blob%20Storage-0089D6?logo=microsoftazure&logoColor=white)
![Azure Key Vault](https://img.shields.io/badge/Azure%20Key%20Vault-0078D4?logo=microsoftazure&logoColor=white)
![Azure SQL Database](https://img.shields.io/badge/Azure%20SQL%20Database-CC2927?logo=microsoftsqlserver&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?logo=dotnet&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-000000?logo=jsonwebtokens&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/EF%20Core-68217A?logo=dotnet&logoColor=white)
![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?logo=typescript&logoColor=white)
![Sass](https://img.shields.io/badge/Sass-CC6699?logo=sass&logoColor=white)
![esbuild](https://img.shields.io/badge/esbuild-FFCF00?logo=esbuild&logoColor=black)
![Node.js](https://img.shields.io/badge/Node.js-43853D?logo=node.js&logoColor=white)
![NPM](https://img.shields.io/badge/NPM-CB3837?logo=npm&logoColor=white)
![Monaco Editor](https://img.shields.io/badge/Monaco%20Editor-007ACC?logo=visualstudiocode&logoColor=white)
![PowerShell](https://img.shields.io/badge/PowerShell-5391FE?logo=powershell&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

<p align="center">
	<img src="DocVersion.Server/wwwroot/Assets/DocVersion-login.png" alt="DocVersion-Login" width="250"/>
</p>

<p align="center">
	<img src="DocVersion.Server/wwwroot/Assets/DocVersion-startsida.png" alt="DocVersion-Startsida" width="600"/>
</p>

<p align="center">
	<img src="DocVersion.Server/wwwroot/Assets/DocVersion-Sync.png" alt="DocVersion-Sync" width="600"/>
</p>

## Beskrivning

DocVersion är en komplett lösning för filhantering och versionshistorik. Systemet låter användare lagra, redigera och hantera dokument och filer på ett säkert sätt, där varje ändring automatiskt sparas som en ny version.

**Funktioner:** Ladda upp och ladda ner filer och mappar samt redigera filer direkt i webbläsaren. Skapa, hantera, byta namn på och ta bort filer och mappar. Borttagna filer hamnar i papperskorgen med 30 dagars ångerrätt — återställ eller radera permanent. Dela filer via länk. Visa tidigare versioner och återställ dem. Förhandsgranska textfiler, bilder, video, ljud, PDF, Excel och Word-dokument. Sök bland filer och se metadata för filer och mappar. Allt med stöd för mörkt läge och responsiv design anpassad för både desktop och mobil.

**Säkerhet:** Varje användare loggar in med e-post och lösenord. Du ser bara dina egna filer. Lösenord är hashade, sessioner förblir aktiva med automatisk tokenuppdatering, och inloggningar kan avslutas för att ta bort åtkomst. Rate limiting skyddar mot brute force-attacker.

**Realtid och synkronisering:** Alla ändringar uppdateras direkt för aktiva användare via SignalR. Systemet återansluter automatiskt vid anslutningsbrott. Ett kommandoradsverktyg (CLI) låter dig synkronisera mappar och filer mellan din dator och servern i realtid (pull, push, sync).

**Teknik:** Backend är byggd med ASP.NET Core, Entity Framework Core och Azure SQL Database. Filhantering sker via Azure Blob Storage. Applikationen körs i Azure App Service. Hemligheter och konfiguration hanteras via Azure Key Vault. Frontend är en webbapplikation med TypeScript. Inbyggd texteditor (Monaco) är integrerad lokalt. Autentisering använder JWT med access token och refresh token i HttpOnly-cookie. Realtidsuppdateringar sker via SignalR.

---

## Arkitektur

Projektet består av tre delar:

- **DocVersion.Server**: Webbservern som hanterar API, autentisering, filer och realtidskommunikation
- **DocVersion.Core**: Gemensam kod och hjälpfunktioner
- **DocVersion.Client**: CLI-verktyg för synkronisering mellan lokal dator och server

### Azure-baserad arkitektur

Systemet använder följande Azure-tjänster:

- **Azure App Service** → Hosting av backend API och webbläsargränssnitt
- **Azure SQL Database** → Databas för användare, metadata och versionshistorik
- **Azure Blob Storage** → Lagring av filer och filversioner
- **Azure Key Vault** → Säker lagring av connection strings, JWT-nycklar och secrets

---

### Server

- API för inloggning, filhantering och versioner
- Azure SQL Database via Entity Framework Core
- Filuppladdning och hämtning via Azure Blob Storage
- JWT-baserad autentisering
- SignalR för realtidsuppdateringar
- Säkerhetslager med rate limiting och token-revocation

---

### Webbläsargränssnitt

- Skrivet i TypeScript
- Bundlas till en enda JavaScript-fil
- SCSS för styling
- Monaco Editor hostas lokalt från servern
- Ingen extern CDN-användning

---

### Lagring och versionering

- Metadata lagras i Azure SQL Database
- Filer och versioner lagras i Azure Blob Storage
- Varje användares filer separeras logiskt i containers/folders
- Versionshistorik kopplas via databasen

---

## Komplett funktionell översikt

### Användarhantering

- Logga in med e-post och lösenord
- Registrera konto med validering
- Refresh token-flöde (automatisk tokenuppdatering)
- Logout och token-invalidation (RefreshTokenVersion)
- Användar-avatar med initialer i topbaren

### Filutforskare (Explorer)

- Lista filer och mappar — sortering: mappar först, alfabetisk
- Navigera in i mappar och tillbaka med `..`
- Sökfunktion med inline-sökfält (togglas via förstoringsglaset)
- Brödsmulestig (explorer path) visar aktuell mapp
- Långa filnamn trunkeras med `…` — knapprader hålls på en rad
- Active state-markering med blå vänsterkant

### Fil- och mapphantering

- Skapa ny fil
- Skapa ny mapp
- Ladda upp fil
- Ladda upp mapp (inkl. undermappar)
- Ladda ner enskild fil
- Ladda ner mapp som ZIP
- Byta namn på fil eller mapp
- Ta bort fil eller mapp (flyttas till papperskorgen)

### Papperskorg (Recycle Bin)

- Panel med bin-ikon i explorer-headern
- Lista alla borttagna filer/mappar med storlek, datum och dagar kvar
- **Återställ** enstaka fil/mapp till ursprunglig plats
- **Radera permanent** enstaka fil/mapp med bekräftelsedialog
- **Töm papperskorgen** — knapp i panelens footer, med Yes/No-bekräftelse
- Automatisk tömning av objekt äldre än 30 dagar (bakgrundstjänst)
- Realtidsuppdatering: bin-listan refreshas direkt när något raderas från explorern
- Empty Bin-knappen döljs automatiskt när korgen är tom

### Versionshistorik

- Skapa ny version automatiskt när filinnehåll ändras (SHA-256 jämförelse)
- Lista alla versioner med nummer och tidpunkt
- Öppna specifik historikversion för preview
- Navigera historik med tangentbord (`Ctrl+Z` för äldre, `Ctrl+Y` för nyare)
- Återställ vald version (blir nuvarande)

### Preview (Förhandsgranskning)

- Textfiler med radnummer och Monaco Editor-stöd
- Bildvisning (PNG, JPG, GIF, SVG, WebP)
- Videouppspelning (MP4, WebM)
- Ljuduppspelning (MP3, WAV, OGG)
- PDF-visning inbäddad
- Word (.docx)-visning
- Excel (.xlsx)-visning med tabellformat
- Binary fallback-meddelande för ej previewbara filtyper

### Metadata-panel

- Visa fil-/mappinformation: Namn, Typ, Storlek, Skapad, Ändrad, Extension

### Delning (Share)

- Skapa delningslänk för en fil
- Kopiera till urklipp eller dela via Web Share API
- Publik åtkomst via `/api/shared/{token}` utan inloggning

### Realtidsfunktioner (SignalR)

- Alla filoperationer broadcastas i realtid
- Automatisk reconnect vid anslutningsbrott
- User-scoped events (endast egna filer)
- UI sync vid filändringar, borttagningar och återställningar
- File content header + Share-knapp rensas automatiskt vid delete

### UI/UX-detaljer

- Alla paneler (explorer, history, metadata, bin) har konsekvent header-styling
- Close-knappar (X) är gemensamma pill-formade knappar med hover-effekt
- Ikon-buttons för Delete, Download, History, Rename, Metadata, Search, Share, Bin
- Smooth scrolling på alla scrollbara element
- Dark mode via `prefers-color-scheme: dark`
- Spinner overlay vid API-anrop
- Fel-/success-meddelanden med auto-dismiss
- Bekräftelsedialoger (Yes/No) för destruktiva operationer
- Responsiv design anpassad för desktop och mobil

---

## API-endpoints

### Auth: api/login

- `POST /api/login`
- `POST /api/login/register`
- `POST /api/login/refresh`
- `POST /api/login/logout`

### Filer: api/files

- `GET /api/files`
- `GET /api/files/{path}`
- `HEAD /api/files/{path}`
- `POST /api/files/{path}`
- `PUT /api/files/{path}`
- `DELETE /api/files/{path}`
- `POST /api/files/rename`
- `GET /api/files/zip/{folder}`
- `POST /api/files/upload-folder`

### Share

- `POST /api/files/share`
- `GET /api/shared/{token}`

### Papperskorg: api/files/bin

- `GET /api/files/bin`
- `POST /api/files/bin/restore/{binItemId}`
- `DELETE /api/files/bin/permanent/{binItemId}`
- `DELETE /api/files/bin/empty`

### Historik

- `GET /api/files/history/{path}`
- `GET /api/files/history/{path}?version={n}`
- `POST /api/files/restore/{path}?version={n}`

### SignalR

- Hub: `/api/events/signalr`

---

## DocVersion.Client: Synkroniseringssverktyg

DocVersion.Client är ett verktyg du kör från kommandoraden (terminal) för att automatiskt synkronisera mappar och filer mellan din dator och servern.

### Kommandon

| Kommando | Beskrivning                                                  |
| -------- | ------------------------------------------------------------ |
| `pull`   | Laddar ner serverns filer/mappar lokalt                      |
| `push`   | Laddar upp lokala filer/mappar till servern                  |
| `sync`   | Kombinerar pull + push med event-baserad synklogik (SignalR) |

### Användning

1. Bygg programmet som en körbar fil:

   ```powershell
   dotnet publish -c Release --self-contained true -p:PublishSingleFile=true
   ```

2. Gå till `Release` mappen i projektet och kopiera den skapade filen till en valfri mapp.

3. Lägg till den mappen i din miljövariabel (PATH) så att du kan köra kommandot från var som helst.
4. Öppna terminalen i mappen du vill synkronisera
5. Kör:
   ```powershell
   DocVersion.Client pull <serverUrl> [email] [password]
   DocVersion.Client push <serverUrl> [email] [password]
   DocVersion.Client sync <serverUrl> [email] [password]
   ```

---

## Projektstruktur

```text
DocVersion/
├─ DocVersion.sln
├─ package.json
├─ package-lock.json
├─ .gitignore
├─ README.md
├─ DocVersion.Core/
│  ├─ Helpers/
│  └─ Models/
│     └─ EventsType.cs
├─ DocVersion.Client/
│  └─ Program.cs
└─ DocVersion.Server/
   ├─ Program.cs
   ├─ appsettings.json
   ├─ appsettings.Development.json
   ├─ tsconfig.json
   ├─ web.config
   ├─ Deploy.ps1
   ├─ Controllers/
   │  ├─ LoginController.cs
   │  ├─ FilesController.cs
   │  └─ SharedController.cs
   ├─ Data/
   │  └─ AppDbContext.cs
   ├─ Hubs/
   │  └─ EventsHub.cs
   ├─ Migrations/
   ├─ Models/
   │  ├─ UserAccount.cs
   │  ├─ FileHistory.cs
   │  ├─ ShareLink.cs
   │  └─ BinItem.cs
   ├─ Security/
   │  ├─ JwtOptions.cs
   │  ├─ JwtService.cs
   │  └─ NameUserIdProvider.cs
   ├─ Services/
   │  ├─ FileService.cs
   │  ├─ BlobStorageService.cs
   │  └─ BinCleanupService.cs
   ├─ src/
   │  ├─ auth.ts
   │  ├─ bin.ts
   │  ├─ display.ts
   │  ├─ files.ts
   │  ├─ history.ts
   │  ├─ index.ts
   │  ├─ messages.ts
   │  ├─ preview.ts
   │  ├─ signalr.ts
   │  ├─ state.ts
   │  ├─ utils.ts
   │  └─ styles.scss
   └─ wwwroot/
      ├─ index.html
      ├─ css/
      ├─ js/
      │  └─ vendor/
      │     └─ scripts/
      │        └─ sync-monaco.mjs
      └─ Assets/
```

---

## Lokal utveckling

### Krav

- .NET SDK 10
- Node.js och npm
- Azure resurser (eller emulatorer lokalt)

### Starta

```powershell
npm install
npm run build
dotnet run --project DocVersion.Server
```

Eller kör allt i watch-läge:

```powershell
npm install
npm run dev
```

---

## Deployment

Automatisk deploy med PowerShell:

```powershell
.\DocVersion.Server\Deploy.ps1
```

Skriptet:

1. Bygger frontend (SCSS + JS + Monaco)
2. Publicerar backend (`dotnet publish -c Release`)
3. Zippar publish-mappen
4. Deployar till Azure via `az webapp deployment source config-zip`

---

## Konfiguration

Servern läser hemligheter från Azure Key Vault (`KeyVaultUrl` i `appsettings.json`):

- `SqlConnectionString` — databasanslutning
- `Jwt-Key` — JWT-signeringsnyckel
- `AzureBlob-ConnectionString` — blob-lagring

Lokalt (development) används `appsettings.Development.json` med inbäddad JWT-nyckel.

---

## Säkerhet

- PasswordHasher för hash och verify
- JWT med validering av issuer, audience, lifetime och signing key
- Refresh token i HttpOnly-cookie med SameSite=Strict
- RefreshTokenVersion för revocation vid logout
- Rate limiter för auth (10 requests/minut)
- Eventflöde scoped per användare i SignalR
- Entity Framework migrationer körs automatiskt vid startup

---

## English Summary

DocVersion is a cloud-based document management system that provides file storage with full version history. It allows users to upload, edit, download, and organize files and folders through a web interface. Every change is automatically saved as a new version, making it possible to restore previous states at any time.

The system is secure and uses authentication with login and password. It also supports real-time updates so that all changes are instantly reflected for active users.

The backend is hosted on Azure App Service and uses Azure SQL Database for storing metadata and version history. Files are stored in Azure Blob Storage, and sensitive configuration such as connection strings and security keys are managed securely using Azure Key Vault.

## Utvecklare

**Alaa Alsous**

# DocVersion

![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-5C2D91?logo=dotnet&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?logo=dotnet&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-000000?logo=jsonwebtokens&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/EF%20Core-68217A?logo=dotnet&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)
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

**Funktioner:** Ladda upp och ladda ner filer och mappar samt redigera filer direkt i webbläsaren. Skapa, hantera, byta namn på och ta bort filer och mappar. Visa tidigare versioner och återställ dem. Förhandsgranska textfiler, bilder, video, ljud, PDF- och Word-dokument samt se ändringshistorik med möjlighet att navigera mellan versioner.

**Säkerhet:** Varje användare loggar in med e-post och lösenord. Du ser bara dina egna filer. Lösenord är hashade, sessioner förblir aktiva med automatisk tokenuppdatering, och inloggningar kan avslutas för att ta bort åtkomst.

**Realtid och synkronisering:** Alla ändringar uppdateras direkt för aktiva användare. Systemet återansluter automatiskt vid anslutningsbrott. Du kan synkronisera mappar och filer mellan din dator och servern via kommandoradsverktyg (pull, push, sync).

**Teknik:** Backend är byggd med ASP.NET Core och SQLite. Frontend är en webbapplikation med TypeScript. Inbyggd texteditor (Monaco) är integrerad lokalt utan CDN. Autentisering använder JWT med access token i klienten och refresh token i HttpOnly-cookie. Realtidsuppdateringar sker via SignalR.

## Arkitektur

Projektet består av tre delar:

- **DocVersion.Server**: Webbservern som hanterar inloggning, filer och uppdateringar
- **DocVersion.Core**: Gemensad kod och hjälpfunktioner
- **DocVersion.Client**: Verktyg för att synkronisera mappar och filer mellan din dator och servern

### Server

- API som tar emot begäranden (login, ladda upp filer, osv)
- Databas som sparar användare och filversioner
- Säker inloggning med lösenord
- Realtidsuppdateringar så att alla ser ändringar direkt
- Begränsning på hur många inloggningsförsök som tillåts

### Webbläsargränssnittet

- Skrivet med TypeScript (programmering)
- Kombineras till en enda JavaScript-fil
- Design med SCSS (stilar)
- Texteditor (Monaco) laddas från servern, inte från internet

### Lagring och versionering

- Varje användares filer sparas i sina egna mappar på servern
- Gamla versioner av filer sparas i en historik-mapp
- Information om vilken version som är vilken sparas i databasen

## Komplett funktionell översikt

### Auth-funktioner

- Logga in med e-post och lösenord.
- Register med e-postvalidering och minimilängd för lösenord
- Uppdatera inloggning med säker token-kontroll.
- Logga ut och stäng av tidigare inloggningar.
- En ruta i gränssnittet där du kan växla mellan inloggning och skapa konto.

### Filfunktioner

- Lista filer och mappar
- Visa filinnehåll eller mappinnehåll
- Hämta metadata via HEAD
- Skapa fil
- Skapa mapp
- Spara fil (inklusive tom fil), tangentbordskortkommando (Ctrl + S) eller save-knapp
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
- Navigera historik med tangentbord (Ctrl + Z och Ctrl + Y)
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

## DocVersion.Client: Synkroniseringssverktyg

DocVersion.Client är ett verktyg du kör från kommandoraden (terminal) för att automatiskt synkronisera mappar och filer mellan din dator och servern.

### Så här använder du det:

1. Bygg programmet som en körbar fil:

   ```powershell
   dotnet publish -c Release --self-contained true -p:PublishSingleFile=true
   ```

2. Gå till `Release` mappen i projektet och kopiera den skapade filen till en valfri mapp.

3. Lägg till den mappen i din miljövariabel (PATH) så att du kan köra kommandot från var som helst.

4. Öppna terminalen i den mapp du vill synkronisera med servern.

5. Kör ett av följande kommandon:
   - `DocVersion.Client pull <serverUrl> [email] [password]`
   - `DocVersion.Client push <serverUrl> [email] [password]`
   - `DocVersion.Client sync <serverUrl> [email] [password]`

Beteende:

- pull: laddar ner serverns filer/mappar och speglar lokalt
- push: laddar upp lokala filer/mappar till servern
- sync: kombinerar pull och push-flöden med event-baserad synklogik
- login sker om email och password skickas med

Exempel:

```powershell
DocVersion.Client pull http://localhost:3000 user@example.com MyPass123
DocVersion.Client push http://localhost:3000 user@example.com MyPass123
DocVersion.Client sync http://localhost:3000 user@example.com MyPass123
```

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
├─ DocVersion.Client/
│  └─ Program.cs
└─ DocVersion.Server/
	 ├─ Program.cs
	 ├─ appsettings.json
	 ├─ appsettings.Development.json
	 ├─ tsconfig.json
	 ├─ Controllers/
	 │  ├─ LoginController.cs
	 │  └─ FilesController.cs
	 ├─ Data/
	 │  └─ AppDbContext.cs
	 │  └─ DocVersion.db
	 ├─ Hubs/
	 │  └─ EventHub.cs
	 ├─ Models/
	 │  ├─ UserAccount.cs
	 │  └─ FileHistory.cs
	 ├─ Security/
	 │  ├─ JwtOptions.cs
	 │  ├─ JwtService.cs
	 │  └─ NameUserIdProvider.cs
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
	 │  ├─ utils.ts
	 │  └─ styles.scss
	 └─ wwwroot/
			├─ index.html
			├─ css/
			├─ js/
			│  └─ vendor/
			│	  └─ scripts/
			│		 └─ sync-monaco.mjs
			└─ Assets/
```

## Lokal utveckling

Vad du behöver:

- .NET SDK 10
- Node.js och npm

Steg 1: Installera allt som behövs

```powershell
npm install
```

Steg 2: Bygg frontend:

```powershell
npm run build
```

Steg 3: Bygg CSS:

```powershell
npx sass DocVersion.Server/src/styles.scss DocVersion.Server/wwwroot/css/styles.css --no-source-map
```

Steg 4: Bygg backend:

```powershell
dotnet build DocVersion.Server/DocVersion.Server.csproj -c Release
```

Steg 5: Starta servern

```powershell
cd DocVersion.Server
dotnet run
```

Server URL:

- http://localhost:3000/

## Konfiguration

Servern behöver en hemlig nyckel för att kryptera inloggningar. Den söker efter den här:

1. Miljövariabel JWT_KEY (bäst)
2. Inställningen Jwt:Key i appsettings-filen

Rekommendation: Sätt JWT_KEY som miljövariabel istället för hemlig nyckel i filer.

## Säkerhet

- PasswordHasher för hash och verify
- JWT med validering av issuer, audience, lifetime och signing key
- Refresh token i HttpOnly-cookie med SameSite=Strict
- RefreshTokenVersion för revocation vid logout
- Rate limiter för auth
- Eventflöde scoped per användare i SignalR

## English Summary

DocVersion is a system for storing files with version history. It has a web interface where you can upload, edit and download files and folders. The system saves old versions so you can restore earlier versions if needed. It's secure with login and password, and uses real-time updates so changes appear immediately.

## Utvecklare

Alaa Alsous

# DirectoryChangeApp

Simple ASP.NET Core application for analyzing changes in a local directory.

## Task Description

CVIČNÝ ÚKOL - Hledáme šikovného web developera!
Napište jednoduchý program, který bude umět detekovat změny v lokálním adresáři uvedeném na
vstupu. Při prvním spuštění si program obsah daného adresáře analyzuje a při každém dalším
spuštění bude hlásit změny od svého posledního spuštění, tj:
a) seznam nových souborů,
b) seznam změněných souborů (změnou se rozumí změna obsahu daného souboru),
c) seznam odstraněných souborů a podadresářů.
U každého souboru evidujte číslo jeho aktuální verze (na začátku budou mít všechny soubory verzi 1,
s každou detekovanou změnou daného souboru bude jeho verze navýšena o 1).
Program realizujte jako jednoduchou ASP.NET aplikaci naprogramovanou v C#. UI vytvořte jako
webovou aplikaci dle své volby (Core MVC, MVC, REST API)
Můžete předpokládat, že velikost souborů v adresáři bude do 50 MB a že počet souborů v každém
adresáři bude nanejvýš 100.
Program se bude spouštět ručně z UI stiskem tlačítka (nedetekujte změny filesystému automaticky).
Pro perzistenci dat nepoužívejte databázi.
UI bude obsahovat alespoň textbox (textový input) pro zadání cesty k analyzovanému adresáři,
tlačítko pro spuštění analýzy a výpis jejího výsledku.
Své řešení stručně popište a zmiňte i jeho případná omezení.

## Solution Overview

The application scans a directory path provided from the web UI. It stores the latest known state in `state.json` and compares the next scan against that saved state.

The analysis reports:

- new files and directories
- modified files, detected by SHA-256 content hash
- removed files and directories
- file versions, starting at version 1 and incrementing when file content changes

No database is used.

## Requirements

- .NET 8 SDK
- Docker Desktop, optional

## Run Locally

From the repository root:

```bash
dotnet restore
dotnet run
```

Open the URL printed by `dotnet run`. The app usually starts on one of these:

```text
http://localhost:5000/
https://localhost:5001/
```

The UI is served from `wwwroot/index.html`.

Swagger is available at:

```text
/swagger
```

Example:

```text
http://localhost:5000/swagger
```

## Run With Docker

From the repository root:

```bash
docker compose up --build
```

Open:

```text
http://localhost:8080/
```

Swagger:

```text
http://localhost:8080/swagger
```

The compose setup mounts `state.json` into the container so the analysis state persists in the repository file.

## How To Use

1. Start the application.
2. Open the web UI in a browser.
3. Enter the absolute path of the local directory to analyze.
4. Click the analyze button.
5. Review the lists of new, modified, and removed items.
6. Run the analysis again after changing the directory contents to see differences from the previous run.

## API

The application exposes a REST API used by the UI. Swagger documents the available endpoints:

```text
/swagger
```

## Tests

Run tests with:

```bash
dotnet test
```

## CI/CD

GitHub Actions workflow is defined in:

```text
.github/workflows/ci-cd.yml
```

It runs restore, build, tests, .NET analyzer build, and CodeQL analysis.

## Limitations

- The app stores state in a single local `state.json` file.
- File changes are detected by content hash, not by filesystem events.
- Directory changes are detected during manual analysis only.
- The task assumes files up to 50 MB and up to 100 files per directory.
- The entered directory path must be accessible to the running process. When running in Docker, host paths must be mounted into the container before they can be analyzed.

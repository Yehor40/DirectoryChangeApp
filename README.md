# DirectoryChangeApp

Jednoduchá ASP.NET Core aplikace pro analýzu změn v lokálním adresáři.

*(English version below)*

## Zadání úkolu

CVIČNÝ ÚKOL - Hledáme šikovného web developera!
Napište jednoduchý program, který bude umět detekovat změny v lokálním adresáři uvedeném na vstupu. Při prvním spuštění si program obsah daného adresáře analyzuje a při každém dalším spuštění bude hlásit změny od svého posledního spuštění, tj:
a) seznam nových souborů,
b) seznam změněných souborů (změnou se rozumí změna obsahu daného souboru),
c) seznam odstraněných souborů a podadresářů.
U každého souboru evidujte číslo jeho aktuální verze (na začátku budou mít všechny soubory verzi 1, s každou detekovanou změnou daného souboru bude jeho verze navýšena o 1).
Program realizujte jako jednoduchou ASP.NET aplikaci naprogramovanou v C#. UI vytvořte jako webovou aplikaci dle své volby (Core MVC, MVC, REST API)
Můžete předpokládat, že velikost souborů v adresáři bude do 50 MB a že počet souborů v každém adresáři bude nanejvýš 100.
Program se bude spouštět ručně z UI stiskem tlačítka (nedetekujte změny filesystému automaticky).
Pro perzistenci dat nepoužívejte databázi.
UI bude obsahovat alespoň textbox (textový input) pro zadání cesty k analyzovanému adresáři, tlačítko pro spuštění analýzy a výpis jejího výsledku.
Své řešení stručně popište a zmiňte i jeho případná omezení.

## Přehled řešení

Aplikace skenuje cestu k adresáři zadanou přes webové UI. Poslední známý stav ukládá do souboru `state.json` a každé další skenování porovnává vůči tomuto uloženému stavu.

Analýza reportuje:
- nové soubory a adresáře
- upravené soubory (detekováno pomocí SHA-256 hashe obsahu)
- odstraněné soubory a adresáře
- verze souborů, začínající na verzi 1, která se při změně obsahu souboru zvyšuje

Aplikace nepoužívá databázi.

## Požadavky

- .NET 8 SDK
- Docker Desktop (volitelné)

## Spuštění pomocí Dockeru

Z kořenového adresáře repozitáře:

```bash
docker compose up --build
```

Otevřete:
```text
http://localhost:8080/
```

Swagger:
```text
http://localhost:8080/swagger
```

Docker compose připojí `state.json` do kontejneru, takže stav analýzy zůstane zachován v souboru uvnitř repozitáře.

## Jak používat

1. Spusťte aplikaci.
2. Otevřete webové UI v prohlížeči.
3. Zadejte absolutní cestu k lokálnímu adresáři, který chcete analyzovat.
4. Klikněte na tlačítko pro spuštění analýzy.
5. Prohlédněte si seznamy nových, upravených a odstraněných položek.
6. Změňte obsah adresáře a spusťte analýzu znovu, abyste viděli rozdíly oproti předchozímu běhu.

## API

Aplikace poskytuje REST API, které využívá uživatelské rozhraní. Dostupné endpointy jsou zdokumentovány ve Swaggeru:
```text
/swagger
```

## Testy

Testy spustíte příkazem:
```bash
dotnet test
```

## CI/CD

Pracovní postup GitHub Actions je definován v:
```text
.github/workflows/ci-cd.yml
```
Spouští obnovu balíčků, build, testy, build .NET analyzátoru a CodeQL analýzu.

## Omezení

- Aplikace ukládá stav do jediného lokálního souboru `state.json`.
- Změny souborů jsou detekovány pomocí hashe obsahu, nikoliv pomocí událostí souborového systému.
- Změny adresářů jsou detekovány pouze během manuální analýzy.
- Úkol předpokládá soubory do velikosti 50 MB a maximálně 100 souborů v adresáři.
- Zadaná cesta k adresáři musí být přístupná běžícímu procesu. Při spuštění v Dockeru musí být hostitelské cesty připojeny do kontejneru, aby je bylo možné analyzovat.

---

# DirectoryChangeApp (English Version)

Simple ASP.NET Core application for analyzing changes in a local directory.

## Task Description

*(See Czech version for original text)*

Write a simple program that will be able to detect changes in a local directory specified in the input. Upon the first run, the program analyzes the contents of the given directory, and at each subsequent run it will report changes since its last run, i.e.:
a) a list of new files,
b) a list of modified files (modification means a change in the content of the given file),
c) a list of removed files and subdirectories.
For each file, track the number of its current version (at the beginning, all files will have version 1, with each detected change of the given file, its version will be increased by 1).
Implement the program as a simple ASP.NET application programmed in C#. Create the UI as a web application of your choice (Core MVC, MVC, REST API).
You can assume that the size of the files in the directory will be up to 50 MB and that the number of files in each directory will be at most 100.
The program will be run manually from the UI by pressing a button (do not detect filesystem changes automatically).
Do not use a database for data persistence.
The UI will contain at least a textbox (text input) for entering the path to the analyzed directory, a button to start the analysis, and a list of its results.
Briefly describe your solution and mention any possible limitations.

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

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

Aplikace skenuje cestu k adresáři zadanou přes webové UI. Poslední známý stav ukládá jako **snapshot** do `bin/App_Data/snapshots/{hash}.json` (jeden soubor na analyzovaný kořenový adresář). Každé další skenování porovnává aktuální stav s uloženým snapshotem.

Analýza reportuje:
- nové soubory (včetně souborů v podadresářích)
- upravené soubory (SHA-256 hash obsahu)
- změny metadat (stejný obsah, jiný čas/velikost)
- odstraněné soubory a podadresáře
- přeskočené / nestabilní soubory při částečném skenu
- verze souborů (od 1, +1 při změně obsahu; při přejmenování se verze zachová)

Databáze se nepoužívá — pouze lokální JSON snapshoty.

## Principy práce (architektura)

1. **Snapshot místo jednoho globálního souboru** — každý analyzovaný kořen má vlastní JSON snapshot (hash cesty), bez kolizí mezi různými adresáři.
2. **Atomický zápis** — snapshot se zapisuje do `.tmp` a teprve potom se nahradí cílový soubor (ochrana proti poškození při pádu procesu).
3. **Ruční analýza na tlačítko** — žádný `FileSystemWatcher`; diff pouze při explicitním požadavku z UI.
4. **Paralelní hashování** — soubory se hashují paralelně (omezený počet vláken), průchod stromem je sekvenční s respektem k oprávněním.
5. **Permission-aware scan** — nepřístupné složky/soubory se přeskočí, výsledek může být označen jako `IsPartial`; stav v přeskočených větvích se nemění (aby nedošlo k falešným „smazaným“ položkám).
6. **Bezpečnost cest** — symlinky/reparse pointy se neprocházejí; skryté položky (`.`) a `App_Data` se ignorují.
7. **Mapování cest v Dockeru** — vstupní cesta z hostitele se mapuje na `/host-files` přes `PathMapping` a `.env` (`HOST_FILES_ROOT`).
8. **Detekce přejmenování** — striktní párování 1:1 (jeden nový + jeden smazaný soubor se stejným hashem) se hlásí jako úprava se zachovanou verzí.
9. **Concurrency** — současné analýzy stejného kořene jsou serializovány (`SemaphoreSlim` na normalizovaný klíč cesty).
10. **Audit logy** — časované fáze skenu, hashování, porovnání a uložení (viz konfigurace `Logging:Console` v `appsettings.json`).

## Požadavky

- .NET 8 SDK
- Docker Desktop

## Běžné spuštění

Pomoci příkázů

```bash
#sestaví projekt
dotnet build
#spustí projekt
dotnet run
```

## Spuštění pomocí Dockeru

Z kořenového adresáře repozitáře:

1) Vytvořte soubor `.env` s kořenovou cestou hostitele, kterou chcete analyzovat:

```env
HOST_FILES_ROOT=/absolute/path/on/host
```

Příklad:
- macOS/Linux: `HOST_FILES_ROOT=/Users/john/`
- Windows (Docker Desktop): `HOST_FILES_ROOT=C:/Users/John/`

2) Spusťte:

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

Docker compose připojí:
- `./App_Data` -> `/app/App_Data` (JSON snapshoty)
- `${HOST_FILES_ROOT}` -> `/host-files` (čtení souborů hostitelského zařízení)

Aplikace mapuje vstupní cestu z hostitele na cestu v kontejneru přes proměnné:
- `PathMapping__HostPathPrefix`
- `PathMapping__ContainerPathPrefix`

## Jak používat

1. Spusťte aplikaci.
2. Otevřete webové UI v prohlížeči.
3. Zadejte absolutní cestu k lokálnímu adresáři, který chcete analyzovat.
4. Klikněte na tlačítko pro spuštění analýzy.
5. Prohlédněte si seznamy nových, upravených a odstraněných položek.
6. Změňte obsah adresáře a spusťte analýzu znovu, abyste viděli rozdíly oproti předchozímu běhu.

## CI/CD

Pracovní postup GitHub Actions je definován v:
```text
.github/workflows/ci-cd.yml
```
Spouští obnovu balíčků, build, testy, build .NET analyzátoru a CodeQL analýzu.

## Omezení

- Stav je v `App_Data/snapshots/` (složka je v `.gitignore`).
- Změny souborů jsou detekovány pomocí hashe obsahu, nikoliv pomocí událostí souborového systému.
- Úkol předpokládá soubory do velikosti 50 MB a maximálně 100 souborů v adresáři.
- Zadaná cesta k adresáři musí být přístupná běžícímu procesu. V Dockeru musí být hostitelská cesta pod `HOST_FILES_ROOT`, aby ji bylo možné mapovat do kontejneru.


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

The application scans a directory path provided from the web UI. It stores the latest known state as a **snapshot** in `bin/App_Data/snapshots/{hash}.json` (one file per analyzed root). Each new scan is compared against that snapshot.

The analysis reports:
- new files (including files in subfolders)
- modified files (SHA-256 content hash)
- metadata-only changes (same hash, different size/timestamp)
- removed files and subdirectories
- skipped/unstable files on partial scans
- file versions (from 1, +1 on content change; preserved on rename)

No database is used — only local JSON snapshots.

## Work principles (architecture)

1. **Per-root snapshots** — each analyzed directory root gets its own JSON file (path hash), avoiding cross-directory collisions.
2. **Atomic writes** — write to `.tmp`, then replace the target file.
3. **Manual analysis only** — no `FileSystemWatcher`; diff runs on explicit UI/API request.
4. **Parallel hashing** — files are hashed in parallel (bounded degree); tree walk respects permissions.
5. **Permission-aware scanning** — inaccessible paths are skipped; `IsPartial` when needed; state under skipped branches is preserved to avoid false deletions.
6. **Path safety** — symlinks/reparse points are not followed; dot-prefixed entries and `App_Data` are ignored.
7. **Docker path mapping** — host paths map to `/host-files` via `PathMapping` and `.env` (`HOST_FILES_ROOT`).
8. **Rename detection** — strict 1:1 add/remove pairs with the same hash are reported as modified with preserved version.
9. **Concurrency** — concurrent analyses of the same root are serialized via `SemaphoreSlim`.
10. **Audit logging** — timed phases for scan, hash, compare, and save (see `Logging:Console` in `appsettings.json`).

## Requirements

- .NET 8 SDK
- Docker Desktop, optional

## Normal startup

With commands

```bash
#builds project
dotnet build
#runs project
dotnet run
```


## Run With Docker

From the repository root:

1) Create `.env` with the host root path you want to scan:

```env
HOST_FILES_ROOT=/absolute/path/on/host
```

Examples:
- macOS/Linux: `HOST_FILES_ROOT=/Users/john/`
- Windows (Docker Desktop): `HOST_FILES_ROOT=C:/Users/John/`

2) Start:

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

The compose setup mounts:
- `./App_Data` -> `/app/App_Data` (JSON snapshots)
- `${HOST_FILES_ROOT}` -> `/host-files` (host device files, read-only)

The app maps incoming host paths to container paths using:
- `PathMapping__HostPathPrefix`
- `PathMapping__ContainerPathPrefix`

## How To Use

1. Start the application.
2. Open the web UI in a browser.
3. Enter the absolute path of the local directory to analyze.
4. Click the analyze button.
5. Review the lists of new, modified, and removed items.
6. Run the analysis again after changing the directory contents to see differences from the previous run.


## CI/CD

GitHub Actions workflow is defined in:
```text
.github/workflows/ci-cd.yml
```

It runs restore, build, tests, .NET analyzer build, and CodeQL analysis.

## Limitations

- State lives under `App_Data/snapshots/` (ignored by git).
- File changes are detected by content hash, not by filesystem events.
- The task assumes files up to 50 MB and up to 100 files per directory.
- The entered directory path must be accessible to the running process. In Docker mode, the path must be under `HOST_FILES_ROOT` so it can be mapped inside the container.

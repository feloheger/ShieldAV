# ShieldAV Antivirus

Ein leichtgewichtiger Antivirusscanner für Windows, gebaut mit .NET 8 WinForms.

## Features

- **SHA256-Hash-Scan** gegen die MalwareBazaar-Datenbank (echte Malware-Signaturen)
- **Heuristik-Engine** – erkennt verdächtige Dateinamen, Doppel-Endungen, Temp-Ordner-Exploits
- **Quarantäne** – isoliert Bedrohungen mit XOR-Verschlüsselung
- **Echtzeit-Protokoll** – farbkodiert, exportierbar als .txt/.csv
- **Multithreaded** – UI friert während des Scans nicht ein
- **Inno Setup Installer** – professionelle Installation mit Autostart-Option

## Voraussetzungen

- Windows 10/11 (64-Bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdownload.php) (nur für Installer)
- Internetverbindung (für MalwareBazaar API-Abfragen)

## Bauen & Installieren

### 1. Schnell starten
```
build.bat
```
Das Skript:
1. Stellt NuGet-Pakete wieder her
2. Kompiliert im Release-Modus
3. Erstellt eine self-contained Single-File EXE (`publish\ShieldAV.exe`)
4. Erstellt den Inno Setup Installer (`installer_output\ShieldAV_Setup_v1.0.0.exe`)

### 2. Manuell bauen
```bash
dotnet restore ShieldAV\ShieldAV.csproj
dotnet publish ShieldAV\ShieldAV.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### 3. Installer kompilieren
```
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

## Projektstruktur

```
ShieldAV/
├── ShieldAV/
│   ├── Engine/
│   │   ├── HashScanner.cs      ← SHA256 + MalwareBazaar API
│   │   ├── ScanEngine.cs       ← Multithreaded Scan-Logik
│   │   └── QuarantineManager.cs← Quarantäne mit XOR-Verschlüsselung
│   ├── Forms/
│   │   └── MainForm.cs         ← WinForms UI (Tabs: Scan, Quarantäne, Log)
│   ├── Models/
│   │   └── ScanResult.cs       ← Datenmodelle
│   ├── Program.cs
│   └── ShieldAV.csproj
├── installer.iss               ← Inno Setup Script
├── build.bat                   ← Build-Automatisierung
└── README.md
```

## Wie es funktioniert

1. **Dateiliste** – alle Dateien im gewählten Ordner werden rekursiv aufgelistet
2. **Heuristik** – schnelle lokale Prüfung (kein Netzwerk nötig):
   - Doppelte Dateiendungen (z.B. `foto.jpg.exe`)
   - Verdächtige Namen (`crack`, `keygen`, `hack`...)
   - Ausführbare Dateien in Temp-Ordnern
3. **Hash-Check** – für EXE/DLL/BAT/PS1 etc. wird SHA256 berechnet und gegen MalwareBazaar geprüft
4. **Quarantäne** – erkannte Dateien werden XOR-verschlüsselt isoliert (können nicht ausgeführt werden)

## Hinweise

- Kein Ersatz für kommerzielle AV-Software mit vollständigen Signaturdatenbanken
- MalwareBazaar API: kostenlos, keine Key nötig, Rate-Limit beachten
- Admin-Rechte empfohlen für systemweite Scans

## Lizenz

Freeware – für Bildungs- und Lernzwecke.

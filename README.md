# Docker Manager — Windows Forms (.NET 8)

Aplikacja WinForms do zarządzania kontenerami Docker przez Docker API (Docker.DotNet).

## Wymagania
- Windows 10/11
- Docker Desktop (uruchomiony)
- Visual Studio 2022 z workloadem ".NET desktop development"
- .NET 8 SDK

## Uruchomienie
1. Otwórz `DockerManager.sln` w Visual Studio
2. NuGet automatycznie pobierze `Docker.DotNet`
3. Kliknij **Start** (F5)

## Funkcjonalności
- Lista kontenerów z kolorowym statusem (zielony = running, czerwony = exited)
- Start / Stop / Restart / Usuń kontenery
- Instalacja MySQL i PostgreSQL jednym kliknięciem
- Automatyczny refresh co 3 sekundy
- Logi kontenera (ostatnie 20 linii)
- Statystyki CPU i RAM w czasie rzeczywistym
- Montowanie lokalnych folderów (wolumeny)
- Sprawdzanie dostępności portów (3306, 5432)

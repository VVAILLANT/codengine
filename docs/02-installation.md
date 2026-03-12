# Installation

## Prérequis

- **.NET SDK 8.0 ou supérieur** (recommandé : .NET 9)
- **Windows, Linux ou macOS**

### Installer .NET SDK

**Windows (winget)** :
```bash
winget install Microsoft.DotNet.SDK.9
```

**Linux (Ubuntu/Debian)** :
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0
```

**macOS (Homebrew)** :
```bash
brew install dotnet-sdk
```

## Installation de Codengine

### Option 1 : Depuis les sources

```bash
# Cloner
git clone <url-du-repo>
cd codengine

# Restaurer les dépendances
dotnet restore

# Builder
dotnet build -c Release

# Publier un exécutable autonome
dotnet publish src/Codengine.Cli -c Release -o ./publish
```

L'exécutable sera dans `./publish/Codengine.Cli.exe` (Windows) ou `./publish/Codengine.Cli` (Linux/macOS).

### Option 2 : Comme outil global .NET (recommandé)

```bash
# Packager
dotnet pack src/Codengine.Cli -o src/Codengine.Cli/nupkg

# Installer globalement
dotnet tool install --global --add-source src/Codengine.Cli/nupkg Codengine.Cli

# Utilisation depuis n'importe où
codengine analyze ./src
```

## Vérification de l'installation

```bash
# Lister les règles disponibles
codengine list-rules

# Ou depuis les sources sans installation
dotnet run --project src/Codengine.Cli -- --version
```

## Structure des fichiers installés

```
codengine/
├── Codengine.Cli.exe          # Exécutable principal
├── Codengine.Cli.dll          # Assembly principal
├── Codengine.Core.dll         # Noyau
├── Codengine.Rules.dll        # Règles d'analyse
├── Codengine.Connectors.dll   # Connecteurs (Oracle)
├── Codengine.Reporters.dll    # Reporters (Console, JSON, HTML)
├── Microsoft.CodeAnalysis.*.dll  # Roslyn (analyse C#)
└── Oracle.ManagedDataAccess.dll  # Driver Oracle
```

## Mise à jour

```bash
# Depuis les sources
git pull

# Re-packager et mettre à jour l'outil global
dotnet pack src/Codengine.Cli -o src/Codengine.Cli/nupkg
dotnet tool update --global --add-source src/Codengine.Cli/nupkg Codengine.Cli
```

## Désinstallation

```bash
dotnet tool uninstall --global Codengine.Cli
```

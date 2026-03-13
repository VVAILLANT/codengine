# Installation

## Prérequis

- **.NET SDK 9.0** ([télécharger](https://dotnet.microsoft.com/download/dotnet/9.0))
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

---

## Installation de Codengine

Codengine s'installe comme **outil global .NET**, disponible depuis n'importe quel répertoire.

**1. Cloner le repo et builder**

```bash
git clone https://github.com/VVAILLANT/codengine.git
cd codengine
dotnet pack src/Codengine.Cli -o src/Codengine.Cli/nupkg
```

**2. Installer globalement**

```bash
dotnet tool install --global --add-source src/Codengine.Cli/nupkg Codengine.Cli
```

**3. Vérifier l'installation**

```bash
codengine list-rules
```

---

## Mise à jour

```bash
cd codengine
git pull
bash scripts/publish-tool.sh
```

Le script bumpe automatiquement la version, repackage et réinstalle l'outil.

---

## Désinstallation

```bash
dotnet tool uninstall --global Codengine.Cli
```

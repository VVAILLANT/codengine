# Guide de démarrage rapide

## Qu'est-ce que Codengine ?

Codengine est un analyseur de code statique pour :
- **C# (.NET Framework 4.8 et .NET 6+)** : Détection de problèmes de qualité de code
- **PL/SQL Oracle** : Extraction et analyse des packages

C'est un outil de "clean code" automatique, similaire à SonarQube, mais léger et personnalisable.

## Installation rapide

```bash
# Cloner le projet
git clone <url-du-repo>
cd codengine

# Builder
dotnet build

# Vérifier l'installation
dotnet run --project src/Codengine.Cli -- --help
```

## Première analyse

```bash
# Analyser un projet C#
dotnet run --project src/Codengine.Cli -- analyze /chemin/vers/mon-projet

# Ou si publié
codengine analyze /chemin/vers/mon-projet
```

## Exemple de sortie

```
Codengine v1.0.0
Analyse de: C:\MonProjet\src

═══════════════════════════════════════════════════════════════
                    CODENGINE - ANALYSE TERMINÉE
═══════════════════════════════════════════════════════════════

  Source analysée : C:\MonProjet\src
  Fichiers analysés : 45
  Durée : 0.23s

  RÉSUMÉ:
    Critiques : 0
    Erreurs   : 3
    Warnings  : 12

───────────────────────────────────────────────────────────────
  VIOLATIONS:
───────────────────────────────────────────────────────────────

  C:\MonProjet\src\Services\UserService.cs
    X [COD001] Ligne 42: La variable 'user' issue de FirstOrDefault()
      doit être vérifiée pour null avant utilisation.
```

## Prochaines étapes

- [Installation complète](./02-installation.md)
- [Configuration](./03-configuration.md)
- [Liste des règles](./05-rules.md)

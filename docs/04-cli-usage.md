# Utilisation CLI

## Syntaxe générale

```bash
codengine <commande> [arguments] [options]
```

## Commandes disponibles

| Commande | Description |
|----------|-------------|
| `analyze` | Analyser le code source |
| `fix` | Appliquer les corrections automatiques |
| `extract-oracle` | Extraire les packages PL/SQL d'Oracle |
| `list-rules` | Lister toutes les règles disponibles |
| `init` | Créer un fichier de configuration |

---

## analyze

Analyse le code source et détecte les violations.

### Syntaxe

```bash
codengine analyze [path] [options]
```

### Arguments

| Argument | Description | Défaut |
|----------|-------------|--------|
| `path` | Chemin du répertoire ou fichier à analyser | `.` (répertoire courant) |

### Options

| Option | Alias | Description |
|--------|-------|-------------|
| `--output` | `-o` | Chemin du fichier de rapport |
| `--format` | `-f` | Format de sortie (`console`, `json`, `html`) |
| `--verbose` | `-v` | Afficher les suggestions de correction |
| `--disable` | `-d` | Règles à désactiver (séparées par virgule) |
| `--config` | `-c` | Chemin du fichier de configuration |

### Exemples

```bash
# Analyser le répertoire courant
codengine analyze

# Analyser un répertoire spécifique
codengine analyze ./src

# Générer un rapport JSON
codengine analyze ./src -f json -o rapport.json

# Générer un rapport HTML
codengine analyze ./src -f html -o rapport.html

# Désactiver certaines règles
codengine analyze ./src -d COD006,COD007

# Mode verbose (affiche les suggestions)
codengine analyze ./src -v

# Utiliser un fichier de configuration spécifique
codengine analyze ./src -c ./custom-config.json

# Combiner plusieurs options
codengine analyze ./src -f html -o rapport.html -v -d COD006
```

### Codes de retour

| Code | Signification |
|------|---------------|
| 0 | Aucune violation Error/Critical |
| 1 | Au moins une violation Error/Critical détectée |

---

## fix

Applique les corrections automatiques disponibles.

### Syntaxe

```bash
codengine fix [path] [options]
```

### Options

| Option | Description |
|--------|-------------|
| `--dry-run` | Afficher les corrections sans les appliquer |
| `--rules` `-r` | Règles à corriger (par défaut : toutes) |

### Exemples

```bash
# Corriger toutes les violations avec auto-fix
codengine fix ./src

# Voir les corrections sans les appliquer
codengine fix ./src --dry-run

# Corriger seulement certaines règles
codengine fix ./src -r COD001,COD005
```

### Règles avec auto-fix

| Règle | Description de la correction |
|-------|------------------------------|
| COD001 | Ajoute un null check après SingleOrDefault/FirstOrDefault |
| COD003 | Renomme les méthodes async pour terminer par "Async" |
| COD005 | Ajoute un rethrow dans les blocs catch vides |

---

## extract-oracle

Extrait les packages PL/SQL d'une base Oracle.

### Syntaxe

```bash
codengine extract-oracle [options]
```

### Options

| Option | Alias | Description | Requis |
|--------|-------|-------------|--------|
| `--connection` | `-c` | Chaîne de connexion Oracle | Oui |
| `--schema` | `-s` | Schéma Oracle | Non |
| `--output` | `-o` | Répertoire de sortie | Non |
| `--include` | `-i` | Patterns d'inclusion | Non |
| `--exclude` | `-e` | Patterns d'exclusion | Non |
| `--no-bodies` | | Ne pas extraire les bodies | Non |

### Exemples

```bash
# Extraire tous les packages du schéma courant
codengine extract-oracle -c "Data Source=//localhost:1521/ORCL;User Id=user;Password=pass;"

# Extraire d'un schéma spécifique
codengine extract-oracle -c "..." -s MY_SCHEMA

# Filtrer les packages
codengine extract-oracle -c "..." -i "PKG_*" -e "PKG_TEST_*"

# Extraire sans les bodies (headers uniquement)
codengine extract-oracle -c "..." --no-bodies

# Spécifier le répertoire de sortie
codengine extract-oracle -c "..." -o ./plsql-packages
```

### Format de sortie

Chaque package est sauvegardé dans un fichier `NOM_PACKAGE.sql` contenant :
- Header (specification)
- Body (si `--no-bodies` non spécifié)

---

## list-rules

Affiche toutes les règles d'analyse disponibles.

### Syntaxe

```bash
codengine list-rules
```

### Exemple de sortie

```
Règles disponibles:
═══════════════════════════════════════════════════════════════

  [ErrorHandling]
    COD005 [Error] EmptyCatchBlock
         Les blocs catch vides masquent les erreurs.

  [NullSafety]
    COD001 [Error] NullCheckAfterOrDefault
         Vérifier null après SingleOrDefault/FirstOrDefault.
    COD002 [Error] EmptyListBeforeContains
         Vérifier liste non null/vide avant Contains().

Total: 9 règle(s)
```

---

## init

Crée un fichier de configuration `codengine.config.json`.

### Syntaxe

```bash
codengine init
```

### Comportement

- Crée le fichier dans le répertoire courant
- N'écrase pas un fichier existant
- Inclut toutes les options avec leurs valeurs par défaut

---

## Options globales

Ces options sont disponibles pour toutes les commandes :

| Option | Description |
|--------|-------------|
| `--help` | Afficher l'aide |
| `--version` | Afficher la version |

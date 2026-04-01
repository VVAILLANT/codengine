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
| `format-oracle` | Formater les fichiers PL/SQL Oracle |
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
| `--tag` | | Annoter les lignes en violation avec un commentaire `// codengine[RULE]` |
| `--untag` | | Retirer tous les commentaires codengine des fichiers source |

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

# Annoter les lignes en violation directement dans les fichiers source
codengine analyze ./src --tag

# Retirer tous les tags codengine des fichiers source
codengine analyze ./src --untag

# Combiner plusieurs options
codengine analyze ./src -f html -o rapport.html -v -d COD006
```

### Annotations de code (`--tag` / `--untag`)

L'option `--tag` ajoute un commentaire en fin de ligne sur chaque violation détectée :

```csharp
// Avant --tag
var user = users.FirstOrDefault(u => u.Id == id);

// Après --tag
var user = users.FirstOrDefault(u => u.Id == id);  // codengine[COD001]

// Plusieurs règles sur la même ligne
catch {}  // codengine[COD005, COD006]
```

**Comportement :**
- **Idempotent** : relancer `--tag` retire d'abord les anciens tags avant d'en ajouter de nouveaux
- **Préserve les fins de ligne** (`\r\n` vs `\n`)
- Fonctionne sur un fichier unique ou un répertoire entier

**Nettoyage :**
```bash
codengine analyze ./src --untag
```
`--untag` ne relance pas l'analyse — il retire simplement tous les commentaires `// codengine[...]` trouvés dans les fichiers `.cs`.

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
| `--connection` | `-c` | Chaîne de connexion Oracle | Oui (sauf avec `--config`) |
| `--schema` | `-s` | Schéma Oracle | Non |
| `--output` | `-o` | Répertoire de sortie | Non |
| `--include` | `-i` | Patterns d'inclusion | Non |
| `--exclude` | `-e` | Patterns d'exclusion | Non |
| `--no-bodies` | | Ne pas extraire les bodies | Non |
| `--config` | | Utiliser les valeurs de la section `oracle` de `codengine.config.json` | Non |

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

# Utiliser les valeurs par défaut depuis codengine.config.json
codengine extract-oracle --config

# Utiliser la config avec override d'options
codengine extract-oracle --config -s AUTRE_SCHEMA -o ./custom-output
```

### Format de sortie

Chaque package est sauvegardé dans un fichier `NOM_PACKAGE.sql` contenant :
- Header (specification)
- Body (si `--no-bodies` non spécifié)

### Encodage

Par défaut les fichiers sont écrits en UTF-8. Pour les bases Oracle avec un charset différent (ex: `WE8ISO8859P15`), configurez `"encoding": "iso-8859-15"` dans la section `oracle` de `codengine.config.json`. Voir [Extraction Oracle](./07-oracle-extraction.md#encodage-des-fichiers-extraits) pour la correspondance complète.

---

## format-oracle

Formate les fichiers PL/SQL `.sql` pour améliorer la lisibilité du code : indentation cohérente, normalisation des mots-clés en majuscules, nettoyage des espaces.

### Syntaxe

```bash
codengine format-oracle [path] [options]
```

### Arguments

| Argument | Description | Défaut |
|----------|-------------|--------|
| `path` | Répertoire contenant les fichiers `.sql` | `oracle.outputDirectory` de la config |

### Options

| Option | Description | Défaut |
|--------|-------------|--------|
| `--dry-run` | Afficher les changements sans modifier les fichiers | false |
| `--backup` | Créer un fichier `.bak` avant modification | false |
| `--indent-size` | Nombre d'espaces par niveau d'indentation | 4 |
| `--uppercase-keywords` | Mettre les mots-clés PL/SQL en majuscules | true |
| `--config` | Utiliser les valeurs de la section `oracle` de `codengine.config.json` | false |

### Exemples

```bash
# Formater avec la configuration du fichier codengine.config.json
codengine format-oracle --config

# Formater un répertoire spécifique
codengine format-oracle C:\Projects\Database\PACKAGE

# Prévisualiser les changements (aucune modification)
codengine format-oracle --config --dry-run

# Créer un backup avant modification
codengine format-oracle --config --backup

# Indentation personnalisée
codengine format-oracle --config --indent-size 2

# Garder la casse originale des mots-clés
codengine format-oracle --config --uppercase-keywords false
```

### Garde-fous

- **Intégrité** : chaque fichier est vérifié après formatage — si le contenu non-whitespace diffère, le fichier est ignoré avec un message d'erreur
- **Strings/commentaires** : les mots-clés dans les string literals (`'BEGIN...'`), commentaires (`--`, `/* */`) et identifiants quotés (`"END"`) ne sont jamais modifiés
- **Seule l'indentation est modifiée** : aucun caractère de code n'est altéré

### Blocs PL/SQL gérés

| Bloc | Ouverture | Fermeture |
|------|-----------|----------|
| BEGIN/END | `BEGIN` | `END;` / `END nom;` |
| IF | `IF ... THEN` | `END IF;` |
| LOOP | `LOOP` / `FOR ... LOOP` / `WHILE ... LOOP` | `END LOOP;` |
| CASE | `CASE` | `END CASE;` |
| EXCEPTION | `EXCEPTION` | (fermé par le `END;` parent) |
| PACKAGE | `CREATE OR REPLACE PACKAGE ... AS/IS` | `END nom;` |
| PROCEDURE/FUNCTION | `PROCEDURE/FUNCTION ... IS/AS` | `END nom;` |

Pour la documentation complète, voir [Formatage PL/SQL Oracle](./13-oracle-formatting.md).

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

## Suppression de violations inline

Pour ignorer une violation sur une ligne précise sans modifier la configuration globale, ajoutez un commentaire `// codengine-ignore` sur **la ligne de la violation** :

```csharp
var item = list.FirstOrDefault().ToString(); // codengine-ignore
var item = list.FirstOrDefault().ToString(); // codengine-ignore COD001
var item = list.FirstOrDefault().ToString(); // codengine-ignore COD001, COD002
```

---

## Options globales

Ces options sont disponibles pour toutes les commandes :

| Option | Description |
|--------|-------------|
| `--help` | Afficher l'aide |
| `--version` | Afficher la version |

# Extraction Oracle

## Vue d'ensemble

Codengine peut extraire les packages PL/SQL d'une base de données Oracle et les sauvegarder en fichiers `.sql` pour :
- Versionner le code PL/SQL dans Git
- Analyser le code avec d'autres outils
- Documentation et audit

## Prérequis

- Accès à une base de données Oracle
- Droits de lecture sur `ALL_OBJECTS` et `ALL_SOURCE`
- Driver Oracle (inclus : Oracle.ManagedDataAccess.Core)

## Utilisation

### Extraction basique

```bash
codengine extract-oracle -c "Data Source=//localhost:1521/ORCL;User Id=user;Password=pass;"
```

### Options complètes

```bash
codengine extract-oracle \
  -c "Data Source=//dbserver:1521/PROD;User Id=app_reader;Password=****;" \
  -s MY_SCHEMA \
  -o ./plsql-packages \
  -i "PKG_*" \
  -e "PKG_TEST_*,PKG_DEBUG_*"
```

## Options

| Option | Alias | Description | Défaut |
|--------|-------|-------------|--------|
| `--connection` | `-c` | Chaîne de connexion Oracle | *Requis* |
| `--schema` | `-s` | Schéma à extraire | Utilisateur courant |
| `--output` | `-o` | Répertoire de sortie | `./oracle_packages` |
| `--include` | `-i` | Patterns d'inclusion | Tous |
| `--exclude` | `-e` | Patterns d'exclusion | Aucun |
| `--no-bodies` | | Extraire uniquement les headers | false |

## Format de chaîne de connexion

### Format Easy Connect

```
Data Source=//host:port/service;User Id=user;Password=pass;
```

Exemples :
```
Data Source=//localhost:1521/ORCL;User Id=scott;Password=tiger;
Data Source=//dbprod.company.com:1521/PRODDB;User Id=app_user;Password=****;
```

### Format TNS

```
Data Source=TNSALIAS;User Id=user;Password=pass;
```

Le fichier `tnsnames.ora` doit être configuré.

### Options supplémentaires

```
Data Source=//host:1521/SID;User Id=user;Password=pass;Connection Timeout=30;
```

## Format de sortie

Chaque package est sauvegardé dans un fichier `NOM_PACKAGE.sql` :

```sql
-- Package: PKG_USERS
-- Schema: APP_SCHEMA
-- Extracted: 2024-01-15 10:30:45 UTC

-- ═══════════════════════════════════════════════════════════════
-- PACKAGE SPECIFICATION
-- ═══════════════════════════════════════════════════════════════

CREATE OR REPLACE PACKAGE PKG_USERS AS
    PROCEDURE create_user(p_name VARCHAR2, p_email VARCHAR2);
    FUNCTION get_user(p_id NUMBER) RETURN SYS_REFCURSOR;
END PKG_USERS;
/

-- ═══════════════════════════════════════════════════════════════
-- PACKAGE BODY
-- ═══════════════════════════════════════════════════════════════

CREATE OR REPLACE PACKAGE BODY PKG_USERS AS
    PROCEDURE create_user(p_name VARCHAR2, p_email VARCHAR2) IS
    BEGIN
        INSERT INTO users (name, email) VALUES (p_name, p_email);
        COMMIT;
    END create_user;

    FUNCTION get_user(p_id NUMBER) RETURN SYS_REFCURSOR IS
        v_cursor SYS_REFCURSOR;
    BEGIN
        OPEN v_cursor FOR SELECT * FROM users WHERE id = p_id;
        RETURN v_cursor;
    END get_user;
END PKG_USERS;
/
```

## Patterns de filtrage

### Patterns d'inclusion (-i)

Seuls les packages correspondant aux patterns seront extraits.

```bash
# Packages commençant par PKG_
-i "PKG_*"

# Packages commençant par PKG_ ou SP_
-i "PKG_*" -i "SP_*"

# Package spécifique
-i "PKG_USERS"
```

### Patterns d'exclusion (-e)

Les packages correspondant seront exclus.

```bash
# Exclure les packages de test
-e "PKG_TEST_*"

# Exclure plusieurs patterns
-e "PKG_TEST_*,PKG_DEBUG_*,PKG_TMP_*"
```

### Syntaxe des patterns

| Pattern | Signification |
|---------|---------------|
| `*` | N'importe quelle séquence de caractères |
| `?` | Un seul caractère |
| `PKG_*` | Commence par PKG_ |
| `*_OLD` | Termine par _OLD |
| `PKG_USER?` | PKG_USER suivi d'un caractère |

## Configuration via fichier

Dans `codengine.config.json` :

```json
{
  "oracle": {
    "connectionString": "Data Source=//localhost:1521/ORCL;User Id=user;Password=****;",
    "schema": "APP_SCHEMA",
    "outputDirectory": "./plsql",
    "includePackageBodies": true,
    "includePatterns": ["PKG_*"],
    "excludePatterns": ["PKG_TEST_*", "PKG_DEBUG_*"]
  }
}
```

Puis simplement :

```bash
codengine extract-oracle -c "..."
```

## Exemples d'utilisation

### Extraction pour Git

```bash
# Extraire dans un dossier versionné
codengine extract-oracle -c "..." -o ./database/packages

# Versionner
cd ./database/packages
git add *.sql
git commit -m "Mise à jour des packages Oracle"
```

### Extraction sélective

```bash
# Seulement les packages métier
codengine extract-oracle -c "..." \
  -i "PKG_BUSINESS_*,PKG_CORE_*" \
  -e "*_LEGACY,*_DEPRECATED"
```

### Extraction headers uniquement

```bash
# Pour documentation API
codengine extract-oracle -c "..." --no-bodies -o ./docs/api
```

## Dépannage

### Erreur de connexion

```
Échec de la connexion à Oracle.
```

Vérifiez :
- La chaîne de connexion
- Les identifiants
- L'accessibilité du serveur (firewall, VPN)
- Le port Oracle (par défaut 1521)

### Aucun package trouvé

Vérifiez :
- Le schéma spécifié existe
- L'utilisateur a les droits sur `ALL_OBJECTS` et `ALL_SOURCE`
- Les patterns d'inclusion/exclusion

### Droits requis

```sql
GRANT SELECT ON ALL_OBJECTS TO user;
GRANT SELECT ON ALL_SOURCE TO user;
```

Ou utiliser un utilisateur avec le rôle `SELECT_CATALOG_ROLE`.

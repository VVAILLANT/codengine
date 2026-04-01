# Formatage PL/SQL Oracle

## Vue d'ensemble

La commande `format-oracle` formate les fichiers `.sql` contenant du code PL/SQL pour améliorer la lisibilité :

- **Indentation cohérente** des blocs (`BEGIN`/`END`, `IF`/`END IF`, `LOOP`/`END LOOP`, etc.)
- **Normalisation des mots-clés** en majuscules (optionnel)
- **Nettoyage des espaces** : suppression des trailing whitespace, consolidation des lignes vides
- **Préservation de l'intégrité** : seule l'indentation est modifiée, jamais le contenu du code

## Utilisation

### Commande de base

```bash
# Formater avec la configuration du fichier codengine.config.json
codengine format-oracle --config

# Formater un répertoire spécifique
codengine format-oracle C:\Projects\Database\PACKAGE
```

### Options

| Option | Description | Défaut |
|--------|-------------|--------|
| `path` | Répertoire contenant les fichiers `.sql` | `oracle.outputDirectory` de la config |
| `--dry-run` | Afficher les changements sans modifier les fichiers | false |
| `--backup` | Créer un fichier `.bak` avant modification | false |
| `--indent-size` | Nombre d'espaces par niveau d'indentation | 4 |
| `--uppercase-keywords` | Mettre les mots-clés PL/SQL en majuscules | true |
| `--config` | Utiliser les valeurs de la section `oracle` de `codengine.config.json` | false |

### Exemples

```bash
# Prévisualiser les changements (aucune modification)
codengine format-oracle --config --dry-run

# Créer un backup avant modification
codengine format-oracle --config --backup

# Indentation de 2 espaces
codengine format-oracle --config --indent-size 2

# Garder la casse originale des mots-clés
codengine format-oracle --config --uppercase-keywords false
```

## Configuration

Dans `codengine.config.json`, section `oracle.format` :

```json
{
  "oracle": {
    "outputDirectory": "C:/Projects/GIT/MROAD/Database/PACKAGE",
    "format": {
      "indentSize": 4,
      "uppercaseKeywords": true,
      "maxConsecutiveBlankLines": 1,
      "trimTrailingWhitespace": true
    }
  }
}
```

| Option | Type | Défaut | Description |
|--------|------|--------|-------------|
| `indentSize` | int | `4` | Nombre d'espaces par niveau d'indentation |
| `uppercaseKeywords` | boolean | `true` | Mettre les mots-clés PL/SQL en majuscules |
| `maxConsecutiveBlankLines` | int | `1` | Nombre maximal de lignes vides consécutives conservées |
| `trimTrailingWhitespace` | boolean | `true` | Supprimer les espaces en fin de ligne |

### Priorité des valeurs

Les options CLI ont toujours priorité sur la configuration :

```bash
# Utilise la config mais override l'indentation
codengine format-oracle --config --indent-size 2
```

## Blocs PL/SQL gérés

Le formateur reconnaît et indente correctement les structures suivantes :

| Bloc | Ouverture | Fermeture | Comportement |
|------|-----------|-----------|-------------|
| Package | `CREATE OR REPLACE PACKAGE [BODY] ... AS/IS` | `END nom;` | Indente le contenu du package |
| Procedure/Function | `PROCEDURE/FUNCTION ... IS/AS` | `END nom;` | Indente déclarations + corps |
| BEGIN/END | `BEGIN` | `END;` / `END nom;` | Indente le corps |
| IF/THEN | `IF ... THEN` | `END IF;` | Indente chaque branche |
| ELSIF/ELSE | `ELSIF ... THEN` / `ELSE` | — | Revient au niveau du IF |
| LOOP | `LOOP` / `FOR ... LOOP` / `WHILE ... LOOP` | `END LOOP;` | Indente le corps |
| CASE | `CASE` | `END CASE;` | Indente les WHEN |
| EXCEPTION | `EXCEPTION` | (fermé par le `END;` parent) | Revient au niveau du BEGIN |
| WHEN (exception) | `WHEN ... THEN` | — | Indente le handler |

## Exemple de résultat

### Avant formatage

```sql
CREATE OR REPLACE PACKAGE BODY PKG_USERS AS
PROCEDURE create_user(p_name IN VARCHAR2, p_email IN VARCHAR2) IS
v_count NUMBER;
BEGIN
SELECT COUNT(*) INTO v_count FROM users WHERE email = p_email;
IF v_count > 0 THEN
RAISE_APPLICATION_ERROR(-20001, 'Email exists');
ELSE
INSERT INTO users(name, email) VALUES(p_name, p_email);
COMMIT;
END IF;
EXCEPTION
WHEN OTHERS THEN
ROLLBACK;
RAISE;
END create_user;
END PKG_USERS;
```

### Après formatage

```sql
CREATE OR REPLACE PACKAGE BODY PKG_USERS AS

    PROCEDURE create_user(p_name IN VARCHAR2, p_email IN VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM users WHERE email = p_email;

        IF v_count > 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'Email exists');
        ELSE
            INSERT INTO users(name, email) VALUES(p_name, p_email);
            COMMIT;
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            RAISE;
    END create_user;

END PKG_USERS;
```

## Garde-fous

### Vérification d'intégrité

Après chaque formatage, le formateur vérifie que le contenu non-whitespace du fichier n'a pas changé. Si l'intégrité est compromise, le fichier est **ignoré** et un message d'erreur est affiché :

```
  ✗ PKG_PROBLEME.sql — intégrité compromise, fichier ignoré
```

### Zones protégées

Le formateur ne modifie **jamais** le contenu à l'intérieur de :

| Zone | Exemple | Raison |
|------|---------|--------|
| String literals | `'BEGIN ... END;'` | Contenu utilisateur |
| Commentaires ligne | `-- END of processing` | Documentation |
| Commentaires bloc | `/* IF ... THEN ... */` | Documentation |
| Identifiants quotés | `"BEGIN"` (nom de colonne) | Identifiant Oracle |

### Mode dry-run

Utilisez `--dry-run` pour prévisualiser les changements avant de les appliquer :

```bash
codengine format-oracle --config --dry-run
```

```
Répertoire : C:\Projects\GIT\MROAD\Database\PACKAGE
Fichiers   : 12 fichier(s) .sql
Indent     : 4 espaces
Keywords   : MAJUSCULES
Mode dry-run : aucune modification ne sera appliquée

  ~ PKG_USERS.sql (85 → 82 lignes)
  ~ PKG_ORDERS.sql (120 → 115 lignes)
  ─ PKG_CONFIG.sql (déjà formaté)

Résultat : 2 formaté(s), 1 inchangé(s), 0 erreur(s)
```

### Mode backup

Utilisez `--backup` pour créer une copie `.bak` avant toute modification :

```bash
codengine format-oracle --config --backup
```

## Workflow recommandé

```bash
# 1. Extraire les packages Oracle
codengine extract-oracle --config

# 2. Prévisualiser le formatage
codengine format-oracle --config --dry-run

# 3. Formater
codengine format-oracle --config

# 4. Vérifier dans Git
git diff
```

## Mots-clés PL/SQL reconnus

Lorsque `uppercaseKeywords` est activé, les mots-clés suivants sont mis en majuscules :

`CREATE`, `OR`, `REPLACE`, `PACKAGE`, `BODY`, `AS`, `IS`, `BEGIN`, `END`, `IF`, `THEN`, `ELSIF`, `ELSE`, `LOOP`, `WHILE`, `FOR`, `EXIT`, `WHEN`, `CASE`, `IN`, `OUT`, `NOCOPY`, `RETURN`, `CURSOR`, `OPEN`, `FETCH`, `CLOSE`, `INTO`, `BULK`, `COLLECT`, `LIMIT`, `FORALL`, `EXCEPTION`, `RAISE`, `DECLARE`, `TYPE`, `SUBTYPE`, `RECORD`, `TABLE`, `INDEX`, `BY`, `OF`, `REF`, `ROWTYPE`, `CONSTANT`, `DEFAULT`, `NOT`, `NULL`, `TRUE`, `FALSE`, `PROCEDURE`, `FUNCTION`, `PRAGMA`, `SELECT`, `FROM`, `WHERE`, `AND`, `SET`, `UPDATE`, `INSERT`, `DELETE`, `VALUES`, `COMMIT`, `ROLLBACK`, `SAVEPOINT`, `MERGE`, `USING`, `MATCHED`, `JOIN`, `LEFT`, `RIGHT`, `INNER`, `OUTER`, `ON`, `CROSS`, `GROUP`, `ORDER`, `HAVING`, `DISTINCT`, `UNION`, `ALL`, `EXISTS`, `BETWEEN`, `LIKE`, `WITH`, `VARCHAR2`, `NUMBER`, `INTEGER`, `PLS_INTEGER`, `BINARY_INTEGER`, `BOOLEAN`, `DATE`, `TIMESTAMP`, `CLOB`, `BLOB`, `RAW`, `CHAR`, `EXECUTE`, `IMMEDIATE`, `OTHERS`, `SQLERRM`, `SQLCODE`, `GRANT`, `REVOKE`, `TO`, `PUBLIC`, `SYNONYM`

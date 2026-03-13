# Rapports

## Formats disponibles

Codengine supporte trois formats de rapport :

| Format | Option | Description |
|--------|--------|-------------|
| Console | `-f console` | Affichage coloré dans le terminal (défaut) |
| JSON | `-f json` | Export structuré pour intégration CI/CD |
| HTML | `-f html` | Rapport visuel pour partage |

## Rapport Console

### Utilisation

```bash
codengine analyze ./src
codengine analyze ./src -v  # Verbose : affiche les suggestions
```

### Exemple de sortie

```
Codengine v1.0.0
Analyse de: E:\MonProjet\src

═══════════════════════════════════════════════════════════════
                    CODENGINE - ANALYSE TERMINÉE
═══════════════════════════════════════════════════════════════

  Source analysée : ./src
  Fichiers analysés : 45
  Durée : 0.23s

  RÉSUMÉ:
    Critiques : 0
    Erreurs   : 3
    Warnings  : 12

───────────────────────────────────────────────────────────────
  VIOLATIONS:
───────────────────────────────────────────────────────────────

  E:\MonProjet\src\Services\UserService.cs
    X [COD001] Ligne 42: La variable 'user' issue de FirstOrDefault()
      doit être vérifiée pour null avant utilisation.
      > var user = users.FirstOrDefault(u => u.Id == id);
      Suggestion: Ajouter: if (user == null) return; // ou gérer le cas null

═══════════════════════════════════════════════════════════════
  ÉCHEC: 15 violation(s) détectée(s)
```

### Codes couleur

| Couleur | Signification |
|---------|---------------|
| Rouge (X) | Erreur / Critical |
| Jaune (!) | Warning |
| Gris (i) | Info |
| Cyan | Chemin de fichier |
| Vert | Suggestion |

---

## Rapport JSON

### Utilisation

```bash
# Afficher dans la console
codengine analyze ./src -f json

# Sauvegarder dans un fichier
codengine analyze ./src -f json -o rapport.json
```

### Structure

```json
{
  "summary": {
    "sourcePath": "./src",
    "analyzedAt": "2024-01-15T10:30:45.123Z",
    "durationMs": 234,
    "filesAnalyzed": 45,
    "totalViolations": 15,
    "criticals": 0,
    "errors": 3,
    "warnings": 12,
    "hasErrors": true
  },
  "violations": [
    {
      "ruleId": "COD001",
      "ruleName": "NullCheckAfterOrDefault",
      "message": "La variable 'user' issue de FirstOrDefault() doit être vérifiée...",
      "filePath": "E:\\MonProjet\\src\\Services\\UserService.cs",
      "line": 42,
      "column": 9,
      "severity": "Error",
      "codeSnippet": "var user = users.FirstOrDefault(u => u.Id == id);",
      "suggestedFix": "Ajouter: if (user == null) return;"
    }
  ]
}
```

### Champs du résumé

| Champ | Type | Description |
|-------|------|-------------|
| `sourcePath` | string | Chemin analysé |
| `analyzedAt` | datetime | Date/heure de l'analyse |
| `durationMs` | int | Durée en millisecondes |
| `filesAnalyzed` | int | Nombre de fichiers analysés |
| `totalViolations` | int | Nombre total de violations |
| `criticals` | int | Violations Critical |
| `errors` | int | Violations Error |
| `warnings` | int | Violations Warning |
| `hasErrors` | bool | `true` si errors > 0 ou criticals > 0 |

### Champs des violations

| Champ | Type | Description |
|-------|------|-------------|
| `ruleId` | string | Identifiant de la règle |
| `ruleName` | string | Nom de la règle |
| `message` | string | Description de la violation |
| `filePath` | string | Chemin du fichier |
| `line` | int | Numéro de ligne |
| `column` | int | Numéro de colonne |
| `severity` | string | Info, Warning, Error, Critical |
| `codeSnippet` | string | Extrait de code (si activé) |
| `suggestedFix` | string | Suggestion de correction |

---

## Rapport HTML

### Utilisation

```bash
codengine analyze ./src -f html -o rapport.html
```

### Caractéristiques

- Design responsive
- Couleurs par sévérité
- Groupement par fichier
- Extraits de code formatés
- Suggestions de correction (si verbose)
- Statistiques visuelles

### Exemple

Le rapport HTML inclut :

1. **En-tête** avec le nom du projet
2. **Résumé** avec les statistiques clés
3. **Compteurs visuels** (Critiques, Erreurs, Warnings, Total)
4. **Liste des violations** groupées par fichier
5. **Extraits de code** avec coloration syntaxique
6. **Pied de page** avec la date de génération

### Personnalisation

Le CSS est intégré dans le fichier HTML. Pour personnaliser :

1. Générer le rapport HTML
2. Modifier le CSS dans la balise `<style>`
3. Ou créer un reporter personnalisé (voir [Architecture](./11-architecture.md))

---

## Options de rapport

### Inclure les extraits de code

Par défaut activé. Pour désactiver dans la configuration :

```json
{
  "reporting": {
    "includeCodeSnippets": false
  }
}
```

### Mode verbose

Affiche les suggestions de correction :

```bash
codengine analyze ./src -v
```

Ou dans la configuration :

```json
{
  "reporting": {
    "verbose": true
  }
}
```

### Chemin de sortie

```bash
# Fichier spécifique
codengine analyze ./src -f json -o ./reports/analysis.json

# Avec date
codengine analyze ./src -f json -o "./reports/analysis-$(date +%Y%m%d).json"
```

---

## Intégration

### Traitement du JSON

**PowerShell** :
```powershell
$report = Get-Content rapport.json | ConvertFrom-Json
$report.violations | Where-Object { $_.severity -eq "Error" } | Format-Table
```

**Bash + jq** :
```bash
# Compter les erreurs
jq '.summary.errors' rapport.json

# Lister les fichiers avec erreurs
jq -r '.violations[] | select(.severity == "Error") | .filePath' rapport.json | sort -u
```

**Python** :
```python
import json

with open('rapport.json') as f:
    report = json.load(f)

errors = [v for v in report['violations'] if v['severity'] == 'Error']
for error in errors:
    print(f"{error['filePath']}:{error['line']} - {error['message']}")
```

### Publication HTML

```bash
# Copier vers un serveur web
codengine analyze ./src -f html -o /var/www/html/codengine/latest.html

# Avec horodatage
DATE=$(date +%Y%m%d-%H%M%S)
codengine analyze ./src -f html -o "/var/www/html/codengine/report-$DATE.html"
```

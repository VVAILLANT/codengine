# Intégration CI/CD

## Vue d'ensemble

Codengine peut être intégré dans vos pipelines CI/CD pour :
- Bloquer les builds avec des violations critiques
- Générer des rapports d'analyse
- Suivre l'évolution de la qualité du code

## GitHub Actions

### Analyse basique

```yaml
name: Code Analysis

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  analyze:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Install Codengine
        run: |
          git clone https://github.com/your-org/codengine.git /tmp/codengine
          dotnet build /tmp/codengine -c Release
          echo "/tmp/codengine/src/Codengine.Cli/bin/Release/net9.0" >> $GITHUB_PATH

      - name: Run Analysis
        run: codengine analyze ./src

      - name: Upload Report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: codengine-report
          path: codengine-report.json
```

### Avec rapport HTML

```yaml
jobs:
  analyze:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build Codengine
        run: |
          git clone https://github.com/your-org/codengine.git /tmp/codengine
          dotnet publish /tmp/codengine/src/Codengine.Cli -c Release -o /tmp/codengine-bin

      - name: Run Analysis
        run: |
          /tmp/codengine-bin/Codengine.Cli analyze ./src -f html -o ./codengine-report.html
        continue-on-error: true

      - name: Upload HTML Report
        uses: actions/upload-artifact@v4
        with:
          name: codengine-html-report
          path: codengine-report.html

      - name: Check for Errors
        run: |
          /tmp/codengine-bin/Codengine.Cli analyze ./src
```

### Commentaire sur Pull Request

```yaml
jobs:
  analyze:
    runs-on: ubuntu-latest
    permissions:
      pull-requests: write

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Run Analysis
        id: analysis
        run: |
          # Build et exécuter Codengine
          dotnet build /path/to/codengine -c Release
          dotnet run --project /path/to/codengine/src/Codengine.Cli -- \
            analyze ./src -f json -o report.json || true

          # Extraire les statistiques
          ERRORS=$(jq '.summary.errors' report.json)
          WARNINGS=$(jq '.summary.warnings' report.json)
          echo "errors=$ERRORS" >> $GITHUB_OUTPUT
          echo "warnings=$WARNINGS" >> $GITHUB_OUTPUT

      - name: Comment PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const errors = ${{ steps.analysis.outputs.errors }};
            const warnings = ${{ steps.analysis.outputs.warnings }};

            let body = '## Codengine Analysis\n\n';
            if (errors > 0) {
              body += `❌ **${errors} error(s)** found\n`;
            }
            if (warnings > 0) {
              body += `⚠️ **${warnings} warning(s)** found\n`;
            }
            if (errors === 0 && warnings === 0) {
              body += '✅ No issues found!\n';
            }

            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: body
            });
```

---

## Azure DevOps

Deux pipelines sont disponibles dans le dossier `pipelines/` du repo Codengine.

---

### Pipeline 1 — Validation des PR du repo Codengine

**Fichier** : `pipelines/pr-validation.yml`

Déclenché automatiquement sur chaque PR vers `master`. Exécute les tests unitaires et analyse le code source de Codengine avec lui-même.

**Ce qu'il fait :**
1. Build du projet en Release
2. Exécution des tests xUnit
3. Analyse Codengine sur `./src`
4. Commentaire automatique sur la PR (✅ OK ou ❌ erreurs)
5. Blocage du merge si erreurs détectées

---

### Pipeline 2 — Validation des PR d'un projet externe (ex: MROAD)

**Fichier** : `pipelines/mroad-pr-validation.yml`
À copier dans le repo cible (ex: `.azure/pr-validation.yml`).

**Caractéristiques :**
- `checkout: none` — ne clone **pas** le repo entier
- Télécharge uniquement les fichiers `.cs` modifiés par la PR via l'API Azure DevOps
- Clone et build Codengine depuis GitHub avec **cache NuGet et build** (invalidé automatiquement à chaque nouvelle version)
- Ne se déclenche pas si seuls des fichiers `.yml`/`.yaml` sont modifiés
- Commentaire sur la PR avec version Codengine, commit et lien vers le rapport
- Mode manuel disponible (smoke test : build + `list-rules`)

**Prérequis :**
- Activer **"Allow scripts to access the OAuth token"** dans les paramètres de la pipeline
- Configurer une **Branch Policy** sur la branche cible avec `Requirement: Required`

**Règles actives par défaut :**

| Règle | Description | Sévérité |
|-------|-------------|----------|
| COD001 | NullCheckAfterSingleOrDefault | Error |
| COD002 | EmptyListBeforeContains (ORM uniquement) | Error |

Pour activer des règles supplémentaires, retirer les IDs de l'option `-d` dans le step `Codengine analyze` :

```yaml
dotnet "$(CODENGINE_DLL)" analyze "$(CHANGED_DIR)" -f json -o "$(REPORT_PATH)" -d COD003,COD004,...
```

**Comportement de la pipeline rouge :**
Une croix rouge indique que Codengine a détecté des erreurs (comportement intentionnel pour bloquer le merge). Ce n'est pas un crash de la pipeline.

---

## GitLab CI

```yaml
stages:
  - analyze

codengine:
  stage: analyze
  image: mcr.microsoft.com/dotnet/sdk:9.0
  script:
    - git clone https://github.com/your-org/codengine.git /tmp/codengine
    - dotnet publish /tmp/codengine/src/Codengine.Cli -c Release -o /tmp/codengine-bin
    - /tmp/codengine-bin/Codengine.Cli analyze ./src -f json -o codengine-report.json
  artifacts:
    paths:
      - codengine-report.json
    reports:
      codequality: codengine-report.json
  allow_failure: true
```

---

## Jenkins

### Jenkinsfile

```groovy
pipeline {
    agent any

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Install Codengine') {
            steps {
                sh '''
                    git clone https://github.com/your-org/codengine.git /tmp/codengine
                    dotnet publish /tmp/codengine/src/Codengine.Cli -c Release -o /tmp/codengine-bin
                '''
            }
        }

        stage('Analyze') {
            steps {
                sh '/tmp/codengine-bin/Codengine.Cli analyze ./src -f json -o codengine-report.json'
            }
            post {
                always {
                    archiveArtifacts artifacts: 'codengine-report.json', fingerprint: true
                }
            }
        }

        stage('Quality Gate') {
            steps {
                script {
                    def report = readJSON file: 'codengine-report.json'
                    if (report.summary.errors > 0) {
                        error "Codengine found ${report.summary.errors} error(s)"
                    }
                }
            }
        }
    }
}
```

---

## Configuration recommandée pour CI

Créez un fichier `codengine.ci.json` :

```json
{
  "sourcePath": "./src",
  "excludePatterns": [
    "**/bin/**",
    "**/obj/**",
    "**/Tests/**",
    "**/*.Designer.cs"
  ],
  "rules": {
    "COD001": { "enabled": true },
    "COD002": { "enabled": true },
    "COD005": { "enabled": true },
    "COD006": { "enabled": false },
    "COD007": { "enabled": false }
  },
  "reporting": {
    "format": "json",
    "includeCodeSnippets": true
  },
  "failOnError": true,
  "failOnWarning": false
}
```

Puis dans le pipeline :

```bash
codengine analyze ./src -c codengine.ci.json
```

---

## Codes de retour

| Code | Signification | Action CI |
|------|---------------|-----------|
| 0 | Pas d'erreurs | Build OK |
| 1 | Erreurs détectées | Build Failed |

Pour ignorer les erreurs et toujours générer le rapport :

```bash
codengine analyze ./src || true  # Continue même en cas d'erreur
```

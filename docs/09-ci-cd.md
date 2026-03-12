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

### Pipeline YAML

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'

  - script: |
      git clone https://github.com/your-org/codengine.git $(Agent.TempDirectory)/codengine
      dotnet publish $(Agent.TempDirectory)/codengine/src/Codengine.Cli -c Release -o $(Agent.TempDirectory)/codengine-bin
    displayName: 'Install Codengine'

  - script: |
      $(Agent.TempDirectory)/codengine-bin/Codengine.Cli analyze ./src -f json -o $(Build.ArtifactStagingDirectory)/codengine-report.json
    displayName: 'Run Codengine Analysis'
    continueOnError: true

  - task: PublishBuildArtifacts@1
    inputs:
      PathtoPublish: '$(Build.ArtifactStagingDirectory)/codengine-report.json'
      ArtifactName: 'CodengineReport'
    displayName: 'Publish Report'

  - script: |
      $(Agent.TempDirectory)/codengine-bin/Codengine.Cli analyze ./src
    displayName: 'Check for Errors (Fail on Error)'
```

### Avec Quality Gate

```yaml
steps:
  - script: |
      RESULT=$($(Agent.TempDirectory)/codengine-bin/Codengine.Cli analyze ./src -f json -o report.json; echo $?)
      ERRORS=$(jq '.summary.errors' report.json)

      if [ "$ERRORS" -gt 0 ]; then
        echo "##vso[task.logissue type=error]$ERRORS error(s) found by Codengine"
        echo "##vso[task.complete result=Failed;]Quality gate failed"
      fi
    displayName: 'Quality Gate'
```

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

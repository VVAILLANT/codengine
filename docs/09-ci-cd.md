# Intégration CI/CD

## Azure DevOps

Le pipeline `pipelines/codengine-pr-validation.yml` est à copier dans le repo cible (ex: `.azure/pr-validation.yml`).

---

### Pipeline — Validation des PR d'un projet externe

**Fichier** : `pipelines/codengine-pr-validation.yml`

**Ce qu'il fait :**
1. Télécharge uniquement les fichiers `.cs` modifiés par la PR via l'API Azure DevOps (`checkout: none` — le repo n'est pas cloné)
2. Clone et build Codengine depuis GitHub avec cache NuGet et build (invalidé automatiquement à chaque nouvelle version)
3. Analyse Codengine sur les fichiers modifiés uniquement
4. Commentaire automatique sur la PR avec version Codengine, commit et lien vers le rapport
5. Blocage du merge si erreurs détectées

**Caractéristiques :**
- Ne se déclenche pas si seuls des fichiers `.yml`/`.yaml` sont modifiés
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

## Codes de retour

| Code | Signification | Action CI |
|------|---------------|-----------|
| 0 | Pas d'erreurs | Build OK |
| 1 | Erreurs détectées | Build Failed |

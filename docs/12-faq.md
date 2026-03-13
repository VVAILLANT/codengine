# FAQ et dépannage

## Questions fréquentes

### Général

#### Q: Quelle est la différence avec SonarQube ?

**R:** Codengine est un outil léger et personnalisable :
- Pas de serveur requis
- Règles métier spécifiques
- Facile à étendre
- Intégration Oracle native
- Auto-fix intégré

SonarQube est plus complet mais plus lourd à déployer.

#### Q: Quels langages sont supportés ?

**R:** Actuellement :
- **C#** : Analyse complète via Roslyn
- **PL/SQL** : Extraction des packages Oracle (analyse future)

#### Q: Codengine modifie-t-il mon code ?

**R:** Dans deux cas :
- La commande `fix` applique des corrections automatiques.
- La commande `analyze --tag` ajoute des commentaires `// codengine[RULE]` en fin de ligne sur les violations. Utilisez `analyze --untag` pour les retirer.

La commande `analyze` sans ces options est en lecture seule.

---

### Installation

#### Q: "dotnet n'est pas reconnu"

**R:** Le SDK .NET n'est pas dans le PATH. Solutions :
1. Redémarrer le terminal après installation
2. Utiliser le chemin complet : `"C:\Program Files\dotnet\dotnet.exe"`
3. Ajouter au PATH : `C:\Program Files\dotnet`

#### Q: Quelle version de .NET est requise ?

**R:** .NET 8.0 minimum, recommandé .NET 9.0.

```bash
dotnet --version
```

#### Q: Erreur de restauration NuGet

**R:** Vérifiez votre connexion internet et les sources NuGet :

```bash
dotnet nuget list source
dotnet restore --verbosity detailed
```

---

### Analyse

#### Q: L'analyse est lente

**R:** Optimisations possibles :
1. Exclure les fichiers générés :
```json
{
  "excludePatterns": ["**/*.Designer.cs", "**/*.g.cs", "**/Migrations/**"]
}
```
2. Désactiver les règles non nécessaires
3. Augmenter la concurrence : `"maxConcurrency": 8`

#### Q: Faux positifs

**R:** Solutions :
1. Désactiver la règle globalement : `-d COD006`
2. Vérifier si le code peut être amélioré
3. Signaler le faux positif pour améliorer la règle

#### Q: Comment ignorer un fichier spécifique ?

**R:** Ajoutez-le aux patterns d'exclusion :
```json
{
  "excludePatterns": ["**/LegacyCode.cs", "**/Generated/**"]
}
```

#### Q: Comment ignorer une ligne spécifique ?

**R:** Cette fonctionnalité n'est pas encore implémentée. Prévue pour une version future avec des commentaires `// codengine-ignore`.

---

### Règles

#### Q: Comment désactiver une règle ?

**R:** Plusieurs méthodes :

1. Ligne de commande :
```bash
codengine analyze ./src -d COD006,COD007
```

2. Fichier de configuration :
```json
{
  "rules": {
    "COD006": { "enabled": false }
  }
}
```

#### Q: Comment créer une règle personnalisée ?

**R:** Voir [Développement de règles](./10-custom-rules.md).

#### Q: Pourquoi COD001 ne détecte pas mon cas ?

**R:** La règle cherche des patterns spécifiques. Vérifiez :
- Le code utilise bien `SingleOrDefault()`, `FirstOrDefault()`, `LastOrDefault()` ou `ElementAtOrDefault()`
- Le résultat est stocké dans une variable locale
- Il y a un accès membre (`variable.Propriété`) après sans null check

#### Q: COD001 signale une violation alors que j'ai un null check

**R:** Vérifiez l'ordre des conditions dans votre guard clause. Le null check doit être **en premier** dans un `||` pour que la règle le reconnaisse comme protecteur :

```csharp
// CORRECT — pas de violation
if (user == null || !user.IsActive) return;

// VIOLATION détectée — user.IsActive évalué avant user == null (risque NPE)
if (!user.IsActive || user == null) return;
```

Les patterns suivants sont également reconnus comme protecteurs :

- **Guard avec `continue`/`break`** : `if (item == null) continue;` dans une boucle
- **Bloc `else`** : `if (item == null) { throw ...; } else { item.Prop → safe }`
- **Ternaire** : `item != null ? item.Prop : default`

#### Q: COD001 signale une violation sur une variable de boucle `foreach`

**R:** Cela peut arriver si une variable issue de `FirstOrDefault()` et une variable de boucle `foreach` portent le même nom dans la même méthode. La règle distingue désormais ces deux cas — mettez à jour vers la dernière version si le faux positif persiste.

---

### Auto-fix

#### Q: Pourquoi ma violation n'est pas corrigée ?

**R:** Seules certaines règles ont un auto-fixer :
- COD001 : Oui
- COD003 : Oui
- COD005 : Oui
- Autres : Non (actuellement)

#### Q: Le fix a cassé mon code

**R:** Les fixes sont génériques. Toujours :
1. Utiliser `--dry-run` d'abord
2. Versionner avant de fixer
3. Revoir les modifications
4. Personnaliser si nécessaire

---

### Oracle

#### Q: Erreur de connexion Oracle

**R:** Vérifiez :
1. Format de la chaîne de connexion :
```
Data Source=//host:port/service;User Id=user;Password=pass;
```
2. Accessibilité réseau (firewall, VPN)
3. Port Oracle (défaut: 1521)
4. Identifiants

#### Q: "ORA-01017: invalid username/password"

**R:** Identifiants incorrects. Vérifiez le username et password.

#### Q: Aucun package trouvé

**R:** Vérifiez :
1. Le schéma existe : `-s NOM_SCHEMA`
2. L'utilisateur a les droits :
```sql
GRANT SELECT ON ALL_OBJECTS TO user;
GRANT SELECT ON ALL_SOURCE TO user;
```
3. Les patterns d'inclusion/exclusion

---

### CI/CD

#### Q: Le build échoue toujours

**R:** Codengine retourne le code 1 s'il y a des erreurs. Options :
1. Corriger les violations
2. Désactiver `failOnError` :
```json
{ "failOnError": false }
```
3. Utiliser `|| true` dans le script (non recommandé)

#### Q: Comment obtenir le rapport en artifact ?

**R:** Générez un fichier et uploadez-le :
```yaml
- run: codengine analyze ./src -f json -o report.json
- uses: actions/upload-artifact@v4
  with:
    name: codengine-report
    path: report.json
```

---

## Messages d'erreur

### "Le nom de type ou d'espace de noms 'X' est introuvable"

Le code analysé a des dépendances non résolues. C'est normal pour l'analyse syntaxique. Si vous avez besoin de l'analyse sémantique, assurez-vous que le projet compile.

### "Index was outside the bounds of the array"

Bug potentiel dans une règle. Créez une issue avec le code minimal qui reproduit l'erreur.

### "Object reference not set to an instance"

Le `SemanticModel` peut être `null`. Les règles doivent gérer ce cas :
```csharp
if (context.SemanticModel == null) yield break;
```

---

## Support

### Signaler un bug

1. Ouvrir une issue sur GitHub
2. Inclure :
   - Version de Codengine
   - Version de .NET
   - Message d'erreur complet
   - Code minimal pour reproduire

### Demander une fonctionnalité

Ouvrir une issue avec le tag "enhancement" décrivant :
- Le besoin métier
- Le comportement attendu
- Des exemples de code

### Contribuer

1. Fork le projet
2. Créer une branche
3. Implémenter et tester
4. Créer une Pull Request

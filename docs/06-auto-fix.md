# Auto-correction (Auto-fix)

## Vue d'ensemble

Codengine peut corriger automatiquement certaines violations de règles. Cette fonctionnalité permet de gagner du temps en appliquant des corrections standardisées.

## Règles avec auto-fix

| Règle | Description de la correction |
|-------|------------------------------|
| COD001 | Ajoute un null check après SingleOrDefault/FirstOrDefault |
| COD003 | Renomme les méthodes async pour terminer par "Async" |
| COD005 | Ajoute un rethrow dans les blocs catch vides |

## Utilisation

### Voir les corrections disponibles (dry-run)

```bash
codengine fix ./src --dry-run
```

Sortie :
```
Codengine v1.0.0
Correction de: E:\MonProjet\src
Mode dry-run: aucune modification ne sera effectuée.

Analyse en cours...
Trouvé 5 violation(s).

3 violation(s) peuvent être corrigées automatiquement.

  [COD001] Services/UserService.cs:42 - La variable 'user' issue de FirstOrDefault()...
  [COD005] Handlers/ErrorHandler.cs:15 - Bloc catch vide pour 'Exception'...
  [COD003] Services/DataService.cs:28 - La méthode async 'GetData' devrait...
```

### Appliquer les corrections

```bash
codengine fix ./src
```

Sortie :
```
Codengine v1.0.0
Correction de: E:\MonProjet\src

Analyse en cours...
Trouvé 5 violation(s).

3 violation(s) peuvent être corrigées automatiquement.
Application des corrections...

  Corrigé: Services/UserService.cs (1 correction(s))
  Corrigé: Handlers/ErrorHandler.cs (1 correction(s))
  Corrigé: Services/DataService.cs (1 correction(s))

Résumé: 3 correction(s) appliquée(s), 0 échec(s).
```

### Corriger des règles spécifiques

```bash
# Corriger seulement COD001 et COD005
codengine fix ./src -r COD001,COD005
```

## Détail des corrections

### COD001 - NullCheckAfterSingleOrDefault

**Avant :**
```csharp
var user = users.FirstOrDefault(u => u.Id == id);
var name = user.Name;
```

**Après :**
```csharp
var user = users.FirstOrDefault(u => u.Id == id);
if (user == null) throw new InvalidOperationException("La valeur attendue est nulle.");
var name = user.Name;
```

### COD003 - AsyncMethodNaming

**Avant :**
```csharp
public async Task<User> GetUser(int id)
{
    return await _repository.FindAsync(id);
}

// Appels
var user = await service.GetUser(5);
```

**Après :**
```csharp
public async Task<User> GetUserAsync(int id)
{
    return await _repository.FindAsync(id);
}

// Appels (renommés aussi)
var user = await service.GetUserAsync(5);
```

### COD005 - EmptyCatchBlock

**Avant :**
```csharp
try
{
    DoSomething();
}
catch (Exception)
{
}
```

**Après :**
```csharp
try
{
    DoSomething();
}
catch (Exception ex)
{
    // TODO: Gérer correctement l'exception ou la logger
    throw; // Rethrow pour ne pas masquer l'erreur
}
```

## Bonnes pratiques

### 1. Toujours utiliser dry-run d'abord

```bash
codengine fix ./src --dry-run
```

Vérifiez les corrections proposées avant de les appliquer.

### 2. Versionner avant de corriger

```bash
git add .
git commit -m "Avant auto-fix Codengine"
codengine fix ./src
git diff  # Vérifier les changements
```

### 3. Relancer l'analyse après correction

```bash
codengine fix ./src
codengine analyze ./src  # Vérifier qu'il reste des violations
```

### 4. Personnaliser après auto-fix

Les corrections automatiques sont génériques. Vous pouvez les personnaliser :

- **COD001** : Remplacer `InvalidOperationException` par une exception métier
- **COD003** : Vérifier que tous les appels ont été renommés
- **COD005** : Ajouter un vrai logging à la place du TODO

## Limitations

- Les corrections sont appliquées fichier par fichier
- Certaines corrections complexes nécessitent une intervention manuelle
- Les renommages (COD003) sont basés sur du texte, pas sur l'analyse sémantique complète
- Faites toujours une revue de code après auto-fix

## Ajouter un nouveau fixer

Voir [Développement de règles](./10-custom-rules.md#créer-un-auto-fixer) pour créer vos propres fixers.

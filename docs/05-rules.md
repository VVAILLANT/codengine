# Règles d'analyse

## Vue d'ensemble

Codengine inclut 9 règles d'analyse pour le code C#, organisées par catégorie.

| Catégorie | Règles | Description |
|-----------|--------|-------------|
| NullSafety | COD001, COD002 | Prévention des NullReferenceException |
| ErrorHandling | COD005 | Gestion correcte des exceptions |
| Naming | COD003 | Conventions de nommage |
| Resources | COD004 | Gestion des ressources IDisposable |
| Maintainability | COD006, COD007 | Maintenabilité du code |
| Performance | COD008, COD009 | Optimisation des performances |

---

## COD001 - NullCheckAfterSingleOrDefault

**Catégorie** : NullSafety
**Sévérité** : Error
**Auto-fix** : Oui

### Description

Vérifie qu'après chaque appel à `SingleOrDefault()` ou `FirstOrDefault()`, le résultat est vérifié pour `null` avant utilisation.

### Problème

```csharp
// VIOLATION : Pas de null check
var user = users.FirstOrDefault(u => u.Id == id);
var name = user.Name;  // NullReferenceException si user est null
```

### Solution

```csharp
// CORRECT : Null check explicite
var user = users.FirstOrDefault(u => u.Id == id);
if (user == null)
{
    throw new InvalidOperationException("Utilisateur non trouvé");
}
var name = user.Name;

// CORRECT : Opérateur null-conditionnel
var user = users.FirstOrDefault(u => u.Id == id);
var name = user?.Name ?? "Inconnu";

// CORRECT : Pattern matching
var user = users.FirstOrDefault(u => u.Id == id);
if (user is null) return;
var name = user.Name;
```

---

## COD002 - EmptyListBeforeContains

**Catégorie** : NullSafety
**Sévérité** : Error
**Auto-fix** : Non

### Description

Vérifie qu'avant d'utiliser `liste.Contains()` dans un `.Where()`, la liste est vérifiée pour ne pas être `null` ou vide. Une liste vide dans un `Contains()` peut annuler le filtrage et produire des résultats inattendus.

### Problème

```csharp
// VIOLATION : Liste potentiellement vide dans un Where
public IEnumerable<User> GetUsers(List<int> ids)
{
    return Query<User>().Where(u => ids.Contains(u.Id));
    // Si ids est vide -> le filtrage est annulé -> résultats inattendus !
}

// VIOLATION : Même problème sur une collection en mémoire
var codes = listeSource.Select(i => i.Code).ToList();
var resultat = autreListe
    .Where(i => codes.Contains(i.Code))
    .ToList();
```

### Solution

```csharp
// CORRECT : Vérification avant utilisation
public IEnumerable<User> GetUsers(List<int> ids)
{
    if (ids == null || !ids.Any())
    {
        return Enumerable.Empty<User>();
    }
    return Query<User>().Where(u => ids.Contains(u.Id));
}

// CORRECT : Avec guard clause
public IEnumerable<User> GetUsers(List<int> ids)
{
    ArgumentNullException.ThrowIfNull(ids);
    if (ids.Count == 0) return Enumerable.Empty<User>();

    return Query<User>().Where(u => ids.Contains(u.Id));
}
```

---

## COD003 - AsyncMethodNaming

**Catégorie** : Naming
**Sévérité** : Warning
**Auto-fix** : Oui

### Description

Vérifie que les méthodes `async` ont un nom se terminant par `Async`.

### Problème

```csharp
// VIOLATION : Méthode async sans suffixe Async
public async Task<User> GetUser(int id)
{
    return await _repository.FindAsync(id);
}
```

### Solution

```csharp
// CORRECT : Suffixe Async
public async Task<User> GetUserAsync(int id)
{
    return await _repository.FindAsync(id);
}
```

### Exceptions

Les méthodes suivantes sont exclues de cette règle :
- `Main`
- `Dispose`
- `DisposeAsync`

---

## COD004 - DisposePattern

**Catégorie** : Resources
**Sévérité** : Warning
**Auto-fix** : Non

### Description

Vérifie que les objets `IDisposable` connus sont utilisés dans un bloc `using`.

### Types détectés

- Connexions : `SqlConnection`, `OracleConnection`, `DbConnection`
- Commandes : `SqlCommand`, `OracleCommand`, `DbCommand`
- Readers : `SqlDataReader`, `StreamReader`, `BinaryReader`
- Writers : `StreamWriter`, `BinaryWriter`
- Streams : `FileStream`, `MemoryStream`
- HTTP : `HttpClient`, `WebClient`
- Transactions : `SqlTransaction`, `DbTransaction`
- GDI+ : `Bitmap`, `Graphics`, `Font`, `Brush`, `Pen`

### Problème

```csharp
// VIOLATION : Pas de using
var connection = new SqlConnection(connectionString);
connection.Open();
// ... utilisation
connection.Close();  // Peut ne pas être appelé si exception
```

### Solution

```csharp
// CORRECT : Using statement
using var connection = new SqlConnection(connectionString);
connection.Open();
// ... utilisation
// Dispose automatique

// CORRECT : Using block
using (var connection = new SqlConnection(connectionString))
{
    connection.Open();
    // ... utilisation
}
```

---

## COD005 - EmptyCatchBlock

**Catégorie** : ErrorHandling
**Sévérité** : Error
**Auto-fix** : Oui

### Description

Détecte les blocs `catch` vides qui avalent silencieusement les exceptions.

### Problème

```csharp
// VIOLATION : Catch vide
try
{
    DoSomething();
}
catch (Exception)
{
    // L'exception est ignorée silencieusement
}
```

### Solution

```csharp
// CORRECT : Logger l'exception
try
{
    DoSomething();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Erreur lors de DoSomething");
    throw;
}

// CORRECT : Gérer l'exception
try
{
    DoSomething();
}
catch (SpecificException ex)
{
    return DefaultValue;
}

// CORRECT : Rethrow explicite
try
{
    DoSomething();
}
catch (Exception)
{
    // Nettoyage si nécessaire
    throw;
}
```

---

## COD006 - MagicNumber

**Catégorie** : Maintainability
**Sévérité** : Warning
**Auto-fix** : Non

### Description

Détecte les valeurs numériques littérales ("magic numbers") qui devraient être définies comme constantes nommées.

### Valeurs autorisées

Les valeurs suivantes sont ignorées : `-1`, `0`, `1`, `2`, `10`, `100`, `1000`

### Exclusions

- Déclarations de constantes
- Attributs
- Enums
- Index de tableaux
- Méthodes de test

### Problème

```csharp
// VIOLATION : Magic number
if (retryCount > 5)  // Que signifie 5 ?
{
    await Task.Delay(30000);  // Que signifie 30000 ?
}
```

### Solution

```csharp
// CORRECT : Constantes nommées
private const int MaxRetryCount = 5;
private const int RetryDelayMs = 30000;

if (retryCount > MaxRetryCount)
{
    await Task.Delay(RetryDelayMs);
}
```

---

## COD007 - LongMethod

**Catégorie** : Maintainability
**Sévérité** : Warning
**Auto-fix** : Non

### Description

Détecte les méthodes trop longues qui devraient être refactorisées.

### Seuils

- Maximum **50 lignes** par méthode
- Maximum **30 statements** par méthode

### Problème

```csharp
// VIOLATION : Méthode de 150 lignes
public void ProcessOrder(Order order)
{
    // 150 lignes de code...
}
```

### Solution

```csharp
// CORRECT : Découper en sous-méthodes
public void ProcessOrder(Order order)
{
    ValidateOrder(order);
    CalculateTotals(order);
    ApplyDiscounts(order);
    SaveOrder(order);
    SendNotifications(order);
}

private void ValidateOrder(Order order) { /* ... */ }
private void CalculateTotals(Order order) { /* ... */ }
// etc.
```

---

## COD008 - StringConcatenationInLoop

**Catégorie** : Performance
**Sévérité** : Warning
**Auto-fix** : Non

### Description

Détecte les concaténations de strings dans les boucles, ce qui cause des allocations mémoire excessives.

### Problème

```csharp
// VIOLATION : Concaténation dans une boucle
string result = "";
foreach (var item in items)
{
    result += item.Name + ", ";  // Crée une nouvelle string à chaque itération
}
```

### Solution

```csharp
// CORRECT : StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item.Name);
    sb.Append(", ");
}
var result = sb.ToString();

// CORRECT : String.Join
var result = string.Join(", ", items.Select(i => i.Name));
```

---

## COD009 - ToListInQuery

**Catégorie** : Performance
**Sévérité** : Warning
**Auto-fix** : Non

### Description

Détecte les appels `ToList()` ou `ToArray()` inutiles avant `Count()`, `Any()`, `First()`, etc.

### Problème

```csharp
// VIOLATION : ToList() inutile
var count = items.Where(x => x.IsActive).ToList().Count();
var hasItems = items.Where(x => x.IsActive).ToList().Any();
var first = items.Where(x => x.IsActive).ToList().First();
```

### Solution

```csharp
// CORRECT : Sans ToList()
var count = items.Where(x => x.IsActive).Count();
var hasItems = items.Where(x => x.IsActive).Any();
var first = items.Where(x => x.IsActive).First();
```

### Méthodes concernées

`Count()`, `Any()`, `All()`, `First()`, `FirstOrDefault()`, `Single()`, `SingleOrDefault()`, `Last()`, `LastOrDefault()`, `Min()`, `Max()`, `Sum()`, `Average()`, `Contains()`

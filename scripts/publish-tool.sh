#!/usr/bin/env bash
# Bumpe la version patch du .csproj, repack et réinstalle l'outil global codengine.
# Usage: ./scripts/publish-tool.sh

set -e

DOTNET="/c/Program Files/dotnet/dotnet.exe"
CSPROJ="src/Codengine.Cli/Codengine.Cli.csproj"
NUPKG_DIR="src/Codengine.Cli/nupkg"

# Lire la version actuelle
CURRENT=$( grep -o '<Version>[^<]*</Version>' "$CSPROJ" | sed 's/<[^>]*>//g' )
echo "Version actuelle : $CURRENT"

# Bumper le patch (ex: 1.0.3 -> 1.0.4)
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"
PATCH=$(( PATCH + 1 ))
NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "Nouvelle version : $NEW_VERSION"

# Mettre à jour le .csproj
sed -i "s|<Version>$CURRENT</Version>|<Version>$NEW_VERSION</Version>|" "$CSPROJ"

# Pack
"$DOTNET" pack src/Codengine.Cli -o "$NUPKG_DIR" 2>&1

# Réinstaller l'outil global
"$DOTNET" tool uninstall --global Codengine.Cli 2>/dev/null || true
"$DOTNET" tool install --global --add-source "$NUPKG_DIR" --ignore-failed-sources Codengine.Cli 2>&1

# Committer et pousser la nouvelle version
git add "$CSPROJ"
git commit -m "chore: bump Codengine.Cli version to $NEW_VERSION"
git push

echo ""
echo "Outil global mis a jour : codengine v$NEW_VERSION"

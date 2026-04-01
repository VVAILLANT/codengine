# Copilot Instructions

## Directives de projet
- After each set of validated code modifications (feature, bug fix, etc.) in the Codengine project, automatically run `bash scripts/publish-tool.sh` as specified in CLAUDE.md. This script increments the patch version in Codengine.Cli.csproj, repacks the .nupkg, and reinstalls the global tool `codengine`. Do NOT run this script if the user only asked for research or explanations without code changes.
# Repository Guidelines

## Project Structure & Modules

- Root contains the WPF app: `PatchGUIlite.csproj`, `App.xaml`, `MainWindow.xaml`, and code-behind `.cs` files.
- Build artefacts are generated under `bin/` and `obj/`; do not commit these.
- Keep new windows, dialogs, and controls grouped as XAML/`.cs` pairs in the project root unless a dedicated folder is introduced.

## Build, Test, and Run

- Build the project from Visual Studio (`Ctrl+Shift+B`) or with `dotnet build PatchGUIlite.csproj` from this directory.
- Run locally via Visual Studio (`F5`/`Start Debugging`) or `dotnet run --project PatchGUIlite.csproj`.
- If unit tests are added in a separate test project, run them with `dotnet test`.

## Coding Style & Naming

- Use C# conventions: 4-space indentation, no tabs.
- PascalCase for classes, methods, and public properties; camelCase for locals and private fields (prefix `_` if consistent with surrounding code).
- Name XAML elements meaningfully (e.g., `ApplyPatchButton`, `SourceFileTextBox`) and keep event handlers in the corresponding `.xaml.cs`.

## Testing Guidelines

- Prefer small, focused tests per feature; group them in a `*Tests` project (e.g., `PatchGUIlite.Tests`).
- Name tests clearly (e.g., `ApplyPatch_WhenInputInvalid_ShowsErrorMessage`).
- Aim to cover core patching logic and error handling paths first.

## Commit & Pull Requests

- Write commits in the imperative mood (e.g., `Add patch validation for empty input`).
- Keep pull requests focused; describe motivation, key changes, and any UI-impacting behavior.
- Link related issues where applicable and include screenshots for visible UI changes.

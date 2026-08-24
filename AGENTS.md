# Repository Guidelines

## Project Structure & Module Organization

This repository is a Unity 6 project (`6000.2.13f1`). Gameplay and shared online logic live in `Assets/Scripts`; authoritative models and engine code are under `Assets/Scripts/Online/Shared`. Unity EditMode tests are in `Assets/Tests/Editor`, with online tests in `Assets/Tests/Editor/Online`. Scenes, UI Toolkit files, art, and Unity Gaming Services definitions are kept in their corresponding `Assets/` subdirectories. The Cloud Code backend is `Backend/NavalCommand.CloudCode`, targeting .NET 9 and linking shared online C# files. Keep each Unity asset’s matching `.meta` file, and do not commit generated `Library/`, `Temp/`, `Logs/`, `obj/`, or `bin/` output.

## Build, Test, and Development Commands

- Open the repository root in Unity `6000.2.13f1` and use Play Mode for local gameplay checks.
- Run EditMode tests from Unity’s **Window > General > Test Runner**. For automation, use Unity’s batch runner, for example: `Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults/editmode.xml -quit`.
- Build the Cloud Code project with `dotnet build Backend/NavalCommand.CloudCode/NavalCommandOnline.csproj -c Release`.
- Publish a Cloud Code artifact with `dotnet publish Backend/NavalCommand.CloudCode/NavalCommandOnline.csproj -c Release -r linux-x64 --self-contained false`. Follow `ONLINE_PLATFORM.md` for Dashboard deployment and environment setup.

## Coding Style & Naming Conventions

Use four-space indentation and standard C# conventions: `PascalCase` for types, methods, and public members; `camelCase` for private fields and locals. Keep online rule logic deterministic and shared between Unity and Cloud Code. Use descriptive NUnit names such as `DuplicateAction_IsIdempotent`. Match existing Unity asset naming and preserve serialized field names unless a migration is intentional. No repository-wide formatter or linter is configured; compile-check changes in the relevant editor or .NET project.

## Testing Guidelines

Tests use NUnit through Unity Test Framework. Add EditMode coverage for gameplay, commander data, UI behavior, and authoritative validation under `Assets/Tests/Editor`. Run the full EditMode suite before submitting changes. Online flow, UGS integration, purchases, and reconnection require the configured development environment and should follow the release gates in `ONLINE_PLATFORM.md`.

## Commit & Pull Request Guidelines

The repository has no commits yet, so no existing convention is available. Use short, imperative subjects, for example `Validate stale online actions`, and keep each commit focused. Pull requests should describe behavior changes, list Unity and backend tests run, identify any UGS resource or environment changes, and include screenshots or captures for UI changes. Never commit credentials, store keys, or service-account files; production configuration belongs in the appropriate Unity Dashboard environment.

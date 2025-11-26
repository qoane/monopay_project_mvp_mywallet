# MonoPay Deployment: Script vs. Visual Studio Folder Publish

## Context
Two deployment approaches are in use:

1. **Automation script (PowerShell)** – pulls the repo, stops IIS sites targeting `C:\inetpub\wwwroot\MonoPay`, kills running `MonoPayAggregator.dll` processes, runs `dotnet publish api/MonoPayAggregator.csproj -c Release -o "C:\inetpub\wwwroot\MonoPay\api"`, copies `api/wwwroot` into the web root, and restarts the IIS sites.
2. **Visual Studio Folder publish profile (screenshot)** – publishes in Release configuration for `net6.0`, framework-dependent, portable target runtime, without single-file output, writing to the default `bin\Release\net6.0\publish` folder.

The API project is configured for .NET 6.0 in `api/MonoPayAggregator.csproj`.

## Key Differences
- **Output location**: the script publishes directly into the live IIS content directory (`C:\inetpub\wwwroot\MonoPay\api`), then copies static assets into `C:\inetpub\wwwroot\MonoPay`. The Visual Studio profile publishes to the build output folder (`bin\Release\net6.0\publish`), so files must be manually copied to IIS afterward.
- **Pre/post steps**: the script handles operational steps (git pull, IIS site stop/start, and terminating running `MonoPayAggregator.dll` processes). The publish profile only builds and copies files; it does not manage IIS or running processes.
- **Static file handling**: the script explicitly copies `api/wwwroot` content to the public root. The publish profile would leave static files inside the publish output unless an additional step copies them.
- **Execution environment**: both use Release configuration and the .NET 6.0 target framework; the script inherits default framework-dependent/portable settings, matching the profile’s deployment mode and runtime selection.

## Takeaway
Keep using the script for end-to-end server deployment (including IIS orchestration and static file copying). The Visual Studio profile is suitable for producing build artifacts, but an extra step is required to move them into the IIS path and recycle sites.

# TcAutomation

Reusable C# library for TwinCAT automation via the `TcSysManagerRM` COM API.

## Contents

- `Automation.cs` — Core wrapper around `ITcSysManager15`: project, system, routes, real-time, tasks, and I/O configuration.
- `MessageFilter.cs` — COM STA message filter (`IOleMessageFilter`) that retries rejected calls, required for reliable COM interop on the UI thread.

## Dependencies

### NuGet

```
Beckhoff.TwinCAT.Ads
```

### COM Reference

| Name | GUID |
|------|------|
| `TcSysManRMLib` | `7b75909c-bb93-4b6c-814b-6378d36c2c58` |

Add via Visual Studio → Add COM Reference, or in your `.csproj`:

```xml
<COMReference Include="TcSysManRMLib">
  <VersionMinor>0</VersionMinor>
  <VersionMajor>1</VersionMajor>
  <Guid>7b75909c-bb93-4b6c-814b-6378d36c2c58</Guid>
  <Lcid>0</Lcid>
  <WrapperTool>tlbimp</WrapperTool>
  <Isolated>false</Isolated>
  <EmbedInteropTypes>true</EmbedInteropTypes>
</COMReference>
```

## Usage in consuming projects

Add to your `.csproj` (adjust relative path as needed):

```xml
<ItemGroup>
  <Compile Include="..\libs\TcAutomation\Automation.cs" Link="TcAutomation\Automation.cs" />
  <Compile Include="..\libs\TcAutomation\MessageFilter.cs" Link="TcAutomation\MessageFilter.cs" />
</ItemGroup>
```

Then add `using TcAutomation;` to any file that uses `Automation`, `RouteInfo`, `CpuAffinity`, etc.

## Git submodule setup

Once this directory's contents are pushed to a standalone GitHub repo:

```sh
git submodule add <url> libs/TcAutomation
```

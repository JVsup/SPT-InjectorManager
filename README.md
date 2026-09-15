# Injector Manager

Injector Manager is a GUI addon for more convenient buying and searching of injectors.

GUI is somewhat customizable via F12.

It is probably-compatible-ish with modded stuff as long as new items are either injectors or stims. YMMV

## Building

Create an ignored `Directory.Build.props.user` beside `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <SptPath>C:\path\to\SPT</SptPath>
  </PropertyGroup>
</Project>
```

Build and create the release archive with PowerShell:

```powershell
./scripts/build-release.ps1
```

## Changelog

### 4.1.0

- Initial release for SPT 4.1.5.

## License

MIT

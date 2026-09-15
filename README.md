# Injector Manager

Injector Manager is a GUI addon for more convenient buying and searching of injectors.

GUI is somewhat customizable via F12.

It is probably-compatible-ish with modded stuff as long as new items are either injectors or stims. YMMV.

<img width="2508" height="1440" alt="{6B732EC0-27E4-4299-9F80-A03BE1EB29D5}" src="https://github.com/user-attachments/assets/4e30816a-cea9-4588-b0f1-86847a8b32b5" />

<img width="1141" height="883" alt="{9282F634-EABD-4F26-B594-44C6ACE0F984}" src="https://github.com/user-attachments/assets/2e873b81-f7e3-48d1-bbd2-a862f0144582" />

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

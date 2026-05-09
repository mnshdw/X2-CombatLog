# Combat Log - Xenonauts 2 mod

A QoL mod for [Xenonauts 2](https://store.steampowered.com/app/538030/Xenonauts_2/). 

Adds an expandable combat log at the bottom left of the screen showing more more information (like rolls, damage) about combat actions.

## Install

1. Download the latest `combat_log-*.zip` from the [Releases page](https://github.com/mnshdw/X2-CombatLog/releases).
2. Extract into your Xenonauts 2 user mods folder:
   - **Windows:** `Documents\My Games\Xenonauts 2\Mods\`
   - **Linux (Steam Proton):** `~/.local/share/Steam/steamapps/compatdata/538030/pfx/drive_c/users/steamuser/AppData/LocalLow/Goldhawk Interactive/Xenonauts 2/`
3. Launch Xenonauts 2 -> main menu -> **Mods** -> enable **Combat Log** -> restart.

## Screenshots

<img width="784" height="394" alt="Screenshot_20260509_151717-2" src="https://github.com/user-attachments/assets/1d4deb54-ac59-45d1-8134-76acf1be1355" />

https://github.com/user-attachments/assets/1f49e0e4-1019-499a-8399-02adcdab873c

## Build from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (8.0 or later) and a Xenonauts 2 install.

```sh
cp Directory.Build.props.template Directory.Build.props
# edit the three paths in Directory.Build.props to match your machine
dotnet build -c Release
```

The build emits `bin/Release/netstandard2.1/CombatLog.dll` and also copies it (plus the manifest) to `$(ModInstanceFolder)` so the game picks it up immediately.

## Cut a release

```sh
./release.sh
```

Produces `dist/combat_log-<version>.zip` ready to attach to a GitHub Release. Version is read from `mod/manifest.json`.

## License

[MIT](LICENSE).

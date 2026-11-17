# Development build

How to make a player of the Unity client that runs outside the Editor and reaches the city screen against your dev server (M0-82).
A **development build** is meant for testing only. It allows plain `http://` and keeps the dev login token on the device. A release build
allows neither, and M0 has no release build (`core-instructions/07-security-model.md`, `core-instructions/09-unity-client.md`).

Order: macOS or Windows first, then Android. iOS only on a Mac with Xcode.

## What you need
- Unity **6000.3.25f1** with the project `client/CityBuilder` (`client/README.md`), and the build support module of the platform, added in
  Unity Hub → Installs → ⋯ → Add modules:
  - macOS player on a Mac: built in. Windows player on Windows: built in. Windows player on a Mac: **Windows Build Support (Mono)**.
  - Android: **Android Build Support** with **OpenJDK** and **Android SDK & NDK Tools** ticked.
  - iOS: **iOS Build Support**, and Xcode.
- The dev server running: `docker compose up -d postgres`, then `dotnet run --project server/src/CityBuilder.Api` (`docs/getting-started.md`).
  The dev login exists only in Development, which `dotnet run` uses.

## Build
**One command (the Editor closed):**
```
export UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app   # Windows (Git Bash): ".../6000.3.25f1/Editor/Unity.exe"
bash scripts/dev/build-client.sh macos      # or: windows, android, ios
```
It runs the Editor headless and calls the Editor script `CityBuilder.Client.Editor.DevBuild`. The script builds the enabled scenes of the
build settings (only `Bootstrap`) with the Development flag. The player goes to `client/CityBuilder/Builds/<platform>/`, which git ignores:

| Platform | Output | Time on an M-series Mac (warm) |
|---|---|---|
| macOS | `Builds/macOS/CityBuilder.app` | about 1 minute, Mono |
| Windows | `Builds/Windows/CityBuilder.exe` (and its `_Data` folder) | not measured |
| Android | `Builds/Android/CityBuilder.apk` (about 50 MB) | about 7 minutes, IL2CPP |
| iOS | `Builds/iOS/` (an Xcode project) | not measured |

Unity's log is in `client/CityBuilder/Logs/build-<platform>.log`. The script prints the `DevBuild:` result line and every `error CS` line.

**From the Editor [MANUAL]:**
1. File → Build Profiles. Pick the platform on the left; press **Switch Platform** if it is not the active one.
2. Check that the scene list holds only `Scenes/Bootstrap`, ticked.
3. Tick **Development Build**.
4. Press **Build**, choose `client/CityBuilder/Builds/<platform>/`, and wait.

Or, with the platform active: menu **CityBuilder → Development build (active target)**. It does the same as the command.

**After a build:** Unity rewrites some settings files: the URP assets' shader prefiltering under `Assets/Settings/`,
`ProjectSettings/ProjectSettings.asset` (the batching list) and `ProjectSettings/UnityConnectSettings.asset`. These rewrites are not part of
your change. Check `git status`, and restore each file you didn't change on purpose: `git checkout -- <file>`.

## Which server the player talks to
The address comes from the settings asset `Assets/_Project/Resources/Config/ApiClientSettings.asset` (`baseUrl`, default
`http://localhost:8080`). A player started with `-server-url <address>` uses that address instead, so one build can reach another server
without a code or asset change (`ServerAddress`, `docs/modules/CityBuilder.Client.Net.md`). The player logs the address it uses:
`GameRoot: server <address>`.

- An `http://` address works only in a development build. A release build refuses it twice: in `ApiClient`, which shows the settings error,
  and in Unity's "Allow downloads over HTTP" setting (*Allowed in Development Builds*).
- A dev player logs in as `devDisplayName` from the same asset (`dev1`), so it shares the city with the Editor.

## Run
**macOS:** double-click `CityBuilder.app`, or start it from a terminal to choose the server or the log:
```
client/CityBuilder/Builds/macOS/CityBuilder.app/Contents/MacOS/CityBuilder -server-url http://localhost:8080 -logFile -
```
The first launch of an unsigned app may be blocked: right-click → Open, or System Settings → Privacy & Security → Open Anyway.

**Windows:** `CityBuilder.exe -server-url http://localhost:8080`. The log is
`%USERPROFILE%\AppData\LocalLow\<company>\CityBuilder\Player.log`.

**Android:** `adb` ships with Unity's SDK, in `<Unity>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`, and Android Studio has it too.
1. On the phone, turn on Developer options → USB debugging, and connect it with a cable. `adb devices` lists it.
2. **Let the phone reach the server on your computer** with `adb reverse tcp:8080 tcp:8080`. Then `http://localhost:8080` on the phone
   is port 8080 on the computer, and the asset's address works as it is. Run it again after each reconnect.
   Without a cable: start the server on every interface (`dotnet run --project server/src/CityBuilder.Api -- --urls http://0.0.0.0:8080`),
   and give the player your computer's LAN address. To do that, change `baseUrl` in the asset before you build: `-server-url` needs a
   command line, which a phone launch doesn't have.
3. `adb install -r client/CityBuilder/Builds/Android/CityBuilder.apk`, then open CityBuilder on the phone.
4. Its log: `adb logcat -s Unity`.

**iOS [MANUAL]:** open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode. Under Signing & Capabilities, pick your team. Run it on a phone that
is on the same network as the computer, with `baseUrl` set to your computer's LAN address and the server started on `0.0.0.0` (see
Android). This path has not been tried yet.

## What you should see
"Connected - Early Middle Ages - Coins: …" and the city, as in the Editor (`docs/testing/LOCAL-E2E.md`). The player's log shows
`Request POST /auth/dev-login -> 200`, `GET /content -> 200` and `GET /city -> 200`.

## When it goes wrong
| Symptom | Cause and fix |
|---|---|
| The status says the settings are wrong ("The base URL must be…") | The address is not an absolute `http(s)://` address, `-server-url` was given without a value, or an `http://` address is used in a non-development build |
| "Offline" right away on Android | No `adb reverse` (step 2), or the server is not running. `adb reverse --list` shows the mapping |
| "Offline" with a LAN address | The server listens on `localhost` only: start it with `--urls http://0.0.0.0:8080`. Or the computer's firewall blocks port 8080 |
| The dev login answers 404 | The server isn't running in Development (`ASPNETCORE_ENVIRONMENT`) |
| `MissingMethodException` or a JSON error in an IL2CPP player | Stripping removed DTO members: the `link.xml` next to the Net code must keep the whole `CityBuilder.Client.Net` assembly (`BuildSettingsTests`) |
| The build fails with "Scripts have compiler errors" | Run the compile check of `docs/getting-started.md` → Run, and fix the first `error CS` |
| Android build: no SDK, NDK or JDK found | Add the Android modules with their three ticks in Unity Hub (What you need) |

## Settings the build depends on
These are tested by `BuildSettingsTests`, `InsecureHttpSettingTests` and `ServerAddressTests` (Unity EditMode):
- Build scenes: `Bootstrap` only. Everything else is made from code (`core-instructions/09-unity-client.md`, AI-friendly rule 1).
- Android scripting backend: IL2CPP. The desktop players use Mono, which strips nothing that matters here.
- `Assets/_Project/Net/link.xml` preserves `CityBuilder.Client.Net`, where the DTOs live. Newtonsoft reads them through reflection.
- Player → Allow downloads over HTTP: *Allowed in Development Builds*.
- Known gap: the Android and iOS application identifiers are still the Unity template's (`com.UnityTechnologies…`). They work for a
  development build. A store build needs real ones (an M1 follow-up).

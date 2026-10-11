# MODLOG - Roblox Obby Peak

- Host: PEAK (Unity, Mono). Route: BepInEx 5 plugin (Melty installs bepinexpack-peak). Reason: established loader; no game file patching.
- Companion: Roblox. Hyperion anti-tamper -> never modded or launched. Read-only access to its install: content/sounds (ouch.ogg confirmed by community guides), content/fonts, PlatformContent/pc/textures/sky (format unverified: loader handles DDS/KTX1/PNG).
- Build machine: Linux cloud container, no PEAK/Roblox. Compiled against BepInEx 5.4.23.3 core + UnityEngine.Modules 2021.3.33 (NuGet) references. PEAK types resolved by reflection (sheets/hooks.json) because Assembly-CSharp isn't available here.
- Blocked: melty.gg denied by session network policy (all Melty tools). In-game test + screenshot need a PC with both games.
- Next: run on a PC with PEAK + Roblox; read BepInEx/LogOutput.log for "hook ... not found" and "RobloxObbyPeak ready:"; flip verifiedInGame in sheets.

# MODLOG - Roblox Obby Peak

- Host: PEAK (Unity, Mono). Route: BepInEx 5 plugin (Melty installs bepinexpack-peak). Reason: established loader; no game file patching.
- Companion: Roblox. Hyperion anti-tamper -> never modded or launched. Read-only access to its install: content/sounds (ouch.ogg confirmed by community guides), content/fonts, PlatformContent/pc/textures/sky (format unverified: loader handles DDS/KTX1/PNG).
- Build machine: Linux cloud container, no PEAK/Roblox. Compiled against BepInEx 5.4.23.3 core + UnityEngine.Modules 2021.3.33 (NuGet) references. PEAK types resolved by reflection (sheets/hooks.json) because Assembly-CSharp isn't available here.
- Melty: draft listing roblox-obby-peak (modId 33556621-89ab-4956-8b30-f0dd28b52cf1), release 0.1.0 draft, one click yes. Recipe in melty/recipe.validated.json. Multiplayer: maxPlayers 4, no connect (PEAK joins via Steam invites; auto-join needs PEAK lobby API + a 2-account test).
- Next: run on a PC with PEAK + Roblox; read BepInEx/LogOutput.log for "hook ... not found" and "RobloxObbyPeak ready:"; flip verifiedInGame in sheets.

# MODLOG - Roblox Obby Peak

- Host: PEAK (Unity, Mono). Route: BepInEx 5 plugin (Melty installs bepinexpack-peak). Reason: established loader; no game file patching.
- Companion: Roblox. Hyperion anti-tamper -> never modded or launched. Read-only access to its install: content/sounds (ouch.ogg confirmed by community guides), content/fonts, PlatformContent/pc/textures/sky (format unverified: loader handles DDS/KTX1/PNG).
- Build machine: Linux cloud container, no PEAK/Roblox. Compiled against BepInEx 5.4.23.3 core + UnityEngine.Modules 2021.3.33 (NuGet) references. PEAK types resolved by reflection (sheets/hooks.json) because Assembly-CSharp isn't available here.
- Melty: draft listing roblox-obby-peak (modId 33556621-89ab-4956-8b30-f0dd28b52cf1), release 0.1.0 draft, one click yes. Recipe in melty/recipe.validated.json. Multiplayer: maxPlayers 4, no connect (PEAK joins via Steam invites; auto-join needs PEAK lobby API + a 2-account test).
- Next: run on a PC with PEAK + Roblox; read BepInEx/LogOutput.log for "hook ... not found" and "RobloxObbyPeak ready:"; flip verifiedInGame in sheets.
- 0.1.0 test: all self-checks passed but PEAK closed ~10 s after "Scene Pretitle" with no error (Player.log just stops). Suspected TMP dynamic glyph rasterisation from the Roblox .otf at first draw. 0.1.1: glyphs baked once + atlas set Static, HideAndDontSave removed, sky via unlit box (Skybox/6 Sided not in PEAK build), session.log breadcrumbs. 0.1.1 test passed 3/3.
- Player's Roblox: C:/XboxGames/Roblox/Content (MS Store). {game:roblox} resolves there; content/sounds has ouch.ogg, action_get_up.mp3, action_jump_land.mp3; fonts/BuilderSans-Bold.otf; PlatformContent/pc/textures/sky/sky512_*.tex readable.
- PEAK hooks confirmed: Character.AllCharacters, localCharacter, Center, characterName, data; CharacterData.passedOut, fallSeconds; Campfire.Light_Rpc. Not found: CharacterData.dead.

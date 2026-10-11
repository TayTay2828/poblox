# Roblox Obby Peak

**Climb PEAK like it's a Roblox obby.** A PEAK mod that brings Roblox onto the mountain, using the Roblox files already installed on your PC.

## What you get
- **Obby stages:** the climb is split into 10 stages by height. When any scout in your group reaches a new stage, a big "Stage N" banner appears in Roblox's own font, saying who got there, and a Roblox sound plays.
- **Oof:** when a scout falls, passes out or dies, you hear Roblox's own death sound (`ouch.ogg` from your Roblox install). Other scouts' oofs play from where they are.
- **Campfire checkpoints:** lighting a campfire shows "Checkpoint!" with a Roblox sound.
- **Roblox sky:** Roblox's classic skybox replaces the sky where PEAK renders a skybox (best effort).
- **Co-op:** works in PEAK's normal co-op for up to 4 scouts. Join friends through PEAK's usual Steam invites. Everyone with the mod sees the stage banners for the whole team.

## Required games
- **PEAK** (the game you play; the mod runs in it through BepInEx).
- **Roblox** (only needs to be installed; it does not have to run). The mod reads Roblox's sounds, font and sky from your own Roblox folder (`%LOCALAPPDATA%\Roblox\Versions\version-…`) while you play. No Roblox or PEAK files are included in this mod, and the mod never touches or launches Roblox.

## Settings
`BepInEx/config/melty.robloxobbypeak.cfg`: turn each feature on/off (`StageBanners`, `OofOnFall`, `CampfireCheckpoints`, `RobloxSky`) or point `RobloxFolder` at a custom Roblox install (for example a Bloxstrap folder).

## How it's built
- Design lives in `sheets/*.json` (Roblox assets, stages, PEAK hooks, systems). `tools/gen.py` preflights them and generates `src/RobloxObbyPeak/Generated.cs`.
- `tools/build.sh` builds the BepInEx plugin and packages `dist/RobloxObbyPeak-<version>.zip`.
- PEAK game members are resolved by name at runtime (see `sheets/hooks.json`). Anything missing is logged and falls back gracefully.

## Status
v0.1.0 compiles and passes Melty's one-click check. It has **not been tested in the game yet**: the rows in `sheets/` marked `verifiedInGame: false` are waiting for a test run on a PC that has both PEAK and Roblox. Melty test runs report the checks in `sheets/tests.json`.

## Credits
- Mod: TayTay2828, built with Claude Code (AI-assisted).
- Runs on BepInEx (BepInEx team) and HarmonyX, which Melty installs for PEAK.
- PEAK by Aggro Crab & Landfall. Roblox by Roblox Corporation. Not affiliated with either.

## License
MIT (the mod's own code). Remixes welcome.

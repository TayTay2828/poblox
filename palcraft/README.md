# PalCraft

**Catch real Palworld Pals in Minecraft.** A Minecraft: Java Edition mod (Fabric, Minecraft 26.3) that brings Palworld's Pals into your world, read from the Palworld installed on your PC.

## What you get
- **Real Pals in Minecraft:** Lamball, Cattiva, Chikipi and Foxparks roam plains, forests, meadows, savannas and taiga. Each one has its own Palworld 3D model, textures and name, read from your own Palworld install.
- **Start with Pal Spheres:** a new world gives you 16 Pal Spheres, and a few of each Pal appear nearby.
- **Catching:** hit a Pal to weaken it, then throw a Pal Sphere at it. The weaker it is, the better your chance. Catch it and the sphere holds the Pal.
- **Pals fight beside you:** use a filled sphere to send your Pal out. It follows you and attacks whatever you attack or whatever attacks you. Use an empty sphere on your own Pal to bring it back.
- **Palworld's Pal Sphere icon:** the item uses Palworld's own icon once PalCraft has read your Palworld.
- **Single player.**

## Required games
- **Minecraft: Java Edition** (you play in it).
- **Palworld** (only needs to be installed). The first time PalCraft starts, it reads the four Pals from Palworld's files on your PC into `.minecraft/palcraft`. No Palworld files are in this download.

## How it's built
- `sheets/*.json` is the design (Pals, items, Palworld assets read, systems, tests). `tools/gen.py` preflights them and generates `fabric/src/main/java/dev/palcraft/gen/Sheets.java`.
- `fabric/`: the Minecraft mod (Fabric Loader 0.19.5, Fabric API 0.161.0, Java 25).
- `extractor/`: PalCraftExtractor, a .NET 10 program using CUE4Parse that reads Palworld's paks (UE 5.1) on the player's PC.
- `tools/package.sh` builds both and packages a portable Prism Launcher with the PalCraft instance.

## Credits
PalCraft by TayTay2828, built with Claude Code (AI-assisted). Uses Prism Launcher, Fabric, CUE4Parse and PalDex's Palworld type mappings (see THIRD-PARTY.txt). Palworld by Pocketpair, Minecraft by Mojang. Not affiliated with either.

## License
MIT (PalCraft's own code). Remixes welcome.

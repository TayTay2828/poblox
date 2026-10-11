// Generated from sheets/*.json by tools/gen.py. Edit the sheets, not this file.
package dev.palcraft.gen;

import java.util.List;

public final class Sheets {
	private Sheets() {}

	public record Pal(String id, String tribe, String fallbackName, float health, float attack, float speed, float width, float height, float catchBase, List<String> spawnBiomes, int spawnWeight, int groupMin, int groupMax, float meshYaw) {}
	public record ItemRow(String id, String name, int maxStack, String texture, String palworldIcon, int starterCount) {}
	public record Test(String id, String passWhen, String system) {}

	public static final List<Pal> PALS = List.of(
		new Pal("lamball", "SheepBall", "Lamball", 14.0f, 2.0f, 0.22f, 0.9f, 0.9f, 0.9f, java.util.List.of("plains", "meadow", "sunflower_plains"), 12, 2, 4, 90.0f),
		new Pal("cattiva", "PinkCat", "Cattiva", 14.0f, 3.0f, 0.26f, 0.7f, 1.0f, 0.85f, java.util.List.of("plains", "forest", "birch_forest"), 10, 1, 3, 90.0f),
		new Pal("chikipi", "ChickenPal", "Chikipi", 10.0f, 1.0f, 0.24f, 0.7f, 0.8f, 0.95f, java.util.List.of("plains", "savanna", "meadow"), 12, 2, 4, 90.0f),
		new Pal("foxparks", "Kitsunebi", "Foxparks", 16.0f, 4.0f, 0.28f, 0.8f, 0.9f, 0.75f, java.util.List.of("forest", "taiga", "savanna"), 8, 1, 2, 90.0f)
	);
	public static final List<ItemRow> ITEMS = List.of(
		new ItemRow("pal_sphere", "Pal Sphere", 16, "item/pal_sphere", "PalSphere", 16)
	);
	public static final List<Test> TESTS = List.of(
		new Test("palworld-read", "the extractor read the player's Palworld and wrote every Pal's mesh and texture", "extract"),
		new Test("pal-models", "every Pal's mesh loaded on the client", "render"),
		new Test("world-joined", "the test world loaded with the player in it", "spawn"),
		new Test("pals-spawned", "starter Pals spawned near the player", "spawn"),
		new Test("pal-caught", "a scripted sphere throw caught a Pal and the sphere holds it", "catch")
	);
}

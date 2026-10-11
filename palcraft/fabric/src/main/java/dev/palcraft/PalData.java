package dev.palcraft;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import dev.palcraft.gen.Sheets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import net.fabricmc.loader.api.FabricLoader;

/** What the extractor read from the player's Palworld: <game dir>/palcraft/pals.json (names) and pals/<id>/ (mesh, textures). */
public final class PalData {
	private static Map<String, String> names;

	private PalData() {}

	public static Path dir() {
		return FabricLoader.getInstance().getGameDir().resolve("palcraft");
	}

	public static Path manifest() {
		return dir().resolve("pals.json");
	}

	public static synchronized void reload() {
		names = null;
	}

	/** The Pal's name as Palworld shows it, else the sheet's fallback. */
	public static synchronized String name(Sheets.Pal pal) {
		if (names == null) {
			names = new HashMap<>();
			try {
				if (Files.exists(manifest())) {
					JsonObject root = JsonParser.parseString(Files.readString(manifest())).getAsJsonObject();
					for (JsonElement e : root.getAsJsonArray("pals")) {
						JsonObject o = e.getAsJsonObject();
						names.put(o.get("id").getAsString(), o.get("name").getAsString());
					}
				}
			} catch (Exception e) {
				PalCraft.LOG.warn("Could not read {}: {}", manifest(), e.toString());
			}
		}
		return names.getOrDefault(pal.id(), pal.fallbackName());
	}

	public static Sheets.Pal byId(String id) {
		for (Sheets.Pal p : Sheets.PALS) if (p.id().equals(id)) return p;
		return null;
	}
}

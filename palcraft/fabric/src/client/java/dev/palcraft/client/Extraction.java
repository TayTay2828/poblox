package dev.palcraft.client;

import dev.palcraft.PalCraft;
import dev.palcraft.PalData;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Properties;
import net.fabricmc.loader.api.FabricLoader;

/**
 * First start: runs the bundled extractor on the player's Palworld (folder from config/palcraft.properties, which Melty
 * fills in before every Play) into <game dir>/palcraft. Later starts reuse what it wrote.
 */
public final class Extraction {
	public enum State { RUNNING, DONE, FAILED }

	public static volatile State state = State.RUNNING;
	public static volatile String failure;

	private Extraction() {}

	public static Path tools() {
		return FabricLoader.getInstance().getGameDir().resolve("palcraft-tools");
	}

	static String palworldDir() {
		String env = System.getenv("PALCRAFT_PALWORLD_DIR");
		if (env != null && !env.isBlank()) return env;
		Path cfg = FabricLoader.getInstance().getConfigDir().resolve("palcraft.properties");
		try (InputStream in = Files.newInputStream(cfg)) {
			Properties p = new Properties();
			p.load(new java.io.InputStreamReader(in, StandardCharsets.UTF_8));
			String v = p.getProperty("palworld");
			return v == null || v.isBlank() ? null : v.trim();
		} catch (Exception e) {
			return null;
		}
	}

	public static void start() {
		Thread t = new Thread(Extraction::run, "PalCraft-extract");
		t.setDaemon(true);
		t.start();
	}

	private static void run() {
		try {
			Path stamp = PalData.dir().resolve("extracted-by.txt");
			String want = "PalCraft " + PalCraft.version();
			if (Files.exists(PalData.manifest()) && Files.exists(stamp) && Files.readString(stamp).trim().equals(want)) {
				PalCraft.LOG.info("Palworld already read ({}), reusing {}", want, PalData.dir());
				state = State.DONE;
				return;
			}
			String palworld = palworldDir();
			if (palworld == null) throw new IllegalStateException("Palworld folder not set (config/palcraft.properties palworld=)");
			Path exe = tools().resolve("PalCraftExtractor.exe");
			if (!Files.exists(exe)) throw new IllegalStateException("extractor missing at " + exe);
			PalCraft.LOG.info("Reading your Palworld at {} ...", palworld);
			Process p = new ProcessBuilder(exe.toString(), "--palworld", palworld, "--out", PalData.dir().toString(),
				"--mappings", tools().resolve("Mappings.usmap").toString())
				.redirectErrorStream(true).redirectOutput(PalData.dir().resolve("extract-console.log").toFile()).start();
			int code = p.waitFor();
			if (code != 0 && !Files.exists(PalData.manifest())) throw new IllegalStateException("extractor exited " + code + " (see palcraft/extract.log)");
			Files.writeString(stamp, want);
			PalData.reload();
			PalCraft.LOG.info("Palworld read: exit {}", code);
			state = State.DONE;
		} catch (Exception e) {
			failure = e.getMessage();
			PalCraft.LOG.error("Could not read Palworld: {}", e.toString());
			state = State.FAILED;
		}
	}

	static {
		try { Files.createDirectories(PalData.dir()); } catch (Exception ignored) {}
	}
}

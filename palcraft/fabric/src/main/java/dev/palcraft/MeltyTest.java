package dev.palcraft;

import dev.palcraft.entity.PalEntity;
import dev.palcraft.entity.PalSphereEntity;
import dev.palcraft.item.PalSphereItem;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.phys.Vec3;

/**
 * Melty test runs: while %LOCALAPPDATA%/Melty/test/palcraft*.json exists, the mod reports each row of sheets/tests.json
 * as "pass id" / "fail id: why" to the file its "checks" names. The server side drives a scripted catch.
 */
public final class MeltyTest {
	private static Path checks;
	private static boolean looked;
	private static final Set<String> reported = new HashSet<>();
	private static int joinedTick = -1;
	private static int spawned;
	private static boolean thrown;

	private MeltyTest() {}

	public static synchronized boolean active() {
		if (!looked) {
			looked = true;
			try {
				String local = System.getenv("LOCALAPPDATA");
				Path dir = local == null ? null : Paths.get(local, "Melty", "test");
				if (dir != null && Files.isDirectory(dir)) {
					try (DirectoryStream<Path> ds = Files.newDirectoryStream(dir, "palcraft*.json")) {
						for (Path f : ds) {
							Matcher m = Pattern.compile("\"checks\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").matcher(Files.readString(f));
							if (m.find()) { checks = Paths.get(m.group(1).replace("\\\\", "\\").replace("\\/", "/")); break; }
						}
					}
				}
			} catch (Exception e) {
				PalCraft.LOG.warn("Melty test file unreadable: {}", e.toString());
			}
			if (checks != null) PalCraft.LOG.info("Melty test run: reporting to {}", checks);
		}
		return checks != null;
	}

	public static synchronized void report(String id, String failure) {
		if (!active() || !reported.add(id)) return;
		String line = failure == null ? "pass " + id : "fail " + id + ": " + failure;
		PalCraft.LOG.info("Melty test: {}", line);
		try {
			Files.createDirectories(checks.getParent());
			Files.writeString(checks, line + "\n", StandardCharsets.UTF_8, StandardOpenOption.CREATE, StandardOpenOption.APPEND);
		} catch (Exception e) {
			PalCraft.LOG.warn("Could not write test check: {}", e.toString());
		}
	}

	public static void serverTick(MinecraftServer server) {
		if (!active()) return;
		List<ServerPlayer> players = server.getPlayerList().getPlayers();
		if (players.isEmpty()) return;
		ServerPlayer player = players.getFirst();
		int now = server.getTickCount();
		if (joinedTick < 0) {
			joinedTick = now;
			report("world-joined", null);
			spawned = Starter.spawnAround(player, 1);
			report("pals-spawned", spawned > 0 ? null : "no Pal could be spawned near the player");
		}
		if (!thrown && now - joinedTick > 200) {
			thrown = true;
			PalEntity target = null;
			for (Entity e : player.level().getEntities(player, player.getBoundingBox().inflate(32), e -> e instanceof PalEntity p && !p.isTame())) {
				if (target == null || e.distanceToSqr(player) < target.distanceToSqr(player)) target = (PalEntity) e;
			}
			if (target == null) { report("pal-caught", "no wild Pal near the player to throw at"); return; }
			PalSphereEntity.alwaysCatch = true;
			PalSphereEntity sphere = new PalSphereEntity(player.level(), player, new ItemStack(PalCraft.PAL_SPHERE));
			Vec3 above = target.position().add(0, target.getBbHeight() + 1.5, 0);
			sphere.setPos(above);
			sphere.setDeltaMovement(0, -0.6, 0);
			player.level().addFreshEntity(sphere);
			PalCraft.LOG.info("Melty test: threw a Pal Sphere at {}", target.pal.id());
		}
		if (thrown && !reported.contains("pal-caught")) {
			boolean holds = false;
			for (ItemStack s : player.getInventory().getNonEquipmentItems()) if (PalSphereItem.heldPal(s) != null) holds = true;
			if (PalSphereEntity.caughtCount > 0 && holds) report("pal-caught", null);
			else if (now - joinedTick > 600) report("pal-caught", "sphere did not catch the Pal within 20 s (caught " + PalSphereEntity.caughtCount + ")");
		}
	}
}

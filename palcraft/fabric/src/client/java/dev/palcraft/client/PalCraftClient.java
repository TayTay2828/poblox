package dev.palcraft.client;

import dev.palcraft.MeltyTest;
import dev.palcraft.PalCraft;
import dev.palcraft.PalData;
import dev.palcraft.entity.PalEntity;
import dev.palcraft.gen.Sheets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.event.lifecycle.v1.ClientTickEvents;
import net.fabricmc.fabric.api.client.rendering.v1.EntityRendererRegistry;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.entity.ThrownItemRenderer;
import net.minecraft.world.entity.EntityType;

public class PalCraftClient implements ClientModInitializer {
	public static final String PACK = "PalCraft Palworld";
	private boolean loaded;

	@Override
	public void onInitializeClient() {
		for (Sheets.Pal pal : Sheets.PALS) {
			EntityType<PalEntity> type = PalCraft.PAL_TYPES.get(pal.id());
			float shadow = pal.width() * 0.5F;
			EntityRendererRegistry.register(type, ctx -> new PalRenderer(ctx, shadow));
		}
		EntityRendererRegistry.register(PalCraft.PAL_SPHERE_ENTITY, ThrownItemRenderer::new);
		Extraction.start();
		ClientTickEvents.END_CLIENT_TICK.register(this::tick);
	}

	private void tick(Minecraft mc) {
		if (loaded || Extraction.state == Extraction.State.RUNNING) return;
		loaded = true;
		if (Extraction.state == Extraction.State.FAILED) {
			MeltyTest.report("palworld-read", Extraction.failure);
			MeltyTest.report("pal-models", "Palworld was not read");
			return;
		}
		int ok = 0;
		for (Sheets.Pal p : Sheets.PALS) if (Files.exists(PalData.dir().resolve("pals").resolve(p.id()).resolve("mesh.bin"))) ok++;
		MeltyTest.report("palworld-read", ok == Sheets.PALS.size() ? null : ok + "/" + Sheets.PALS.size() + " Pals read (see palcraft/extract.log)");
		List<String> failed = PalMesh.loadAll();
		MeltyTest.report("pal-models", failed.isEmpty() ? null : "not loaded: " + String.join(", ", failed));
		installSphereIcon(mc);
	}

	/** Puts Palworld's own Pal Sphere icon into a resource pack made on this PC, and turns it on. */
	private static void installSphereIcon(Minecraft mc) {
		try {
			Path icon = PalData.dir().resolve("pal_sphere.png");
			if (!Files.exists(icon)) return;
			Path pack = mc.gameDirectory.toPath().resolve("resourcepacks").resolve(PACK);
			Path tex = pack.resolve("assets/palcraft/textures/item/pal_sphere.png");
			Files.createDirectories(tex.getParent());
			Files.copy(icon, tex, java.nio.file.StandardCopyOption.REPLACE_EXISTING);
			Files.writeString(pack.resolve("pack.mcmeta"),
				"{\"pack\":{\"description\":\"Palworld's Pal Sphere icon, read from your own Palworld by PalCraft\",\"min_format\":[97,0],\"max_format\":[97,99]}}");
			String id = "file/" + PACK;
			mc.getResourcePackRepository().reload();
			if (!mc.options.resourcePacks.contains(id)) {
				mc.options.resourcePacks.add(id);
				mc.getResourcePackRepository().setSelected(mc.options.resourcePacks);
				mc.options.save();
				mc.reloadResourcePacks();
			}
			PalCraft.LOG.info("Pal Sphere icon from Palworld enabled");
		} catch (Exception e) {
			PalCraft.LOG.warn("Pal Sphere icon not installed: {}", e.toString());
		}
	}
}

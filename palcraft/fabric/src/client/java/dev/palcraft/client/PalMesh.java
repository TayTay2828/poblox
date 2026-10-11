package dev.palcraft.client;

import com.mojang.blaze3d.platform.NativeImage;
import dev.palcraft.PalCraft;
import dev.palcraft.PalData;
import dev.palcraft.gen.Sheets;
import java.io.DataInputStream;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.resources.Identifier;

/** A Pal's Palworld mesh (written by the extractor as pals/<id>/mesh.bin) and its textures, normalised to the hitbox. */
public final class PalMesh {
	public record Section(Identifier texture, int[] indices) {}

	public final float[] pos; // x y z per vertex, in model units: feet at y=0, height 1
	public final float[] uv;
	public final List<Section> sections;

	private PalMesh(float[] pos, float[] uv, List<Section> sections) {
		this.pos = pos;
		this.uv = uv;
		this.sections = sections;
	}

	private static final Map<String, PalMesh> LOADED = new HashMap<>();

	public static PalMesh get(String id) {
		return LOADED.get(id);
	}

	public static int loadedCount() {
		return LOADED.size();
	}

	/** Loads every extracted Pal on the render thread. Returns the ids that failed. */
	public static List<String> loadAll() {
		List<String> failed = new ArrayList<>();
		for (Sheets.Pal pal : Sheets.PALS) {
			try {
				LOADED.put(pal.id(), load(pal));
			} catch (Exception e) {
				failed.add(pal.id());
				PalCraft.LOG.warn("Pal {} mesh not loaded: {}", pal.id(), e.toString());
			}
		}
		PalCraft.LOG.info("Pal meshes loaded: {}/{}", LOADED.size(), Sheets.PALS.size());
		return failed;
	}

	private static PalMesh load(Sheets.Pal pal) throws IOException {
		Path dir = PalData.dir().resolve("pals").resolve(pal.id());
		ByteBuffer b = ByteBuffer.wrap(Files.readAllBytes(dir.resolve("mesh.bin"))).order(ByteOrder.LITTLE_ENDIAN);
		if (b.get() != 'P' || b.get() != 'C' || b.get() != 'M' || b.get() != '1') throw new IOException("not a PCM1 mesh");
		int n = b.getInt();
		float[] pos = new float[n * 3], uv = new float[n * 2];
		float minX = Float.MAX_VALUE, minY = Float.MAX_VALUE, minZ = Float.MAX_VALUE, maxX = -Float.MAX_VALUE, maxY = -Float.MAX_VALUE, maxZ = -Float.MAX_VALUE;
		double yaw = Math.toRadians(pal.meshYaw());
		float cos = (float) Math.cos(yaw), sin = (float) Math.sin(yaw);
		for (int i = 0; i < n; i++) {
			// file: Minecraft axes from Unreal's (left-handed); mirror X to keep the model the right way round, then turn by meshYaw
			float x = -b.getFloat(), y = b.getFloat(), z = b.getFloat();
			float rx = x * cos - z * sin, rz = x * sin + z * cos;
			pos[i * 3] = rx; pos[i * 3 + 1] = y; pos[i * 3 + 2] = rz;
			uv[i * 2] = b.getFloat(); uv[i * 2 + 1] = b.getFloat();
			minX = Math.min(minX, rx); maxX = Math.max(maxX, rx);
			minY = Math.min(minY, y); maxY = Math.max(maxY, y);
			minZ = Math.min(minZ, rz); maxZ = Math.max(maxZ, rz);
		}
		float h = Math.max(1e-4F, maxY - minY), cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
		for (int i = 0; i < n; i++) {
			pos[i * 3] = (pos[i * 3] - cx) / h;
			pos[i * 3 + 1] = (pos[i * 3 + 1] - minY) / h;
			pos[i * 3 + 2] = (pos[i * 3 + 2] - cz) / h;
		}
		int sectionCount = b.getInt();
		List<Section> sections = new ArrayList<>();
		Map<Integer, Identifier> textures = new HashMap<>();
		for (int s = 0; s < sectionCount; s++) {
			int slot = b.getInt(), count = b.getInt();
			int[] idx = new int[count];
			for (int i = 0; i < count; i++) idx[i] = b.getInt();
			Identifier tex = textures.computeIfAbsent(slot, k -> texture(pal, dir, k));
			if (tex != null) sections.add(new Section(tex, idx));
		}
		if (sections.isEmpty()) throw new IOException("no textured sections");
		return new PalMesh(pos, uv, sections);
	}

	private static Identifier texture(Sheets.Pal pal, Path dir, int slot) {
		Path png = dir.resolve("tex_" + slot + ".png");
		if (!Files.exists(png)) png = dir.resolve("tex_0.png");
		if (!Files.exists(png)) return null;
		try {
			NativeImage image = NativeImage.read(Files.readAllBytes(png));
			Identifier id = PalCraft.id("palworld/" + pal.id() + "_" + slot);
			Minecraft.getInstance().getTextureManager().register(id, new DynamicTexture(() -> "palcraft " + pal.id() + " " + slot, image));
			return id;
		} catch (IOException e) {
			PalCraft.LOG.warn("Texture {} unreadable: {}", png, e.toString());
			return null;
		}
	}
}

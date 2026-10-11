package dev.palcraft;

import dev.palcraft.entity.PalEntity;
import dev.palcraft.gen.Sheets;
import net.minecraft.core.BlockPos;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.EntitySpawnReason;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.level.levelgen.Heightmap;

/** First join in a world: a stack of Pal Spheres and a few of each Pal nearby (systems: starter, spawn). */
public final class Starter {
	public static final String TAG = "palcraft_started";

	private Starter() {}

	public static void onJoin(ServerPlayer player) {
		if (player.entityTags().contains(TAG)) return;
		player.addTag(TAG);
		player.getInventory().add(new ItemStack(PalCraft.PAL_SPHERE, Sheets.ITEMS.getFirst().starterCount()));
		int spawned = spawnAround(player, 2);
		player.sendSystemMessage(Component.literal("PalCraft: wild Pals are nearby! Weaken one, then throw a Pal Sphere at it."));
		PalCraft.LOG.info("Starter kit for {}: {} spheres, {} Pals spawned", player.getName().getString(), Sheets.ITEMS.getFirst().starterCount(), spawned);
	}

	/** Spawns `each` of every Pal within ~10 blocks of the player; returns how many spawned. */
	public static int spawnAround(ServerPlayer player, int each) {
		ServerLevel level = player.level();
		int n = 0, i = 0;
		for (EntityType<PalEntity> type : PalCraft.PAL_TYPES.values()) {
			for (int k = 0; k < each; k++, i++) {
				double angle = i * 2.4;
				int x = (int) Math.floor(player.getX() + Math.cos(angle) * (5 + (i % 3) * 2));
				int z = (int) Math.floor(player.getZ() + Math.sin(angle) * (5 + (i % 3) * 2));
				BlockPos top = level.getHeightmapPos(Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, new BlockPos(x, 0, z));
				PalEntity pal = type.create(level, EntitySpawnReason.MOB_SUMMONED);
				if (pal == null) continue;
				pal.snapTo(top.getX() + 0.5, top.getY(), top.getZ() + 0.5, level.getRandom().nextFloat() * 360F, 0F);
				if (level.addFreshEntity(pal)) n++;
			}
		}
		return n;
	}
}

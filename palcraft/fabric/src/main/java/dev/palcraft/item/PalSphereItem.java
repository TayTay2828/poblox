package dev.palcraft.item;

import dev.palcraft.PalCraft;
import dev.palcraft.PalData;
import dev.palcraft.entity.PalEntity;
import dev.palcraft.entity.PalSphereEntity;
import dev.palcraft.gen.Sheets;
import net.minecraft.core.BlockPos;
import net.minecraft.core.component.DataComponents;
import net.minecraft.nbt.CompoundTag;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.sounds.SoundSource;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.InteractionResult;
import net.minecraft.world.entity.EntitySpawnReason;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.entity.projectile.Projectile;
import net.minecraft.world.item.Item;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.component.CustomData;
import net.minecraft.world.level.ClipContext;
import net.minecraft.world.level.Level;
import net.minecraft.world.phys.BlockHitResult;
import net.minecraft.world.phys.HitResult;
import net.minecraft.world.phys.Vec3;

/**
 * Pal Sphere (sheets/items.json). Empty: throw it to catch a Pal, or use it on your own Pal to recall it.
 * Holding a Pal: use it to send the Pal out; it follows you and fights beside you.
 */
public class PalSphereItem extends Item {
	public static final String KEY_PAL = "palcraft_pal";
	public static final String KEY_HEALTH = "palcraft_health";

	public PalSphereItem(Item.Properties properties) {
		super(properties);
	}

	public static String heldPal(ItemStack stack) {
		CustomData data = stack.get(DataComponents.CUSTOM_DATA);
		if (data == null) return null;
		return data.copyTag().getString(KEY_PAL).orElse(null);
	}

	public static ItemStack filled(Sheets.Pal pal, float health) {
		ItemStack stack = new ItemStack(PalCraft.PAL_SPHERE);
		CompoundTag tag = new CompoundTag();
		tag.putString(KEY_PAL, pal.id());
		tag.putFloat(KEY_HEALTH, health);
		CustomData.set(DataComponents.CUSTOM_DATA, stack, tag);
		stack.set(DataComponents.ITEM_NAME, Component.literal("Pal Sphere (" + PalData.name(pal) + ")"));
		return stack;
	}

	@Override
	public InteractionResult use(Level level, Player player, InteractionHand hand) {
		ItemStack stack = player.getItemInHand(hand);
		String held = heldPal(stack);
		if (held != null) {
			if (level instanceof ServerLevel server) release(server, player, stack, held);
			return InteractionResult.SUCCESS;
		}
		level.playSound(null, player.getX(), player.getY(), player.getZ(), SoundEvents.SNOWBALL_THROW, SoundSource.NEUTRAL, 0.5F, 0.6F);
		if (level instanceof ServerLevel server) {
			Projectile.spawnProjectileFromRotation(PalSphereEntity::new, server, stack, player, 0.0F, 1.5F, 1.0F);
		}
		stack.consume(1, player);
		return InteractionResult.SUCCESS;
	}

	private static void release(ServerLevel level, Player player, ItemStack stack, String palId) {
		Sheets.Pal pal = PalData.byId(palId);
		if (pal == null) return;
		Vec3 eye = player.getEyePosition();
		BlockHitResult hit = level.clip(new ClipContext(eye, eye.add(player.getLookAngle().scale(6)), ClipContext.Block.COLLIDER, ClipContext.Fluid.NONE, player));
		BlockPos at = hit.getType() == HitResult.Type.BLOCK ? hit.getBlockPos().relative(hit.getDirection()) : player.blockPosition().relative(player.getDirection(), 2);
		PalEntity entity = PalCraft.PAL_TYPES.get(palId).create(level, EntitySpawnReason.MOB_SUMMONED);
		if (entity == null) return;
		entity.snapTo(at.getX() + 0.5, at.getY(), at.getZ() + 0.5, player.getYRot() + 180F, 0F);
		entity.tame(player);
		float health = stack.get(DataComponents.CUSTOM_DATA).copyTag().getFloat(KEY_HEALTH).orElse(pal.health());
		entity.setHealth(Math.max(1F, health));
		level.addFreshEntity(entity);
		level.playSound(null, at, SoundEvents.CHICKEN_EGG, SoundSource.NEUTRAL, 1.0F, 1.2F);
		stack.shrink(1);
		ItemStack empty = new ItemStack(PalCraft.PAL_SPHERE);
		give(player, empty);
		player.sendOverlayMessage(Component.literal("Go, " + PalData.name(pal) + "!"));
	}

	private static void give(Player player, ItemStack stack) {
		if (!player.getInventory().add(stack))
			player.level().addFreshEntity(new net.minecraft.world.entity.item.ItemEntity(player.level(), player.getX(), player.getY(), player.getZ(), stack));
	}

	@Override
	public InteractionResult interactLivingEntity(ItemStack stack, Player player, LivingEntity target, InteractionHand hand) {
		if (heldPal(stack) != null || !(target instanceof PalEntity pal) || !pal.isOwnedBy(player)) return InteractionResult.PASS;
		if (!player.level().isClientSide()) {
			ItemStack full = filled(pal.pal, pal.getHealth());
			stack.shrink(1);
			give(player, full);
			pal.discard();
			player.sendOverlayMessage(Component.literal(PalData.name(pal.pal) + ", come back!"));
		}
		return InteractionResult.SUCCESS;
	}
}

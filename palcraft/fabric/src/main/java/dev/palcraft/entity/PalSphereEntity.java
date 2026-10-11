package dev.palcraft.entity;

import dev.palcraft.PalCraft;
import dev.palcraft.PalData;
import dev.palcraft.item.PalSphereItem;
import net.minecraft.core.particles.ParticleTypes;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.sounds.SoundSource;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.item.ItemEntity;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.entity.projectile.throwableitemprojectile.ThrowableItemProjectile;
import net.minecraft.world.item.Item;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.level.Level;
import net.minecraft.world.phys.EntityHitResult;
import net.minecraft.world.phys.HitResult;

/** A thrown Pal Sphere. Hitting a wild Pal tries to catch it: chance = catchBase x (1.5 - health fraction), capped at 95%. */
public class PalSphereEntity extends ThrowableItemProjectile {
	/** Test runs force the catch so the check is deterministic. */
	public static boolean alwaysCatch;
	public static int caughtCount;

	public PalSphereEntity(EntityType<? extends PalSphereEntity> type, Level level) {
		super(type, level);
	}

	public PalSphereEntity(Level level, LivingEntity owner, ItemStack stack) {
		super(PalCraft.PAL_SPHERE_ENTITY, owner, level, stack);
	}

	public PalSphereEntity(Level level, double x, double y, double z, ItemStack stack) {
		super(PalCraft.PAL_SPHERE_ENTITY, x, y, z, level, stack);
	}

	@Override
	protected Item getDefaultItem() {
		return PalCraft.PAL_SPHERE;
	}

	public static float catchChance(PalEntity pal) {
		float frac = pal.getHealth() / pal.getMaxHealth();
		return Math.min(0.95F, pal.pal.catchBase() * (1.5F - frac) * 0.75F);
	}

	@Override
	protected void onHitEntity(EntityHitResult hitResult) {
		super.onHitEntity(hitResult);
		if (!(this.level() instanceof ServerLevel level)) return;
		Entity target = hitResult.getEntity();
		Player thrower = this.getOwner() instanceof Player p ? p : null;
		if (!(target instanceof PalEntity pal) || pal.isTame()) {
			drop(level, new ItemStack(PalCraft.PAL_SPHERE));
			return;
		}
		float chance = catchChance(pal);
		boolean caught = alwaysCatch || level.getRandom().nextFloat() < chance;
		String name = PalData.name(pal.pal);
		if (caught) {
			ItemStack full = PalSphereItem.filled(pal.pal, pal.getHealth());
			pal.discard();
			level.sendParticles(ParticleTypes.HAPPY_VILLAGER, pal.getX(), pal.getY() + 0.5, pal.getZ(), 20, 0.4, 0.4, 0.4, 0.1);
			level.playSound(null, pal.blockPosition(), SoundEvents.PLAYER_LEVELUP, SoundSource.PLAYERS, 0.8F, 1.4F);
			if (thrower == null || !thrower.getInventory().add(full)) drop(level, full);
			if (thrower != null) thrower.sendOverlayMessage(Component.literal("Gotcha! " + name + " was caught!"));
			caughtCount++;
			PalCraft.LOG.info("Caught {} (chance {})", pal.pal.id(), String.format("%.2f", chance));
		} else {
			level.sendParticles(ParticleTypes.SMOKE, pal.getX(), pal.getY() + 0.5, pal.getZ(), 12, 0.3, 0.3, 0.3, 0.02);
			level.playSound(null, pal.blockPosition(), SoundEvents.ITEM_BREAK.value(), SoundSource.NEUTRAL, 0.8F, 1.0F);
			if (thrower != null) thrower.sendOverlayMessage(Component.literal(name + " broke free! (" + Math.round(chance * 100) + "% chance)"));
		}
	}

	private void drop(ServerLevel level, ItemStack stack) {
		level.addFreshEntity(new ItemEntity(level, this.getX(), this.getY(), this.getZ(), stack));
	}

	@Override
	protected void onHit(HitResult hitResult) {
		super.onHit(hitResult);
		if (!this.level().isClientSide()) {
			if (hitResult.getType() == HitResult.Type.BLOCK && this.level() instanceof ServerLevel level) drop(level, new ItemStack(PalCraft.PAL_SPHERE));
			this.discard();
		}
	}
}

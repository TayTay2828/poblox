package dev.palcraft.entity;

import dev.palcraft.PalData;
import dev.palcraft.gen.Sheets;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.AgeableMob;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.TamableAnimal;
import net.minecraft.world.entity.ai.attributes.AttributeSupplier;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.entity.ai.goal.FloatGoal;
import net.minecraft.world.entity.ai.goal.FollowOwnerGoal;
import net.minecraft.world.entity.ai.goal.LookAtPlayerGoal;
import net.minecraft.world.entity.ai.goal.MeleeAttackGoal;
import net.minecraft.world.entity.ai.goal.PanicGoal;
import net.minecraft.world.entity.ai.goal.RandomLookAroundGoal;
import net.minecraft.world.entity.ai.goal.WaterAvoidingRandomStrollGoal;
import net.minecraft.world.entity.ai.goal.target.HurtByTargetGoal;
import net.minecraft.world.entity.ai.goal.target.OwnerHurtByTargetGoal;
import net.minecraft.world.entity.ai.goal.target.OwnerHurtTargetGoal;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.level.Level;
import org.jspecify.annotations.Nullable;

/** A Pal (one row of sheets/pals.json). Wild ones wander and flee when hit; caught ones follow and fight for their owner. */
public class PalEntity extends TamableAnimal {
	public final Sheets.Pal pal;

	public PalEntity(EntityType<? extends PalEntity> type, Level level, Sheets.Pal pal) {
		super(type, level);
		this.pal = pal;
	}

	public static AttributeSupplier.Builder attributes(Sheets.Pal pal) {
		return TamableAnimal.createAnimalAttributes()
			.add(Attributes.MAX_HEALTH, pal.health())
			.add(Attributes.ATTACK_DAMAGE, pal.attack())
			.add(Attributes.MOVEMENT_SPEED, pal.speed())
			.add(Attributes.FOLLOW_RANGE, 24.0);
	}

	@Override
	protected void registerGoals() {
		this.goalSelector.addGoal(1, new FloatGoal(this));
		this.goalSelector.addGoal(2, new MeleeAttackGoal(this, 1.2, true));
		this.goalSelector.addGoal(3, new WildPanicGoal(this));
		this.goalSelector.addGoal(4, new FollowOwnerGoal(this, 1.1, 8.0F, 2.5F));
		this.goalSelector.addGoal(6, new WaterAvoidingRandomStrollGoal(this, 0.9));
		this.goalSelector.addGoal(7, new LookAtPlayerGoal(this, Player.class, 8.0F));
		this.goalSelector.addGoal(8, new RandomLookAroundGoal(this));
		this.targetSelector.addGoal(1, new OwnerHurtByTargetGoal(this));
		this.targetSelector.addGoal(2, new OwnerHurtTargetGoal(this));
		this.targetSelector.addGoal(3, new TamedHurtByTargetGoal(this));
	}

	@Override
	protected Component getTypeName() {
		return Component.literal(PalData.name(pal));
	}

	@Override
	public boolean isFood(ItemStack itemStack) {
		return false;
	}

	@Override
	public @Nullable AgeableMob getBreedOffspring(ServerLevel level, AgeableMob partner) {
		return null;
	}

	@Override
	public boolean removeWhenFarAway(double distSqr) {
		return false;
	}

	/** Wild Pals run when hurt; your own Pals stand and fight. */
	static class WildPanicGoal extends PanicGoal {
		private final PalEntity pal;
		WildPanicGoal(PalEntity pal) { super(pal, 1.4); this.pal = pal; }
		@Override public boolean canUse() { return !pal.isTame() && super.canUse(); }
	}

	/** Only a caught Pal hits back. */
	static class TamedHurtByTargetGoal extends HurtByTargetGoal {
		private final PalEntity pal;
		TamedHurtByTargetGoal(PalEntity pal) { super(pal); this.pal = pal; }
		@Override public boolean canUse() { return pal.isTame() && super.canUse(); }
	}
}

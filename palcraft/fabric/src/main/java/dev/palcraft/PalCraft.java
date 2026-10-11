package dev.palcraft;

import dev.palcraft.entity.PalEntity;
import dev.palcraft.entity.PalSphereEntity;
import dev.palcraft.gen.Sheets;
import dev.palcraft.item.PalSphereItem;
import java.util.LinkedHashMap;
import java.util.Map;
import net.fabricmc.api.ModInitializer;
import net.fabricmc.fabric.api.biome.v1.BiomeModifications;
import net.fabricmc.fabric.api.biome.v1.BiomeSelectors;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.fabricmc.fabric.api.networking.v1.ServerPlayConnectionEvents;
import net.fabricmc.fabric.api.object.builder.v1.entity.FabricDefaultAttributeRegistry;
import net.minecraft.core.Registry;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.Identifier;
import net.minecraft.resources.ResourceKey;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.MobCategory;
import net.minecraft.world.entity.SpawnPlacementTypes;
import net.minecraft.world.entity.SpawnPlacements;
import net.minecraft.world.entity.animal.Animal;
import net.minecraft.world.item.Item;
import net.minecraft.world.level.levelgen.Heightmap;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class PalCraft implements ModInitializer {
	public static final String MOD_ID = "palcraft";
	public static final Logger LOG = LoggerFactory.getLogger("PalCraft");

	/** One entity type per row of sheets/pals.json, in sheet order. */
	public static final Map<String, EntityType<PalEntity>> PAL_TYPES = new LinkedHashMap<>();
	public static EntityType<PalSphereEntity> PAL_SPHERE_ENTITY;
	public static Item PAL_SPHERE;

	public static Identifier id(String path) {
		return Identifier.fromNamespaceAndPath(MOD_ID, path);
	}

	@Override
	public void onInitialize() {
		for (Sheets.Pal pal : Sheets.PALS) {
			ResourceKey<EntityType<?>> key = ResourceKey.create(Registries.ENTITY_TYPE, id(pal.id()));
			EntityType<PalEntity> type = Registry.register(BuiltInRegistries.ENTITY_TYPE, key,
				EntityType.Builder.<PalEntity>of((t, level) -> new PalEntity(t, level, pal), MobCategory.CREATURE)
					.sized(pal.width(), pal.height()).clientTrackingRange(10).build(key));
			PAL_TYPES.put(pal.id(), type);
			FabricDefaultAttributeRegistry.register(type, PalEntity.attributes(pal));
			SpawnPlacements.register(type, SpawnPlacementTypes.ON_GROUND, Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, Animal::checkAnimalSpawnRules);
			BiomeModifications.addSpawn(
				BiomeSelectors.includeByKey(pal.spawnBiomes().stream()
					.map(b -> ResourceKey.create(Registries.BIOME, Identifier.withDefaultNamespace(b))).toList()),
				MobCategory.CREATURE, type, pal.spawnWeight(), pal.groupMin(), pal.groupMax());
		}

		Sheets.ItemRow sphere = Sheets.ITEMS.getFirst();
		ResourceKey<Item> itemKey = ResourceKey.create(Registries.ITEM, id(sphere.id()));
		PAL_SPHERE = Registry.register(BuiltInRegistries.ITEM, itemKey,
			new PalSphereItem(new Item.Properties().stacksTo(sphere.maxStack()).setId(itemKey)));

		ResourceKey<EntityType<?>> sphereKey = ResourceKey.create(Registries.ENTITY_TYPE, id(sphere.id()));
		PAL_SPHERE_ENTITY = Registry.register(BuiltInRegistries.ENTITY_TYPE, sphereKey,
			EntityType.Builder.<PalSphereEntity>of(PalSphereEntity::new, MobCategory.MISC)
				.noLootTable().sized(0.25F, 0.25F).clientTrackingRange(4).updateInterval(10).build(sphereKey));

		ServerPlayConnectionEvents.JOIN.register((handler, sender, server) -> Starter.onJoin(handler.player));
		ServerTickEvents.END_SERVER_TICK.register(MeltyTest::serverTick);
		LOG.info("PalCraft {} ready: {} Pals ({})", version(), PAL_TYPES.size(), String.join(", ", PAL_TYPES.keySet()));
	}

	public static String version() {
		return net.fabricmc.loader.api.FabricLoader.getInstance().getModContainer(MOD_ID)
			.map(c -> c.getMetadata().getVersion().getFriendlyString()).orElse("?");
	}
}

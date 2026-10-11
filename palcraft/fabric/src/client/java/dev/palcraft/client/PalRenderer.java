package dev.palcraft.client;

import com.mojang.blaze3d.vertex.PoseStack;
import com.mojang.blaze3d.vertex.VertexConsumer;
import com.mojang.math.Axis;
import dev.palcraft.entity.PalEntity;
import net.minecraft.client.renderer.SubmitNodeCollector;
import net.minecraft.client.renderer.entity.EntityRenderer;
import net.minecraft.client.renderer.entity.EntityRendererProvider;
import net.minecraft.client.renderer.entity.LivingEntityRenderer;
import net.minecraft.client.renderer.rendertype.RenderTypes;
import net.minecraft.client.renderer.state.level.CameraRenderState;
import net.minecraft.util.Mth;

/** Draws a Pal with its own Palworld mesh and textures (bind pose) with a little walk bob and lean. */
public class PalRenderer extends EntityRenderer<PalEntity, PalRenderState> {
	public PalRenderer(EntityRendererProvider.Context context, float shadow) {
		super(context);
		this.shadowRadius = shadow;
	}

	@Override
	public PalRenderState createRenderState() {
		return new PalRenderState();
	}

	@Override
	public void extractRenderState(PalEntity entity, PalRenderState state, float partialTicks) {
		super.extractRenderState(entity, state, partialTicks);
		state.palId = entity.pal.id();
		state.height = entity.getBbHeight();
		state.tame = entity.isTame();
		state.bodyRot = Mth.rotLerp(partialTicks, entity.yBodyRotO, entity.yBodyRot);
		state.walkAnimationPos = entity.walkAnimation.position(partialTicks);
		state.walkAnimationSpeed = entity.walkAnimation.speed(partialTicks);
		state.deathTime = entity.deathTime > 0 ? entity.deathTime + partialTicks : 0;
		state.hasRedOverlay = entity.hurtTime > 0 || entity.deathTime > 0;
	}

	@Override
	public void submit(PalRenderState state, PoseStack poseStack, SubmitNodeCollector collector, CameraRenderState camera) {
		PalMesh mesh = PalMesh.get(state.palId);
		if (mesh != null) {
			poseStack.pushPose();
			float bob = Math.abs(Mth.sin(state.walkAnimationPos * 0.6F)) * 0.08F * Math.min(1F, state.walkAnimationSpeed);
			poseStack.translate(0F, bob * state.height, 0F);
			poseStack.rotateDegrees(Axis.YP, -state.bodyRot);
			poseStack.rotateDegrees(Axis.ZP, Mth.sin(state.walkAnimationPos * 0.6F) * 6F * Math.min(1F, state.walkAnimationSpeed));
			if (state.deathTime > 0) poseStack.rotateDegrees(Axis.ZP, Math.min(1F, state.deathTime / 20F * 1.6F) * 90F);
			poseStack.scale(state.height, state.height, state.height);
			int light = state.lightCoords;
			int overlay = LivingEntityRenderer.getOverlayCoords(state, 0F);
			for (PalMesh.Section s : mesh.sections) {
				collector.submitCustomGeometry(poseStack, RenderTypes.entityCutout(s.texture()), (pose, vc) -> draw(mesh, s.indices(), pose, vc, light, overlay));
			}
			poseStack.popPose();
		}
		super.submit(state, poseStack, collector, camera);
	}

	/** Triangles as quads with the last corner repeated (entity render types are quad lists). */
	private static void draw(PalMesh m, int[] idx, PoseStack.Pose pose, VertexConsumer vc, int light, int overlay) {
		float[] p = m.pos, uv = m.uv;
		for (int t = 0; t + 2 < idx.length; t += 3) {
			int a = idx[t], b = idx[t + 1], c = idx[t + 2];
			float ux = p[b * 3] - p[a * 3], uy = p[b * 3 + 1] - p[a * 3 + 1], uz = p[b * 3 + 2] - p[a * 3 + 2];
			float wx = p[c * 3] - p[a * 3], wy = p[c * 3 + 1] - p[a * 3 + 1], wz = p[c * 3 + 2] - p[a * 3 + 2];
			float nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
			float len = Mth.sqrt(nx * nx + ny * ny + nz * nz);
			if (len < 1e-9F) continue;
			nx /= len; ny /= len; nz /= len;
			vertex(vc, pose, p, uv, a, light, overlay, nx, ny, nz);
			vertex(vc, pose, p, uv, b, light, overlay, nx, ny, nz);
			vertex(vc, pose, p, uv, c, light, overlay, nx, ny, nz);
			vertex(vc, pose, p, uv, c, light, overlay, nx, ny, nz);
		}
	}

	private static void vertex(VertexConsumer vc, PoseStack.Pose pose, float[] p, float[] uv, int i, int light, int overlay, float nx, float ny, float nz) {
		vc.addVertex(pose, p[i * 3], p[i * 3 + 1], p[i * 3 + 2])
			.setColor(255, 255, 255, 255)
			.setUv(uv[i * 2], uv[i * 2 + 1])
			.setOverlay(overlay)
			.setLight(light)
			.setNormal(pose, nx, ny, nz);
	}
}

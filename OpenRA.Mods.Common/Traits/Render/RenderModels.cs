#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public interface IRenderActorPreviewModelsInfo : ITraitInfoInterface
	{
		IEnumerable<ModelAnimation> RenderPreviewModels(
			ActorPreviewInitializer init, RenderModelsInfo rv, string image, Func<WRot> orientation, int facings, PaletteReference p);
	}

	public class RenderModelsInfo : TraitInfo, IRenderActorPreviewInfo, Requires<BodyOrientationInfo>
	{
		[Desc("Defaults to the actor name.")]
		public readonly string Image = null;

		[Desc("A dictionary of faction-specific image overrides.")]
		public readonly Dictionary<string, string> FactionImages = null;

		[Desc("Custom palette name")]
		[PaletteReference]
		public readonly string Palette = null;

		[PaletteReference]
		[Desc("Custom PlayerColorPalette: BaseName")]
		public readonly string PlayerPalette = "player";

		[PaletteReference]
		public readonly string NormalsPalette = null;

		[PaletteReference]
		public readonly string ShadowPalette = null;

		[Desc("Change the image size.")]
		public readonly float Scale = 1;

		public readonly float LightScale = 0.4f;
		public readonly float SpecularScale = 0.13f;
		public readonly float AmbientScale = 1.12f;

		public readonly int ZOffset = 1;

		public readonly WAngle LightPitch = WAngle.FromDegrees(50);
		public readonly WAngle LightYaw = WAngle.FromDegrees(240);
		public readonly float[] LightAmbientColor = { 0.6f, 0.6f, 0.6f };
		public readonly float[] LightDiffuseColor = { 0.4f, 0.4f, 0.4f };

		public override object Create(ActorInitializer init) { return new RenderModels(init.Self, this); }

		public virtual IEnumerable<IActorPreview> RenderPreview(ActorPreviewInitializer init)
		{
			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var faction = init.GetValue<FactionInit, string>(this);
			var ownerName = init.Get<OwnerInit>().InternalName;
			var sequenceProvider = init.World.Map.Rules.Sequences;
			var image = Image ?? init.Actor.Name;
			if (FactionImages != null && !string.IsNullOrEmpty(faction) && FactionImages.TryGetValue(faction, out var factionImage))
				image = factionImage;
			image = image.ToLowerInvariant();
			var facings = body.QuantizedFacings == -1 ?
				init.Actor.TraitInfo<IQuantizeBodyOrientationInfo>().QuantizedBodyFacings(init.Actor, sequenceProvider, faction) :
				body.QuantizedFacings;
			var palette = init.WorldRenderer.Palette(Palette ?? PlayerPalette + ownerName);

			var components = init.Actor.TraitInfos<IRenderActorPreviewModelsInfo>()
				.SelectMany(rvpi => rvpi.RenderPreviewModels(init, this, image, init.GetOrientation(), facings, palette))
				.ToArray();

			yield return new ModelPreview(components, WVec.Zero, 0, Scale, LightAmbientColor, LightDiffuseColor, body.CameraPitch, LightScale, AmbientScale, SpecularScale,
				palette, init.WorldRenderer.Palette(NormalsPalette), init.WorldRenderer.Palette(ShadowPalette));
		}
	}

	public class RenderModels : IRender, ITick, INotifyOwnerChanged, INotifyCreated
	{
		public float ScaleOverride = 1;

		class AnimationWrapper
		{
			readonly ModelAnimation model;
			bool cachedVisible;
			WVec cachedOffset;

			public AnimationWrapper(ModelAnimation model)
			{
				this.model = model;
			}

			public bool Tick()
			{
				// Return to the caller whether the renderable position or size has changed
				var visible = model.IsVisible;
				var offset = model.OffsetFunc?.Invoke() ?? WVec.Zero;

				var updated = visible != cachedVisible || offset != cachedOffset;
				cachedVisible = visible;
				cachedOffset = offset;

				return updated;
			}
		}

		public readonly RenderModelsInfo Info;

		readonly List<ModelAnimation> components = new List<ModelAnimation>();
		readonly Dictionary<ModelAnimation, AnimationWrapper> wrappers = new Dictionary<ModelAnimation, AnimationWrapper>();

		readonly Actor self;
		readonly BodyOrientation body;
		readonly WRot camera;
		readonly WRot lightSource;
		readonly string faction;
		public ITwistActorMesh[] AllTwistor;

		public RenderModels(Actor self, RenderModelsInfo info)
		{
			this.self = self;
			faction = self.Owner.Faction.InternalName;
			Info = info;
			ScaleOverride = info.Scale;
			body = self.Trait<BodyOrientation>();
			camera = new WRot(WAngle.Zero, body.CameraPitch - new WAngle(256), new WAngle(256));
			lightSource = new WRot(WAngle.Zero, new WAngle(256) - info.LightPitch, info.LightYaw);
		}

		bool initializePalettes = true;
		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { initializePalettes = true; }

		public void Created(Actor self)
		{
			AllTwistor = self.TraitsImplementing<ITwistActorMesh>().ToArray();
		}

		void ITick.Tick(Actor self)
		{
			var updated = false;
			foreach (var w in wrappers.Values)
				updated |= w.Tick();

			if (updated)
				self.World.ScreenMap.AddOrUpdate(self);
		}

		protected PaletteReference colorPalette, normalsPalette, shadowPalette;
		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			if (initializePalettes)
			{
				var paletteName = Info.Palette ?? Info.PlayerPalette + self.Owner.InternalName;
				colorPalette = wr.Palette(paletteName);
				normalsPalette = wr.Palette(Info.NormalsPalette);
				shadowPalette = wr.Palette(Info.ShadowPalette);
				initializePalettes = false;
			}

			bool twist = false;
			foreach (var t in AllTwistor)
			{
				if (t.IsTwisting)
				{
					twist = true;
					break;
				}
			}

			var tint = float3.Ones;
			if (twist)
				tint = 0.5f * Color.ToFloat3(self.Owner.Color) + float3.Half;

			return new IRenderable[]
			{
				new ModelRenderable(
					components, self.CenterPosition, Info.ZOffset, camera, ScaleOverride,
					Info.LightAmbientColor, Info.LightDiffuseColor, Info.LightScale, Info.AmbientScale, Info.SpecularScale,
					colorPalette, normalsPalette, shadowPalette, 1f, tint, TintModifiers.None, twist)
			};
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			var pos = self.CenterPosition;
			foreach (var c in components)
				if (c.IsVisible)
					yield return c.ScreenBounds(pos, wr, ScaleOverride);
		}

		public string Image
		{
			get
			{
				if (Info.FactionImages != null && !string.IsNullOrEmpty(faction) && Info.FactionImages.TryGetValue(faction, out var factionImage))
					return factionImage.ToLowerInvariant();

				return (Info.Image ?? self.Info.Name).ToLowerInvariant();
			}
		}

		public void Add(ModelAnimation m)
		{
			components.Add(m);
			wrappers.Add(m, new AnimationWrapper(m));
		}

		public void Remove(ModelAnimation m)
		{
			components.Remove(m);
			wrappers.Remove(m);
		}
	}
}

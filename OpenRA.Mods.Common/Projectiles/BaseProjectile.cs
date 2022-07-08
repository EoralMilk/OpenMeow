using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Projectiles
{
	public abstract class CorporealProjectileInfo
	{
		[Desc("Projectile speed in WDist / tick, two values indicate variable velocity.")]
		public readonly WDist[] Speed = { new WDist(17) };

		[Desc("The maximum/constant/incremental inaccuracy used in conjunction with the InaccuracyType property.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("Inaccuracy value in Vertical space.")]
		public readonly WDist VerticalInaccuracy = WDist.Zero;

		[Desc("Controls the way inaccuracy is calculated. Possible values are 'Maximum' - scale from 0 to max with range, 'PerCellIncrement' - scale from 0 with range and 'Absolute' - use set value regardless of range.")]
		public readonly InaccuracyType InaccuracyType = InaccuracyType.Maximum;

		[Desc("Image to display.")]
		public readonly string Image = null;

		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("Loop a randomly chosen sequence of Image from this list while this projectile is moving.")]
		public readonly string[] Sequences = { "idle" };

		[PaletteReference(nameof(IsPlayerPalette))]
		[Desc("The palette used to draw this projectile.")]
		public readonly string Palette = "effect";

		[Desc("Palette is a player palette BaseName")]
		public readonly bool IsPlayerPalette = false;

		[Desc("Does this projectile have a shadow?")]
		public readonly bool Shadow = false;

		[Desc("Color to draw shadow if Shadow is true.")]
		public readonly Color ShadowColor = Color.FromArgb(140, 0, 0, 0);

		[Desc("Image that contains the jet animation")]
		public readonly string JetImage = null;

		[SequenceReference(nameof(JetImage), allowNullImage: true)]
		[Desc("Loop a randomly chosen sequence of JetImage from this list while this projectile is moving.")]
		public readonly string[] JetSequence = { "idle" };

		[PaletteReference(nameof(JetUsePlayerPalette))]
		[Desc("Palette used to render the jet sequence. ")]
		public readonly string JetPalette = "effect";

		[Desc("Use the Player Palette to render the jet sequence.")]
		public readonly bool JetUsePlayerPalette = false;

		[Desc("Trail animation.")]
		public readonly string TrailImage = null;

		[Desc("Trail animation render counts.")]
		public readonly int TrailCount = 1;

		[SequenceReference(nameof(TrailImage), allowNullImage: true)]
		[Desc("Loop a randomly chosen sequence of TrailImage from this list while this projectile is moving.")]
		public readonly string[] TrailSequences = { "idle" };

		[Desc("Interval in ticks between each spawned Trail animation.")]
		public readonly int TrailInterval = 2;

		[Desc("Delay in ticks until trail animation is spawned.")]
		public readonly int TrailDelay = 1;

		[PaletteReference(nameof(TrailUsePlayerPalette))]
		[Desc("Palette used to render the trail sequence.")]
		public readonly string TrailPalette = "effect";

		[Desc("Use the Player Palette to render the trail sequence.")]
		public readonly bool TrailUsePlayerPalette = false;

		[Desc("Is this blocked by actors with BlocksProjectiles trait.")]
		public readonly bool Blockable = true;

		[Desc("Width of projectile (used for finding blocking actors).")]
		public readonly WDist Width = new WDist(1);

		[Desc("Always detect valid target in Width.")]
		public readonly bool AlwaysDetectTarget = false;

		[Desc("Only Explode when hit some thing, which means that the projectile can't explode in sky where the passive target is.")]
		public readonly bool OnlyHitToExplode = false;

		[Desc("Arc in WAngles, two values indicate variable arc.")]
		public readonly WAngle[] LaunchAngle = { WAngle.Zero };

		[Desc("Altitude where this bullet should explode when reached.",
			"Negative values allow this bullet to pass cliffs and terrain bumps.")]
		public readonly WDist ExplodeUnderThisAltitude = new WDist(-736);

		[Desc("How long this projectile can live, LifeTime < 0 means lives forever if not hit the target.")]
		public readonly int[] LifeTime = { -1 };

		public readonly int ContrailLength = 0;
		public readonly int ContrailZOffset = 2047;
		public readonly Color ContrailColor = Color.White;
		public readonly bool ContrailUsePlayerColor = false;
		public readonly int ContrailDelay = 1;
		public readonly WDist ContrailWidth = new WDist(64);
		public readonly BlendMode ContrailBlendMode = BlendMode.Alpha;
	}

	public abstract class CorporealProjectile
	{
		readonly CorporealProjectileInfo info;
		readonly ProjectileArgs args;

		bool hasInitPal = false;
		readonly Animation anim;
		readonly Animation jetanim;

		PaletteReference pal;
		PaletteReference jetPal;
		string trailPalette;

		readonly float3 shadowColor;
		readonly float shadowAlpha;

		readonly ContrailRenderable contrail;
		int trailTicks;
		WPos trailLastPos, pos;

		protected bool explode = false;

		public CorporealProjectile(CorporealProjectileInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			var world = args.SourceActor.World;

			if (!string.IsNullOrEmpty(info.Image))
			{
				anim = new Animation(world, info.Image, new Func<WAngle>(GetEffectiveFacing));
				anim.PlayRepeating(info.Sequences.Random(world.SharedRandom));
			}

			if (!string.IsNullOrEmpty(info.JetImage))
			{
				jetanim = new Animation(world, info.JetImage);
				jetanim.PlayRepeating(info.JetSequence.Random(world.SharedRandom));
			}

			if (info.ContrailLength > 0)
			{
				var color = info.ContrailUsePlayerColor ? ContrailRenderable.ChooseColor(args.SourceActor) : info.ContrailColor;
				contrail = new ContrailRenderable(world, color, info.ContrailWidth, info.ContrailLength, info.ContrailDelay, info.ContrailZOffset, info.ContrailBlendMode);
			}

			trailTicks = info.TrailDelay;
			trailLastPos = args.Source;

			shadowColor = new float3(info.ShadowColor.R, info.ShadowColor.G, info.ShadowColor.B) / 255f;
			shadowAlpha = info.ShadowColor.A / 255f;
		}

		protected virtual WAngle GetEffectiveFacing()
		{
			return WAngle.Zero;
		}

		protected virtual void RenderTick(World world, in WPos pos)
		{
			anim?.Tick();
			jetanim?.Tick();
			this.pos = pos;

			if (info.ContrailLength > 0)
				contrail.Update(pos);

			if (hasInitPal)
			{
				if (info.TrailImage != null && info.TrailCount > 0 && --trailTicks < 0)
				{
					var v = (pos - trailLastPos) / info.TrailCount;

					for (int i = 0; i < info.TrailCount; i++)
					{
						trailLastPos += v;
						var ppos = trailLastPos; // delayed pos
						world.AddFrameEndTask(w => w.Add(new SpriteEffect(ppos, w, info.TrailImage, info.TrailSequences.Random(world.SharedRandom), trailPalette)));
					}

					trailTicks = info.TrailInterval;
					trailLastPos = pos;
				}
			}
		}

		protected virtual void UpdatePalette(WorldRenderer wr)
		{
			var paletteName = info.Palette;
			if (paletteName != null && info.IsPlayerPalette)
				paletteName += args.SourceActor.Owner.InternalName;
			trailPalette = info.TrailPalette;
			if (info.TrailUsePlayerPalette)
				trailPalette += args.SourceActor.Owner.InternalName;

			pal = wr.Palette(paletteName);
			jetPal = wr.Palette(info.JetPalette + (info.JetUsePlayerPalette ? args.SourceActor.Owner.InternalName : ""));
		}

		public virtual IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (!hasInitPal)
			{
				UpdatePalette(wr);
				hasInitPal = true;
			}

			if (info.ContrailLength > 0)
				yield return contrail;

			if (explode)
				yield break;

			var world = args.SourceActor.World;
			if (!world.FogObscures(pos))
			{
				if (anim != null)
				{
					if (info.Shadow)
					{
						var dat = world.Map.DistanceAboveTerrain(pos);
						var shadowPos = pos - new WVec(0, 0, dat.Length);
						foreach (var r in anim.Render(shadowPos, pal))
							yield return ((IModifyableRenderable)r)
								.WithTint(shadowColor, ((IModifyableRenderable)r).TintModifiers | TintModifiers.ReplaceColor)
								.WithAlpha(shadowAlpha);
					}

					foreach (var r in anim.Render(pos, pal))
						yield return r;
				}

				if (jetanim != null)
				{
					foreach (var r in jetanim.Render(pos, jetPal))
						yield return r;
				}
			}
		}

		protected virtual void RenderExplode(World world, WPos pos)
		{
			if (info.ContrailLength > 0)
				world.AddFrameEndTask(w => w.Add(new ContrailFader(pos, contrail)));
		}
	}

}

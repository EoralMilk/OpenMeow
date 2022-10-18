using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
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
		public readonly bool UseVerticalInaccuracy = false;

		[Desc("Controls the way inaccuracy is calculated. Possible values are 'Maximum' - scale from 0 to max with range, 'PerCellIncrement' - scale from 0 with range and 'Absolute' - use set value regardless of range.")]
		public readonly InaccuracyType InaccuracyType = InaccuracyType.Maximum;

		[Desc("3D Unit to display.")]
		public readonly string Unit = null;

		[Desc("Loop a randomly chosen mesh of Unit from this list while this projectile is moving.")]
		public readonly string[] Meshes = { "idle" };

		[Desc("3D mesh display Scale.")]
		public readonly float MeshScale = 1f;

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

		[Desc("Determines what actors to detect based on their allegiance to the source owner.")]
		public readonly PlayerRelationship ValidDetectRelationships = PlayerRelationship.Neutral | PlayerRelationship.Enemy;

		[Desc("Only Explode when hit some thing, which means that the projectile can't explode in sky where the passive target is.")]
		public readonly bool OnlyHitToExplode = false;

		[Desc("Arc in WAngles, two values indicate variable arc.")]
		public readonly WAngle[] LaunchAngle = { WAngle.Zero };

		[Desc("Altitude where this bullet should explode when reached.",
			"Negative values allow this bullet to pass cliffs and terrain bumps.")]
		public readonly WDist ExplodeUnderThisAltitude = new WDist(-64);

		[Desc("How long this projectile can live, LifeTime < 0 means lives forever if not hit the target.")]
		public readonly int[] LifeTime = { -1 };

		[Desc("Length of the contrail (in ticks).")]
		public readonly int ContrailLength = 0;

		[Desc("Offset for contrail's Z sorting.")]
		public readonly int ContrailZOffset = 2047;

		[Desc("Delay of the contrail.")]
		public readonly int ContrailDelay = 1;

		[Desc("Width of the contrail.")]
		public readonly WDist ContrailWidth = new WDist(64);

		// color
		public readonly bool ContrailUseInnerOuterColor = false;

		// _start
		[Desc("Use player remap color instead of a custom color when the contrail starts.")]
		public readonly bool ContrailStartColorUsePlayerColor = false;

		[Desc("RGB color when the contrail starts.")]
		public readonly Color ContrailStartColor = Color.White;

		[Desc("RGB Outer color when the contrail starts.")]
		public readonly Color ContrailStartColorOuter = Color.White;

		[Desc("The alpha value [from 0 to 255] of color when the contrail starts.")]
		public readonly int ContrailStartColorAlpha = 255;

		[Desc("The alpha value [from 0 to 255] of Outer color when the contrail starts.")]
		public readonly int ContrailStartColorAlphaOuter = 255;

		// _end
		[Desc("Use player remap color instead of a custom color when the contrail ends.")]
		public readonly bool ContrailEndColorUsePlayerColor = false;

		[Desc("RGB color when the contrail ends.")]
		public readonly Color ContrailEndColor = Color.White;

		[Desc("RGB Outer color when the contrail ends.")]
		public readonly Color ContrailEndColorOuter = Color.White;

		[Desc("The alpha value [from 0 to 255] of color when the contrail ends.")]
		public readonly int ContrailEndColorAlpha = 0;

		[Desc("The alpha value [from 0 to 255] of Outer color when the contrail ends.")]
		public readonly int ContrailEndColorAlphaOuter = 0;

		// fade rate
		[Desc("Contrail will fade with contrail width. Set 1.0 to make contrail fades just by length. Can be set with negative value")]
		public readonly float ContrailWidthFadeRate = 0;

		[Desc("Contrail blendmode.")]
		public readonly BlendMode ContrailBlendMode = BlendMode.Alpha;
	}

	public abstract class CorporealProjectile
	{
		readonly CorporealProjectileInfo info;
		readonly ProjectileArgs args;

		protected	readonly TSVector Front;
		protected readonly TSQuaternion RotFix;

		Color remapColor = Color.White;
		readonly List<MeshInstance> meshes = new List<MeshInstance>();

		bool hasInitPal = false;
		readonly Animation anim;
		readonly Animation jetanim;
		protected bool renderJet = true;
		protected bool renderTrail = true;
		protected bool renderContrail = true;

		PaletteReference pal;
		PaletteReference jetPal;
		string trailPalette;

		readonly float3 shadowColor;
		readonly float shadowAlpha;

		readonly ContrailRenderable contrail;
		int trailTicks;
		WPos trailLastPos, pos, matLastPos;
		TSMatrix4x4 effectMatrix;
		protected WVec matVec = WVec.Zero;

		protected bool explode = false;

		public CorporealProjectile(CorporealProjectileInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			var world = args.SourceActor.World;
			Front = TSVector.forward; // World3DCoordinate.Front;
			RotFix = TSQuaternion.FromToRotation(Front, TSVector.forward);

			if (!string.IsNullOrEmpty(info.Unit))
			{
				if (args.Matrix != TSMatrix4x4.Identity)
				{
					effectMatrix = args.Matrix;
					matVec = World3DCoordinate.TSVec3ToWPos(Transformation.MatWithOutScale(effectMatrix) * Front) - args.Source;
				}
				else
				{
					matVec = args.PassiveTarget - args.Source;
				}

				if (args.SourceActor != null)
					remapColor = args.SourceActor.Owner.Color;
				var mesh = world.MeshCache.GetMeshSequence(info.Unit, info.Meshes[0]);
				meshes.Add(new MeshInstance(mesh,
					GetMatrix,
					() => true,
					null));
			}

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
				var startcolor = info.ContrailStartColorUsePlayerColor ? Color.FromArgb(info.ContrailStartColorAlpha, args.SourceActor.Owner.Color) : Color.FromArgb(info.ContrailStartColorAlpha, info.ContrailStartColor);
				var endcolor = info.ContrailEndColorUsePlayerColor ? Color.FromArgb(info.ContrailEndColorAlpha, args.SourceActor.Owner.Color) : Color.FromArgb(info.ContrailEndColorAlpha, info.ContrailEndColor);

				if (info.ContrailUseInnerOuterColor)
				{
					var startcolorOuter = info.ContrailStartColorUsePlayerColor ? Color.FromArgb(info.ContrailStartColorAlphaOuter, args.SourceActor.Owner.Color) : Color.FromArgb(info.ContrailStartColorAlphaOuter, info.ContrailStartColorOuter);
					var endcolorOuter = info.ContrailEndColorUsePlayerColor ? Color.FromArgb(info.ContrailEndColorAlphaOuter, args.SourceActor.Owner.Color) : Color.FromArgb(info.ContrailEndColorAlphaOuter, info.ContrailEndColorOuter);
					contrail = new ContrailRenderable(world, startcolor, endcolor, info.ContrailWidth, info.ContrailLength, info.ContrailDelay, info.ContrailZOffset, info.ContrailWidthFadeRate, info.ContrailBlendMode, startcolorOuter, endcolorOuter);
				}
				else
					contrail = new ContrailRenderable(world, startcolor, endcolor, info.ContrailWidth, info.ContrailLength, info.ContrailDelay, info.ContrailZOffset, info.ContrailWidthFadeRate, info.ContrailBlendMode);
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

		protected virtual TSMatrix4x4 GetMatrix()
		{
			var effectFacing = TSQuaternion.FromToRotation(Front, World3DCoordinate.WVecToTSVec3(matVec));
			effectMatrix = TSMatrix4x4.Rotate(effectFacing);
			effectMatrix.SetTranslatePart(World3DCoordinate.WPosToTSVec3(pos));
			return Transformation.MatWithNewScale(effectMatrix, info.MeshScale);
		}

		bool renderTickStarted = false;
		protected virtual void RenderTick(World world, in WPos pos)
		{
			anim?.Tick();
			if (renderJet)
				jetanim?.Tick();
			this.pos = pos;
			matVec = pos - matLastPos;
			matLastPos = pos;

			if (info.ContrailLength > 0 && renderContrail)
				contrail.Update(pos);

			if (hasInitPal)
			{
				if (info.TrailImage != null && info.TrailCount > 0 && --trailTicks < 0 && renderTrail)
				{
					var v = (pos - trailLastPos) / info.TrailCount;

					for (var i = 0; i < info.TrailCount; i++)
					{
						trailLastPos += v;
						var ppos = trailLastPos; // delayed pos
						world.AddFrameEndTask(w => w.Add(new SpriteEffect(ppos, w, info.TrailImage, info.TrailSequences.Random(world.SharedRandom), trailPalette)));
					}

					trailTicks = info.TrailInterval;
					trailLastPos = pos;
				}
			}

			renderTickStarted = true;
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

		protected virtual IEnumerable<IRenderable> RenderSelf(WorldRenderer wr) { return Array.Empty<IRenderable>(); }

		public virtual IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (!hasInitPal)
			{
				UpdatePalette(wr);
				hasInitPal = true;
			}

			if (info.ContrailLength > 0 && renderContrail)
				yield return contrail;

			if (explode)
				yield break;

			var world = args.SourceActor.World;
			if (!world.FogObscures(pos))
			{
				var rs = RenderSelf(wr);
				foreach (var r in rs)
				{
					yield return r;
				}

				if (renderTickStarted && meshes.Count > 0)
					yield return new MeshRenderable(meshes, pos, 0, args.SourceActor.Owner.Color, info.MeshScale, null);

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

				if (jetanim != null && renderJet)
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

		public bool FirstValidTargetsOnLine(World world, WPos lineStart, WPos lineEnd, WDist lineWidth, Actor firedBy, bool checkTargetType, Actor targetActor, out WPos hitPos, out Actor hitActor, bool onlyBlockers = false)
		{
			// This line intersection check is done by first just finding all actors within a square that starts at the source, and ends at the target.
			// Then we iterate over this list, and find all actors for which their health radius is at least within lineWidth of the line.
			// For actors without a health radius, we simply check their center point.
			// The square in which we select all actors must be large enough to encompass the entire line's width.
			// xDir and yDir must never be 0, otherwise the overscan will be 0 in the respective direction.
			var xDiff = lineEnd.X - lineStart.X;
			var yDiff = lineEnd.Y - lineStart.Y;
			var xDir = xDiff < 0 ? -1 : 1;
			var yDir = yDiff < 0 ? -1 : 1;

			var dir = new WVec(xDir, yDir, 0);
			var largestValidActorRadius = onlyBlockers ? world.ActorMap.LargestBlockingActorRadius.Length : world.ActorMap.LargestActorRadius.Length;
			var overselect = dir * (1024 + lineWidth.Length + largestValidActorRadius);
			var finalTarget = lineEnd + overselect;
			var finalSource = lineStart - overselect;

			var actorsInSquare = world.ActorMap.ActorsInBox(finalTarget, finalSource);
			hitActor = null;
			var intersectedActors = new List<Actor>();
			var min = (lineStart - lineEnd).Length;
			var temp = 0;
			WPos tempHit;
			hitPos = lineEnd;
			foreach (var currActor in actorsInSquare)
			{
				if (currActor == firedBy || firedBy == null)
					continue;

				if (!((targetActor != null && currActor == targetActor) || info.ValidDetectRelationships.HasRelationship(currActor.Owner.RelationshipWith(firedBy.Owner))))
					continue;

				var shapes = currActor.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled);
				var checkPos = lineStart.MinimumPointLineProjection(lineEnd, currActor.CenterPosition);

				foreach (var shape in shapes)
				{
					var cedge = shape.DistanceFromEdge(currActor, checkPos);
					if (cedge <= lineWidth)
					{
						tempHit = shape.GetHitPos(currActor, checkPos);
						tempHit = lineStart.MinimumPointLineProjection(lineEnd, tempHit);
						temp = (lineStart - tempHit).Length;
						if (temp <= min)
						{
							min = temp;
							hitActor = currActor;
							hitPos = tempHit;
						}
					}
				}
			}

			if (hitActor != null)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}

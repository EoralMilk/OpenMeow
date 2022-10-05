using System;
using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using TagLib.Flac;

namespace OpenRA.Mods.Common.Projectiles
{
	[Desc("The projectile would remain in flight, release the warhead at intervals, " +
		"fly a specified distance, release the warhead again and destroy it, stop flying when blocked, " +
		"but remain in place until it had reached the time (in terms of speed and distance) needed to fly")]
	public class BlastWaveInfo : IProjectileInfo
	{
		[Desc("Projectile speed in WDist / tick, two values indicate variable velocity.")]
		public readonly WDist[] Speed = { new WDist(17) };

		[Desc("The distance traveled by the shockwave projectile.")]
		public readonly WDist ShockDist = new WDist(2048);

		[Desc("Maximum offset at the maximum range.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("Image to display.")]
		public readonly string Image = null;

		[SequenceReference(nameof(Image), allowNullImage: true)]
		[Desc("Loop a randomly chosen sequence of Image from this list while this projectile is moving.")]
		public readonly string[] Sequences = { "idle" };

		[Desc("The palette used to draw this projectile.")]
		[PaletteReference("IsPlayerPalette")]
		public readonly string Palette = "effect";

		public readonly bool IsPlayerPalette = false;

		[Desc("Is this blocked by actors with BlocksProjectiles trait.")]
		public readonly bool Blockable = true;

		[Desc("Is this blocked by actors with BlocksEnemyProjectiles trait.")]
		public readonly bool EnemyBlockable = true;

		[Desc("Width of projectile (used for finding blocking actors).")]
		public readonly WDist Width = new WDist(1);

		[Desc("Altitude where this bullet should explode when reached.",
			"Negative values allow this bullet to pass cliffs and terrain bumps.")]
		public readonly WDist ExplodeUnderThisAltitude = new WDist(-1536);

		[Desc("Interval in ticks between each spawned Warheads.")]
		public readonly int BlastInterval = 5;

		[Desc("Delay Before spawned first Warheads.")]
		public readonly int BlastDelay = 5;

		[Desc("Keep flying height as source height?")]
		public readonly bool KeepSourceAltitude = true;

		public IProjectile Create(ProjectileArgs args) { return new BlastWave(this, args); }
	}

	public class BlastWave : IProjectile, ISync
	{
		readonly BlastWaveInfo info;
		readonly ProjectileArgs args;
		readonly Animation anim;

		readonly WAngle angle;
		readonly WDist speed;
		readonly WAngle facing;

		readonly string palette;

		[Sync]
		WPos pos, lastPos;

		readonly WPos target, source;

		readonly int length;
		Actor blocker;
		readonly int lifetime, blastInterval;

		int liveTicks, blastTicks;
		bool stopped = false;
		public Actor SourceActor { get { return args.SourceActor; } }

		public BlastWave(BlastWaveInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			pos = args.Source;
			source = args.Source;

			var world = args.SourceActor.World;

			palette = info.Palette;
			if (info.IsPlayerPalette)
				palette += args.SourceActor.Owner.InternalName;

			angle = WAngle.Zero;

			if (info.Speed.Length > 1)
				speed = new WDist(world.SharedRandom.Next(info.Speed[0].Length, info.Speed[1].Length));
			else
				speed = info.Speed[0];

			target = args.PassiveTarget;
			if (info.Inaccuracy.Length > 0)
			{
				var inaccuracy = Util.ApplyPercentageModifiers(info.Inaccuracy.Length, args.InaccuracyModifiers);
				var range = Util.ApplyPercentageModifiers(args.Weapon.Range.Length, args.RangeModifiers);
				var maxOffset = inaccuracy * (target - pos).Length / range;
				target += WVec.FromPDF(world.SharedRandom, 2) * maxOffset / 1024;
			}

			if (info.KeepSourceAltitude)
				target = new WPos(target.X, target.Y, source.Z); // Keep height

			facing = (target - pos).Yaw;
			length = Math.Max((target - pos).Length / speed.Length, 1);

			lifetime = info.ShockDist.Length / speed.Length; // cal life time
			liveTicks = 0;

			blastInterval = info.BlastInterval;
			blastTicks = info.BlastInterval - info.BlastDelay;

			if (!string.IsNullOrEmpty(info.Image))
			{
				anim = new Animation(world, info.Image, () => facing);
				anim.PlayFetchIndex(info.Sequences.Random(world.SharedRandom),
						() => int2.Lerp(0, anim.CurrentSequence.Length, liveTicks, lifetime + 1));
			}
		}

		public void Tick(World world)
		{
			anim?.Tick();

			lastPos = pos;

			if (!stopped)
				pos = WPos.LerpQuadratic(source, target, angle, liveTicks, length);

			if (!stopped && ShouldStopFly(world))
			{
				stopped = true;
			}

			liveTicks++;
			blastTicks++;
			if (blastTicks >= blastInterval && blastInterval > 0)
			{
				Blast();
				blastTicks = 0;
			}

			if (lifetime > 0 && liveTicks > lifetime)
			{
				Explode(world);
			}
		}

		bool ShouldStopFly(World world)
		{
			if (info.Blockable && BlocksProjectiles.AnyBlockingActorsBetween(world, args.SourceActor.Owner, lastPos, pos, info.Width,
				out var blockedPos, out blocker, args))
			{
				pos = blockedPos;
				return true;
			}

			// Driving into cell with different height level
			if (world.Map.DistanceAboveTerrain(pos) < info.ExplodeUnderThisAltitude)
				return true;

			return false;
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			var world = args.SourceActor.World;
			if (!world.FogObscures(pos))
			{
				if (anim != null)
				{
					// var palette = wr.Palette(info.Palette + (info.IsPlayerPalette ? args.SourceActor.Owner.InternalName : ""));
					foreach (var r in anim.Render(pos, wr.Palette(palette)))
						yield return r;
				}
			}
		}

		void Blast()
		{
			var warheadArgs = new WarheadArgs(args)
			{
				ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
				ImpactPosition = pos,
				Blocker = blocker,
			};

			args.Weapon.Impact(Target.FromPos(pos), warheadArgs);
		}

		void Explode(World world)
		{
			var warheadArgs = new WarheadArgs(args)
			{
				ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), args.Facing),
				ImpactPosition = pos,
				Blocker = blocker,
			};

			args.Weapon.Impact(Target.FromPos(pos), warheadArgs);

			world.AddFrameEndTask(w => w.Remove(this));
		}
	}
}

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

		[Desc("Inaccuracy value in Vertical space.")]
		public readonly bool UseVerticalInaccuracy = false;

		[Desc("Maximum offset at the maximum range.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("Controls the way inaccuracy is calculated. Possible values are 'Maximum' - scale from 0 to max with range, 'PerCellIncrement' - scale from 0 with range and 'Absolute' - use set value regardless of range.")]
		public readonly InaccuracyType InaccuracyType = InaccuracyType.Maximum;

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

		[Desc("Mess it up when it blocked by something.")]
		public readonly bool Chaos = false;

		[Desc("Mess it up at horizontal.")]
		public readonly WDist ChaosInaccuracy = WDist.Zero;

		[Desc("Mess it up at height，necessary for avoiding depth conflict.")]
		public readonly WDist ChaosHeightInaccuracy = new WDist(128);

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
		readonly WVec offset = WVec.Zero;

		[Sync]
		WPos pos, lastPos, target, source;

		int length;
		readonly int chaosheightadd;
		Actor blocker;
		readonly int lifetime, blastInterval;

		int liveTicks, blastTicks, moveTicks;
		bool stopped = false;

		bool exploded = false;
		public Actor SourceActor { get { return args.SourceActor; } }

		public BlastWave(BlastWaveInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			pos = args.Source;
			source = args.Source;
			lastPos = pos;

			if (info.Chaos && args.SourceActor != null)
				chaosheightadd = args.SourceActor.World.SharedRandom.Next(0, info.ChaosHeightInaccuracy.Length);

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
				var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.Inaccuracy.Length, info.InaccuracyType, args);
				offset = WVec.FromPDF(world.SharedRandom, 2, info.UseVerticalInaccuracy) * maxInaccuracyOffset / 1024;
			}

			target += offset;

			if (info.KeepSourceAltitude)
				target = new WPos(target.X, target.Y, source.Z); // Keep height

			facing = (target - pos).Yaw;
			length = Math.Max((target - pos).Length / speed.Length, 1);

			lifetime = info.ShockDist.Length / speed.Length; // cal life time
			liveTicks = 0;
			moveTicks = 0;

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
			if (exploded)
				return;

			anim?.Tick();

			lastPos = pos;

			if (!stopped)
				pos = WPos.LerpQuadratic(source, target, angle, moveTicks, length);

			if (!stopped && ShouldStopFly(world))
			{
				stopped = true;
			}

			liveTicks++;
			blastTicks++;
			moveTicks++;
			if (blastTicks >= blastInterval && blastInterval > 0)
			{
				Blast();
				blastTicks = 0;
			}

			if (lifetime > 0 && liveTicks > lifetime)
			{
				exploded = true;
				Explode(world);
			}
		}

		bool ShouldStopFly(World world)
		{
			if (info.Blockable && BlocksProjectiles.AnyBlockingActorsBetween(world, args.SourceActor.Owner, lastPos, pos, info.Width,
				out var blockedPos, out blocker, args))
			{
				pos = blockedPos;
				if (info.Chaos)
				{
					source = pos;

					if (info.ChaosInaccuracy.Length > 0)
					{
						var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.ChaosInaccuracy.Length, info.InaccuracyType, args);
						target = new WPos(target.X, target.Y, source.Z) + WVec.FromPDF(world.SharedRandom, 2, info.UseVerticalInaccuracy) * maxInaccuracyOffset / 1024;
					}

					length = Math.Max((target - pos).Length / speed.Length * 2, 1);
					moveTicks = 0;
				}
				else
					return true;
			}

			var posh = world.Map.HeightOfTerrain(pos);

			// Driving into cell with different height level
			if (pos.Z - posh < info.ExplodeUnderThisAltitude.Length - 1)
			{
				if (info.Chaos)
				{
					posh += chaosheightadd;
					source = new WPos(pos.X, pos.Y, posh + info.ExplodeUnderThisAltitude.Length);
					pos = source;
					if (info.ChaosInaccuracy.Length > 0)
					{
						var maxInaccuracyOffset = Util.GetProjectileInaccuracy(info.ChaosInaccuracy.Length, info.InaccuracyType, args);
						target = new WPos(target.X, target.Y, source.Z) + WVec.FromPDF(world.SharedRandom, 2, info.UseVerticalInaccuracy) * maxInaccuracyOffset / 1024;
					}

					length = Math.Max((target - source).Length / speed.Length * 2, 1);
					moveTicks = 0;
				}
				else
				{
					pos = new WPos(pos.X, pos.Y, posh + info.ExplodeUnderThisAltitude.Length);
				}

			}

			return false;
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			var world = args.SourceActor.World;
			if (!world.FogObscures(pos))
			{
				if (anim != null)
				{
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

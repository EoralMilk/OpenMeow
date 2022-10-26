using System;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Warheads
{
	[Desc("Fires weapons if the target cell is valid.")]
	public class TerrainReactionInitWarhead : Warhead, IRulesetLoaded<WeaponInfo>
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		[Desc("Start offset of the first fire target")]
		public readonly WVec StartOffset = new WVec(0, 0, 0);

		[Desc("Number of weapons to fire.")]
		public readonly int FireCount = 1;

		[Desc("Fire the weapon to the ground level.")]
		public readonly bool ForceTargetGround = true;

		[Desc("Percentage chance the smudge is created.")]
		public readonly int Chance = 100;

		WeaponInfo weapon;

		WVec[] facingOffsets;

		public void RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (!rules.Weapons.TryGetValue(Weapon.ToLowerInvariant(), out weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{Weapon.ToLowerInvariant()}'");
		}

		/// <summary>Checks if the warhead is valid against the terrain at impact position.</summary>
		bool IsValidAgainstTerrain(Map map, WPos pos)
		{
			var cell = map.CellContaining(pos);
			if (!map.Contains(cell))
				return false;
			var dat = map.DistanceAboveTerrain(pos);
			var tts = map.GetTerrainInfo(cell).TargetTypes;
			if (dat > AirThreshold)
				return false;

			return IsValidTarget(tts);
		}

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var firedBy = args.SourceActor;
			var world = firedBy.World;
			var map = firedBy.World.Map;

			if (FireCount <= 0 || !IsValidAgainstTerrain(map, target.CenterPosition))
				return;

			if (Chance < world.SharedRandom.Next(100))
				return;

			facingOffsets = Exts.MakeArray(FireCount, i => StartOffset.Rotate(WRot.FromFacing(i * 256 / FireCount)));

			foreach (var c in facingOffsets)
				FireProjectileAtOffset(world, map, firedBy, target, c, args);
		}

		void FireProjectileAtOffset(World world, Map map, Actor firedBy, Target target, WVec offset, WarheadArgs args)
		{
			var pos = target.CenterPosition + offset;
			if (ForceTargetGround)
				pos = new WPos(pos.X, pos.Y, map.HeightOfTerrain(pos));
			var cell = map.CellContaining(pos);
			var projectileArgs = new ProjectileArgs
			{
				Weapon = weapon,
				Facing = offset.Yaw,
				CurrentMuzzleFacing = () => offset.Yaw,

				DamageModifiers = args.DamageModifiers,
				InaccuracyModifiers = Array.Empty<int>(),
				RangeModifiers = Array.Empty<int>(),

				Source = target.CenterPosition,
				CurrentSource = () => target.CenterPosition,
				SourceActor = firedBy,
				PassiveTarget = pos,
				GuidedTarget = Target.FromTerrainPos(world, cell, pos)
			};

			if (projectileArgs.Weapon.Projectile != null)
			{
				var projectile = projectileArgs.Weapon.Projectile.Create(projectileArgs);
				if (projectile != null)
					firedBy.World.AddFrameEndTask(w => w.Add(projectile));

				if (projectileArgs.Weapon.Report != null && projectileArgs.Weapon.Report.Length > 0)
					Game.Sound.Play(SoundType.World, projectileArgs.Weapon.Report, firedBy.World, target.CenterPosition);
			}
		}

	}
}

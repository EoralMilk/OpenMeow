using System;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Warheads
{
	[Desc("Fires weapons from the point of impact.")]
	public class FireRadiusWarhead : Warhead, IRulesetLoaded<WeaponInfo>
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		[Desc("Start offset of the first fire target")]
		public readonly WVec StartOffset = new WVec(0, -1024, 0);

		[Desc("Number of weapons to fire.")]
		public readonly int FireCount = 1;

		[Desc("Fire the weapon to the ground level.")]
		public readonly bool ForceTargetGround = false;

		WeaponInfo weapon;

		WVec[] facingOffsets;

		public void RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (!rules.Weapons.TryGetValue(Weapon.ToLowerInvariant(), out weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{Weapon.ToLowerInvariant()}'");
		}

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			if (target.Type == TargetType.Invalid || FireCount <= 0)
				return;

			facingOffsets = Exts.MakeArray(FireCount, i => StartOffset.Rotate(WRot.FromFacing(i * 256 / FireCount)));

			var firedBy = args.SourceActor;
			var map = firedBy.World.Map;

			foreach (var c in facingOffsets)
				FireProjectileAtOffset(map, firedBy, target, c, args);
		}

		void FireProjectileAtOffset(Map map, Actor firedBy, Target target, WVec offset, WarheadArgs args)
		{
			var pos = target.CenterPosition + offset;
			if (ForceTargetGround)
				pos = new WPos(pos.X, pos.Y, map.HeightOfTerrain(pos));

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
				GuidedTarget = Target.FromPos(pos)
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

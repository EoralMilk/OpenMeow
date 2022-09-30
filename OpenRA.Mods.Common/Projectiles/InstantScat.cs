using System;
using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Projectiles
{
	public class InstantScatInfo : IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{
		[Desc("sub weapon count.")]
		public readonly int[] ScatCounts = { 0 };

		[WeaponReference]
		[Desc("Weapon fire when projectile die.")]
		public readonly string[] ScatWeapons = null;

		public WeaponInfo[] ScatWeaponInfos { get; private set; }

		public IProjectile Create(ProjectileArgs args) { return new InstantScat(this, args); }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (ScatWeapons == null)
				return;

			if (ScatCounts.Length != ScatWeapons.Length)
				throw new Exception("ScatCounts.Length != ScatWeaponInfos.Length");

			ScatWeaponInfos = new WeaponInfo[ScatWeapons.Length];
			for (int i = 0; i < ScatWeapons.Length; i++)
			{
				WeaponInfo weapon;

				if (!rules.Weapons.TryGetValue(ScatWeapons[i].ToLowerInvariant(), out weapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(ScatWeapons[i].ToLowerInvariant()));
				ScatWeaponInfos[i] = weapon;
			}
		}
	}

	class InstantScat : IProjectile
	{
		readonly ProjectileArgs args;
		readonly InstantScatInfo info;
		public InstantScat(InstantScatInfo info, ProjectileArgs args)
		{
			this.args = args;
			this.info = info;
		}

		public void Tick(World world)
		{
			world.AddFrameEndTask(w => w.Remove(this));

			if (info.ScatCounts[0] > 0 && info.ScatWeaponInfos != null)
			{
				for (int i = 0; i < info.ScatWeaponInfos.Length; i++)
				{
					var pArgs = new ProjectileArgs
					{
						Weapon = info.ScatWeaponInfos[i],
						Facing = args.Facing,
						CurrentMuzzleFacing = args.CurrentMuzzleFacing,

						DamageModifiers = args.DamageModifiers,

						InaccuracyModifiers = args.InaccuracyModifiers,

						RangeModifiers = args.RangeModifiers,

						Source = args.Source,
						CurrentSource = args.CurrentSource,
						SourceActor = args.SourceActor,
						PassiveTarget = args.PassiveTarget,
						GuidedTarget = args.GuidedTarget
					};

					if (pArgs.Weapon.Projectile != null)
					{
						for (var p = 0; p < info.ScatCounts[i]; p++)
						{
							var projectile = info.ScatWeaponInfos[i].Projectile.Create(pArgs);
							world.AddFrameEndTask(w => w.Add(projectile));
						}
					}
				}
			}

			var warheadArgs = new WarheadArgs(args)
			{
				ImpactOrientation = new WRot(WAngle.Zero, WAngle.Zero, args.CurrentMuzzleFacing()),
				ImpactPosition = args.Source,
			};

			args.Weapon.Impact(Target.FromPos(args.Source), warheadArgs);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			yield break;
		}
	}
}

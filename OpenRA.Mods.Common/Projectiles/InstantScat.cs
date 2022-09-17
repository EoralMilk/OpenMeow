using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Projectiles
{
	public class InstantScatInfo : IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{
		[Desc("sub weapon count.")]
		public readonly int ScatCount = 0;

		[WeaponReference]
		[Desc("Weapon fire when projectile die.")]
		public readonly string ScatWeapon = null;

		public WeaponInfo ScatWeaponInfo { get; private set; }

		public IProjectile Create(ProjectileArgs args) { return new InstantScat(this, args); }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (ScatWeapon == null)
				return;

			WeaponInfo weapon;

			if (!rules.Weapons.TryGetValue(ScatWeapon.ToLowerInvariant(), out weapon))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(ScatWeapon.ToLowerInvariant()));
			ScatWeaponInfo = weapon;
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

			if (info.ScatCount > 0 && info.ScatWeaponInfo != null)
			{
				var pArgs = new ProjectileArgs
				{
					Weapon = info.ScatWeaponInfo,
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
					for (var i = 0; i < info.ScatCount; i++)
					{
						var projectile = info.ScatWeaponInfo.Projectile.Create(pArgs);
						world.AddFrameEndTask(w => w.Add(projectile));
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

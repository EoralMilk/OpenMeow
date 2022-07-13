using System;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Warheads
{
	public enum DamageCalculationType { HitShape, ClosestTargetablePosition, CenterPosition }

	// 球形伤害计算
	public class Spread3DDamageWarhead : DamageWarhead, IRulesetLoaded<WeaponInfo>
	{
		[Desc("Range between falloff steps.")]
		public readonly WDist Spread = new WDist(43);

		[Desc("Damage percentage at each range step")]
		public readonly int[] Falloff = { 100, 37, 14, 5, 0 };

		[Desc("Ranges at which each Falloff step is defined. Overrides Spread.")]
		public WDist[] Range = null;

		[Desc("Controls the way damage is calculated. Possible values are 'HitShape', 'ClosestTargetablePosition' and 'CenterPosition'.")]
		public readonly DamageCalculationType DamageCalculationType = DamageCalculationType.HitShape;

		[Desc("Deals column damage that goes all the way to the ground, not ball damage")]
		public readonly bool ColumnarDamage = false;

		[Desc("Only one optimal target is dealt damage")]
		public readonly bool DamageOne = false;

		[Desc("Only deals damage to blockers (if any)")]
		public readonly bool DamageBlocker = false;

		[Desc("If the impact point is less than ground, force the impact point to ground")]
		public bool ForceUnderGroundHitToSurface = true;

		[Desc("The condition to apply. Must be included in the target actor's ExternalConditions list.")]
		public readonly string Condition = null;

		[Desc("Duration of the condition (in ticks). Set to 0 for a permanent condition.")]
		public readonly int Duration = 0;

		Actor bestTarget = null;
		int maxDamage = 0;
		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (Range != null)
			{
				if (Range.Length != 1 && Range.Length != Falloff.Length)
					throw new YamlException("Number of range values must be 1 or equal to the number of Falloff values.");

				for (var i = 0; i < Range.Length - 1; i++)
					if (Range[i] > Range[i + 1])
						throw new YamlException("Range values must be specified in an increasing order.");
			}
			else
				Range = Exts.MakeArray(Falloff.Length, i => i * Spread);
		}

		void AdditionalEffect(in WPos pos, in Actor firedBy, in Actor victim, in WarheadArgs args, int damage)
		{
			if (victim.IsDead || !victim.IsInWorld)
				return;

			if (Condition != null)
			{
				var sourceActor = firedBy;
				victim.TraitsImplementing<ExternalCondition>()
						.FirstOrDefault(t => t.Info.Condition == Condition && t.CanGrantCondition(sourceActor))
						?.GrantCondition(victim, sourceActor, Duration);
				//Console.WriteLine("GrantCondition: " + victim.Info.Name + victim.ActorID + " from " + firedBy.Info.Name + firedBy.ActorID);
			}

			// var explodes = args.Blocker.TraitsImplementing<IGetBlownUp>();
			// foreach (var explode in explodes)
			// {
			// 	explode.GetBlownInfo(args.ImpactPosition, damage);
			// }
		}

		protected override void DoImpact(WPos pos, Actor firedBy, WarheadArgs args)
		{
			var debugVis = firedBy.World.WorldActor.TraitOrDefault<DebugVisualizations>();
			if (debugVis != null && debugVis.CombatGeometry)
				firedBy.World.WorldActor.Trait<WarheadDebugOverlay>().AddImpact(pos, Range, DebugOverlayColor);

			if (ForceUnderGroundHitToSurface && firedBy.World.Map.DistanceAboveTerrain(pos) < WDist.Zero)
				pos = new WPos(pos.X, pos.Y, pos.Z - firedBy.World.Map.DistanceAboveTerrain(pos).Length);
			if (ForceUnderGroundHitToSurface && firedBy.World.Map.DistanceAboveTerrain(args.ImpactPosition) < WDist.Zero)
				args.ImpactPosition = new WPos(args.ImpactPosition.X, args.ImpactPosition.Y, args.ImpactPosition.Z - firedBy.World.Map.DistanceAboveTerrain(args.ImpactPosition).Length);

			if (DamageBlocker && args.Blocker != null && args.Blocker.IsInWorld && !args.Blocker.IsDead)
			{
				if (!IsValidAgainst(args.Blocker, firedBy))
					return;

				var closestActiveShape = args.Blocker.TraitsImplementing<HitShape>()
					.Where(Exts.IsTraitEnabled)
					.Select(s => (HitShape: s, Distance: ColumnarDamage ? s.Info.Type.DistanceFromEdge(new WVec(args.Blocker.CenterPosition.X - pos.X, args.Blocker.CenterPosition.Y - pos.Y, 0)) : s.DistanceFromEdge(args.Blocker, pos)))
					.MinByOrDefault(s => s.Distance);

				// Cannot be damaged without an active HitShape.
				if (closestActiveShape.HitShape == null)
					return;

				var falloffDistance = 0;

				var localModifiers = args.DamageModifiers.Append(GetDamageFalloff(falloffDistance));
				var impactOrientation = args.ImpactOrientation;

				var updatedWarheadArgs = new WarheadArgs(args)
				{
					DamageModifiers = localModifiers.ToArray(),
					ImpactOrientation = impactOrientation,
				};

				var damage = Inflict3DDamage(args.Blocker, firedBy, closestActiveShape.HitShape, updatedWarheadArgs);

				AdditionalEffect(pos, firedBy, args.Blocker, args, damage);

				return;
			}

			bestTarget = null;
			maxDamage = 0;

			foreach (var victim in firedBy.World.FindActorsOnCircle(pos, Range[Range.Length - 1]))
			{
				if (!IsValidAgainst(victim, firedBy))
					continue;

				var closestActiveShape = victim.TraitsImplementing<HitShape>()
					.Where(Exts.IsTraitEnabled)
					.Select(s => (HitShape: s, Distance: ColumnarDamage? s.Info.Type.DistanceFromEdge(new WVec(victim.CenterPosition.X - pos.X, victim.CenterPosition.Y - pos.Y, 0)) : s.DistanceFromEdge(victim, pos)))
					.MinByOrDefault(s => s.Distance);

				// Cannot be damaged without an active HitShape.
				if (closestActiveShape.HitShape == null)
					continue;

				var falloffDistance = 0;
				switch (DamageCalculationType)
				{
					case DamageCalculationType.HitShape:
						falloffDistance = closestActiveShape.Distance.Length;
						break;
					case DamageCalculationType.ClosestTargetablePosition:
						falloffDistance = victim.GetTargetablePositions().Select(x => (x - pos).Length).Min();
						break;
					case DamageCalculationType.CenterPosition:
						falloffDistance = (victim.CenterPosition - pos).Length;
						break;
				}

				// The range to target is more than the range the warhead covers, so GetDamageFalloff() is going to give us 0 and we're going to do 0 damage anyway, so bail early.
				if (falloffDistance > Range[Range.Length - 1].Length)
					continue;

				var localModifiers = args.DamageModifiers.Append(GetDamageFalloff(falloffDistance));
				var impactOrientation = args.ImpactOrientation;

				// If a warhead lands outside the victim's HitShape, we need to calculate the vertical and horizontal impact angles
				// from impact position, rather than last projectile facing/angle.
				if (falloffDistance > 0)
				{
					var towardsTargetYaw = (victim.CenterPosition - args.ImpactPosition).Yaw;
					var impactAngle = Util.GetVerticalAngle(args.ImpactPosition, victim.CenterPosition);
					impactOrientation = new WRot(WAngle.Zero, impactAngle, towardsTargetYaw);
				}

				var updatedWarheadArgs = new WarheadArgs(args)
				{
					DamageModifiers = localModifiers.ToArray(),
					ImpactOrientation = impactOrientation,
				};

				int damage = 0;

				if (DamageOne)
				{
					damage = CalculateDamage(victim, firedBy, closestActiveShape.HitShape, updatedWarheadArgs);
					if (Math.Abs(damage) > Math.Abs(maxDamage))
					{
						maxDamage = damage;
						bestTarget = victim;
					}
				}
				else
				{
					damage = Inflict3DDamage(victim, firedBy, closestActiveShape.HitShape, updatedWarheadArgs);
					AdditionalEffect(pos, firedBy, victim, args, damage);
				}
			}

			if (DamageOne && bestTarget != null && bestTarget.IsInWorld && !bestTarget.IsDead)
			{
				bestTarget.InflictDamage(firedBy, new Damage(maxDamage, DamageTypes));
				AdditionalEffect(pos, firedBy, bestTarget, args, maxDamage);
			}

		}

		int CalculateDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			return Util.ApplyPercentageModifiers(Damage, args.DamageModifiers.Append(DamageVersus(victim, shape, args)));
		}

		int Inflict3DDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			var damage = Util.ApplyPercentageModifiers(Damage, args.DamageModifiers.Append(DamageVersus(victim, shape, args)));
			victim.InflictDamage(firedBy, new Damage(damage, DamageTypes));
			return damage;
		}

		int GetDamageFalloff(int distance)
		{
			var inner = Range[0].Length;
			for (var i = 1; i < Range.Length; i++)
			{
				var outer = Range[i].Length;
				if (outer > distance)
					return int2.Lerp(Falloff[i - 1], Falloff[i], distance - inner, outer - inner);

				inner = outer;
			}

			return 0;
		}
	}
}

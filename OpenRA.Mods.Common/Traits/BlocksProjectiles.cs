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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor blocks bullets and missiles with 'Blockable' property.")]
	public class BlocksProjectilesInfo : ConditionalTraitInfo, IBlocksProjectilesInfo
	{
		public readonly WDist Height = WDist.FromCells(1);

		[Desc("Use bindage logic, which will check actors who are using this bindage and won't block their projectiles")]
		public readonly bool IsBindage = false;

		[Desc("Determines what projectiles to block based on their allegiance to the wall owner.")]
		public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Ally | PlayerRelationship.Neutral | PlayerRelationship.Enemy;

		public override object Create(ActorInitializer init) { return new BlocksProjectiles(this); }
	}

	public class BlocksProjectiles : ConditionalTrait<BlocksProjectilesInfo>, IBlocksProjectiles
	{
		public BlocksProjectiles(BlocksProjectilesInfo info)
			: base(info) { }

		WDist IBlocksProjectiles.BlockingHeight => Info.Height;

		PlayerRelationship IBlocksProjectiles.ValidRelationships { get { return Info.ValidRelationships; } }
		bool IBlocksProjectiles.IsBindage { get { return Info.IsBindage; } }
		bool IBlocksProjectiles.IsBlocking => true;

		public static bool AnyBlockingActorAt(World world, WPos pos)
		{
			var dat = world.Map.DistanceAboveTerrain(pos);

			return world.ActorMap.GetActorsAt(world.Map.CellContaining(pos))
				.Any(a => a.TraitsImplementing<IBlocksProjectiles>()
					.Where(t => t.BlockingHeight > dat)
					.Any(Exts.IsTraitEnabled));
		}

		public static bool AnyBlockingActorsBetween(World world, Player owner, WPos start, WPos end, WDist width, out WPos hit, out Actor blocker, GameRules.ProjectileArgs args = null)
		{
			var actors = world.FindBlockingActorsOnLine(start, end, width);
			var length = (end - start).Length;

			foreach (var a in actors)
			{
				if (args != null && args.IgnoredActors.Contains(a))
					continue;

				var blockers = a.TraitsImplementing<IBlocksProjectiles>()
					.Where(Exts.IsTraitEnabled).Where(t => (t.ValidRelationships.HasRelationship(a.Owner.RelationshipWith(owner)) && t.IsBlocking))
					.ToList();

				if (blockers.Count == 0)
					continue;

				var checkPos = start.MinimumPointLineProjection(end, a.CenterPosition);
				var activeShapes = a.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled);
				foreach (var i in activeShapes)
				{
					if (i.DistanceFromEdge(a, checkPos).Length <= 0)
					{
						var hitPos = i.GetHitPos(a, end);
						hitPos = start.MinimumPointLineProjection(end, hitPos);
						hit = hitPos;
						blocker = a;
						return true;
					}
				}

				// var hitPos = start.MinimumPointLineProjection(end, a.CenterPosition);
				// var dat = world.Map.DistanceAboveTerrain(hitPos);
				// if ((hitPos - start).Length < length && blockers.Any(t => t.BlockingHeight > dat))
				// {
				// 	hit = hitPos;
				// 	return true;
				// }
			}

			hit = WPos.Zero;
			blocker = null;
			return false;
		}
	}
}

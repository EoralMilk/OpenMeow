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

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{

	public class WithSkeletonInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string Sequence = "idle";

		public override object Create(ActorInitializer init) { return new WithSkeleton(init.Self, this); }
	}

	public class WithSkeleton : ConditionalTrait<WithSkeletonInfo>, ITick
	{
		readonly OrderedSkeleton orderedSkeleton;
		readonly SkeletonInstance skeleton;
		readonly SkeletalAnim[] skeletalAnims;
		readonly SkeletalAnim currentAnim;
		readonly RenderMeshes rm;
		public bool Draw;
		int tick = 0;
		int frameTick = 0;
		readonly float scale = 1;
		readonly Actor self;
		readonly BodyOrientation body;
		public WithSkeleton(Actor self, WithSkeletonInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			this.self = self;

			rm = self.Trait<RenderMeshes>();
			scale = rm.Info.Scale;
			orderedSkeleton = self.World.SkeletonCache.GetOrderedSkeleton(rm.Image);
			if (orderedSkeleton == null)
				throw new Exception("orderedSkeleton is null");

			skeleton = orderedSkeleton.CreateInstance();

			if (orderedSkeleton.SkeletonAsset.Animations.ContainsKey(info.Sequence))
			{
				currentAnim = orderedSkeleton.SkeletonAsset.Animations[info.Sequence];
			}
			else if (orderedSkeleton.SkeletonAsset.Animations.Count > 0)
			{
				Console.WriteLine("Unit WithSkeleton: " + rm.Image + "dose not find animation: " + info.Sequence);
				int i = 0;
				skeletalAnims = new SkeletalAnim[orderedSkeleton.SkeletonAsset.Animations.Count];
				foreach (var anim in orderedSkeleton.SkeletonAsset.Animations)
				{
					skeletalAnims[i] = anim.Value;
					i++;
				}

				currentAnim = skeletalAnims[0];
			}
			else
			{
				throw new Exception("unit " + rm.Image + " has no animation");
			}
		}

		void ITick.Tick(Actor self)
		{
			SelfTick();
		}

		void SelfTick()
		{
			Draw = !IsTraitDisabled;

			skeleton.SetOffset(self.CenterPosition, body.QuantizeOrientation(self.Orientation), scale);

			if (currentAnim != null)
			{
				frameTick = tick % currentAnim.Frames.Length;
				skeleton.UpdateOffset(currentAnim.Frames[frameTick]);
			}

			if (Draw)
			{
				// update my skeletonInstance drawId
				orderedSkeleton.AddInstance(skeleton);
				skeleton.ProcessManagerData();
			}
			else
			{
				skeleton.DrawID = -1;
			}

			tick++;
		}

		public int GetDrawId()
		{
			if (tick == 0)
				SelfTick();
			return skeleton.DrawID;
		}

	}
}

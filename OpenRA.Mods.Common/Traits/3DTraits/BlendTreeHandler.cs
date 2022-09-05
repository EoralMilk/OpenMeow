using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class BlendTreeHandlerInfo : TraitInfo, Requires<WithSkeletonInfo>
	{
		public readonly string DirectionTurret = null;

		public readonly string SkeletonToUse = null;
		public readonly string Walk = null;
		public readonly string WalkBack = null;

		public readonly string Forward = null;
		public readonly string ForwardRight = null;
		public readonly string ForwardLeft = null;
		public readonly string StrafeRight = null;
		public readonly string Stand = null;
		public readonly string StrafeLeft = null;
		public readonly string Backward = null;
		public readonly string BackwardLeft = null;
		public readonly string BackwardRight = null;

		public readonly string Guard = null;
		public readonly string UpperMask = null;
		public readonly string LowerMask = null;

		public readonly int Stand2WalkTick = 10;
		public readonly int GuardBlendTick = 80;

		public override object Create(ActorInitializer init) { return new BlendTreeHandler(init.Self, this); }
	}

	public class BlendTreeHandler : IBlendTreeHandler, IPrepareForAttack, ITick, INotifyCreated
	{
		readonly BlendTree blendTree;
		readonly BlendTreeHandlerInfo info;
		readonly WithSkeleton withSkeleton;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;
		readonly IMove move;

		// nodes
		//readonly Switch moveSwitch;
		readonly AnimationNode walkAnim;
		readonly AnimationNode walkBackAnim;

		readonly AnimationNode guardAnim;
		readonly AnimationNode idleAnim;
		readonly Switch guardSwitch;

		readonly AnimationNode forwardAnim;
		readonly AnimationNode forwardRightAnim;
		readonly AnimationNode forwardLeftAnim;
		readonly AnimationNode strafeRightAnim;
		readonly AnimationNode standAnim;
		readonly AnimationNode strafeLeftAnim;
		readonly AnimationNode backwardAnim;
		readonly AnimationNode backwardRightAnim;
		readonly AnimationNode backwardLeftAnim;

		readonly Blend9Pos locomotion;
		readonly Blend2 guardBlend2;

		// temp test
		readonly SkeletalAnim walk;
		readonly SkeletalAnim walkBack;

		readonly SkeletalAnim forward;
		readonly SkeletalAnim forwardRight;
		readonly SkeletalAnim forwardLeft;

		readonly SkeletalAnim strafeRight;
		readonly SkeletalAnim stand;
		readonly SkeletalAnim strafeLeft;

		readonly SkeletalAnim backward;
		readonly SkeletalAnim backwardRight;
		readonly SkeletalAnim backwardLeft;

		readonly SkeletalAnim guard;

		Turreted turret;
		public BlendTreeHandler(Actor self, BlendTreeHandlerInfo info)
		{
			this.info = info;
			this.self = self;
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();

			if (info.SkeletonToUse == null)
				throw new YamlException("BlendTreeHandler must define a SkeletonToUse for get animations");
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);
			walkBack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.WalkBack);

			forward = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Forward);
			forwardLeft = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ForwardLeft);
			forwardRight = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ForwardRight);
			strafeLeft = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StrafeLeft);
			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
			strafeRight = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StrafeRight);
			backward = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Backward);
			backwardLeft = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.BackwardLeft);
			backwardRight = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.BackwardRight);

			if (withSkeleton.OrderedSkeleton.SkeletonAsset.Animations.Count == 0)
			{
				throw new Exception("unit " + withSkeleton.Image + " has no animation");
			}

			if (info.UpperMask == null)
				throw new Exception("Need UpperrMask");
			if (info.LowerMask == null)
				throw new Exception("Need LowerMask");

			blendTree = new BlendTree();
			var uppermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.UpperMask);
			var lowermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.LowerMask);
			var allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.AllValidMask;
			walkAnim = new AnimationNode(info.Walk, 1, blendTree, allvalidmask, walk);
			walkBackAnim = new AnimationNode(info.WalkBack, 2, blendTree, allvalidmask, walkBack);
			guardAnim = new AnimationNode("guard", 3, blendTree, uppermask, guard);
			idleAnim = new AnimationNode("idle", 15, blendTree, uppermask, stand);

			forwardLeftAnim = new AnimationNode("FL", 11, blendTree, lowermask, forwardLeft);
			forwardAnim = new AnimationNode("F", 12, blendTree, lowermask, forward);
			forwardRightAnim = new AnimationNode("FR", 13, blendTree, lowermask, forwardRight);
			strafeLeftAnim = new AnimationNode("L", 14, blendTree, lowermask, strafeLeft);
			standAnim = new AnimationNode("M", 15, blendTree, lowermask, stand);
			strafeRightAnim = new AnimationNode("R", 16, blendTree, lowermask, strafeRight);
			backwardLeftAnim = new AnimationNode("BL", 17, blendTree, lowermask, backwardLeft);
			backwardAnim = new AnimationNode("B", 18, blendTree, lowermask, backward);
			backwardRightAnim = new AnimationNode("BR", 19, blendTree, lowermask, backwardRight);
			var locomotionInput = new AnimationNode[9]
			{
				forwardLeftAnim, forwardAnim, forwardRightAnim,
				strafeLeftAnim, standAnim, strafeRightAnim,
				backwardLeftAnim, backwardAnim, backwardRightAnim,
			};

			locomotion = new Blend9Pos("locomotion", 5, blendTree, lowermask, locomotionInput);
			guardSwitch = new Switch("idle2guard", 22, blendTree, uppermask, idleAnim, guardAnim, info.GuardBlendTick);
			// moveSwitch = new Switch("Stand2Walk", 20, blendTree, allvalidmask, standAnim, locomotion, info.Stand2WalkTick);
			guardBlend2 = new Blend2("GuardBlend", 21, blendTree, allvalidmask, locomotion, guardSwitch);
			blendTree.InitTree(guardBlend2);
			guardBlend2.BlendValue = FP.One;
			withSkeleton.BlendTreeHandler = this;
			guardBlendSpeed = FP.One / info.GuardBlendTick;
		}

		public void Created(Actor self)
		{
			if (info.DirectionTurret != null)
			{
				turret = self.TraitsImplementing<Turreted>().FirstOrDefault(t => t.Name == info.DirectionTurret);
			}
		}

		public BlendTreeNodeOutPut GetResult()
		{
			return blendTree.GetOutPut();
		}

		void IBlendTreeHandler.UpdateTick()
		{
			blendTree.UpdateTick();
		}

		WRot IBlendTreeHandler.FacingOverride()
		{
			//Console.WriteLine(turret?.WorldOrientation ?? myFacing.Orientation);
			return turret?.WorldOrientation ?? myFacing.Orientation;
		}

		FP guardBlendSpeed;
		int guardTick = 0;
		readonly int guardTime = 50;
		public bool PrepareForAttack(in Target target)
		{
			guardSwitch.SetFlag(true);
			guardTick = 0;
			if (guardSwitch.BlendValue < FP.One)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		FP lerpSpeed = 0;
		public void Tick(Actor self)
		{
			if (guardTick == guardTime)
			{
				guardSwitch.SetFlag(false);
			}
			else
			{
				guardTick++;
			}

			if (move.CurrentMovementTypes != MovementType.None)
			{
				//moveSwitch.SetFlag(true);
				//var angle = (-(turret?.LocalOrientation.Yaw) ?? WAngle.Zero);
				//WVec vec = new WVec(0, -1024, 0).Rotate(WRot.None.WithYaw(angle));

				var angle = (myFacing.Facing - (turret?.WorldOrientation.Yaw) ?? WAngle.Zero).Angle;
				FP x = FP.Zero, y = FP.Zero;
				if (angle <= 128 || angle >= 896)
				{
					y = FP.One;
					x = FP.One * (angle >= 896 ? 1024 - angle : -angle) / 128;
				}
				else if (angle > 128 && angle <= 384)
				{
					x = -FP.One;
					y = FP.One * (256 - angle) / 128;
				}
				else if (angle > 384 && angle <= 640)
				{
					y = -FP.One;
					x = FP.One * (angle - 512) / 128;
				}
				else
				{
					x = FP.One;
					y = FP.One * (angle - 768) / 128;
				}

				lerpSpeed = lerpSpeed < FP.One ? lerpSpeed + FP.FromFloat(0.1f) : FP.One;
				locomotion.BlendPos = new TSVector2(x, y);
				locomotion.BlendPos = locomotion.BlendPos * lerpSpeed;
				//Console.WriteLine(locomotion.BlendPos);
			}
			else
			{
				//moveSwitch.SetFlag(false);
				lerpSpeed = lerpSpeed > FP.Zero ? lerpSpeed - FP.FromFloat(0.1f) : FP.Zero;

				locomotion.BlendPos = locomotion.BlendPos.normalized * lerpSpeed;
			}

			//var angle = (-(turret?.LocalOrientation.Yaw) ?? WAngle.Zero).Angle;
			//FP x = FP.Zero, y = FP.Zero;
			//if (angle <= 128 || angle >= 896)
			//{
			//	y = FP.One;
			//	x = FP.One * (angle >= 896 ? 1024 - angle : -angle) / 128;
			//}
			//else if (angle > 128 && angle <= 384)
			//{
			//	x = -FP.One;
			//	y = FP.One * (256 - angle) / 128;
			//}
			//else if (angle > 384 && angle <= 640)
			//{
			//	y = -FP.One;
			//	x = FP.One * (angle - 512) / 128;
			//}
			//else
			//{
			//	x = FP.One;
			//	y = FP.One * (angle - 768) / 128;
			//}

			//locomotion.BlendPos = new TSVector2(x, y);
			//Console.WriteLine(locomotion.BlendPos);
		}
	}
}

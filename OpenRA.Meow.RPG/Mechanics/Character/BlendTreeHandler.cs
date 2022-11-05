using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class BlendTreeHandlerInfo : TraitInfo, Requires<WithSkeletonInfo>, IRulesetLoaded
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

		public readonly string Carrying = null;
		public readonly string LeftFootBone = "a-s-Rig_Foot.L";
		public readonly string RightFootBone = "a-s-Rig_Foot.R";
		public readonly int FootDownFrameLeft = 10;
		public readonly int FootDownFrameRight = 22;

		public readonly string[] BellyBones = Array.Empty<string>();

		public readonly string[] ChurnSounds = Array.Empty<string>();
		public readonly float ChurnMinSoundScale = 0.2f;
		public readonly float ChurnMaxSoundScale = 3f;

		public readonly int ChurnHealthPercentage = 1;

		public readonly int ChurnInterval = 40;
		public readonly float DigestionSpeed = 0.001f;

		public readonly string[] EmptySounds = Array.Empty<string>();

		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string WalkWeapon = null;
		public WeaponInfo WalkWeaponInfo { get; private set; }
		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (WalkWeapon != null)
			{
				WeaponInfo weapon;

				if (!rules.Weapons.TryGetValue(WalkWeapon.ToLowerInvariant(), out weapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(WalkWeapon.ToLowerInvariant()));
				WalkWeaponInfo = weapon;
			}
		}

		public override object Create(ActorInitializer init) { return new BlendTreeHandler(init.Self, this); }
	}

	public class BlendTreeHandler : IBlendTreeHandler, IPrepareForAttack, ITick, INotifyCreated,
		INotifyLongJump, INotifyPickUpItem, INotifyConsumeItem
	{
		readonly BlendTree blendTree;
		readonly BlendTreeHandlerInfo info;
		readonly WithSkeleton withSkeleton;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;
		readonly IMove move;
		readonly IHealth health;

		// nodes
		//readonly Switch moveSwitch;
		readonly AnimationNode walkAnim;
		readonly AnimationNode walkBackAnim;

		readonly AnimationNode guardAnim;
		readonly AnimationNode idleAnim;
		readonly AnimationNode guardUpperAnim;
		readonly AnimationNode idleUpperAnim;
		readonly Switch guardSwitch;
		readonly Switch guardUpperSwitch;

		readonly AnimationNode forwardAnim;
		readonly AnimationNode forwardRightAnim;
		readonly AnimationNode forwardLeftAnim;
		readonly AnimationNode strafeRightAnim;
		readonly AnimationNode standAnim;
		readonly AnimationNode strafeLeftAnim;
		readonly AnimationNode backwardAnim;
		readonly AnimationNode backwardRightAnim;
		readonly AnimationNode backwardLeftAnim;
		readonly AnimationNode overide;

		readonly Blend9Pos locomotion;
		readonly Blend2 guardBlend2;

		readonly Switch overideSwitch;

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

		readonly SkeletalAnim carrying;

		Carryable carryable;
		Turreted turret;

		IEnumerable<int> damageModifiers;

		readonly int footLId, footRId;
		readonly int[] bellyBones;
		readonly HashSet<int> hashBellyBones;
		readonly ModifiedBoneRestPose[] bellyScalesMb;

		int GetBoneId(string name)
		{
			var boneid = withSkeleton.GetBoneId(name);
			if (boneid == -1)
				throw new Exception("can't find bone " + name + " in skeleton.");
			return boneid;
		}

		public BlendTreeHandler(Actor self, BlendTreeHandlerInfo info)
		{
			this.info = info;
			this.self = self;
			body = self.Trait<BodyOrientation>();
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();
			health = self.Trait<Health>();

			if (info.SkeletonToUse == null)
				throw new YamlException("BlendTreeHandler must define a SkeletonToUse for get animations");
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

			footLId = GetBoneId(info.LeftFootBone);
			footRId = GetBoneId(info.RightFootBone);

			if (info.BellyBones.Length == 0)
				throw new Exception("Belly bones length can not be zero");
			bellyBones = new int[info.BellyBones.Length];
			hashBellyBones = new HashSet<int>();
			for (int i = 0; i < bellyBones.Length; i++)
			{
				bellyBones[i] = GetBoneId(info.BellyBones[i]);
				hashBellyBones.Add(bellyBones[i]);
				if (!withSkeleton.OrderedSkeleton.ModifiedBoneRestPoses.ContainsKey(bellyBones[i]))
					throw new Exception("bellyBones  " + info.BellyBones[i] + " has no modifyer");
			}

			List<ModifiedBoneRestPose> tempBellyScale = new List<ModifiedBoneRestPose>();
			foreach (var kv in withSkeleton.OrderedSkeleton.ModifiedBoneRestPoses)
			{
				if (kv.Value.OnlyRestPose)
					continue;

				if (hashBellyBones.Contains(kv.Key))
				{
					tempBellyScale.Add(kv.Value);
				}
			}

			bellyScalesMb = tempBellyScale.ToArray();

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

			carrying = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Carrying);

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
			guardAnim = new AnimationNode("guard", 3, blendTree, allvalidmask, guard);
			idleAnim = new AnimationNode("idle", 15, blendTree, allvalidmask, stand);
			guardUpperAnim = new AnimationNode("guard", 3, blendTree, uppermask, guard);
			idleUpperAnim = new AnimationNode("idle", 15, blendTree, uppermask, stand);

			forwardLeftAnim = new AnimationNode("FL", 11, blendTree, allvalidmask, forwardLeft);
			forwardLeftAnim.AddFrameAction(7, FootDownImpactLeft);
			forwardLeftAnim.AddFrameAction(20, FootDownImpactRight);

			forwardAnim = new AnimationNode("F", 12, blendTree, allvalidmask, forward);
			//forwardAnim.AddFrameAction(info.FootDownFrameLeft, FootDownImpactLeft);
			//forwardAnim.AddFrameAction(info.FootDownFrameRight, FootDownImpactRight);

			forwardRightAnim = new AnimationNode("FR", 13, blendTree, allvalidmask, forwardRight);
			//forwardRightAnim.AddFrameAction(info.FootDownFrameLeft, FootDownImpactLeft);
			//forwardRightAnim.AddFrameAction(info.FootDownFrameRight, FootDownImpactRight);

			strafeLeftAnim = new AnimationNode("L", 14, blendTree, allvalidmask, strafeLeft);
			//strafeLeftAnim.AddFrameAction(9, FootDownImpactLeft);
			//strafeLeftAnim.AddFrameAction(21, FootDownImpactRight);

			standAnim = new AnimationNode("M", 15, blendTree, allvalidmask, stand);

			strafeRightAnim = new AnimationNode("R", 16, blendTree, allvalidmask, strafeRight);
			//strafeRightAnim.AddFrameAction(6, FootDownImpactLeft);
			//strafeRightAnim.AddFrameAction(20, FootDownImpactRight);

			backwardLeftAnim = new AnimationNode("BL", 17, blendTree, allvalidmask, backwardLeft);
			//backwardLeftAnim.AddFrameAction(6, FootDownImpactLeft);
			//backwardLeftAnim.AddFrameAction(19, FootDownImpactRight);

			backwardAnim = new AnimationNode("B", 18, blendTree, allvalidmask, backward);
			//backwardAnim.AddFrameAction(6, FootDownImpactLeft);
			//backwardAnim.AddFrameAction(19, FootDownImpactRight);

			backwardRightAnim = new AnimationNode("BR", 19, blendTree, allvalidmask, backwardRight);
			//backwardRightAnim.AddFrameAction(6, FootDownImpactLeft);
			//backwardRightAnim.AddFrameAction(19, FootDownImpactRight);

			overide = new AnimationNode("Carrying", 55, blendTree, allvalidmask, carrying);

			guardSwitch = new Switch("idle2guard", 22, blendTree, allvalidmask, idleAnim, guardAnim, info.GuardBlendTick);
			var locomotionInput = new BlendTreeNode[9]
			{
				forwardLeftAnim, forwardAnim, forwardRightAnim,
				strafeLeftAnim, guardSwitch, strafeRightAnim,
				backwardLeftAnim, backwardAnim, backwardRightAnim,
			};
			guardUpperSwitch = new Switch("idle2guard", 22, blendTree, uppermask, idleUpperAnim, guardUpperAnim, info.GuardBlendTick);
			locomotion = new Blend9Pos("locomotion", 5, blendTree, allvalidmask, locomotionInput);

			// moveSwitch = new Switch("Stand2Walk", 20, blendTree, allvalidmask, standAnim, locomotion, info.Stand2WalkTick);
			guardBlend2 = new Blend2("GuardBlend", 21, blendTree, allvalidmask, locomotion, guardUpperSwitch);
			overideSwitch = new Switch("OverideSwitch", 24, blendTree, allvalidmask, guardBlend2, overide, 30);

			blendTree.InitTree(overideSwitch);
			guardBlend2.BlendValue = FP.FromFloat(0.8f);
			withSkeleton.BlendTreeHandler = this;
			guardBlendSpeed = FP.One / info.GuardBlendTick;
		}

		public void Created(Actor self)
		{
			if (info.DirectionTurret != null)
			{
				turret = self.TraitsImplementing<Turreted>().FirstOrDefault(t => t.Name == info.DirectionTurret);
			}

			damageModifiers = self.TraitsImplementing<IFirepowerModifier>().ToArray().Select(m => m.GetFirepowerModifier());
			carryable = self.TraitOrDefault<Carryable>();
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

		public void FootDownImpactLeft()
		{
			if (locomotion.BlendPos.LengthSquared() <= FP.Zero)
				return;
			WalkImpactAtBone(footLId);
		}

		public void FootDownImpactRight()
		{
			if (locomotion.BlendPos.LengthSquared() <= FP.Zero)
				return;
			WalkImpactAtBone(footRId);
		}

		public void WalkImpactAtBone(int boneId)
		{
			var pos = withSkeleton.GetWPosFromBoneId(boneId);
			var h = self.World.Map.HeightOfTerrain(pos);
			if (pos.Z > h + 256)
			{
				return;
			}

			pos = new WPos(pos.X, pos.Y, h);

			if (info.WalkWeaponInfo != null)
			{
				var warheadArgs = new WarheadArgs()
				{
					Weapon = info.WalkWeaponInfo,
					DamageModifiers = damageModifiers.ToArray(),
					ImpactPosition = pos,
					Source = self.CenterPosition,
					SourceActor = self,
					WeaponTarget = Target.FromPos(pos)
				};
				info.WalkWeaponInfo.Impact(warheadArgs.WeaponTarget, warheadArgs);
			}
		}

		FP guardBlendSpeed;
		int guardTick = 0;
		readonly int guardTime = 50;
		public bool PrepareForAttack(in Target target)
		{
			if (carryable != null && carryable.Reserved)
				return false;

			guardSwitch.SetFlag(true);
			guardUpperSwitch.SetFlag(true);
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
		int angle;
		int lastangle;

		bool playOverideBlend;
		public bool PrepareForLongJump()
		{
			playOverideBlend = true;
			return overideSwitch.BlendValue >= FP.One;
		}

		public void OnStartJump()
		{
			playOverideBlend = true;
		}

		public void OnLand()
		{
			playOverideBlend = false;
		}

		public bool PrepareForPickUpItem()
		{
			playOverideBlend = true;
			return true;
		}

		public bool Picking()
		{
			playOverideBlend = true;
			return overideSwitch.BlendValue >= FP.One;
		}

		public void OnPickUpItem(Actor item)
		{
			playOverideBlend = false;
		}

		FP maxStomachSize = 10;
		FP stomachSize = 0;
		int churnTick = 0;
		public void Consume(Item item)
		{
			stomachSize = TSMath.Clamp(stomachSize + 1, 0, maxStomachSize);
		}

		public bool CanConsume(Item item)
		{
			return stomachSize <= (maxStomachSize - 1);
		}

		public void Tick(Actor self)
		{
			if (stomachSize > 0)
			{
				if (churnTick++ >= info.ChurnInterval)
				{
					var churnSound = info.ChurnSounds.RandomOrDefault(self.World.LocalRandom);
					if (churnSound != null)
						Game.Sound.Play(SoundType.World, churnSound, self.CenterPosition, float2.Lerp(info.ChurnMinSoundScale, info.ChurnMaxSoundScale, (float)(stomachSize / maxStomachSize)));
					health.InflictDamage(self, self, new Damage(-health.MaxHP / 100 * info.ChurnHealthPercentage), true);
					churnTick = 0;
				}

				foreach (var bg in bellyScalesMb)
				{
					withSkeleton.Skeleton.Bones[bg.Id].SetRestPose(
						Transformation.LerpMatrix(bg.FirstTransform, bg.LastTransform, stomachSize / maxStomachSize));
				}

				stomachSize -= info.DigestionSpeed;
				if (stomachSize <= 0)
				{
					stomachSize = 0;
					var emptySound = info.EmptySounds.RandomOrDefault(self.World.LocalRandom);
					if (emptySound != null)
						Game.Sound.Play(SoundType.World, emptySound, self.CenterPosition, 1f);
				}
			}

			if (playOverideBlend || (carryable != null && carryable.Reserved))
			{
				overideSwitch.SetFlag(true);
			}
			else
			{
				overideSwitch.SetFlag(false);
			}

			if (guardTick == guardTime)
			{
				guardSwitch.SetFlag(false);
				guardUpperSwitch.SetFlag(false);
			}
			else
			{
				guardTick++;
			}

			if (move.CurrentSpeed != WVec.Zero)
			{
				//moveSwitch.SetFlag(true);
				lerpSpeed = lerpSpeed < FP.One ? lerpSpeed + (FP.One / info.Stand2WalkTick) : FP.One;
			}
			else if (lastangle != angle)
			{
				lerpSpeed = lerpSpeed > FP.Half ? lerpSpeed - (FP.One / info.Stand2WalkTick) : FP.Half;
			}
			else
			{
				//moveSwitch.SetFlag(false);
				lerpSpeed = lerpSpeed > FP.Zero ? lerpSpeed - (FP.One / info.Stand2WalkTick) : FP.Zero;
			}

			angle = (myFacing.Facing - (turret?.WorldOrientation.Yaw) ?? myFacing.Facing).Angle;
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

			locomotion.BlendPos = new TSVector2(x, y);
			locomotion.BlendPos = locomotion.BlendPos * lerpSpeed;
			lastangle = angle;
		}
	}
}

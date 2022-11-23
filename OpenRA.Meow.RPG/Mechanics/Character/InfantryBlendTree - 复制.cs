//using System;
//using System.Collections.Generic;
//using System.Linq;
//using OpenRA.Graphics;
//using OpenRA.Mods.Common;
//using OpenRA.Mods.Common.Traits;
//using OpenRA.Mods.Common.Traits.Trait3D;
//using OpenRA.Primitives;
//using OpenRA.Traits;
//using TrueSync;

//namespace OpenRA.Meow.RPG.Mechanics
//{
//	public class InfantryBlendTreeInfo : TraitInfo, Requires<WithSkeletonInfo>
//	{
//		public readonly string SkeletonToUse = null;

//		// basic
//		public readonly string Stand = null;
//		public readonly string Walk = null;
//		public readonly string Guard = null;

//		public readonly int GuardTick = 50;

//		// guard convert
//		public readonly string StandToGuard = null;
//		public readonly string GuardToStand = null;

//		// attack
//		public readonly string Attack = null;

//		// die
//		public readonly string DieStand = null;

//		public readonly bool CanProne = false;

//		// prone
//		public readonly string Prone = null;
//		public readonly string Crawl = null;

//		// prone attack
//		public readonly string ProneAttack = null;

//		// prone convert
//		public readonly string StandToProne = null;
//		public readonly string ProneToStand = null;

//		// die prone
//		public readonly string DieProne = null;

//		public readonly int AttackFrame = 0;
//		public readonly int ProneAttackFrame = 0;

//		public readonly string[] IdleActions = null;

//		public readonly string UpperMask = null;
//		public readonly string LowerMask = null;

//		public readonly int Stand2WalkTick = 5;
//		public readonly int GuardBlendTick = 5;

//		[Desc("How long (in ticks) the actor remains prone.",
//			"Negative values mean actor remains prone permanently.")]
//		public readonly int ProneDuration = 100;

//		[Desc("Prone movement speed as a percentage of the normal speed.")]
//		public readonly int ProneSpeedModifier = 50;

//		[Desc("Damage types that trigger prone state. Defined on the warheads.",
//			"If Duration is negative (permanent), you can leave this empty to trigger prone state immediately.")]
//		public readonly BitSet<DamageType> ProneDamageTriggers = default;

//		[Desc("Damage modifiers for each damage type (defined on the warheads) while the unit is prone.")]
//		public readonly Dictionary<string, int> ProneDamageModifiers = new Dictionary<string, int>();

//		[GrantedConditionReference]
//		[Desc("Condition to grant.")]
//		public readonly string ProneCondition = null;

//		public override object Create(ActorInitializer init) { return new InfantryBlendTree(init.Self, this); }
//	}

//	public class InfantryBlendTree : IBlendTreeHandler, IPrepareForAttack, ITick, INotifyCreated, INotifyAttack, INotifyAiming,
//		INotifyDamage, IDamageModifier, ISpeedModifier
//	{
//		readonly BlendTree blendTree;
//		readonly InfantryBlendTreeInfo info;
//		readonly WithSkeleton withSkeleton;
//		readonly Actor self;
//		readonly IFacing myFacing;
//		readonly IMove move;

//		readonly SkeletalAnim stand;
//		readonly SkeletalAnim walk;
//		readonly SkeletalAnim guard;
//		readonly SkeletalAnim standToGuard;
//		readonly SkeletalAnim guardToStand;
//		readonly SkeletalAnim attack;

//		readonly SkeletalAnim prone;
//		readonly SkeletalAnim crawl;
//		readonly SkeletalAnim proneAttack;
//		readonly SkeletalAnim standToProne;
//		readonly SkeletalAnim proneToStand;

//		readonly SkeletalAnim die;
//		readonly SkeletalAnim dieProne;

//		readonly AnimationNode animStand;
//		readonly AnimationNode animWalk;
//		readonly AnimationNode animGuard;

//		readonly AnimationNode animProne;
//		readonly AnimationNode animCrawl;

//		readonly AnimationNode animDie;
//		readonly AnimationNode animOverride;
//		readonly AnimationNode animOverride2;

//		readonly Switch switchWalk;
//		readonly Switch switchCrawl;
//		readonly Switch switchGuard;
//		readonly Switch switchProne;

//		readonly OneShot shotOverride;
//		readonly OneShot shotOverride2;

//		readonly OneShot shotDie;

//		public enum InfantryState
//		{
//			Idle,
//			Guard,
//			Action,
//		}

//		public enum PoseState
//		{
//			Stand,
//			Prone,
//			StandToProne,
//			ProneToStand,
//		}

//		PoseState currentPose = PoseState.Stand;
//		InfantryState currentState = InfantryState.Idle;

//		[Sync]
//		int guardTick = 9999;

//		int proneRemainingDuration = 0;
//		int proneConditionToken = Actor.InvalidConditionToken;

//		int GetBoneId(string name)
//		{
//			var boneid = withSkeleton.GetBoneId(name);
//			if (boneid == -1)
//				throw new Exception("can't find bone " + name + " in skeleton.");
//			return boneid;
//		}

//		public InfantryBlendTree(Actor self, InfantryBlendTreeInfo info)
//		{
//			this.info = info;
//			this.self = self;
//			move = self.Trait<IMove>();
//			myFacing = self.Trait<IFacing>();

//			if (info.SkeletonToUse == null)
//				throw new YamlException("InfantryBlendTree must define a SkeletonToUse for get animations");
//			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

//			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
//			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
//			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);

//			if (!string.IsNullOrEmpty(info.StandToGuard))
//				standToGuard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToGuard);

//			if (!string.IsNullOrEmpty(info.GuardToStand))
//				guardToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardToStand);

//			attack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Attack);

//			die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieStand);

//			if (info.CanProne)
//			{
//				prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Prone);
//				crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Crawl);
//				proneAttack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneAttack);
//				standToProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToProne);
//				proneToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneToStand);
//				dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieProne);
//			}

//			if (info.UpperMask == null)
//				throw new Exception("Need UpperrMask");
//			if (info.LowerMask == null)
//				throw new Exception("Need LowerMask");

//			var uppermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.UpperMask);
//			var lowermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.LowerMask);
//			var allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.AllValidMask;

//			blendTree = new BlendTree();

//			animStand = new AnimationNode("Stand", 1, blendTree, allvalidmask, stand);
//			animWalk = new AnimationNode("Walk", 1, blendTree, allvalidmask, walk);
//			animGuard = new AnimationNode("Guard", 1, blendTree, allvalidmask, guard);

//			if (info.CanProne)
//			{
//				animProne = new AnimationNode("Prone", 1, blendTree, allvalidmask, prone);
//				animCrawl = new AnimationNode("Crawl", 1, blendTree, allvalidmask, crawl);
//			}

//			// the override node and die node should handle with trait
//			animOverride = new AnimationNode("Override", 1, blendTree, allvalidmask, die)
//			{
//				NodePlayType = LeafNode.PlayType.Once
//			};

//			animOverride2 = new AnimationNode("Override2", 1, blendTree, allvalidmask, die)
//			{
//				NodePlayType = LeafNode.PlayType.Once
//			};

//			animDie = new AnimationNode("Die", 1, blendTree, allvalidmask, die)
//			{
//				NodePlayType = LeafNode.PlayType.Once
//			};

//			// set the lerp tick as 1, need handle it in trait
//			switchGuard = new Switch("StandGuard", 1, blendTree, allvalidmask, animStand, animGuard, info.GuardBlendTick);

//			switchWalk = new Switch("StandWalk", 1, blendTree, allvalidmask, switchGuard, animWalk, info.Stand2WalkTick);

//			if (info.CanProne)
//			{
//				switchCrawl = new Switch("ProneCrawl", 1, blendTree, allvalidmask, animProne, animCrawl, info.Stand2WalkTick);

//				// set the lerp tick as 1, need handle it in trait
//				switchProne = new Switch("ProneState", 1, blendTree, allvalidmask, switchWalk, switchCrawl, 1);

//				shotOverride = new OneShot("Override", 1, blendTree, allvalidmask, switchProne, animOverride, OneShot.ShotEndType.Recover, 5);
//			}
//			else
//			{
//				shotOverride = new OneShot("Override", 1, blendTree, allvalidmask, switchWalk, animOverride, OneShot.ShotEndType.Recover, 5);
//			}

//			shotOverride2 = new OneShot("Override2", 1, blendTree, allvalidmask, shotOverride, animOverride2, OneShot.ShotEndType.Recover, 5);
//			shotDie = new OneShot("Die", 1, blendTree, allvalidmask, shotOverride2, animDie, OneShot.ShotEndType.Keep, 5);

//			blendTree.InitTree(shotDie);
//			withSkeleton.BlendTreeHandler = this;
//		}

//		public void Created(Actor self)
//		{
//		}

//		public BlendTreeNodeOutPut GetResult()
//		{
//			return blendTree.GetOutPut();
//		}

//		void IBlendTreeHandler.UpdateTick()
//		{
//			blendTree.UpdateTick();
//		}

//		WRot IBlendTreeHandler.FacingOverride()
//		{
//			return myFacing.Orientation;
//		}

//		public bool PrepareForAttack(in Target target)
//		{
//			if (currentPose == PoseState.StandToProne || currentPose == PoseState.ProneToStand)
//				return false;

//			guardTick = 0;

//			if (currentState == InfantryState.Guard)
//			{
//				return true;
//			}

//			return false;
//		}

//		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
//		{
//			guardTick = 0;
//		}

//		void INotifyAiming.StartedAiming(Actor self, AttackBase attack)
//		{
//			guardTick = 0;
//		}

//		void INotifyAiming.StoppedAiming(Actor self, AttackBase attack)
//		{
//			guardTick = 0;
//		}

//		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
//		{
//			guardTick = 0;

//			// we should in Guard State now
//			// or there must be something wrong
//			currentState = InfantryState.Action;
//			animOverride.ChangeAnimation(GetAttackAnim());

//			shotOverride.ShotEndAction = () =>
//			{
//				guardTick = 0;
//				shotOverride.ShotEndAction = null;
//			};

//			shotOverride.ShotEndBlendAction = () =>
//			{
//				currentState = InfantryState.Guard;
//				shotOverride.ShotEndBlendAction = null;
//			};

//			shotOverride.StartShot();
//		}

//		SkeletalAnim GetAttackAnim()
//		{
//			if (currentPose == PoseState.Stand)
//				return attack;
//			else if (currentPose == PoseState.Prone)
//				return proneAttack;
//			else
//				return null;
//		}

//		public void Tick(Actor self)
//		{
//			if (proneRemainingDuration > 0 && currentState != InfantryState.Action && currentPose == PoseState.Stand)
//			{
//				animOverride2.ChangeAnimation(standToProne);
//				currentPose = PoseState.StandToProne;
//				shotOverride2.ShotEndAction = () =>
//				{
//					switchProne.SetFlag(true);
//					shotOverride2.ShotEndAction = null;
//				};
//				shotOverride2.ShotEndBlendAction = () =>
//				{
//					currentPose = PoseState.Prone;
//					shotOverride2.ShotEndBlendAction = null;
//				};

//				shotOverride2.StartShot();
//			}

//			if (currentPose == PoseState.Prone)
//			{
//				proneRemainingDuration--;
//				if (currentState != InfantryState.Action && proneRemainingDuration <= 0)
//				{
//					animOverride2.ChangeAnimation(proneToStand);
//					currentPose = PoseState.ProneToStand;
//					shotOverride2.ShotEndAction = () =>
//					{
//						switchProne.SetFlag(false);
//						proneRemainingDuration = 0;
//						shotOverride2.ShotEndAction = null;
//					};
//					shotOverride2.ShotEndBlendAction = () =>
//					{
//						currentPose = PoseState.Stand;
//						shotOverride2.ShotEndBlendAction = null;
//					};
//					shotOverride2.StartShot();
//				}

//				// prone pose must be guard
//				currentState = InfantryState.Guard;
//				switchGuard.SetFlag(true);
//				guardTick = 0;

//				if (proneConditionToken == Actor.InvalidConditionToken)
//					proneConditionToken = self.GrantCondition(info.ProneCondition);
//			}
//			else
//			{
//				if (proneConditionToken != Actor.InvalidConditionToken)
//					proneConditionToken = self.RevokeCondition(proneConditionToken);
//			}

//			// we only unguard when not move
//			if (currentPose == PoseState.Stand && guardTick >= info.GuardTick)
//			{
//				// prepare to unguard
//				if (currentState == InfantryState.Guard)
//				{
//					// convert anim or convert blend should be Action state
//					currentState = InfantryState.Action;
//					if (currentPose == PoseState.Stand && guardToStand != null)
//					{
//						animOverride.ChangeAnimation(guardToStand);
//						shotOverride.ShotEndAction = () =>
//						{
//							switchGuard.SwitchTick = 1;
//							switchGuard.SetFlag(false);
//							shotOverride.ShotEndAction = null;
//						};
//						shotOverride.ShotEndBlendAction = () =>
//						{
//							currentState = InfantryState.Idle;
//							shotOverride.ShotEndBlendAction = null;
//						};
//						shotOverride.StartShot();
//					}
//					else
//					{
//						switchGuard.SwitchTick = info.GuardBlendTick;
//						switchGuard.SetFlag(false);
//					}
//				}

//				if (switchGuard.BlendValue <= FP.Zero)
//				{
//					currentState = InfantryState.Idle;
//				}
//			}
//			else if (currentPose == PoseState.Stand)
//			{
//				// prepare to guard
//				if (currentState != InfantryState.Guard && currentState != InfantryState.Action)
//				{
//					// convert anim or convert blend should be Action state
//					currentState = InfantryState.Action;
//					if (currentPose == PoseState.Stand && standToGuard != null)
//					{
//						animOverride.ChangeAnimation(standToGuard);
//						shotOverride.ShotEndAction = () =>
//						{
//							switchGuard.SwitchTick = 1;
//							switchGuard.SetFlag(true);
//							shotOverride.ShotEndAction = null;
//						};
//						shotOverride.ShotEndBlendAction = () =>
//						{
//							currentState = InfantryState.Guard;
//							shotOverride.ShotEndBlendAction = null;
//						};
//						shotOverride.StartShot();
//					}
//					else
//					{
//						switchGuard.SwitchTick = info.GuardBlendTick;
//						switchGuard.SetFlag(true);
//					}
//				}

//				if (switchGuard.BlendValue >= FP.One)
//				{
//					currentState = InfantryState.Guard;
//				}

//				if (currentState == InfantryState.Guard && move.CurrentMovementTypes == MovementType.None)
//					guardTick++;
//			}

//			// handle movement
//			if (move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
//			{
//				if (currentState == InfantryState.Action)
//					shotOverride.Interrupt();

//				if (currentPose == PoseState.Stand || currentPose == PoseState.ProneToStand)
//				{
//					switchWalk.SetFlag(true);
//				}
//				else if (currentPose == PoseState.Prone || currentPose == PoseState.StandToProne)
//				{
//					// when convert or prone 
//					switchCrawl.SetFlag(true);
//				}
//			}
//			else
//			{
//				switchWalk.SetFlag(false);
//				switchCrawl.SetFlag(false);
//			}

//		}

//		void INotifyDamage.Damaged(Actor self, AttackInfo e)
//		{
//			if (!info.CanProne)
//				return;

//			if (e.Damage.Value <= 0 || !e.Damage.DamageTypes.Overlaps(info.ProneDamageTriggers))
//				return;

//			proneRemainingDuration = info.ProneDuration;
//		}

//		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
//		{
//			if (currentPose != PoseState.Prone)
//				return 100;

//			if (damage == null || damage.DamageTypes.IsEmpty)
//				return 100;

//			var modifierPercentages = info.ProneDamageModifiers.Where(x => damage.DamageTypes.Contains(x.Key)).Select(x => x.Value);
//			return Mods.Common.Util.ApplyPercentageModifiers(100, modifierPercentages);
//		}

//		int ISpeedModifier.GetSpeedModifier()
//		{
//			return currentPose == PoseState.Prone ? info.ProneSpeedModifier : 100;
//		}
//	}
//}

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
using TagLib.Id3v2;
using TrueSync;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class InfantryBlendTreeInfo : TraitInfo, Requires<WithSkeletonInfo>, Requires<HealthInfo>
	{
		public readonly string SkeletonToUse = null;

		// basic
		public readonly string Stand = null;
		public readonly string Walk = null;
		public readonly string Guard = null;
		public readonly string GuardMove = null;

		// guard convert
		public readonly string StandToGuard = null;
		public readonly string GuardToStand = null;

		// attack
		public readonly string Attack = null;

		// die
		public readonly string DieStand = null;

		public readonly bool CanProne = false;

		// prone
		public readonly string Prone = null;
		public readonly string Crawl = null;

		// prone attack
		public readonly string ProneAttack = null;

		// prone convert
		public readonly string StandToProne = null;
		public readonly string ProneToStand = null;
		public readonly int ProneBlendFadeTick = 8;

		// die prone
		public readonly string DieProne = null;

		public readonly string[] IdleActions = null;

		public readonly int IdleBlendFadeTick = 15;

		public readonly int MinIdleDelay = 110;
		public readonly int MaxIdleDelay = 400;

		public readonly string UpperMask = null;
		public readonly string LowerMask = null;
		public readonly string FullMask = null;

		public readonly int StopMoveBlendTick = 5;
		public readonly int GuardBlendTick = 5;

		public readonly int GuardTick = 50;

		public readonly int CommonBlendFadeTick = 8;

		[Desc("If move in Guard state, the Guard timer will not be reduced," +
			" which means the actor will always be in Guard state when moving")]
		public readonly bool KeepGuardStateWhenMoving = false;

		[Desc("How long (in ticks) the actor remains prone.",
			"Negative values mean actor remains prone permanently.")]
		public readonly int ProneDuration = 100;

		[Desc("Prone movement speed as a percentage of the normal speed.")]
		public readonly int ProneSpeedModifier = 50;

		[Desc("Damage types that trigger prone state. Defined on the warheads.",
			"If Duration is negative (permanent), you can leave this empty to trigger prone state immediately.")]
		public readonly BitSet<DamageType> ProneDamageTriggers = default;

		[Desc("Damage modifiers for each damage type (defined on the warheads) while the unit is prone.")]
		public readonly Dictionary<string, int> ProneDamageModifiers = new Dictionary<string, int>();

		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string ProneCondition = null;

		[Desc("It attempts to modify the FireDelay of these Armament using the AttackFrame and ProneAttackFrame")]
		public readonly string[] ArmamentsToDelay = { "primary" };

		public readonly int AttackFrame = -1;
		public readonly int ProneAttackFrame = -1;
		public readonly WVec ProneOffset = WVec.Zero;

		[Desc("How long the body remains after death.")]
		public readonly int DeathBodyRemain = 50;

		[Desc("The vector that the body moves per frame after death.")]
		public readonly WVec DeathFadeVec = new WVec(0, 0, -4);

		[Desc("The amount of Alpha decreased per frame when death")]
		public readonly float DeathFadeSpeed = 0;

		public override object Create(ActorInitializer init) { return new InfantryBlendTree(init.Self, this); }
	}

	public class InfantryBlendTree : IBlendTreeHandler, IPrepareForAttack, ITick, INotifyCreated, INotifyAttack, INotifyAiming,
		INotifyDamage, IDamageModifier, ISpeedModifier, INotifyKilled, INotifyEquip, INotifyConsumeItem
	{
		readonly BlendTree blendTree;
		readonly InfantryBlendTreeInfo info;
		readonly WithSkeleton withSkeleton;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly IMove move;
		readonly Health health;
		readonly RenderMeshes rm;

		#region animations
		SkeletalAnim stand;
		SkeletalAnim walk;
		SkeletalAnim guard;
		SkeletalAnim guardMove;

		SkeletalAnim standToGuard;
		SkeletalAnim guardToStand;
		SkeletalAnim attack;

		SkeletalAnim prone;
		SkeletalAnim crawl;
		SkeletalAnim proneAttack;
		SkeletalAnim standToProne;
		SkeletalAnim proneToStand;

		SkeletalAnim die;
		SkeletalAnim dieProne;

		SkeletalAnim[] idleActions;

		#endregion
		#region nodes
		readonly AnimationNode animStop;
		readonly AnimationNode animMove;
		readonly AnimationNode animGuard;
		readonly AnimationNode animGuardMove;

		readonly AnimationNode animDie;
		readonly AnimationNode animOverride;
		readonly AnimationNode animIdleOverride;
		readonly AnimationNode animFullOverride;

		readonly Switch switchStopGuard;
		readonly Switch switchMoveGuard;

		readonly OneShot shotLower;
		readonly OneShot shotUpper;

		readonly Switch switchMoveLower;
		readonly Switch switchMoveUpper;

		readonly Blend2 merge;

		readonly OneShot shotIdleOverride;
		readonly OneShot shotFullOverride;
		readonly OneShot shotDie;

		#endregion

		public enum InfantryState
		{
			Idle,
			Guard,
			Action,
			Die,
		}

		public enum PoseState
		{
			Stand,
			Prone,
			StandToProne,
			ProneToStand,
		}

		public enum ActionType
		{
			None,
			Attack,
			Switch,
		}

		public PoseState CurrentPose => currentPose;
		public InfantryState CurrentState => currentState;

		PoseState currentPose = PoseState.Stand;
		InfantryState currentState = InfantryState.Idle;
		ActionType currentAction = ActionType.None;

		public bool PlayingIdleAction => playingIdleAction;
		bool playingIdleAction = false;

		[Sync]
		int guardTick = 9999;

		int proneRemainingDuration = 0;
		int proneConditionToken = Actor.InvalidConditionToken;

		Armament[] armamentsToModify;
		TurnOnIdle turnOnIdle;
		int nextIdleActionTick = 0;

		BlendTreeNodeOutPut dieBlendResultOutPut;

		WithEquipmentAnimationInfo replaceAnimsBy;

		int attackFrame, proneAttackFrame;

		int GetBoneId(string name)
		{
			var boneid = withSkeleton.GetBoneId(name);
			if (boneid == -1)
				throw new Exception("can't find bone " + name + " in skeleton.");
			return boneid;
		}

		public InfantryBlendTree(Actor self, InfantryBlendTreeInfo info)
		{
			this.info = info;
			this.self = self;
			move = self.Trait<IMove>();
			myFacing = self.Trait<IFacing>();
			health = self.Trait<Health>();
			health.RemoveOnDeath = false;
			rm = self.Trait<RenderMeshes>();

			if (info.SkeletonToUse == null)
				throw new YamlException("InfantryBlendTree must define a SkeletonToUse for get animations");
			withSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.SkeletonToUse);

			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);
			guardMove = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardMove);

			if (!string.IsNullOrEmpty(info.StandToGuard))
				standToGuard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToGuard);

			if (!string.IsNullOrEmpty(info.GuardToStand))
				guardToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardToStand);

			attack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Attack);

			die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieStand);

			if (info.CanProne)
			{
				prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Prone);
				crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Crawl);
				proneAttack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneAttack);
				standToProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToProne);
				proneToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneToStand);
				dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieProne);
			}

			if (info.IdleActions != null && info.IdleActions.Length > 0)
			{
				idleActions = new SkeletalAnim[info.IdleActions.Length];

				for (int i = 0; i < idleActions.Length; i++)
				{
					idleActions[i] = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.IdleActions[i]);
				}
			}

			var allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.AllValidMask;

			if (info.UpperMask == null)
				throw new Exception("Need UpperrMask");
			if (info.LowerMask == null)
				throw new Exception("Need LowerMask");
			if (info.FullMask != null)
			{
				allvalidmask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.FullMask);
			}

			var uppermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.UpperMask);
			var lowermask = withSkeleton.OrderedSkeleton.SkeletonAsset.GetAnimMask(withSkeleton.Image, info.LowerMask);

			blendTree = new BlendTree();

			uint id = 0;

			animStop = new AnimationNode("Stop", id++, blendTree, allvalidmask, stand);
			animMove = new AnimationNode("Move", id++, blendTree, allvalidmask, walk);
			animGuard = new AnimationNode("Guard", id++, blendTree, allvalidmask, guard);
			animGuardMove = new AnimationNode("GuardMove", id++, blendTree, allvalidmask, guardMove);

			// the override node and die node should handle with trait
			animOverride = new AnimationNode("Override", id++, blendTree, allvalidmask, die)
			{
				NodePlayType = LeafNode.PlayType.Once
			};

			animIdleOverride = new AnimationNode("IdleOverride", id++, blendTree, allvalidmask, die)
			{
				NodePlayType = LeafNode.PlayType.Once
			};

			animFullOverride = new AnimationNode("Override2", id++, blendTree, allvalidmask, die)
			{
				NodePlayType = LeafNode.PlayType.Once
			};

			animDie = new AnimationNode("Die", id++, blendTree, allvalidmask, die)
			{
				NodePlayType = LeafNode.PlayType.Once
			};

			switchStopGuard = new Switch("StopGuard", id++, blendTree, allvalidmask, animStop, animGuard, info.GuardBlendTick);
			switchMoveGuard = new Switch("MoveGuard", id++, blendTree, allvalidmask, animMove, animGuardMove, info.GuardBlendTick);

			shotLower = new OneShot("Lower", id++, blendTree, lowermask, switchStopGuard, animOverride, OneShot.ShotEndType.Recover, 5);

			switchMoveLower = new Switch("MoveLower", id++, blendTree, lowermask, shotLower, switchMoveGuard, info.StopMoveBlendTick);
			switchMoveUpper = new Switch("MoveUpper", id++, blendTree, uppermask, switchStopGuard, switchMoveGuard, info.StopMoveBlendTick);

			shotUpper = new OneShot("Upper", id++, blendTree, uppermask, switchMoveUpper, animOverride, OneShot.ShotEndType.Recover, 5);

			merge = new Blend2("Merge", id++, blendTree, allvalidmask, switchMoveLower, shotUpper);

			merge.BlendValue = FP.One;

			shotIdleOverride = new OneShot("IdleOverride", id++, blendTree, allvalidmask, merge, animIdleOverride, OneShot.ShotEndType.Recover, info.IdleBlendFadeTick);

			shotFullOverride = new OneShot("FullOverride", id++, blendTree, allvalidmask, shotIdleOverride, animFullOverride, OneShot.ShotEndType.Recover, info.ProneBlendFadeTick);

			shotDie = new OneShot("Die", id++, blendTree, allvalidmask, shotFullOverride, animDie, OneShot.ShotEndType.Keep, 8);

			blendTree.InitTree(shotDie);
			withSkeleton.BlendTreeHandler = this;

			nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);

			attackFrame = info.AttackFrame;
			proneAttackFrame = info.ProneAttackFrame;
		}

		void ReplaceAnim(WithEquipmentAnimationInfo replaceInfo)
		{
			replaceAnimsBy = replaceInfo;
			var animReplaceDict = replaceInfo.AnimsReplace;

			string name;
			if (animReplaceDict.TryGetValue("Stand", out name))
			{
				stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);
			}

			if (animReplaceDict.TryGetValue("Walk", out name))
			{
				walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);
			}

			if (animReplaceDict.TryGetValue("Guard", out name))
			{
				guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);
			}

			if (animReplaceDict.TryGetValue("GuardMove", out name))
			{
				guardMove = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				guardMove = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardMove);
			}

			if (animReplaceDict.TryGetValue("StandToGuard", out name))
			{
				standToGuard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else if (!string.IsNullOrEmpty(info.StandToGuard))
			{
				standToGuard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToGuard);
			}
			else
			{
				standToGuard = null;
			}

			if (animReplaceDict.TryGetValue("GuardToStand", out name))
			{
				guardToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else if (!string.IsNullOrEmpty(info.GuardToStand))
			{
				guardToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardToStand);
			}
			else
			{
				guardToStand = null;
			}

			if (animReplaceDict.TryGetValue("Attack", out name))
			{
				attack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				attack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Attack);
			}

			if (info.CanProne)
			{
				if (animReplaceDict.TryGetValue("Prone", out name))
				{
					prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Prone);
				}

				if (animReplaceDict.TryGetValue("Crawl", out name))
				{
					crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Crawl);
				}

				if (animReplaceDict.TryGetValue("ProneAttack", out name))
				{
					proneAttack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					proneAttack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneAttack);
				}

				if (animReplaceDict.TryGetValue("StandToProne", out name))
				{
					standToProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					standToProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToProne);
				}

				if (animReplaceDict.TryGetValue("ProneToStand", out name))
				{
					proneToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					proneToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneToStand);
				}

				if (animReplaceDict.TryGetValue("DieProne", out name))
				{
					dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
				}
				else
				{
					dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieProne);
				}
			}

			if (animReplaceDict.TryGetValue("DieStand", out name))
			{
				die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, name);
			}
			else
			{
				die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieStand);
			}

			if (replaceInfo.IdleActions != null && replaceInfo.IdleActions.Length > 0)
			{
				idleActions = new SkeletalAnim[replaceInfo.IdleActions.Length];

				for (int i = 0; i < idleActions.Length; i++)
				{
					idleActions[i] = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, replaceInfo.IdleActions[i]);
				}
			}

			int frame;
			if (replaceInfo.ActionFrameReplace.TryGetValue("AttackFrame", out frame))
			{
				attackFrame = frame;
			}
			else
			{
				attackFrame = info.AttackFrame;
			}

			if (replaceInfo.ActionFrameReplace.TryGetValue("ProneAttackFrame", out frame))
			{
				proneAttackFrame = frame;
			}
			else
			{
				proneAttackFrame = info.ProneAttackFrame;
			}

			SwitchAnim(CurrentPose);
		}

		void ResetAnim()
		{
			replaceAnimsBy = null;

			stand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Stand);

			walk = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Walk);

			guard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Guard);

			guardMove = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardMove);

			if (!string.IsNullOrEmpty(info.StandToGuard))
			{
				standToGuard = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToGuard);
			}
			else
			{
				standToGuard = null;
			}

			if (!string.IsNullOrEmpty(info.GuardToStand))
			{
				guardToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.GuardToStand);
			}
			else
			{
				guardToStand = null;
			}

			attack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Attack);

			die = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieStand);

			if (info.CanProne)
			{
				prone = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Prone);

				crawl = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.Crawl);

				proneAttack = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneAttack);

				standToProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.StandToProne);

				proneToStand = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.ProneToStand);

				dieProne = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.DieProne);
			}

			if (info.IdleActions != null && info.IdleActions.Length > 0)
			{
				idleActions = new SkeletalAnim[info.IdleActions.Length];

				for (int i = 0; i < idleActions.Length; i++)
				{
					idleActions[i] = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.IdleActions[i]);
				}
			}

			attackFrame = info.AttackFrame;
			proneAttackFrame = info.ProneAttackFrame;

			SwitchAnim(CurrentPose);
		}

		public void Created(Actor self)
		{
			armamentsToModify = self.TraitsImplementing<Armament>().Where(a => info.ArmamentsToDelay.Contains(a.Info.Name)).ToArray();
			foreach (var a in armamentsToModify)
			{
				a.AdditionalLocalOffset = () => currentPose == PoseState.Prone ? info.ProneOffset : WVec.Zero;
				a.OverrideFireDelay = () => currentPose == PoseState.Prone ? proneAttackFrame : attackFrame;
			}

			turnOnIdle = self.TraitOrDefault<TurnOnIdle>();
		}

		public BlendTreeNodeOutPut GetResult()
		{
			if (deathFade)
			{
				return dieBlendResultOutPut;
			}
			else
				return blendTree.GetOutPut();
		}

		void IBlendTreeHandler.UpdateTick()
		{
			blendTree.UpdateTick();
		}

		WRot IBlendTreeHandler.FacingOverride()
		{
			return myFacing.Orientation;
		}

		public bool PrepareForAttack(in Target target)
		{
			if (currentState == InfantryState.Die)
				return false;

			if (currentPose == PoseState.StandToProne || currentPose == PoseState.ProneToStand)
				return false;

			guardTick = 0;

			if (currentState == InfantryState.Guard ||
				(currentState == InfantryState.Action && currentAction == ActionType.Attack))
			{
				return true;
			}

			return false;
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			guardTick = 0;
		}

		void INotifyAiming.StartedAiming(Actor self, AttackBase attack)
		{
			guardTick = 0;
		}

		void INotifyAiming.StoppedAiming(Actor self, AttackBase attack)
		{
			guardTick = 0;
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
		{
			guardTick = 0;

			// we should in Guard State now
			// or there must be something wrong
			currentState = InfantryState.Action;
			currentAction = ActionType.Attack;
			animOverride.ChangeAnimation(GetAttackAnim());

			shotUpper.ShotEndAction = () =>
			{
				guardTick = 0;
				shotUpper.ShotEndAction = null;
			};

			shotUpper.ShotEndBlendAction = () =>
			{
				currentState = InfantryState.Guard;
				currentAction = ActionType.None;
				shotUpper.ShotEndBlendAction = null;
			};

			shotUpper.StartShot();
			shotLower.StartShot();
		}

		SkeletalAnim GetAttackAnim()
		{
			if (currentPose == PoseState.Stand)
				return attack;
			else if (currentPose == PoseState.Prone)
				return proneAttack;
			else
				return null;
		}

		void SwitchAnim(PoseState poseState)
		{
			if (poseState == PoseState.Stand)
			{
				animStop.ChangeAnimation(stand);
				animMove.ChangeAnimation(walk);
				animGuard.ChangeAnimation(guard);
				animGuardMove.ChangeAnimation(guardMove);
			}
			else if (info.CanProne && poseState == PoseState.Prone)
			{
				animStop.ChangeAnimation(prone);
				animMove.ChangeAnimation(crawl);
				animGuard.ChangeAnimation(prone);
				animGuardMove.ChangeAnimation(crawl);
			}

		}

		public void Tick(Actor self)
		{
			if (turnOnIdle != null)
				turnOnIdle.HoldTurn = true;

			if (deathFade)
			{
				deathFadeAlpha -= info.DeathFadeSpeed;
				withSkeleton.SkeletonOffset += info.DeathFadeVec;

				if (deathFadeTick-- <= 0)
				{
					self.Dispose();
				}

				if (deathFadeAlpha != 1)
					rm.RenderAlpha = deathFadeAlpha;
			}

			if (currentState == InfantryState.Die)
			{
				return;
			}

			if (currentState != InfantryState.Action)
				currentAction = ActionType.None;

			if (idleActions != null && idleActions.Length > 0)
			{
				if (currentState == InfantryState.Idle && self.IsIdle && self.IsInWorld &&
					currentPose == PoseState.Stand && move.CurrentMovementTypes == MovementType.None)
				{
					if (turnOnIdle != null && !playingIdleAction)
						turnOnIdle.HoldTurn = false;

					if (!playingIdleAction && nextIdleActionTick-- <= 0)
					{
						playingIdleAction = true;
						animIdleOverride.ChangeAnimation(idleActions[self.World.SharedRandom.Next(0, idleActions.Length - 1)]);
						shotIdleOverride.FadeTick = info.IdleBlendFadeTick;
						shotIdleOverride.ShotEndAction = () =>
						{
							playingIdleAction = false;
							shotIdleOverride.ShotEndAction = null;
							nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
						};
						shotIdleOverride.StartShot();
					}
				}
				else
				{
					if (playingIdleAction)
						shotIdleOverride.Interrupt();
					else
					{
						nextIdleActionTick = Math.Max(nextIdleActionTick, info.MinIdleDelay);
					}
				}
			}

			if (turnOnIdle != null &&
				currentState == InfantryState.Idle && self.IsIdle && self.IsInWorld &&
				currentPose == PoseState.Stand && move.CurrentMovementTypes == MovementType.None)
				turnOnIdle.HoldTurn = false;

			if (proneRemainingDuration > 0 && currentState != InfantryState.Action && currentPose == PoseState.Stand)
			{
				animFullOverride.ChangeAnimation(standToProne);
				currentPose = PoseState.StandToProne;
				currentState = InfantryState.Action;
				currentAction = ActionType.Switch;
				shotLower.Interrupt();
				shotUpper.Interrupt();
				shotFullOverride.FadeTick = info.ProneBlendFadeTick;
				shotFullOverride.ShotEndAction = () =>
				{
					SwitchAnim(PoseState.Prone);
					shotFullOverride.ShotEndAction = null;
				};
				shotFullOverride.ShotEndBlendAction = () =>
				{
					currentPose = PoseState.Prone;
					currentAction = ActionType.None;
					currentState = InfantryState.Guard;
					shotFullOverride.ShotEndBlendAction = null;
				};

				shotFullOverride.StartShot();
			}

			if (currentPose == PoseState.Prone)
			{
				proneRemainingDuration--;
				if (currentState != InfantryState.Action && proneRemainingDuration <= 0)
				{
					animFullOverride.ChangeAnimation(proneToStand);
					shotFullOverride.FadeTick = info.ProneBlendFadeTick;
					currentPose = PoseState.ProneToStand;
					currentState = InfantryState.Action;
					currentAction = ActionType.Switch;
					shotLower.Interrupt();
					shotUpper.Interrupt();
					shotFullOverride.ShotEndAction = () =>
					{
						SwitchAnim(PoseState.Stand);
						proneRemainingDuration = 0;
						shotFullOverride.ShotEndAction = null;
					};
					shotFullOverride.ShotEndBlendAction = () =>
					{
						currentPose = PoseState.Stand;
						currentAction = ActionType.None;
						currentState = InfantryState.Idle;
						shotFullOverride.ShotEndBlendAction = null;
					};
					shotFullOverride.StartShot();
				}

				// prone pose must be guard
				currentState = InfantryState.Guard;
				switchStopGuard.SwitchTick = 1;
				// switchMoveGuard.SwitchTick = 1;
				switchStopGuard.SetFlag(true);
				switchMoveGuard.SetFlag(true);

				guardTick = 0;

				if (proneConditionToken == Actor.InvalidConditionToken)
					proneConditionToken = self.GrantCondition(info.ProneCondition);
			}
			else
			{
				if (proneConditionToken != Actor.InvalidConditionToken)
					proneConditionToken = self.RevokeCondition(proneConditionToken);
			}

			// we only unguard when not move
			if (currentPose == PoseState.Stand && guardTick >= info.GuardTick)
			{
				// prepare to unguard
				if (currentState == InfantryState.Guard)
				{
					// convert anim or convert blend should be Action state
					if (guardToStand != null)
					{
						currentState = InfantryState.Action;
						currentAction = ActionType.Switch;
						animOverride.ChangeAnimation(guardToStand);
						shotUpper.ShotEndAction = () =>
						{
							switchStopGuard.SwitchTick = 1;

							switchStopGuard.SetFlag(false);
							switchMoveGuard.SetFlag(false);
							shotUpper.ShotEndAction = null;
						};
						shotUpper.ShotEndBlendAction = () =>
						{
							currentState = InfantryState.Idle;
							currentAction = ActionType.None;
							shotUpper.ShotEndBlendAction = null;
						};
						shotUpper.StartShot();
						shotLower.StartShot();
					}
					else
					{
						switchStopGuard.SwitchTick = info.GuardBlendTick;
						currentState = InfantryState.Idle;
						switchStopGuard.SetFlag(false);
						switchMoveGuard.SetFlag(false);
					}
				}

				if (guardToStand == null && switchStopGuard.BlendValue <= FP.Zero)
				{
					currentState = InfantryState.Idle;
					currentAction = ActionType.None;
				}
			}
			else if (currentPose == PoseState.Stand)
			{
				// prepare to guard
				if (currentState != InfantryState.Guard && currentState != InfantryState.Action)
				{
					// convert anim or convert blend should be Action state
					if (currentPose == PoseState.Stand && standToGuard != null)
					{
						currentState = InfantryState.Action;
						currentAction = ActionType.Switch;
						animOverride.ChangeAnimation(standToGuard);
						shotUpper.ShotEndAction = () =>
						{
							switchStopGuard.SwitchTick = 1;

							switchStopGuard.SetFlag(true);
							switchMoveGuard.SetFlag(true);
							shotUpper.ShotEndAction = null;
						};
						shotUpper.ShotEndBlendAction = () =>
						{
							currentState = InfantryState.Guard;
							currentAction = ActionType.None;
							shotUpper.ShotEndBlendAction = null;
						};
						shotUpper.StartShot();
						shotLower.StartShot();
					}
					else
					{
						switchStopGuard.SwitchTick = info.GuardBlendTick;
						switchStopGuard.SetFlag(true);
						switchMoveGuard.SetFlag(true);
					}
				}

				if (standToGuard == null && switchStopGuard.BlendValue >= FP.One)
				{
					currentState = InfantryState.Guard;
					currentAction = ActionType.None;
				}

				if (!info.KeepGuardStateWhenMoving || (currentState == InfantryState.Guard && move.CurrentMovementTypes == MovementType.None))
					guardTick++;
			}

			// handle movement
			if (move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
			{
				switchMoveLower.SetFlag(true);
				switchMoveUpper.SetFlag(true);
			}
			else
			{
				switchMoveLower.SetFlag(false);
				switchMoveUpper.SetFlag(false);
			}

		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (!info.CanProne)
				return;

			if (e.Damage.Value <= 0 || !e.Damage.DamageTypes.Overlaps(info.ProneDamageTriggers))
				return;

			proneRemainingDuration = info.ProneDuration;
		}

		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
		{
			if (currentPose != PoseState.Prone)
				return 100;

			if (damage == null || damage.DamageTypes.IsEmpty)
				return 100;

			var modifierPercentages = info.ProneDamageModifiers.Where(x => damage.DamageTypes.Contains(x.Key)).Select(x => x.Value);
			return Mods.Common.Util.ApplyPercentageModifiers(100, modifierPercentages);
		}

		int ISpeedModifier.GetSpeedModifier()
		{
			if (currentState == InfantryState.Die)
				return 0;

			if (currentPose == PoseState.ProneToStand)
				return 0;

			if (currentPose == PoseState.Prone && currentState == InfantryState.Action)
				return 0;

			return currentPose != PoseState.Stand ? info.ProneSpeedModifier : 100;
		}

		bool deathFade = false;
		float deathFadeAlpha = 1;
		int deathFadeTick = 0;
		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			animDie.ChangeAnimation(!info.CanProne || currentPose == PoseState.Stand ? die : dieProne);

			currentState = InfantryState.Die;

			self.CancelActivity();

			var me = self;

			shotDie.ShotEndAction = () =>
			{
				dieBlendResultOutPut = GetResult();
				deathFade = true;
				deathFadeTick = info.DeathBodyRemain;
			};

			shotDie.StartShot();
		}

		bool INotifyEquip.CanEquip(Actor self, Item item)
		{
			if (currentState == InfantryState.Die)
				return false;

			var animRef = item.ItemActor.TraitOrDefault<WithEquipmentAnimation>();
			if (animRef == null)
				return true;

			return currentState != InfantryState.Action && currentPose == PoseState.Stand;
		}

		void INotifyEquip.Equipped(Actor self, Item item)
		{
			if (playingIdleAction)
				shotFullOverride.Interrupt();
			else
			{
				nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
			}

			if (currentState == InfantryState.Die)
				return;

			var animRef = item.ItemActor.TraitOrDefault<WithEquipmentAnimation>();
			if (animRef == null)
				return;

			ReplaceAnim(animRef.Info);

			if (!string.IsNullOrEmpty(animRef.Info.TakeOutAnim))
			{
				// make a smooth blend
				// get the current blend result than use OneShot to make a smooth Blend
				var currentAnimBlend = new SkeletalAnim(GetResult());
				animFullOverride.ChangeAnimation(currentAnimBlend);
				shotFullOverride.FadeTick = info.CommonBlendFadeTick;
				shotFullOverride.StartShot();
				shotFullOverride.ForceShotTickToFadeTick();

				// item.EquipmentSlot?.ToggleEquipmentRender(false);
				var takeOutAnim = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, animRef.Info.TakeOutAnim);
				animOverride.ChangeAnimation(takeOutAnim);
				// animOverride.AddFrameAction(animRef.Info.TakeOutFrame, () =>
				// {
				// 	self.World.AddFrameEndTask(w => animOverride.ClearFrameAction(animRef.Info.TakeOutFrame));
				// 	item.EquipmentSlot?.ToggleEquipmentRender(true);
				// });
				var lastState = currentState;
				if (lastState == InfantryState.Action)
					lastState = InfantryState.Idle;

				currentAction = ActionType.Switch;
				currentState = InfantryState.Action;

				// shotUpper.ShotEndAction = () =>
				// {
				// 	currentState = lastState;
				// 	shotUpper.ShotEndAction = null;
				// };
				shotUpper.ShotEndBlendAction = () =>
				{
					currentState = lastState;
					currentAction = ActionType.None;
					shotUpper.ShotEndBlendAction = null;
				};

				shotUpper.StartShot();
				shotLower.StartShot();
			}
		}

		bool INotifyEquip.CanUnequip(Actor self, Item item)
		{
			if (currentState == InfantryState.Die)
				return true;

			if (item is ConsumableItem && (item as ConsumableItem).IsBeingConsumed == true)
			{
				return false;
			}

			var animRef = item.ItemActor.TraitOrDefault<WithEquipmentAnimation>();
			if (animRef == null)
				return true;

			return currentState != InfantryState.Action && currentPose == PoseState.Stand;
		}

		void INotifyEquip.Unequipped(Actor self, Item item)
		{
			if (playingIdleAction)
				shotFullOverride.Interrupt();
			else
			{
				nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
			}

			if (currentState == InfantryState.Die)
				return;

			var animRef = item.ItemActor.TraitOrDefault<WithEquipmentAnimation>();
			if (animRef == null)
				return;

			ResetAnim();

			if (!string.IsNullOrEmpty(animRef.Info.PutAwayAnim))
			{
				// make a smooth blend
				// get the current blend result than use OneShot to make a smooth Blend
				var currentAnimBlend = new SkeletalAnim(GetResult());
				animFullOverride.ChangeAnimation(currentAnimBlend);
				shotFullOverride.FadeTick = info.CommonBlendFadeTick;
				shotFullOverride.StartShot();
				shotFullOverride.ForceShotTickToFadeTick();

				var putAwayAnim = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, animRef.Info.PutAwayAnim);
				animOverride.ChangeAnimation(putAwayAnim);
				var lastState = currentState;
				if (lastState == InfantryState.Action)
					lastState = InfantryState.Idle;
				currentState = InfantryState.Action;
				currentAction = ActionType.Switch;
				// shotUpper.ShotEndAction = () =>
				// {
				// 	currentState = lastState;
				// 	shotUpper.ShotEndAction = null;
				// };
				shotUpper.ShotEndBlendAction = () =>
				{
					currentState = lastState;
					currentAction = ActionType.None;
					shotUpper.ShotEndBlendAction = null;
				};

				shotUpper.StartShot();
				shotLower.StartShot();
			}
		}

		public bool CanConsume(Item item)
		{
			if (currentState == InfantryState.Die)
				return false;

			return currentAction != ActionType.Switch && currentPose != PoseState.ProneToStand && currentPose != PoseState.StandToProne;
		}

		public void Consume(Item item)
		{
			var anim = (item as ConsumableItem).UseAnim;
			var useFrame = (item as ConsumableItem).UseFrame;
			if (!string.IsNullOrEmpty(anim))
			{
				// make a smooth blend
				// get the current blend result than use OneShot to make a smooth Blend
				var currentAnimBlend = new SkeletalAnim(GetResult());
				animFullOverride.ChangeAnimation(currentAnimBlend);
				shotFullOverride.FadeTick = info.CommonBlendFadeTick;
				shotFullOverride.StartShot();
				shotFullOverride.ForceShotTickToFadeTick();

				// item.EquipmentSlot?.ToggleEquipmentRender(false);
				var takeOutAnim = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, anim);
				animOverride.ChangeAnimation(takeOutAnim);
				bool hasConsumed = false;
				animOverride.AddFrameAction(useFrame, () =>
				{
					self.World.AddFrameEndTask(w => animOverride.ClearFrameAction(useFrame));
					(item as ConsumableItem).ConsumeAction(self);
					item.EquipmentSlot?.RemoveItem(item.EquipmentSlot.SlotOwnerActor); // directly remove, avoid the render toggle
					item.Inventory?.TryRemove(item.Inventory.InventoryActor, item);
					hasConsumed = true;
				});
				animOverride.ChangeFallbackAcition(() =>
				{
					animOverride.ChangeFallbackAcition(null);
					(item as ConsumableItem).StopConsume(self);
					item.EquipmentSlot?.RemoveItem(item.EquipmentSlot.SlotOwnerActor);
					item.Inventory?.TryRemove(item.Inventory.InventoryActor, item);
					hasConsumed = true;
				});
				var lastState = currentState;
				if (lastState == InfantryState.Action)
					lastState = InfantryState.Idle;

				currentAction = ActionType.Switch;
				currentState = InfantryState.Action;

				shotUpper.ShotEndAction = () =>
				{
					shotUpper.ShotEndAction = null;
					self.World.AddFrameEndTask(w => animOverride.ClearFrameAction(useFrame));
					animOverride.ChangeFallbackAcition(null);
					if (!hasConsumed)
					{
						(item as ConsumableItem).StopConsume(self);
						item.EquipmentSlot?.RemoveItem(item.EquipmentSlot.SlotOwnerActor);
						item.Inventory?.TryRemove(item.Inventory.InventoryActor, item);
					}
				};

				shotUpper.ShotEndBlendAction = () =>
				{
					currentState = lastState;
					currentAction = ActionType.None;
					shotUpper.ShotEndBlendAction = null;
				};

				shotUpper.StartShot();
				shotLower.StartShot();
			}
		}
	}
}

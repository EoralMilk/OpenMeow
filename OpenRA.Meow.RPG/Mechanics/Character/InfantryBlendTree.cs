using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;
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

		public readonly int GuardTick = 50;

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

		// die prone
		public readonly string DieProne = null;

		public readonly string[] IdleActions = null;

		public readonly int MinIdleDelay = 30;
		public readonly int MaxIdleDelay = 110;

		public readonly string UpperMask = null;
		public readonly string LowerMask = null;
		public readonly string FullMask = null;

		public readonly int StopMoveBlendTick = 5;
		public readonly int GuardBlendTick = 5;

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

		public readonly int AttackFrame = 0;
		public readonly int ProneAttackFrame = 0;
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
		INotifyDamage, IDamageModifier, ISpeedModifier, INotifyKilled
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
		readonly SkeletalAnim stand;
		readonly SkeletalAnim walk;
		readonly SkeletalAnim guard;
		readonly SkeletalAnim guardMove;

		readonly SkeletalAnim standToGuard;
		readonly SkeletalAnim guardToStand;
		readonly SkeletalAnim attack;

		readonly SkeletalAnim prone;
		readonly SkeletalAnim crawl;
		readonly SkeletalAnim proneAttack;
		readonly SkeletalAnim standToProne;
		readonly SkeletalAnim proneToStand;

		readonly SkeletalAnim die;
		readonly SkeletalAnim dieProne;

		readonly SkeletalAnim[] idleActions;

		#endregion
		#region nodes
		readonly AnimationNode animStop;
		readonly AnimationNode animMove;
		readonly AnimationNode animGuard;
		readonly AnimationNode animGuardMove;

		readonly AnimationNode animDie;
		readonly AnimationNode animOverride;
		readonly AnimationNode animFullOverride;

		readonly Switch switchStopGuard;
		readonly Switch switchMoveGuard;

		readonly OneShot shotLower;
		readonly OneShot shotUpper;

		readonly Switch switchMoveLower;
		readonly Switch switchMoveUpper;

		readonly Blend2 merge;

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

		public PoseState CurrentPose => currentPose;
		public InfantryState CurrentState => currentState;

		PoseState currentPose = PoseState.Stand;
		InfantryState currentState = InfantryState.Idle;

		public bool PlayingIdleAction => playingIdleAction;
		bool playingIdleAction = false;

		[Sync]
		int guardTick = 9999;

		int proneRemainingDuration = 0;
		int proneConditionToken = Actor.InvalidConditionToken;

		Armament[] armamentsToModify;
		TurnOnIdle turnOnIdle;
		int nextIdleActionTick = 0;

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

			idleActions = new SkeletalAnim[info.IdleActions.Length];

			for (int i = 0 ; i < idleActions.Length; i++)
			{
				idleActions[i] = withSkeleton.OrderedSkeleton.SkeletonAsset.GetSkeletalAnim(withSkeleton.Image, info.IdleActions[i]);
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

			shotFullOverride = new OneShot("FullOverride", id++, blendTree, allvalidmask, merge, animFullOverride, OneShot.ShotEndType.Recover, 5);

			shotDie = new OneShot("Die", id++, blendTree, allvalidmask, shotFullOverride, animDie, OneShot.ShotEndType.Keep, 8);

			blendTree.InitTree(shotDie);
			withSkeleton.BlendTreeHandler = this;

			nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
		}

		public void Created(Actor self)
		{
			armamentsToModify = self.TraitsImplementing<Armament>().Where(a => info.ArmamentsToDelay.Contains(a.Info.Name)).ToArray();
			foreach (var a in armamentsToModify)
			{
				a.AdditionalLocalOffset = () => currentPose == PoseState.Prone ? info.ProneOffset : WVec.Zero;
				a.OverrideFireDelay = () => currentPose == PoseState.Prone ? info.ProneAttackFrame : info.AttackFrame;
			}

			turnOnIdle = self.TraitOrDefault<TurnOnIdle>();
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
			return myFacing.Orientation;
		}

		public bool PrepareForAttack(in Target target)
		{
			if (currentState == InfantryState.Die)
				return false;

			if (currentPose == PoseState.StandToProne || currentPose == PoseState.ProneToStand)
				return false;

			guardTick = 0;

			if (currentState == InfantryState.Guard)
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
			animOverride.ChangeAnimation(GetAttackAnim());

			shotUpper.ShotEndAction = () =>
			{
				guardTick = 0;
				shotUpper.ShotEndAction = null;
			};

			shotUpper.ShotEndBlendAction = () =>
			{
				currentState = InfantryState.Guard;
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
				animMove.ChangeAnimation(walk) ;
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

			if (proneRemainingDuration > 0 && currentState != InfantryState.Action && currentPose == PoseState.Stand)
			{
				animFullOverride.ChangeAnimation(standToProne);
				currentPose = PoseState.StandToProne;
				shotFullOverride.ShotEndAction = () =>
				{
					SwitchAnim(PoseState.Prone);
					shotFullOverride.ShotEndAction = null;
				};
				shotFullOverride.ShotEndBlendAction = () =>
				{
					currentPose = PoseState.Prone;
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
					currentPose = PoseState.ProneToStand;
					shotFullOverride.ShotEndAction = () =>
					{
						SwitchAnim(PoseState.Stand);
						proneRemainingDuration = 0;
						shotFullOverride.ShotEndAction = null;
					};
					shotFullOverride.ShotEndBlendAction = () =>
					{
						currentPose = PoseState.Stand;
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
					currentState = InfantryState.Action;
					if (guardToStand != null)
					{
						animOverride.ChangeAnimation(guardToStand);
						shotUpper.ShotEndAction = () =>
						{
							switchStopGuard.SwitchTick = 1;
							// switchMoveGuard.SwitchTick = 1;
							switchStopGuard.SetFlag(false);
							switchMoveGuard.SetFlag(false);
							shotUpper.ShotEndAction = null;
						};
						shotUpper.ShotEndBlendAction = () =>
						{
							currentState = InfantryState.Idle;
							shotUpper.ShotEndBlendAction = null;
						};
						shotUpper.StartShot();
						shotLower.StartShot();
					}
					else
					{
						switchStopGuard.SwitchTick = info.GuardBlendTick;
						// switchMoveGuard.SwitchTick = info.GuardBlendTick;
						switchStopGuard.SetFlag(false);
						switchMoveGuard.SetFlag(false);
					}
				}

				if (switchStopGuard.BlendValue <= FP.Zero)
				{
					currentState = InfantryState.Idle;
				}
			}
			else if (currentPose == PoseState.Stand)
			{
				// prepare to guard
				if (currentState != InfantryState.Guard && currentState != InfantryState.Action)
				{
					// convert anim or convert blend should be Action state
					currentState = InfantryState.Action;
					if (currentPose == PoseState.Stand && standToGuard != null)
					{
						animOverride.ChangeAnimation(standToGuard);
						shotUpper.ShotEndAction = () =>
						{
							switchStopGuard.SwitchTick = 1;
							// switchMoveGuard.SwitchTick = 1;
							switchStopGuard.SetFlag(true);
							switchMoveGuard.SetFlag(true);
							shotUpper.ShotEndAction = null;
						};
						shotUpper.ShotEndBlendAction = () =>
						{
							currentState = InfantryState.Guard;
							shotUpper.ShotEndBlendAction = null;
						};
						shotUpper.StartShot();
						shotLower.StartShot();
					}
					else
					{
						switchStopGuard.SwitchTick = info.GuardBlendTick;
						// switchMoveGuard.SwitchTick = info.GuardBlendTick;
						switchStopGuard.SetFlag(true);
						switchMoveGuard.SetFlag(true);
					}
				}

				if (switchStopGuard.BlendValue >= FP.One)
				{
					currentState = InfantryState.Guard;
				}

				if (!info.KeepGuardStateWhenMoving || (currentState == InfantryState.Guard && move.CurrentMovementTypes == MovementType.None))
					guardTick++;
			}

			// handle movement
			if (move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
			{
				switchMoveLower.SetFlag(true);
				switchMoveUpper.SetFlag(true);

				if (playingIdleAction)
					shotFullOverride.Interrupt();
			}
			else
			{
				switchMoveLower.SetFlag(false);
				switchMoveUpper.SetFlag(false);

				// idle action
				if (idleActions.Length > 0)
				{
					if (currentState == InfantryState.Idle && self.IsIdle && self.IsInWorld && currentPose == PoseState.Stand && move.CurrentMovementTypes == MovementType.None)
					{
						if (turnOnIdle != null && !playingIdleAction)
							turnOnIdle.HoldTurn = false;

						if (!playingIdleAction && nextIdleActionTick-- <= 0)
						{
							playingIdleAction = true;
							animFullOverride.ChangeAnimation(idleActions[self.World.SharedRandom.Next(0, idleActions.Length - 1)]);
							shotFullOverride.ShotEndAction = () =>
							{
								playingIdleAction = false;
							};
							shotFullOverride.StartShot();
							nextIdleActionTick = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
						}
					}
					else
					{
						if (playingIdleAction)
							shotFullOverride.Interrupt();
						else
						{
							nextIdleActionTick = Math.Max(nextIdleActionTick, info.MinIdleDelay);
						}
					}
				}
				else
				{
					if (turnOnIdle != null &&
						currentState == InfantryState.Idle && self.IsIdle && self.IsInWorld &&
						currentPose == PoseState.Stand && move.CurrentMovementTypes == MovementType.None)
						turnOnIdle.HoldTurn = false;
				}
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
			if (currentPose == PoseState.ProneToStand)
				return 0;

			return currentPose == PoseState.Prone ? info.ProneSpeedModifier : 100;
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

			shotDie.ShotEndAction = () => { deathFade = true; deathFadeTick = info.DeathBodyRemain; };

			shotDie.StartShot();
		}
	}
}

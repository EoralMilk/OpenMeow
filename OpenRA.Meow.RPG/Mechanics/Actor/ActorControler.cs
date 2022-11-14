using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Meow.RPG.Mechanics.Display;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG
{
	public class ActorControlerInfo : PausableConditionalTraitInfo
	{
		[CursorReference]
		[Desc("Cursor to display when targeting a teleport location.")]
		public readonly string TargetCursor = "attack";

		[GrantedConditionReference]
		[Desc("Condition to grant when under control.")]
		public readonly string Condition = "under-control";

		public override object Create(ActorInitializer init) { return new ActorControler(init.Self, this); }

	}

	public class ActorControler : PausableConditionalTrait<ActorControlerInfo>,
		INotifyCreated, IResolveOrder, ITick
	{
		AttackBase attack;
		IFacing facing;
		Turreted[] turreteds;
		IMover mover;
		readonly ActorControlerInfo info;
		readonly Actor self;
		public bool UnderControl;

		Target attackTarget = Target.Invalid;
		WVec moverDir = WVec.Zero;
		int conditionToken = Actor.InvalidConditionToken;

		public string TargetCursor => info.TargetCursor;
		public ActorControler(Actor self , ActorControlerInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;
		}

		protected override void TraitEnabled(Actor self)
		{
			if (conditionToken == Actor.InvalidConditionToken && UnderControl)
				conditionToken = self.GrantCondition(Info.Condition);
		}

		protected override void TraitDisabled(Actor self)
		{
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}

		public void Tick(Actor self)
		{
			if (UnderControl)
			{
				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(Info.Condition);
			}
			else
			{
				if (conditionToken != Actor.InvalidConditionToken)
					conditionToken = self.RevokeCondition(conditionToken);

				moverDir = WVec.Zero;
				attackTarget = Target.Invalid;
				if (attack != null)
				{
					attack.IsAiming = false;
					if (attack is AttackFollow && (attack as AttackFollow).RequestedTarget.Type != TargetType.Invalid && UnderControl)
					{
						(attack as AttackFollow).ClearRequestedTarget(false);
					}
				}
			}

			if (mover != null && moverDir != WVec.Zero)
			{
				Game.GetWorldRenderer()?.Viewport.CenterLerp(self.CenterPosition, 0.6f);
				mover.MoveToward(moverDir);
			}

			if (!CanAttack() || attackTarget.Type == TargetType.Invalid)
			{
				if (attack != null)
				{
					attack.IsAiming = false;
				}

				return;
			}

			if (attack != null)
			{
				attack.IsAiming = true;
				var range = attack.GetMiniArmMaximumRangeVersusTarget(attackTarget).Length;
				var dir = attackTarget.CenterPosition - self.CenterPosition;
				var dist = dir.Length;
				if (range < dist)
				{
					var tPos = self.CenterPosition + ((range - 1) * dir / dist);
					tPos = new WPos(tPos, self.World.Map.HeightOfTerrain(tPos));
					attackTarget = Target.FromPos(tPos);
				}
			}

			// if (turreteds != null && turreteds.Where(tur => !tur.IsTraitDisabled).Any())
			// {
			// 	// use AttackFollow SetRequestedTarget
			// }
			if (attack is AttackFollow)
			{
				(attack as AttackFollow).SetRequestedTarget(attackTarget, true);
			}
			else if (facing != null)
			{
				var desiredFacing = (attackTarget.CenterPosition - self.CenterPosition).Yaw;
				if (desiredFacing + attack.Info.FiringAngle != facing.Facing)
					facing.Facing = Util.TickFacing(facing.Facing, desiredFacing + attack.Info.FiringAngle, facing.TurnSpeed);

				attack.DoAttack(self, attackTarget);
			}
		}

		public bool CanAttack()
		{
			return UnderControl && !IsTraitDisabled && attack != null;
		}

		//public IEnumerable<IOrderTargeter> Orders
		//{
		//	get
		//	{
		//		if (!IsTraitDisabled && UnderControl)
		//			yield return new ControlerAttackOrderTargeter(self, this);
		//	}
		//}

		protected override void Created(Actor self)
		{
			attack = self.TraitOrDefault<AttackBase>();
			facing = self.TraitOrDefault<IFacing>();
			turreteds = self.TraitsImplementing<Turreted>().ToArray();
			mover = self.TraitOrDefault<IMover>();
			base.Created(self);
		}

		//public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		//{
		//	//if (order.OrderID == "Contorler:Attack")
		//	//	return new Order(order.OrderID, self, target, queued);

		//	return null;
		//}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Controler:Enable")
			{
				UnderControl = true;
			}
			else if (order.OrderString == "Controler:Disable")
			{
				UnderControl = false;
			}

			if (order.OrderString == "Controler:Mi1Down" && order.Target.Type != TargetType.Invalid && CanAttack())
			{
				attackTarget = order.Target;
			}

			if (order.OrderString == "Contorler:Mi1Up")
			{
				attackTarget = Target.Invalid;
				if (attack != null)
				{
					attack.IsAiming = false;
					if (attack is AttackFollow && (attack as AttackFollow).RequestedTarget.Type != TargetType.Invalid && UnderControl)
					{
						(attack as AttackFollow).ClearRequestedTarget(false);
					}
				}
			}

			if (order.OrderString == "Mover:Move")
			{
				self.CancelActivity();
				moverDir = new WVec(order.Target.CenterPosition);
			}
			else if (order.OrderString == "Mover:Stop")
			{
				moverDir = WVec.Zero;
			}
		}
	}

	class ControlerAttackOrderTargeter : IOrderTargeter
	{
		readonly Actor self;
		readonly ActorControler actorControler;
		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers)
		{
			return true;
		}

		public ControlerAttackOrderTargeter(Actor self, ActorControler unit)
		{
			this.self = self;
			actorControler = unit;
		}

		public string OrderID => "";
		public int OrderPriority => 7;
		public bool IsQueued => false;

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			if (!actorControler.CanAttack())
				return false;

			if (modifiers == TargetModifiers.None)
			{
				cursor = actorControler.TargetCursor;
				return true;
			}

			return false;
		}
	}

	public class ControlerOrderGenerator : UnitOrderGenerator
	{
		public override bool ClearSelectionOnLeftClick => false;
	}
}

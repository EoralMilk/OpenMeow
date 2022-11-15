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
		AttackBase[] attacks;
		IFacing facing;
		Turreted[] turreteds;
		IMover mover;
		readonly ActorControlerInfo info;
		readonly Actor self;

		// state
		Target attackTarget = Target.Invalid;
		WVec moverDir = WVec.Zero;
		int controlingConditionToken = Actor.InvalidConditionToken;
		public bool UnderControl;

		public string TargetCursor => info.TargetCursor;

		ControlerType controlerType;

		public enum ControlerType
		{
			None,
			Mobile,
			Airborne,
		}

		public ActorControler(Actor self , ActorControlerInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;
		}

		protected override void TraitEnabled(Actor self)
		{
			if (controlingConditionToken == Actor.InvalidConditionToken && UnderControl)
				controlingConditionToken = self.GrantCondition(Info.Condition);
		}

		protected override void TraitDisabled(Actor self)
		{
			if (controlingConditionToken != Actor.InvalidConditionToken)
				controlingConditionToken = self.RevokeCondition(controlingConditionToken);

			ClearTarget();
			moverDir = WVec.Zero;
		}

		public void Tick(Actor self)
		{
			if (UnderControl)
			{
				if (controlingConditionToken == Actor.InvalidConditionToken)
					controlingConditionToken = self.GrantCondition(Info.Condition);
			}
			else
			{
				if (controlingConditionToken != Actor.InvalidConditionToken)
					controlingConditionToken = self.RevokeCondition(controlingConditionToken);

				moverDir = WVec.Zero;
			}

			if (mover != null)
			{
				mover.MoveToward(moverDir);
			}

			if (!CanAttack() || attackTarget.Type == TargetType.Invalid)
			{
				if (attacks != null)
				{
					foreach (var a in attacks)
					{
						a.IsAiming = false;
					}
				}

				return;
			}

			bool turnFacing = false;
			WAngle attackFace = WAngle.Zero;
			int range = 0;

			// determine if we should turn turrets or faceing
			if (attacks != null)
			{
				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					a.IsAiming = true;
					if (!(a is AttackFollow))
					{
						attackFace = a.Info.FiringAngle;
						turnFacing = true;
					}

					range = Math.Max(range, a.GetMaximumRangeVersusTarget(attackTarget).Length);
				}

				// re-calculate target
				var dir = attackTarget.CenterPosition - self.CenterPosition;
				var dist = dir.Length;
				if (range < dist)
				{
					var tPos = self.CenterPosition + ((range - 1) * dir / dist);
					tPos = new WPos(tPos, self.World.Map.HeightOfTerrain(tPos));
					attackTarget = Target.FromPos(tPos);
				}

				if (facing != null && turnFacing)
				{
					var desiredFacing = (attackTarget.CenterPosition - self.CenterPosition).Yaw;
					if (desiredFacing + attackFace != facing.Facing)
						facing.Facing = Util.TickFacing(facing.Facing, desiredFacing + attackFace, facing.TurnSpeed);
				}

				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					if (a is AttackFollow && UnderControl)
					{
						(a as AttackFollow).SetRequestedTarget(attackTarget, true);
					}
					else
					{
						a.DoAttack(self, attackTarget);
					}
				}
			}
		}

		public bool CanAttack()
		{
			return UnderControl && !IsTraitDisabled && attacks != null && attacks.Length > 0;
		}

		void ClearTarget()
		{
			attackTarget = Target.Invalid;
			if (attacks != null)
			{
				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					a.IsAiming = false;
					if (a is AttackFollow && (a as AttackFollow).RequestedTarget.Type != TargetType.Invalid)
					{
						(a as AttackFollow).ClearRequestedTarget(false);
					}
				}
			}
		}

		protected override void Created(Actor self)
		{
			attacks = self.TraitsImplementing<AttackBase>().ToArray();
			facing = self.TraitOrDefault<IFacing>();
			turreteds = self.TraitsImplementing<Turreted>().ToArray();
			mover = self.TraitOrDefault<IMover>();
			if (mover != null && (mover is Mobile))
				controlerType = ControlerType.Mobile;

			base.Created(self);
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Controler:Enable")
			{
				UnderControl = true;
			}
			else if (order.OrderString == "Controler:Disable")
			{
				UnderControl = false;
				ClearTarget();
			}

			if (order.OrderString == "Controler:Mi1Down" && order.Target.Type != TargetType.Invalid && CanAttack())
			{
				attackTarget = order.Target;
			}

			if (order.OrderString == "Contorler:Mi1Up")
			{
				ClearTarget();
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
}

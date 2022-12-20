using System;
using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.Activities;
using System.Reflection;
using OpenRA.Mods.Common.Orders;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using System.Linq;
using OpenRA.Primitives;
using Util = OpenRA.Mods.Common.Util;

namespace OpenRA.Meow.RPG.Mechanics
{
	public interface INotifyPickUpItem
	{
		bool PrepareForPickUpItem();
		bool Picking();
		void OnPickUpItem(Actor item);
	}

	public class PickUpItemInfo : PausableConditionalTraitInfo, Requires<IMoveInfo>
	{
		[Desc("Can the unit teleport only a certain distance?")]
		public readonly bool HasDistanceLimit = true;

		[Desc("The maximum distance in cells this unit can teleport (only used if HasDistanceLimit = true).")]
		public readonly int MaxDistance = 2;

		[CursorReference]
		[Desc("Cursor to display when targeting a teleport location.")]
		public readonly string TargetCursor = "ability";

		[Desc("Color to use for the target line.")]
		public readonly Color TargetLineColor = Color.Cyan;

		[VoiceReference]
		public readonly string Voice = "Action";

		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = "Picking";

		[Desc("Is the condition irrevocable once it has been activated?")]
		public readonly bool GrantPermanently = false;

		public override object Create(ActorInitializer init) { return new PickUpItem(init.Self, this); }
	}

	public class PickUpItem : PausableConditionalTrait<PickUpItemInfo>, IIssueOrder, IResolveOrder, IOrderVoice, ISync
	{
		readonly PickUpItemInfo info;
		readonly Actor self;
		readonly IMove move;

		public bool CanAct => !IsTraitDisabled && !IsTraitPaused;

		public PickUpItem(Actor self, PickUpItemInfo info)
			: base(info)
		{
			this.info = info;
			this.self = self;
			move = self.Trait<IMove>();
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				if (IsTraitDisabled)
					yield break;

				yield return new PickUpItemOrderTargeter(Info);
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			if (order.OrderID == "PickUpItem")
				return new Order(order.OrderID, self, target, queued);

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "PickUpItem" && order.Target.Type == TargetType.Actor)
			{
				var maxDistance = Info.HasDistanceLimit ? Info.MaxDistance : (int?)null;
				if (!order.Queued)
					self.CancelActivity();

				if (maxDistance != null)
					self.QueueActivity(move.MoveWithinRange(order.Target, WDist.FromCells(maxDistance.Value), targetLineColor: Info.TargetLineColor));
				self.QueueActivity(new PickUp(self, info, order.Target.Actor));
				self.ShowTargetLines();
			}
		}

		public bool FindItemToPick(Actor self, string itemType, WDist range)
		{
			var targetsInRange = self.World.FindActorsInCircle(self.CenterPosition, range)
				.Where(a => {
					var item = a.TraitOrDefault<Item>();
					return item != null && item.Type == itemType;
				});

			if (!targetsInRange.Any())
				return false;

			Actor target = targetsInRange.First();
			if (targetsInRange.Count() > 1)
			{
				var dist = (target.CenterPosition - self.CenterPosition).LengthSquared;
				foreach (var a in targetsInRange)
				{
					var cdist = (a.CenterPosition - self.CenterPosition).LengthSquared;
					if (cdist < dist)
					{
						target = a;
						dist = cdist;
					}
				}
			}

			self.CancelActivity();
			var maxDistance = Info.HasDistanceLimit ? Info.MaxDistance : (int?)null;
			if (maxDistance != null)
				self.QueueActivity(move.MoveWithinRange(Target.FromActor(target), WDist.FromCells(maxDistance.Value), targetLineColor: Info.TargetLineColor));
			self.QueueActivity(new PickUp(self, info, target));
			self.ShowTargetLines();
			return true;
		}

		public void PickUp(Actor target)
		{
			self.CancelActivity();
			var maxDistance = Info.HasDistanceLimit ? Info.MaxDistance : (int?)null;
			if (maxDistance != null)
				self.QueueActivity(move.MoveWithinRange(Target.FromActor(target), WDist.FromCells(maxDistance.Value), targetLineColor: Info.TargetLineColor));
			self.QueueActivity(new PickUp(self, info, target));
			self.ShowTargetLines();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "PickUpItem" ? Info.Voice : null;
		}
	}

	public class PickUp : Activity
	{
		readonly IFacing facing;
		readonly PickUpItem pickUpItem;
		readonly PickUpItemInfo info;

		readonly Actor target;

		readonly INotifyPickUpItem[] notifyPicking;

		int conditionToken = Actor.InvalidConditionToken;

		bool started = false;

		public PickUp(Actor self, PickUpItemInfo info, Actor topickup)
		{
			ActivityType = ActivityType.Move;
			this.info = info;
			facing = self.Trait<IFacing>();
			pickUpItem = self.Trait<PickUpItem>();
			target = topickup;
			notifyPicking = self.TraitsImplementing<INotifyPickUpItem>().ToArray();
		}

		public override bool Tick(Actor self)
		{
			var desiredFacing = (target.CenterPosition - self.CenterPosition).Yaw;
			if (!target.IsInWorld || target.IsDead)
				Cancel(self, true);

			if (!started)
			{
				if (IsCanceling)
					return true;

				if (desiredFacing != facing.Facing)
				{
					facing.Facing = Util.TickFacing(facing.Facing, desiredFacing, facing.TurnSpeed);
				}

				if (notifyPicking != null &&  notifyPicking.Length > 0)
				{
					bool ready = true;
					foreach (var notify in notifyPicking)
					{
						if (!notify.PrepareForPickUpItem())
							ready = false;
					}

					if (ready == false)
						return false;
				}

				if (pickUpItem != null && !pickUpItem.CanAct)
					return false;

				started = true;

				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(info.Condition);
			}

			var facingToTarget = true;
			facing.Facing = Util.TickFacing(facing.Facing, desiredFacing, facing.TurnSpeed);
			if (desiredFacing != facing.Facing)
			{
				facingToTarget = false;
			}

			if (notifyPicking != null && notifyPicking.Length > 0)
			{
				bool picked = true;
				foreach (var notify in notifyPicking)
				{
					if (!notify.Picking())
						picked = false;
				}

				if (picked == false)
					return false;
			}

			if (IsCanceling)
			{
				if (notifyPicking != null && notifyPicking.Length > 0)
				{
					foreach (var notify in notifyPicking)
					{
						notify.OnPickUpItem(target);
					}
				}

				return true;
			}

			if (facingToTarget == false)
				return false;

			if (notifyPicking != null && notifyPicking.Length > 0)
			{
				foreach (var notify in notifyPicking)
				{
					notify.OnPickUpItem(target);
				}
			}

			if (info.GrantPermanently || conditionToken == Actor.InvalidConditionToken)
				return true;
			else
				conditionToken = self.RevokeCondition(conditionToken);

			return true;
		}
	}

	class PickUpItemOrderTargeter : IOrderTargeter
	{
		readonly PickUpItemInfo info;

		public PickUpItemOrderTargeter(PickUpItemInfo info)
		{
			this.info = info;
		}

		public string OrderID => "PickUpItem";
		public int OrderPriority => 7;
		public bool IsQueued { get; protected set; }
		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers) { return true; }

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			if (modifiers.HasModifier(TargetModifiers.ForceAttack) ||
				modifiers.HasModifier(TargetModifiers.ForceMove) ||
				target.Type != TargetType.Actor ||
				target.Actor == null || target.Actor.IsDead || !target.Actor.IsInWorld || target.Actor == self ||
				target.Actor.TraitOrDefault<Item>() == null)
				return false;

			var xy = self.World.Map.CellContaining(target.CenterPosition);
			IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);
			if (self.IsInWorld && self.Owner.Shroud.IsExplored(xy))
			{
				cursor = info.TargetCursor;
				return true;
			}

			return false;
		}
	}

}

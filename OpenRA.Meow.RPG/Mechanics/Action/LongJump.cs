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
	public interface INotifyLongJump
	{
		bool PrepareForLongJump();
		void OnStartJump();
		void OnLand();

	}

	public class LongJumpSkillInfo : PausableConditionalTraitInfo, IRulesetLoaded, Requires<IMoveInfo>
	{
		[Desc("Cooldown in ticks until the unit can teleport.")]
		public readonly int ChargeDelay = 500;

		[Desc("Can the unit teleport only a certain distance?")]
		public readonly bool HasDistanceLimit = true;

		[Desc("The maximum distance in cells this unit can teleport (only used if HasDistanceLimit = true).")]
		public readonly int MaxDistance = 12;

		[CursorReference]
		[Desc("Cursor to display when targeting a teleport location.")]
		public readonly string TargetCursor = "attack";

		[CursorReference]
		[Desc("Cursor to display when the targeted location is blocked.")]
		public readonly string TargetBlockedCursor = "move-blocked";

		[Desc("Color to use for the target line.")]
		public readonly Color TargetLineColor = Color.LawnGreen;

		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Range circle color.")]
		public readonly Color CircleColor = Color.FromArgb(128, Color.LawnGreen);

		[Desc("Range circle line width.")]
		public readonly float CircleWidth = 1;

		[Desc("Range circle border color.")]
		public readonly Color CircleBorderColor = Color.FromArgb(96, Color.Black);

		[Desc("Range circle border width.")]
		public readonly float CircleBorderWidth = 3;

		[WeaponReference]
		[Desc("Explosion weapon that triggers when start jump.")]
		public readonly string TakeOffWeapon = null;

		[WeaponReference]
		[Desc("Explosion weapon that triggers when hitting ground.")]
		public readonly string LandWeapon = null;

		public readonly WDist Speed = new WDist(125);

		public readonly WAngle JumpAngle = new WAngle(128);

		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = "Jumping";

		[Desc("Is the condition irrevocable once it has been activated?")]
		public readonly bool GrantPermanently = false;

		public WeaponInfo JumpWeapon { get; private set; }
		public WeaponInfo LandingWeapon { get; private set; }

		public override object Create(ActorInitializer init) { return new LongJumpSkill(init.Self, this); }
		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (!string.IsNullOrEmpty(TakeOffWeapon))
			{
				var weaponToLower = TakeOffWeapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

				JumpWeapon = weapon;
			}

			if (!string.IsNullOrEmpty(LandWeapon))
			{
				var weaponToLower = LandWeapon.ToLowerInvariant();
				if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
					throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(weaponToLower));

				LandingWeapon = weapon;
			}
		}
	}

	public class LongJumpSkill : PausableConditionalTrait<LongJumpSkillInfo>, IIssueOrder, IResolveOrder, ITick, ISelectionBar, IOrderVoice, ISync
	{
		readonly LongJumpSkillInfo info;
		readonly Actor self;
		readonly IMove move;
		[Sync]
		int chargeTick = 0;

		public bool CanAct => !IsTraitDisabled && !IsTraitPaused && chargeTick <= 0;

		public LongJumpSkill(Actor self, LongJumpSkillInfo info)
			: base(info)
		{
			this.info = info;
			this.self = self;
			move = self.Trait<IMove>();
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			if (chargeTick > 0)
				chargeTick--;
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				if (IsTraitDisabled)
					yield break;

				yield return new LongJumpOrderTargeter(Info);
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			if (order.OrderID == "LongJump")
				return new Order(order.OrderID, self, target, queued);

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "LongJump" && order.Target.Type != TargetType.Invalid)
			{
				var maxDistance = Info.HasDistanceLimit ? Info.MaxDistance : (int?)null;
				if (!order.Queued)
					self.CancelActivity();

				var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
				if (maxDistance != null)
					self.QueueActivity(move.MoveWithinRange(order.Target, WDist.FromCells(maxDistance.Value), targetLineColor: Info.TargetLineColor));
				self.QueueActivity(new JumpTo(self, info, order.Target.CenterPosition));
				self.QueueActivity(new FallDown(self, WPos.Zero, info.Speed.Length));
				self.ShowTargetLines();
			}
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == "LongJump" ? Info.Voice : null;
		}

		public void ResetChargeTime()
		{
			chargeTick = Info.ChargeDelay;
		}

		float ISelectionBar.GetValue()
		{
			if (IsTraitDisabled)
				return 0f;

			return (float)(Info.ChargeDelay - chargeTick) / Info.ChargeDelay;
		}

		Color ISelectionBar.GetColor() { return Color.Magenta; }
		bool ISelectionBar.DisplayWhenEmpty => false;

		protected override void TraitDisabled(Actor self)
		{
			ResetChargeTime();
		}

		public void JumpTo(Actor self, WPos target)
		{
			self.QueueActivity(false, new JumpTo(self, info, target));
		}

		public void JumpToAndAction(Actor self, WPos target, Action after)
		{
			self.QueueActivity(false, new JumpTo(self, info, target, after));
		}

	}

	public class JumpTo : Activity
	{
		readonly Mobile mobile;
		readonly IFacing facing;
		readonly LongJumpSkill jumping;
		readonly LongJumpSkillInfo info;

		WPos target;
		readonly WAngle angle;
		readonly WDist speed;
		CPos? targetCell;

		int length;

		readonly INotifyLongJump[] notifyLongJumps;

		WPos sourcePos;
		int conditionToken = Actor.InvalidConditionToken;

		readonly Action doAfterJump;
		readonly int? maximumDistance;

		bool started = false;

		static HashSet<CPos> reserved = new HashSet<CPos>();

		[Sync]
		int ticks;

		public JumpTo(Actor self, LongJumpSkillInfo info, WPos t, Action after = null)
		{
			ActivityType = ActivityType.Move;
			this.info = info;
			IsInterruptible = false;

			mobile = self.TraitOrDefault<Mobile>();
			facing = self.Trait<IFacing>();
			jumping = self.Trait<LongJumpSkill>();

			target = t;
			angle = info.JumpAngle;
			speed = info.Speed;
			sourcePos = self.CenterPosition;
			ticks = 0;
			doAfterJump = after;

			maximumDistance = 8;

			notifyLongJumps = self.TraitsImplementing<INotifyLongJump>().ToArray();
		}

		public override bool Tick(Actor self)
		{
			if (self.IsDead || !self.IsInWorld)
			{
				if (targetCell != null)
					reserved.Remove(targetCell.Value);
				return true;
			}

			if (!started)
			{
				if (IsCanceling)
					return true;

				if (mobile != null && (mobile.IsTraitDisabled || mobile.IsTraitPaused))
					return false;

				if (jumping != null && !jumping.CanAct)
					return false;

				var h = self.World.Map.HeightOfTerrain(target);

				// to ground
				if (h >= target.Z - 512 && targetCell == null)
				{
					targetCell = ChooseBestDestinationCell(self, self.World.Map.CellContaining(target));

					if (targetCell != null)
						target = self.World.Map.CenterOfCell(targetCell.Value);
				}

				var desiredFacing = (target - self.CenterPosition).Yaw;

				if (desiredFacing != facing.Facing)
				{
					facing.Facing = Util.TickFacing(facing.Facing, desiredFacing, facing.TurnSpeed);
					return false;
				}

				if (notifyLongJumps != null &&  notifyLongJumps.Length > 0)
				{
					bool ready = true;
					foreach (var notify in notifyLongJumps)
					{
						if (!notify.PrepareForLongJump())
							ready = false;
					}

					if (ready == false)
						return false;
				}

				if (notifyLongJumps != null && notifyLongJumps.Length > 0)
				{
					foreach (var notify in notifyLongJumps)
					{
						notify.OnStartJump();
					}
				}

				started = true;

				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(info.Condition);

				sourcePos = self.CenterPosition;
				length = Math.Max((target - sourcePos).Length / speed.Length, 1);
				jumping.ResetChargeTime();
				if (info.JumpWeapon != null)
				{
					info.JumpWeapon.Impact(Target.FromPos(self.CenterPosition), self);
				}
			}

			mobile.SetPosition(self, WPos.LerpQuadratic(sourcePos, target, angle, ticks, length), true);
			mobile.Facing = (target - self.CenterPosition).Yaw;

			ticks++;

			if (ticks >= length)
			{
				doAfterJump?.Invoke();
				mobile.SetPosition(self, target, true);
				if (targetCell != null)
					reserved.Remove(targetCell.Value);
				if (notifyLongJumps != null && notifyLongJumps.Length > 0)
				{
					foreach (var notify in notifyLongJumps)
					{
						notify.OnLand();
					}
				}

				if (info.LandingWeapon != null)
				{
					// Use .FromPos since this actor is killed. Cannot use Target.FromActor
					info.LandingWeapon.Impact(Target.FromPos(self.CenterPosition), self);
				}

				if (info.GrantPermanently || conditionToken == Actor.InvalidConditionToken)
					return true;
				else
					conditionToken = self.RevokeCondition(conditionToken);

				return true;
			}

			return false;
		}

		CPos? ChooseBestDestinationCell(Actor self, CPos destination)
		{
			if (mobile == null)
				return null;

			if (mobile.CanEnterCell(destination) && self.Owner.Shroud.IsExplored(destination) && !reserved.Contains(destination))
			{
				reserved.Add(destination);
				return destination;
			}

			var max = maximumDistance != null ? maximumDistance.Value : self.World.Map.Grid.MaximumTileSearchRange;
			foreach (var tile in self.World.Map.FindTilesInCircle(destination, max))
			{
				if (self.Owner.Shroud.IsExplored(tile)
					&& !reserved.Contains(tile)
					&& mobile.CanEnterCell(tile))
				{
					reserved.Add(tile);
					return tile;
				}
			}

			return null;
		}
	}

	class LongJumpOrderTargeter : IOrderTargeter
	{
		readonly LongJumpSkillInfo info;

		public LongJumpOrderTargeter(LongJumpSkillInfo info)
		{
			this.info = info;
		}

		public string OrderID => "LongJump";
		public int OrderPriority => 5;
		public bool IsQueued { get; protected set; }
		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers) { return true; }

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			if (modifiers.HasModifier(TargetModifiers.ForceMove))
			{
				var xy = self.World.Map.CellContaining(target.CenterPosition);

				IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);
				var positionable = self.Info.TraitInfo<IPositionableInfo>();
				if (self.IsInWorld && self.Owner.Shroud.IsExplored(xy) &&
					positionable.CanEnterCell(self.World, null, xy, check: BlockedByActor.None))
				{
					cursor = info.TargetCursor;
					return true;
				}

				//cursor = info.TargetBlockedCursor;
				return false;
			}

			return false;
		}
	}

	class LongJumpOrderGenerator : OrderGenerator
	{
		readonly Actor self;
		readonly LongJumpSkill longJumpSkill;
		readonly LongJumpSkillInfo info;

		public LongJumpOrderGenerator(Actor self, LongJumpSkill longJumpSkill)
		{
			this.self = self;
			this.longJumpSkill = longJumpSkill;
			info = longJumpSkill.Info;
		}

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == Game.Settings.Game.MouseButtonPreference.Cancel)
			{
				world.CancelInputMode();
				yield break;
			}

			if (self.IsInWorld && self.Location != cell
				&& self.Trait<LongJumpSkill>().CanAct && self.Owner.Shroud.IsExplored(cell))
			{
				world.CancelInputMode();
				yield return new Order("LongJump", self, Target.FromCell(world, cell), mi.Modifiers.HasModifier(Modifiers.Shift));
			}
		}

		protected override void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			if (!selected.Contains(self))
				world.CancelInputMode();
		}

		protected override void Tick(World world)
		{
			if (longJumpSkill.IsTraitDisabled || longJumpSkill.IsTraitPaused)
			{
				world.CancelInputMode();
				return;
			}
		}

		protected override IEnumerable<IRenderable> Render(WorldRenderer wr, World world) { yield break; }

		protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
		{
			if (!self.IsInWorld || self.Owner != self.World.LocalPlayer)
				yield break;

			if (!info.HasDistanceLimit)
				yield break;

			yield return new RangeCircleAnnotationRenderable(
				self.CenterPosition,
				WDist.FromCells(info.MaxDistance),
				1024,
				info.CircleColor,
				info.CircleWidth,
				info.CircleBorderColor,
				info.CircleBorderWidth);
		}

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var positionable = self.Info.TraitInfo<IPositionableInfo>();

			if (self.IsInWorld && self.Location != cell
				&& longJumpSkill.CanAct && self.Owner.Shroud.IsExplored(cell) &&
				positionable.CanEnterCell(self.World, null, cell))
				return info.TargetCursor;
			else
				return info.TargetBlockedCursor;
		}
	}
}

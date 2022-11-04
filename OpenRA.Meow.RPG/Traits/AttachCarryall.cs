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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Traits
{
	public interface ICarryingPositionModifier
	{
		WVec WorldOffset { get; }
	}

	[Desc("Transports actors with the `" + nameof(AttachCarryable) + "` trait.")]
	public class AttachCarryallInfo : TraitInfo, Requires<BodyOrientationInfo>, Requires<AircraftInfo>
	{
		[ActorReference(typeof(AttachCarryableInfo))]
		[Desc("Actor type that is initially spawned into this actor.")]
		public readonly string InitialActor = null;

		[Desc("Delay (in ticks) on the ground while attaching an actor to the carryall.")]
		public readonly int BeforeLoadDelay = 0;

		[Desc("Delay (in ticks) on the ground while detaching an actor from the carryall.")]
		public readonly int BeforeUnloadDelay = 0;

		[Desc("AttachCarryable attachment point relative to body.")]
		public readonly WVec LocalOffset = WVec.Zero;

		[Desc("Radius around the target drop location that are considered if the target tile is blocked.")]
		public readonly WDist DropRange = WDist.FromCells(5);

		[CursorReference]
		[Desc("Cursor to display when able to unload the passengers.")]
		public readonly string UnloadCursor = "deploy";

		[CursorReference]
		[Desc("Cursor to display when unable to unload the passengers.")]
		public readonly string UnloadBlockedCursor = "deploy-blocked";

		[Desc("Allow moving and unloading with one order using force-move")]
		public readonly bool AllowDropOff = false;

		[CursorReference]
		[Desc("Cursor to display when able to drop off the passengers at location.")]
		public readonly string DropOffCursor = "ability";

		[CursorReference]
		[Desc("Cursor to display when unable to drop off the passengers at location.")]
		public readonly string DropOffBlockedCursor = "move-blocked";

		[CursorReference]
		[Desc("Cursor to display when picking up the passengers.")]
		public readonly string PickUpCursor = "ability";

		[GrantedConditionReference]
		[Desc("Condition to grant to the AttachCarryall while it is carrying something.")]
		public readonly string CarryCondition = null;

		[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
		[Desc("Conditions to grant when a specified actor is being carried.",
			"A dictionary of [actor name]: [condition].")]
		public readonly Dictionary<string, string> AttachCarryableConditions = new Dictionary<string, string>();

		public readonly bool AttachCarryableAnyCamp = false;

		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Color to use for the target line.")]
		public readonly Color TargetLineColor = Color.Yellow;

		[GrantedConditionReference]
		public IEnumerable<string> LinterAttachCarryableConditions => AttachCarryableConditions.Values;

		public override object Create(ActorInitializer init) { return new AttachCarryall(init.Self, this); }
	}

	public class AttachCarryall : INotifyKilled, ISync, ITick, IRender, INotifyActorDisposing, IIssueOrder, IResolveOrder,
		IOrderVoice, IIssueDeployOrder, IAircraftCenterPositionOffset, IOverrideAircraftLanding
	{
		public enum AttachCarryallState
		{
			Idle,
			Reserved,
			Carrying
		}

		public readonly AttachCarryallInfo Info;
		protected readonly AircraftInfo AircraftInfo;
		readonly Aircraft aircraft;
		readonly BodyOrientation body;
		readonly IFacing facing;
		readonly Actor self;
		readonly ICarryingPositionModifier[] carryingPositionModifiers;

		// The actor we are currently carrying.
		[Sync]
		public Actor AttachCarryable { get; protected set; }
		public AttachCarryallState State { get; protected set; }

		WAngle cachedFacing;
		IActorPreview[] carryablePreview;
		HashSet<string> landableTerrainTypes;
		int carryConditionToken = Actor.InvalidConditionToken;
		int carryableConditionToken = Actor.InvalidConditionToken;

		/// <summary>Offset between the carryall's and the carried actor's CenterPositions</summary>
		public WVec AttachCarryableOffset { get; private set; }

		public AttachCarryall(Actor self, AttachCarryallInfo info)
		{
			Info = info;

			AttachCarryable = null;
			State = AttachCarryallState.Idle;

			AircraftInfo = self.Info.TraitInfoOrDefault<AircraftInfo>();
			aircraft = self.Trait<Aircraft>();
			body = self.Trait<BodyOrientation>();
			facing = self.Trait<IFacing>();
			this.self = self;

			carryingPositionModifiers = self.TraitsImplementing<ICarryingPositionModifier>().ToArray();

			if (!string.IsNullOrEmpty(info.InitialActor))
			{
				var unit = self.World.CreateActor(false, info.InitialActor.ToLowerInvariant(), new TypeDictionary
				{
					new ParentActorInit(self),
					new OwnerInit(self.Owner)
				});

				unit.Trait<AttachCarryable>().Attached();
				AttachAttachCarryable(self, unit);
			}
		}

		void ITick.Tick(Actor self)
		{
			// Cargo may be killed in the same tick as, but after they are attached
			if (AttachCarryable != null && AttachCarryable.IsDead)
				DetachAttachCarryable(self);
			else if (State == AttachCarryallState.Carrying && AttachCarryable != null && !AttachCarryable.IsDead && AttachCarryable.IsInWorld)
			{
				var offset = AttachCarryableOffset.Rotate(body.QuantizeOrientation(self.Orientation));
				if (carryingPositionModifiers != null && carryingPositionModifiers.Length > 0)
				{
					foreach (var m in carryingPositionModifiers)
					{
						offset += m.WorldOffset;
					}
				}

				var carryable = AttachCarryable.TraitsImplementing<AttachCarryable>().FirstEnabledConditionalTraitOrDefault();
				carryable.Mobile.SetPosition(AttachCarryable, self.CenterPosition + offset, true);
				carryable.Mobile.Orientation = self.Orientation;
			}

			// HACK: We don't have an efficient way to know when the preview
			// bounds change, so assume that we need to update the screen map
			// (only) when the facing changes
			if (facing.Facing != cachedFacing && carryablePreview != null)
			{
				self.World.ScreenMap.AddOrUpdate(self);
				cachedFacing = facing.Facing;
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (State == AttachCarryallState.Carrying)
			{
				AttachCarryable.Dispose();
				AttachCarryable = null;
			}

			UnreserveAttachCarryable(self);
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (State == AttachCarryallState.Carrying)
			{
				if (!AttachCarryable.IsDead)
				{
					// var positionable = AttachCarryable.Trait<IPositionable>();
					// positionable.SetPosition(AttachCarryable, self.Location);
					// AttachCarryable.Kill(e.Attacker);
					var actor = AttachCarryable;
					var carryable = actor.TraitsImplementing<AttachCarryable>().FirstEnabledConditionalTraitOrDefault();
					DetachAttachCarryable(self);
					carryable.UnReserve();
					carryable.Detached();
					actor.CancelActivity();
					actor.QueueActivity(new AttachedFallDown(actor, actor.CenterPosition, carryable.Info));
				}

				AttachCarryable = null;
			}

			UnreserveAttachCarryable(self);
		}

		public virtual WVec OffsetForAttachCarryable(Actor self, Actor carryable)
		{
			return Info.LocalOffset - carryable.Info.TraitInfo<AttachCarryableInfo>().LocalOffset;
		}

		WVec IAircraftCenterPositionOffset.PositionOffset
		{
			get
			{
				var localOffset = AttachCarryableOffset.Rotate(body.QuantizeOrientation(self.Orientation));
				return body.LocalToWorld(localOffset);
			}
		}

		HashSet<string> IOverrideAircraftLanding.LandableTerrainTypes => landableTerrainTypes ?? aircraft.Info.LandableTerrainTypes;

		public virtual bool AttachAttachCarryable(Actor self, Actor carryable)
		{
			if (State == AttachCarryallState.Carrying)
				return false;

			AttachCarryable = carryable;
			State = AttachCarryallState.Carrying;
			self.World.ScreenMap.AddOrUpdate(self);
			if (carryConditionToken == Actor.InvalidConditionToken)
				carryConditionToken = self.GrantCondition(Info.CarryCondition);

			if (Info.AttachCarryableConditions.TryGetValue(carryable.Info.Name, out var carryableCondition))
				carryableConditionToken = self.GrantCondition(carryableCondition);

			AttachCarryableOffset = OffsetForAttachCarryable(self, carryable);
			landableTerrainTypes = AttachCarryable.Trait<Mobile>().Info.LocomotorInfo.TerrainSpeeds.Keys.ToHashSet();

			return true;
		}

		public virtual void DetachAttachCarryable(Actor self)
		{
			UnreserveAttachCarryable(self);
			self.World.ScreenMap.AddOrUpdate(self);
			if (carryConditionToken != Actor.InvalidConditionToken)
				carryConditionToken = self.RevokeCondition(carryConditionToken);

			if (carryableConditionToken != Actor.InvalidConditionToken)
				carryableConditionToken = self.RevokeCondition(carryableConditionToken);

			carryablePreview = null;
			landableTerrainTypes = null;
			AttachCarryableOffset = WVec.Zero;
		}

		public virtual bool ReserveAttachCarryable(Actor self, Actor carryable)
		{
			if (State == AttachCarryallState.Reserved)
				UnreserveAttachCarryable(self);

			if (State != AttachCarryallState.Idle || !carryable.Trait<AttachCarryable>().Reserve(self))
				return false;

			AttachCarryable = carryable;
			State = AttachCarryallState.Reserved;
			return true;
		}

		public virtual void UnreserveAttachCarryable(Actor self)
		{
			if (AttachCarryable != null && AttachCarryable.IsInWorld && !AttachCarryable.IsDead)
				AttachCarryable.Trait<AttachCarryable>().UnReserve();

			AttachCarryable = null;
			State = AttachCarryallState.Idle;
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			yield break;

			/*
			if (State == AttachCarryallState.Carrying && !AttachCarryable.IsDead)
			{
				if (carryablePreview == null)
				{
					var carryableInits = new TypeDictionary()
					{
						new OwnerInit(AttachCarryable.Owner),
						new DynamicFacingInit(() => facing.Facing),
					};

					foreach (var api in AttachCarryable.TraitsImplementing<IActorPreviewInitModifier>())
						api.ModifyActorPreviewInit(AttachCarryable, carryableInits);

					var init = new ActorPreviewInitializer(AttachCarryable.Info, wr, carryableInits);
					carryablePreview = AttachCarryable.Info.TraitInfos<IRenderActorPreviewInfo>()
						.SelectMany(rpi => rpi.RenderPreview(init))
						.ToArray();
				}

				var offset = body.LocalToWorld(AttachCarryableOffset.Rotate(body.QuantizeOrientation(self.Orientation)));
				var previewRenderables = carryablePreview
					.SelectMany(p => p.Render(wr, self.CenterPosition + offset))
					.OrderBy(WorldRenderer.RenderableZPositionComparisonKey);

				foreach (var r in previewRenderables)
					yield return r;
			}
			*/
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			if (carryablePreview == null)
				yield break;

			var pos = self.CenterPosition;
			foreach (var p in carryablePreview)
				foreach (var b in p.ScreenBounds(wr, pos))
					yield return b;
		}

		// Check if we can drop the unit at our current location.
		public bool CanUnload()
		{
			var targetCell = self.World.Map.CellContaining(aircraft.GetPosition());
			return AttachCarryable != null && aircraft.CanLand(targetCell, blockedByMobile: false);
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				yield return new AttachCarryallPickupOrderTargeter(Info);
				yield return new DeployOrderTargeter("Unload", 10,
				() => CanUnload() ? Info.UnloadCursor : Info.UnloadBlockedCursor);
				yield return new AttachCarryallDeliverAttachedUnitTargeter(AircraftInfo, Info);
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			if (order.OrderID == "PickupAttachedUnit" || order.OrderID == "DeliverAttachedUnit" || order.OrderID == "Unload")
				return new Order(order.OrderID, self, target, queued);

			return null;
		}

		Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
		{
			return new Order("Unload", self, queued);
		}

		bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return true; }

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "DeliverAttachedUnit")
			{
				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));
				if (!AircraftInfo.MoveIntoShroud && !self.Owner.Shroud.IsExplored(cell))
					return;

				self.QueueActivity(order.Queued, new DeliverAttachedUnit(self, order.Target, Info.DropRange, Info.TargetLineColor));
				self.ShowTargetLines();
			}
			else if (order.OrderString == "Unload")
			{
				if (!order.Queued && !CanUnload())
					return;

				self.QueueActivity(order.Queued, new DeliverAttachedUnit(self, Info.DropRange, Info.TargetLineColor));
			}
			else if (order.OrderString == "PickupAttachedUnit")
			{
				if (order.Target.Type != TargetType.Actor)
					return;

				self.QueueActivity(order.Queued, new PickupAttachedUnit(self, order.Target.Actor, Info.BeforeLoadDelay, Info.TargetLineColor));
				self.ShowTargetLines();
			}
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case "DeliverAttachedUnit":
				case "Unload":
				case "PickupAttachedUnit":
					return Info.Voice;
				default:
					return null;
			}
		}

		class AttachCarryallPickupOrderTargeter : UnitOrderTargeter
		{
			public AttachCarryallPickupOrderTargeter(AttachCarryallInfo info)
				: base("PickupAttachedUnit", 5, info.PickUpCursor, false, true)
			{
			}

			static bool CanTarget(Actor self, Actor target)
			{
				if (target == null)
					return false;
				var carryall = self.TraitOrDefault<AttachCarryall>();
				if (!carryall.Info.AttachCarryableAnyCamp && !target.AppearsFriendlyTo(self))
					return false;

				var carryable = target.TraitOrDefault<AttachCarryable>();
				if (carryable == null || carryable.IsTraitDisabled)
					return false;

				if (carryable.Reserved && carryable.Carrier != self)
					return false;

				return true;
			}

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return CanTarget(self, target);
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return CanTarget(self, target.Actor);
			}
		}

		class AttachCarryallDeliverAttachedUnitTargeter : IOrderTargeter
		{
			readonly AircraftInfo aircraftInfo;
			readonly AttachCarryallInfo info;

			public string OrderID => "DeliverAttachedUnit";
			public int OrderPriority => 6;
			public bool IsQueued { get; protected set; }
			public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers) { return true; }

			public AttachCarryallDeliverAttachedUnitTargeter(AircraftInfo aircraftInfo, AttachCarryallInfo info)
			{
				this.aircraftInfo = aircraftInfo;
				this.info = info;
			}

			public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
			{
				if (!info.AllowDropOff || !modifiers.HasModifier(TargetModifiers.ForceMove))
					return false;

				var type = target.Type;
				if ((type == TargetType.Actor && target.Actor.Info.HasTraitInfo<BuildingInfo>())
					|| (target.Type == TargetType.FrozenActor && target.FrozenActor.Info.HasTraitInfo<BuildingInfo>()))
				{
					cursor = info.DropOffBlockedCursor;
					return true;
				}

				var location = self.World.Map.CellContaining(target.CenterPosition);
				var explored = self.Owner.Shroud.IsExplored(location);
				cursor = self.World.Map.Contains(location) ? info.DropOffCursor : info.DropOffBlockedCursor;

				IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

				if (!explored && !aircraftInfo.MoveIntoShroud)
					cursor = info.DropOffBlockedCursor;

				return true;
			}
		}
	}
}

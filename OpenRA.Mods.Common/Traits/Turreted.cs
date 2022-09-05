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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class TurretedInfo : PausableConditionalTraitInfo, Requires<BodyOrientationInfo>, IActorPreviewInitInfo, IEditorActorOptions
	{
		public readonly string Turret = "primary";

		[Desc("Speed at which the turret turns.")]
		public readonly WAngle TurnSpeed = new WAngle(512);

		public readonly WAngle InitialFacing = WAngle.Zero;

		[Desc("Number of ticks before turret is realigned. (-1 turns off realignment)")]
		public readonly int RealignDelay = 40;

		[Desc("Turret is realign when disable")]
		public readonly bool RealignWhenDisable = false;

		[Desc("Speed at which the turret 'Realign' when disable.")]
		public readonly WAngle DisableRealignSpeed = new WAngle(512);

		[Desc("Muzzle position relative to turret or body. (forward, right, up) triples")]
		public readonly WVec Offset = WVec.Zero;

		[Desc("Display order for the turret facing slider in the map editor")]
		public readonly int EditorTurretFacingDisplayOrder = 4;

		[Desc("Realign turret before deploy")]
		public readonly bool RealignBeforeDeploy = false;

		public readonly bool Floating = false;
		IEnumerable<ActorInit> IActorPreviewInitInfo.ActorPreviewInits(ActorInfo ai, ActorPreviewType type)
		{
			yield return new TurretFacingInit(this, InitialFacing);
		}

		IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, World world)
		{
			yield return new EditorActorSlider("Turret", EditorTurretFacingDisplayOrder, 0, 1023, 8,
				actor =>
				{
					var init = actor.GetInitOrDefault<TurretFacingInit>(this);
					if (init != null)
						return init.Value.Angle;

					return InitialFacing.Angle;
				},
				(actor, value) => actor.ReplaceInit(new TurretFacingInit(this, new WAngle((int)value)), this));
		}

		public static Func<WAngle> WorldFacingFromInit(IActorInitializer init, TraitInfo info, WAngle defaultFacing)
		{
			// (Dynamic)TurretFacingInit is specified relative to the actor body.
			// We need to add the body facing to return an absolute world angle.
			Func<WAngle> bodyFacing = null;
			var facingInit = init.GetOrDefault<FacingInit>();
			if (facingInit != null)
			{
				var facing = facingInit.Value;
				bodyFacing = () => facing;
			}

			var turretFacingInit = init.GetOrDefault<TurretFacingInit>(info);
			if (turretFacingInit != null)
			{
				var facing = turretFacingInit.Value;
				return bodyFacing != null ? (Func<WAngle>)(() => bodyFacing() + facing) : () => facing;
			}

			var dynamicFacingInit = init.GetOrDefault<DynamicTurretFacingInit>(info);
			if (dynamicFacingInit != null)
				return bodyFacing != null ? () => bodyFacing() + dynamicFacingInit.Value() : dynamicFacingInit.Value;

			return bodyFacing ?? (() => defaultFacing);
		}

		public Func<WAngle> WorldFacingFromInit(IActorInitializer init)
		{
			return WorldFacingFromInit(init, this, InitialFacing);
		}

		public Func<WAngle> LocalFacingFromInit(IActorInitializer init)
		{
			var turretFacingInit = init.GetOrDefault<TurretFacingInit>(this);
			if (turretFacingInit != null)
			{
				var facing = turretFacingInit.Value;
				return () => facing;
			}

			var dynamicFacingInit = init.GetOrDefault<DynamicTurretFacingInit>(this);
			if (dynamicFacingInit != null)
				return dynamicFacingInit.Value;

			return () => InitialFacing;
		}

		// Turret offset in world-space
		public Func<WVec> PreviewPosition(ActorPreviewInitializer init, Func<WRot> orientation)
		{
			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			return () => body.LocalToWorld(Offset.Rotate(orientation()));
		}

		// Orientation in world-space
		public Func<WRot> PreviewOrientation(ActorPreviewInitializer init, Func<WRot> orientation, int facings)
		{
			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var turretFacing = LocalFacingFromInit(init);

			Func<WRot> world = () => WRot.FromYaw(turretFacing()).Rotate(orientation());
			if (facings == 0)
				return world;

			// Quantize orientation to match a rendered sprite
			// Implies no pitch or roll
			return () => WRot.FromYaw(body.QuantizeFacing(world().Yaw, facings));
		}

		public override object Create(ActorInitializer init) { return new Turreted(init, this); }
	}

	public class Turreted : PausableConditionalTrait<TurretedInfo>, ITick, IDeathActorInitModifier, IActorPreviewInitModifier, INotifyDeployTriggeredPrepare
	{
		AttackTurreted attack;
		IFacing facing;
		BodyOrientation body;
		INotifyDeployPrepareComplete[] notify;

		[Sync]
		public int QuantizedFacings = 0;

		WVec desiredDirection;
		int realignTick = 0;
		bool realignDesired;
		bool forceRealigning = false;
		public WRot WorldOrientation
		{
			get
			{
				var world = facing != null && !Info.Floating ? LocalOrientation.Rotate(facing.Orientation) : LocalOrientation;
				if (QuantizedFacings == 0)
					return world;

				// Quantize orientation to match a rendered sprite
				// Implies no pitch or roll
				return WRot.FromYaw(body.QuantizeFacing(world.Yaw, QuantizedFacings));
			}
		}

		public WRot LocalOrientation { get; private set; }

		// For subclasses that want to move the turret relative to the body
		protected WVec localOffset = WVec.Zero;

		public WVec Offset => Info.Offset + localOffset;
		public string Name => Info.Turret;

		public Turreted(ActorInitializer init, TurretedInfo info)
			: base(info)
		{
			LocalOrientation = WRot.FromYaw(info.LocalFacingFromInit(init)());
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			attack = self.TraitsImplementing<AttackTurreted>().SingleOrDefault(at => ((AttackTurretedInfo)at.Info).Turrets.Contains(Info.Turret));
			facing = self.TraitOrDefault<IFacing>();
			body = self.Trait<BodyOrientation>();
			notify = self.TraitsImplementing<INotifyDeployPrepareComplete>().ToArray();
		}

		void ITick.Tick(Actor self)
		{
			Tick(self);
		}

		int forceRealigningWait = 0;
		public bool CanUndeploy()
		{
			if (Info.RealignBeforeDeploy)
			{
				if (LocalOrientation.Yaw == Info.InitialFacing)
					return true;

				forceRealigningWait = 2;
				forceRealigning = true;

				return false;
			}

			return true;
		}

		bool toDeploy = false;
		bool toUndeploy = false;

		int INotifyDeployTriggeredPrepare.Deploy(Actor self, bool skipMakeAnim)
		{
			if (Info.RealignBeforeDeploy)
			{
				if (LocalOrientation.Yaw == Info.InitialFacing)
				{
					foreach (var n in notify)
					{
						n.FinishedDeployPrepare(self);
					}
				}
				else
				{
					toDeploy = true;
					forceRealigningWait = 2;
					forceRealigning = true;
				}
			}

			return 1;
		}

		int INotifyDeployTriggeredPrepare.Undeploy(Actor self, bool skipMakeAnim)
		{
			if (Info.RealignBeforeDeploy)
			{
				if (LocalOrientation.Yaw == Info.InitialFacing)
				{
					foreach (var n in notify)
					{
						n.FinishedUndeployPrepare(self);
					}
				}
				else
				{
					toUndeploy = true;
					forceRealigningWait = 2;
					forceRealigning = true;
				}
			}

			return 1;
		}

		protected virtual void Tick(Actor self)
		{

			if (Info.RealignBeforeDeploy && forceRealigning)
			{
				if (LocalOrientation.Yaw == Info.InitialFacing)
				{
					if (toDeploy || toUndeploy)
					{
						foreach (var n in notify)
						{
							if (toDeploy)
								n.FinishedUndeployPrepare(self);
							else if (toUndeploy)
								n.FinishedUndeployPrepare(self);
						}

						toDeploy = false;
						toUndeploy = false;
					}

					if (forceRealigningWait-- < 0)
						forceRealigning = false;
				}
				else
				{
					forceRealigningWait = 2;
				}

				MoveTurret();

				return;
			}

			if (IsTraitDisabled)
			{
				if (Info.RealignWhenDisable)
				{
					realignDesired = true;
					desiredDirection = WVec.Zero;
					MoveTurret(true);
				}

				return;
			}

			// NOTE: FaceTarget is called in AttackTurreted.CanAttack if the turret has a target.
			if (attack != null)
			{
				// Only realign while not attacking anything
				if (attack.IsAiming)
				{
					realignTick = 0;
					return;
				}

				if (realignTick < Info.RealignDelay)
					realignTick++;
				else if (Info.RealignDelay > -1)
				{
					realignDesired = true;
					desiredDirection = WVec.Zero;
				}

				MoveTurret();
			}
			else
			{
				realignTick = 0;
				MoveTurret();
			}
		}

		WAngle DesiredLocalFacing
		{
			get
			{
				// A zero value means that we have a target, but it is on top of us
				if (desiredDirection == WVec.Zero)
					return LocalOrientation.Yaw;

				if (facing == null || Info.Floating)
					return desiredDirection.Yaw;

				// PERF: If the turret rotation axis is vertical we can directly take the difference in facing/yaw
				var orientation = facing.Orientation;
				if (orientation.Pitch == WAngle.Zero && orientation.Roll == WAngle.Zero)
					return desiredDirection.Yaw - orientation.Yaw;

				// If the turret rotation axis is not vertical we must transform the
				// target direction into the turrets local coordinate system
				return desiredDirection.Rotate(-orientation).Yaw;
			}
		}

		void MoveTurret(bool disable = false)
		{
			if (Info.Floating)
			{
				var desired = realignDesired || forceRealigning ? facing.Facing + Info.InitialFacing : DesiredLocalFacing;

				if (desired == LocalOrientation.Yaw)
					return;

				LocalOrientation = LocalOrientation.WithYaw(Util.TickFacing(LocalOrientation.Yaw, desired, disable ? Info.DisableRealignSpeed : Info.TurnSpeed));

				if (desired == LocalOrientation.Yaw)
				{
					realignDesired = false;
					desiredDirection = WVec.Zero;
				}
			}
			else
			{
				var desired = realignDesired || forceRealigning ? Info.InitialFacing : DesiredLocalFacing;

				if (desired == LocalOrientation.Yaw)
					return;

				LocalOrientation = LocalOrientation.WithYaw(Util.TickFacing(LocalOrientation.Yaw, desired, disable ? Info.DisableRealignSpeed : Info.TurnSpeed));

				if (desired == LocalOrientation.Yaw)
				{
					realignDesired = false;
					desiredDirection = WVec.Zero;
				}
			}
		}

		public bool FaceTarget(Actor self, in Target target)
		{
			if (IsTraitDisabled || IsTraitPaused || attack == null || attack.IsTraitDisabled || attack.IsTraitPaused)
				return false;

			if (target.Type == TargetType.Invalid)
			{
				desiredDirection = WVec.Zero;
				return false;
			}

			var turretPos = self.CenterPosition + Position(self);
			var targetPos = attack.GetTargetPosition(turretPos, target);
			desiredDirection = targetPos - turretPos;
			realignDesired = false;

			MoveTurret();
			return HasAchievedDesiredFacing;
		}

		public virtual bool HasAchievedDesiredFacing
		{
			get
			{
				if (Info.Floating)
				{
					var desired = realignDesired ? facing.Facing + Info.InitialFacing : DesiredLocalFacing;
					return desired == LocalOrientation.Yaw;
				}
				else
				{
					var desired = realignDesired ? Info.InitialFacing : DesiredLocalFacing;
					return desired == LocalOrientation.Yaw;
				}
			}
		}

		// Turret offset in world-space
		public WVec Position(Actor self)
		{
			var bodyOrientation = body.QuantizeOrientation(self.Orientation);
			return body.LocalToWorld(Offset.Rotate(bodyOrientation));
		}

		public void ModifyDeathActorInit(Actor self, TypeDictionary init)
		{
			init.Add(new TurretFacingInit(Info, LocalOrientation.Yaw));
		}

		void IActorPreviewInitModifier.ModifyActorPreviewInit(Actor self, TypeDictionary inits)
		{
			inits.Add(new DynamicTurretFacingInit(Info, () => LocalOrientation.Yaw));
		}

		protected override void TraitDisabled(Actor self)
		{
			if (attack != null && attack.IsAiming)
				attack.OnStopOrder(self);

			if (Info.RealignWhenDisable)
			{
				realignDesired = true;
				desiredDirection = WVec.Zero;
				MoveTurret(true);
			}
		}
	}

	public class TurretFacingInit : ValueActorInit<WAngle>
	{
		public TurretFacingInit(TraitInfo info, WAngle value)
			: base(info, value) { }

		public TurretFacingInit(string instanceName, WAngle value)
			: base(instanceName, value) { }

		public TurretFacingInit(WAngle value)
			: base(value) { }
	}

	public class DynamicTurretFacingInit : ValueActorInit<Func<WAngle>>
	{
		public DynamicTurretFacingInit(TraitInfo info, Func<WAngle> value)
			: base(info, value) { }
	}
}

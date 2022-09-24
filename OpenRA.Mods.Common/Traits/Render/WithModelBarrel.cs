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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public class WithModelBarrelInfo : ConditionalTraitInfo, IRenderActorPreviewModelsInfo, Requires<RenderModelsInfo>, Requires<ArmamentInfo>, Requires<TurretedInfo>
	{
		[Desc("Voxel sequence name to use")]
		public readonly string Sequence = "barrel";

		[Desc("Armament to use for recoil")]
		public readonly string Armament = "primary";

		[Desc("Visual offset")]
		public readonly WVec LocalOffset = WVec.Zero;

		[Desc("Rotate the barrel relative to the body init")]
		public readonly WRot InitOrientation = WRot.None;

		[Desc("Rotate the barrel relative to the body")]
		public readonly WRot LocalOrientation = WRot.None;

		[Desc("Defines if the Model should have a shadow.")]
		public readonly bool ShowShadow = true;

		[Desc("Barrel rotation speed")]
		public readonly WAngle RotationSpeed = new WAngle(512);

		[GrantedConditionReference]
		[Desc("Condition to grant when rotate barrel to LocalOrientation.")]
		public readonly string RotateCondition = "init-barrel";

		[Desc("Pack barrel before deploy")]
		public readonly bool PackBarrelBeforeDeploy = true;

		[Desc("Pack barrel before undeploy")]
		public readonly bool PackBarrelBeforeUndeploy = true;

		public override object Create(ActorInitializer init) { return new WithModelBarrel(init.Self, this); }

		public IEnumerable<ModelAnimation> RenderPreviewModels(
			ActorPreviewInitializer init, RenderModelsInfo rv, string image, Func<WRot> orientation, int facings, PaletteReference p)
		{
			if (!EnabledByDefault)
				yield break;

			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var armament = init.Actor.TraitInfos<ArmamentInfo>()
				.First(a => a.Name == Armament);
			var t = init.Actor.TraitInfos<TurretedInfo>()
				.First(tt => tt.Turret == armament.Turret);

			var model = init.World.ModelCache.GetModelSequence(image, Sequence);

			var turretOrientation = t.PreviewOrientation(init, orientation, facings);
			Func<WVec> barrelOffset = () => body.LocalToWorld(t.Offset + LocalOffset.Rotate(turretOrientation()));
			Func<WRot> barrelOrientation = () => LocalOrientation.Rotate(turretOrientation());

			yield return new ModelAnimation(model, barrelOffset, barrelOrientation, () => false, () => 0, ShowShadow);
		}
	}

	public class WithModelBarrel : ConditionalTrait<WithModelBarrelInfo>, ITick, INotifyDeployTriggeredPrepare
	{
		readonly Actor self;
		readonly Armament armament;
		readonly Turreted turreted;
		readonly BodyOrientation body;
		WRot currentOrientation = WRot.None;
		WRot targetOrientation = WRot.None;
		INotifyDeployPrepareComplete[] notify;

		int conditionToken = Actor.InvalidConditionToken;

		protected override void TraitDisabled(Actor self)
		{
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}

		public WithModelBarrel(Actor self, WithModelBarrelInfo info)
			: base(info)
		{
			this.self = self;
			body = self.Trait<BodyOrientation>();
			armament = self.TraitsImplementing<Armament>()
				.First(a => a.Info.Name == Info.Armament);
			turreted = self.TraitsImplementing<Turreted>()
				.First(tt => tt.Name == armament.Info.Turret);
			currentOrientation = Info.InitOrientation;
			targetOrientation = Info.LocalOrientation;
			notify = self.TraitsImplementing<INotifyDeployPrepareComplete>().ToArray();

			var rv = self.Trait<RenderModels>();
			rv.Add(new ModelAnimation(self.World.ModelCache.GetModelSequence(rv.Image, Info.Sequence),
				BarrelOffset, BarrelRotation,
				() => IsTraitDisabled, () => 0, info.ShowShadow));
		}

		public void Tick(Actor self)
		{
			if ((Info.PackBarrelBeforeDeploy || Info.PackBarrelBeforeUndeploy) && (toUndeploy || toDeploy))
			{
				if (currentOrientation == targetOrientation)
				{
					foreach (var n in notify)
					{
						if (toDeploy)
							n.FinishedDeployPrepare(self);
						else if (toUndeploy)
							n.FinishedUndeployPrepare(self);
					}

					toDeploy = false;
					toUndeploy = false;
				}
			}

			if (IsTraitDisabled)
			{
				currentOrientation = Info.InitOrientation;
				targetOrientation = Info.LocalOrientation;
				return;
			}

			if (currentOrientation != targetOrientation)
			{
				if (conditionToken == Actor.InvalidConditionToken)
					conditionToken = self.GrantCondition(Info.RotateCondition);
				currentOrientation = WRot.TickRot(currentOrientation, targetOrientation, Info.RotationSpeed);
			}
			else
			{
				currentOrientation = targetOrientation;

				if (conditionToken != Actor.InvalidConditionToken)
					conditionToken = self.RevokeCondition(conditionToken);
			}
		}

		WVec BarrelOffset()
		{
			// Barrel offset in turret coordinates
			var localOffset = Info.LocalOffset + new WVec(-armament.Recoil, WDist.Zero, WDist.Zero).Rotate(currentOrientation);

			// Turret coordinates to body coordinates
			var bodyOrientation = body.QuantizeOrientation(self.Orientation);
			localOffset = localOffset.Rotate(turreted.WorldOrientation) + turreted.Offset.Rotate(bodyOrientation);

			// Body coordinates to world coordinates
			return body.LocalToWorld(localOffset);
		}

		WRot BarrelRotation()
		{
			return currentOrientation.Rotate(turreted.WorldOrientation);
		}

		bool toDeploy = false;
		bool toUndeploy = false;

		int INotifyDeployTriggeredPrepare.Deploy(Actor self, bool skipMakeAnim)
		{
			if (IsTraitDisabled)
				return 0;

			if (Info.PackBarrelBeforeDeploy)
			{
				targetOrientation = Info.LocalOrientation;
				toDeploy = true;
				return 1;
			}

			return 0;
		}

		int INotifyDeployTriggeredPrepare.Undeploy(Actor self, bool skipMakeAnim)
		{
			if (IsTraitDisabled)
				return 0;

			if (Info.PackBarrelBeforeUndeploy)
			{
				targetOrientation = Info.InitOrientation;
				toUndeploy = true;
				return 1;
			}

			return 0;
		}
	}
}

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public class AttachManagerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new AttachManager(init.Self, this); }
	}

	public class AttachManager
	{
		readonly Actor self;
		public readonly AttachManagerInfo Info;
		readonly List<Actor> attachments = new List<Actor>();
		Actor parent;
		public bool HasParent { get => parent != null; }

		public AttachManager(Actor self, AttachManagerInfo info)
		{
			this.self = self;
			Info = info;
		}

		public bool AddAttachment(Actor attachment)
		{
			if (IsParent(attachment))
				return false;

			attachments.Add(attachment);
			//var occupySpace = attachment.TraitOrDefault<IOccupySpace>();
			//if (occupySpace != null)
			//	occupySpace.OccupySpace = false;
			var mobile = attachment.TraitOrDefault<Mobile>();
			if (mobile != null)
			{
				mobile.TerrainOrientationAdjustmentMargin = -1;
				mobile.CurrentMovementTypes = MovementType.None;
			}

			var am = attachment.TraitOrDefault<AttachManager>();
			if (am != null)
				am.parent = self;
			return true;
		}

		public bool ReleaseAttachment(Actor attachment)
		{
			if (attachment == null)
				throw new Exception("the attachment " + attachment.Info.Name + attachment.ActorID + " to release is null");

			attachments.Remove(attachment);
			//var occupySpace = attachment.TraitOrDefault<IOccupySpace>();
			//if (occupySpace != null)
			//	occupySpace.OccupySpace = true;
			var mobile = attachment.TraitOrDefault<Mobile>();
			if (mobile != null)
			{
				mobile.TerrainOrientationAdjustmentMargin = mobile.Info.TerrainOrientationAdjustmentMargin.Length;
			}

			var am = attachment.TraitOrDefault<AttachManager>();
			if (am != null)
				am.parent = null;
			return true;
		}

		public bool IsParent(Actor actor)
		{
			if (parent == null)
				return false;

			if (parent == actor)
				return true;
			else
			{
				var am = parent.TraitOrDefault<AttachManager>();
				if (am != null)
					return am.IsParent(actor);
				else
				{
					Console.WriteLine("Actor " + parent.Info.Name + parent.ActorID + " is the parent of " + self.Info.Name + self.ActorID + " but it has no AttachPointManager");
					return false;
				}
			}
		}
	}

	public class AttachPointInfo : ConditionalTraitInfo, Requires<AttachManagerInfo>, Requires<WithSkeletonInfo>
	{
		public readonly string BoneAttach = null;
		public override object Create(ActorInitializer init) { return new AttachPoint(init.Self, this); }
	}

	public class AttachPoint : ConditionalTrait<AttachPointInfo>, ITick, INotifyAttack, INotifyKilled
	{
		int attachBoneId = -1;
		WithSkeleton withSkeleton;

		// this actor attached to other's with matrix
		public bool Attached = false;

		// this actor scale
		public readonly float Scale = 1;
		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;

		Actor actor;
		IPositionable ap;
		IFacing ar;
		WithSkeleton aws;
		readonly AttachManager manager;
		World3DRenderer w3dr;
		public AttachPoint(Actor self, AttachPointInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			myFacing = self.Trait<IFacing>();
			this.self = self;
			withSkeleton = self.Trait<WithSkeleton>();
			attachBoneId = withSkeleton.GetBoneId(Info.BoneAttach);
			if (attachBoneId == -1)
				throw new Exception("can't find bone " + info.BoneAttach + " in skeleton.");

			manager = self.Trait<AttachManager>();
			w3dr = Game.Renderer.World3DRenderer;
		}

		void ITick.Tick(Actor self)
		{
			SelfTick();
		}

		void SelfTick()
		{
			if (actor != null && !actor.IsDead && actor.IsInWorld)
				TickAttach();
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			AttachActor(target.Actor);

			//if (actor != null && !actor.IsDead && actor.IsInWorld)
			//{
			//	var from = w3dr.Get3DPositionFromWPos(actor.CenterPosition);
			//	var to = w3dr.Get3DPositionFromWPos(target.CenterPosition);
			//	WRot rot = WRot.LookAt(from, to);
			//	if (ar != null)
			//		ar.Orientation = rot;
			//}
		}

		void AttachActor(Actor target)
		{
			if (target == null || target.IsDead || !target.IsInWorld || attachBoneId == -1)
				return;

			ReleaseAttach();

			if (!manager.AddAttachment(target))
				return;

			actor = target;
			ap = actor.TraitOrDefault<IPositionable>();
			ar = actor.TraitOrDefault<IFacing>();
			aws = actor.TraitOrDefault<WithSkeleton>();
			TickAttach();
		}

		void TickAttach()
		{
			if (aws != null)
			{
				aws.Skeleton.SetOffset(Transformation.MatWithNewScale(withSkeleton.Skeleton.BoneOffsetMat(attachBoneId), aws.Scale));
				ap?.SetPosition(actor, withSkeleton.GetWPosFromBoneId(attachBoneId), true);
			}
			else
			{
				ap?.SetPosition(actor, withSkeleton.GetWPosFromBoneId(attachBoneId), true);

				if (ar != null)
					ar.Orientation = withSkeleton.GetWRotFromBoneId(attachBoneId);
			}
		}

		void ReleaseAttach()
		{
			if (actor == null || actor.IsDead || !actor.IsInWorld)
				return;

			if (manager.ReleaseAttachment(actor))
			{
				TickAttach();
				if (ar != null)
					ar.Orientation = WRot.None.WithYaw(withSkeleton.GetWRotFromBoneId(attachBoneId).Yaw);

				if (actor.World.Map.DistanceAboveTerrain(actor.CenterPosition) > WDist.Zero)
				{
					actor.QueueActivity(new Parachute(actor));
				}

				actor = null;
			}
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
		{
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			ReleaseAttach();
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class AttachManagerInfo : TraitInfo, Requires<WithSkeletonInfo>
	{
		public readonly string MainSkeleton = "body";
		public override object Create(ActorInitializer init) { return new AttachManager(init.Self, this); }
	}

	public class AttachManager
	{
		readonly Actor self;
		public readonly WithSkeleton MainSkeleton;
		public readonly AttachManagerInfo Info;
		readonly List<Actor> attachments = new List<Actor>();
		Actor parent;
		public bool HasParent { get => parent != null; }

		public AttachManager(Actor self, AttachManagerInfo info)
		{
			this.self = self;
			Info = info;
			MainSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.MainSkeleton);
		}

		public bool AddAttachment(Actor attachment)
		{
			//if (attachment.TraitOrDefault<AttachManager>() == null)
			//	return false;

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
		public readonly string Name = "point";
		public readonly string Skeleton = "body";
		public readonly string BoneAttach = null;
		public override object Create(ActorInitializer init) { return new AttachPoint(init.Self, this); }
	}

	public class AttachPoint : ConditionalTrait<AttachPointInfo>, ITick, INotifyAttack, INotifyKilled
	{
		readonly int attachBoneId = -1;
		public readonly WithSkeleton MainSkeleton;
		public readonly string Name;

		// this actor attached to other's with matrix
		public bool Attached = false;

		readonly Actor self;
		readonly IFacing myFacing;
		readonly BodyOrientation body;

		Actor attachmentActor;
		IPositionable attachmentPositionable;
		IFacing attachmentFacing;
		AttachManager attachmentAM;
		WithSkeleton attachmentSkeleton;
		readonly AttachManager manager;
		World3DRenderer w3dr;
		public AttachPoint(Actor self, AttachPointInfo info)
			: base(info)
		{
			body = self.Trait<BodyOrientation>();
			myFacing = self.Trait<IFacing>();
			Name = info.Name;
			this.self = self;
			MainSkeleton = self.TraitsImplementing<WithSkeleton>().Single(w => w.Info.Name == info.Skeleton);
			attachBoneId = MainSkeleton.GetBoneId(Info.BoneAttach);
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
			if (attachmentActor != null && !attachmentActor.IsDead && attachmentActor.IsInWorld)
				TickAttach();
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			//AttachActor(target.Actor);
		}

		public bool AttachActor(Actor target)
		{
			if (target == null || target.IsDead || !target.IsInWorld || attachBoneId == -1)
				return false;

			ReleaseAttach();

			if (!manager.AddAttachment(target))
				return false;

			attachmentActor = target;
			attachmentPositionable = attachmentActor.TraitOrDefault<IPositionable>();
			attachmentFacing = attachmentActor.TraitOrDefault<IFacing>();
			attachmentAM = attachmentActor.TraitOrDefault<AttachManager>();
			if (attachmentAM != null)
			{
				attachmentSkeleton = attachmentAM.MainSkeleton;
				attachmentSkeleton.SetParent(MainSkeleton, attachBoneId);
			}

			MainSkeleton.CallForUpdate(attachBoneId);
			return true;
		}

		void TickAttach()
		{
			attachmentPositionable?.SetPosition(attachmentActor, MainSkeleton.GetWPosFromBoneId(attachBoneId), true);
			if (attachmentFacing != null)
				attachmentFacing.Orientation = MainSkeleton.GetWRotFromBoneId(attachBoneId);
		}

		void ReleaseAttach()
		{
			if (attachmentActor == null || attachmentActor.IsDead || !attachmentActor.IsInWorld)
				return;

			if (manager.ReleaseAttachment(attachmentActor))
			{
				TickAttach();
				if (attachmentFacing != null)
					attachmentFacing.Orientation = WRot.None.WithYaw(MainSkeleton.GetWRotFromBoneId(attachBoneId).Yaw);

				if (attachmentSkeleton != null)
				{
					attachmentSkeleton.ReleaseFromParent();
				}

				if (attachmentActor.World.Map.DistanceAboveTerrain(attachmentActor.CenterPosition) > WDist.Zero)
				{
					attachmentActor.QueueActivity(new Parachute(attachmentActor));
				}

				attachmentActor = null;
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

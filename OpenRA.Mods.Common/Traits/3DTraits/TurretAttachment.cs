using System;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;
using TrueSync;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class TurretAttachmentInfo : MeshAttachmentInfo
	{
		public readonly string BarrelBone = null;
		public readonly string TurretBone = null;
		public readonly string FireBone = null;
		//public readonly WVec Offset = WVec.Zero;
		//public readonly WVec BarrelOffset = WVec.Zero;
		public override object Create(ActorInitializer init) { return new TurretAttachment(init.Self, this); }
	}

	public class TurretAttachment : MeshAttachment, INotifyAttack, ITick
	{
		//TSMatrix4x4 offsetMatrix;
		//TSMatrix4x4 barrelOffsetMatrix;
		readonly bool hasBarrel = false;

		public readonly int TurretBoneId = -1;
		public readonly int BarrelBoneId = -1;
		readonly TurretIK turretIk;
		readonly BarrelIk barrelIk;

		public TurretAttachment(Actor self, TurretAttachmentInfo info)
			: base(self, info, false)
		{
			if (AttachmentSkeleton == null)
				throw new Exception(self.Info.Name + " Turret Attachment Can not find attachment skeleton " + info.AttachmentSkeleton);

			TurretBoneId = AttachmentSkeleton.GetBoneId(info.TurretBone);
			if (TurretBoneId == -1)
				throw new Exception("can't find turret bone " + info.TurretBone + " in skeleton.");

			turretIk = new TurretIK(RenderMeshes.W3dr, 5);
			AttachmentSkeleton.Skeleton.AddInverseKinematic(TurretBoneId, turretIk);
			if (info.BarrelBone != null)
			{
				hasBarrel = true;
				BarrelBoneId = AttachmentSkeleton.GetBoneId(info.BarrelBone);
				if (BarrelBoneId == -1)
					throw new Exception("can't find barrel bone " + info.BarrelBone + " in skeleton.");

				barrelIk = new BarrelIk(RenderMeshes.W3dr, 5);
				AttachmentSkeleton.Skeleton.AddInverseKinematic(BarrelBoneId, barrelIk);
			}
		}

		WPos targetPos = WPos.Zero;

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			targetPos = target.CenterPosition;
			turretIk.TargetPos = targetPos;
			if (hasBarrel)
				barrelIk.TargetPos = targetPos;
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
		{

		}

		public override void Tick(Actor self)
		{
		}
	}

	class TurretIK : IBonePoseModifyer
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		TSQuaternion forward = TSQuaternion.LookRotation(TSVector.forward);

		public TurretIK(in World3DRenderer w3dr, in FP speed)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			var end = w3dr.Get3DPositionFromWPos(TargetPos);

			var offsetedTurBaseRot = Transformation.MatRotation(self);
			var start = Transformation.MatPosition(self);
			var dir = end - start;
			var localDir = offsetedTurBaseRot * dir;

			var yawRot = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));
			if (!yawRot.Equals(forward))
				forward = TSQuaternion.RotateTowards(forward, yawRot, RotateSpeed);
			self = self * TSMatrix4x4.Rotate(forward);
		}
	}

	class BarrelIk : IBonePoseModifyer
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		TSQuaternion barrelforward = TSQuaternion.LookRotation(TSVector.up);

		public BarrelIk(in World3DRenderer w3dr, in FP speed)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			var end = w3dr.Get3DPositionFromWPos(TargetPos);

			var offsetedBarrelBaseRot = Transformation.MatRotation(self);
			var start = Transformation.MatPosition(self);
			var dir = end - start;
			var localDir = offsetedBarrelBaseRot * dir;

			var pitchRot = TSQuaternion.FromToRotation(TSVector.up, new TSVector(0, localDir.y, localDir.z));
			if (!pitchRot.Equals(barrelforward))
				barrelforward = TSQuaternion.RotateTowards(barrelforward, pitchRot, RotateSpeed);
			self = self * TSMatrix4x4.Rotate(barrelforward);
		}
	}

}

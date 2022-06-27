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
		public readonly string Name = "turret";
		public readonly string BarrelBone = null;
		public readonly string TurretBone = null;
		[Desc("Number of ticks before turret is realigned. (-1 turns off realignment)")]
		public readonly int RealignDelay = 40;
		public readonly float RotationSpeed = 2.0f;
		public override object Create(ActorInitializer init) { return new TurretAttachment(init.Self, this); }
	}

	public class TurretAttachment : MeshAttachment, ITick
	{
		public readonly string Name;
		public readonly TurretAttachmentInfo TurretInfo;
		readonly bool hasBarrel = false;

		public readonly int TurretBoneId = -1;
		public readonly int BarrelBoneId = -1;
		readonly TurretIK turretIk;
		readonly BarrelIk barrelIk;
		readonly int realignDelay;
		public TurretAttachment(Actor self, TurretAttachmentInfo info)
			: base(self, info, false)
		{
			Name = info.Name;
			TurretInfo = info;

			if (AttachmentSkeleton == null)
				throw new Exception(self.Info.Name + " Turret Attachment Can not find attachment skeleton " + info.AttachmentSkeleton);

			TurretBoneId = AttachmentSkeleton.GetBoneId(info.TurretBone);
			if (TurretBoneId == -1)
				throw new Exception("can't find turret bone " + info.TurretBone + " in skeleton.");

			turretIk = new TurretIK(RenderMeshes.W3dr, info.RotationSpeed);
			AttachmentSkeleton.Skeleton.AddInverseKinematic(TurretBoneId, turretIk);
			if (info.BarrelBone != null)
			{
				hasBarrel = true;
				BarrelBoneId = AttachmentSkeleton.GetBoneId(info.BarrelBone);
				if (BarrelBoneId == -1)
					throw new Exception("can't find barrel bone " + info.BarrelBone + " in skeleton.");

				barrelIk = new BarrelIk(RenderMeshes.W3dr, info.RotationSpeed);
				AttachmentSkeleton.Skeleton.AddInverseKinematic(BarrelBoneId, barrelIk);
			}

			realignDelay = info.RealignDelay;
		}

		FP deg;
		int realignTick = 0;
		public bool FacingTarget(in Target target, in WPos targetPos)
		{
			realignTick = 0;

			turretIk.TargetPos = targetPos;
			turretIk.State = AimState.Aim;
			if (hasBarrel)
			{
				barrelIk.TargetPos = targetPos;
				barrelIk.State = AimState.Aim;
			}

			AttachmentSkeleton.CallForUpdate();
			return true;
		}

		public bool FacingWithInTolerance(in WAngle facingTolerance)
		{
			if (!MainSkeleton.HasUpdated)
				return false;
			deg = (FP)facingTolerance.Angle / 512 * 180;

			if (hasBarrel)
			{
				return barrelIk.FacingWithInTolerance(deg) && turretIk.FacingWithInTolerance(deg);
			}
			else
				return turretIk.FacingWithInTolerance(deg);
		}

		public override void Tick(Actor self)
		{
			if (++realignTick > realignDelay)
			{
				turretIk.State = AimState.Realign;
				if (hasBarrel)
				{
					barrelIk.State = AimState.Realign;
				}
			}
		}
	}

	public enum AimState
	{
		Keep,
		Aim,
		Realign
	}

	class TurretIK : IBonePoseModifyer
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		public AimState State;

		TSQuaternion forward = TSQuaternion.LookRotation(TSVector.forward);
		TSQuaternion initFacing = TSQuaternion.LookRotation(TSVector.forward);

		TSVector dir;
		TSVector start;
		TSVector end;
		TSVector localDir;
		TSQuaternion yawRot;
		TSQuaternion offsetedTurBaseRot;
		public TurretIK(in World3DRenderer w3dr, in FP speed)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			if (State == AimState.Realign)
			{
				yawRot = initFacing;
				if (!yawRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, yawRot, RotateSpeed);
				else
					State = AimState.Keep;
				self = self * TSMatrix4x4.Rotate(forward);
			}
			else if (State == AimState.Aim)
			{
				end = w3dr.Get3DPositionFromWPos(TargetPos);

				offsetedTurBaseRot = Transformation.MatRotation(self);
				start = Transformation.MatPosition(self);
				dir = end - start;
				localDir = offsetedTurBaseRot * dir;

				yawRot = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));
				if (!yawRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, yawRot, RotateSpeed);
				else
					State = AimState.Keep;
				self = self * TSMatrix4x4.Rotate(forward);
			}
		}

		public bool FacingWithInTolerance(in FP deg)
		{
			end = w3dr.Get3DPositionFromWPos(TargetPos);
			dir = end - start;
			localDir = offsetedTurBaseRot * dir;
			yawRot = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));

			return FP.Abs(TSQuaternion.Angle(forward, yawRot)) <= deg;
		}
	}

	class BarrelIk : IBonePoseModifyer
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		public AimState State;

		TSQuaternion forward = TSQuaternion.LookRotation(TSVector.up);
		TSQuaternion initFacing = TSQuaternion.LookRotation(TSVector.forward);

		TSVector dir;
		TSVector start;
		TSVector end;
		TSVector localDir;
		TSQuaternion pitchRot;
		TSQuaternion offsetedBarrelBaseRot;
		public BarrelIk(in World3DRenderer w3dr, in FP speed)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			if (State == AimState.Realign)
			{
				pitchRot = initFacing;
				if (!pitchRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, pitchRot, RotateSpeed);
				else
					State = AimState.Keep;
				self = self * TSMatrix4x4.Rotate(forward);
			}
			else if (State == AimState.Aim)
			{
				end = w3dr.Get3DPositionFromWPos(TargetPos);

				offsetedBarrelBaseRot = Transformation.MatRotation(self);
				start = Transformation.MatPosition(self);
				dir = end - start;
				localDir = offsetedBarrelBaseRot * dir;

				pitchRot = TSQuaternion.FromToRotation(TSVector.up, new TSVector(0, localDir.y, localDir.z));
				if (!pitchRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, pitchRot, RotateSpeed);
				else
					State = AimState.Keep;
				self = self * TSMatrix4x4.Rotate(forward);
			}
		}

		public bool FacingWithInTolerance(in FP deg)
		{
			end = w3dr.Get3DPositionFromWPos(TargetPos);
			dir = end - start;
			localDir = offsetedBarrelBaseRot * dir;
			pitchRot = TSQuaternion.FromToRotation(TSVector.up, new TSVector(0, localDir.y, localDir.z));

			return FP.Abs(TSQuaternion.Angle(forward, pitchRot)) <= deg;
		}

	}

}

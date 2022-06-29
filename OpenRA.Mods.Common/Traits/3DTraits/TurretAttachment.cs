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
		public readonly float BarrelRotationSpeed = 2.0f;

		public readonly float InitTurretAngle = 0;
		public readonly float TurretRotationMin = -180;
		public readonly float TurretRoataionMax = 180;
		public readonly bool TurretFireAngleLimitation = true;

		public readonly float InitBarrelAngle = 45;
		public readonly float BarrelMaxDepression = 15;
		public readonly float BarrelMaxElevation = 80;
		public readonly bool BarrelFireAngleLimitation = false;

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

			turretIk = new TurretIK(RenderMeshes.W3dr, info.RotationSpeed, info.InitTurretAngle, info.TurretRotationMin, info.TurretRoataionMax, info.TurretFireAngleLimitation);
			AttachmentSkeleton.AddBonePoseModifier(TurretBoneId, turretIk);
			if (info.BarrelBone != null)
			{
				hasBarrel = true;
				BarrelBoneId = AttachmentSkeleton.GetBoneId(info.BarrelBone);
				if (BarrelBoneId == -1)
					throw new Exception("can't find barrel bone " + info.BarrelBone + " in skeleton.");

				barrelIk = new BarrelIk(RenderMeshes.W3dr, info.BarrelRotationSpeed, info.InitBarrelAngle, info.BarrelMaxDepression, info.BarrelMaxElevation, info.BarrelFireAngleLimitation);
				AttachmentSkeleton.AddBonePoseModifier(BarrelBoneId, barrelIk);
			}

			realignDelay = info.RealignDelay;

			turretIk.State = AimState.Realign;
			if (hasBarrel)
			{
				barrelIk.State = AimState.Realign;
			}

			realignTick = realignDelay - 1;
		}

		FP deg;
		int realignTick;
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
			if (realignTick == realignDelay)
			{
				turretIk.State = AimState.Realign;
				if (hasBarrel)
				{
					barrelIk.State = AimState.Realign;
				}
			}
			else
			{
				realignTick++;
			}
		}
	}

	public enum AimState
	{
		Keep,
		Aim,
		Realign
	}

	class TurretIK : IBonePoseModifier
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		public AimState State = AimState.Realign;

		TSQuaternion initFacing = TSQuaternion.LookRotation(TSVector.forward);
		TSQuaternion forward = TSQuaternion.LookRotation(TSVector.forward);

		TSVector dir;
		TSVector start;
		TSVector end;
		TSVector localDir;
		TSQuaternion yawRot;
		TSQuaternion offsetedTurBaseRot;

		readonly FP rotationMin;
		readonly FP rotationMax;
		readonly bool noRotationClamp;
		readonly bool checkAngleLimitation;
		FP currentAngle;

		public InverseKinematicState IKState
		{
			get => ikState;
		}

		InverseKinematicState ikState = InverseKinematicState.Resolving;

		public TurretIK(in World3DRenderer w3dr, in FP speed, in FP initAngle, in FP rotationMin, in FP rotationMax, bool angleLimitation)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
			this.rotationMin = rotationMin;
			this.rotationMax = rotationMax;
			currentAngle = initAngle;
			initFacing = TSQuaternion.Euler(new TSVector(0, initAngle, 0));
			checkAngleLimitation = angleLimitation;
			forward = initFacing;

			if (rotationMin <= -179.99f && rotationMax >= 179.99f)
				noRotationClamp = true;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			if (State == AimState.Realign)
			{
				if (!initFacing.Equals(forward))
				{
					forward = TSQuaternion.RotateTowards(forward, initFacing, RotateSpeed);
					ikState = InverseKinematicState.Resolving;
				}
				else
				{
					State = AimState.Keep;
					ikState = InverseKinematicState.Keeping;
				}

				self = self * TSMatrix4x4.Rotate(forward);
			}
			else if (State == AimState.Aim)
			{
				ikState = InverseKinematicState.Resolving;
				end = w3dr.Get3DPositionFromWPos(TargetPos);

				offsetedTurBaseRot = Transformation.MatRotation(self);
				start = Transformation.MatPosition(self);
				dir = end - start;
				localDir = offsetedTurBaseRot * dir;

				//var yawDir = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));
				//Console.WriteLine("yawDir.eulerAngles: " + yawDir.eulerAngles + "   angle: " + (TSMath.Atan(localDir.x / localDir.z) * TSMath.Rad2Deg) + "  vec: " + new TSVector(localDir.x, 0, localDir.z));

				CalculateRoataion();

				if (!yawRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, yawRot, RotateSpeed);
				self = self * TSMatrix4x4.Rotate(forward);
			}
		}

		void CalculateRoataion()
		{
			if (noRotationClamp)
			{
				yawRot = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));
				return;
			}

			var angle = TSMath.Abs(TSMath.Atan(localDir.x / localDir.z) * TSMath.Rad2Deg);

			// right
			if (localDir.x > 0)
			{
				// front
				if (localDir.z >= 0)
				{
				}
				else
				{
					angle = 180 - angle;
				}
			}
			else
			{
				// front
				if (localDir.z >= 0)
				{
					angle = -angle;
				}
				else
				{
					angle = angle - 180;
				}

			}

			if (angle < rotationMin)
			{
				FP toMin = TSMath.Abs(angle - rotationMin);
				FP toMax = TSMath.Abs((180 - rotationMax) + (angle + 180));
				if (toMin > toMax)
					currentAngle = rotationMax;
				else
					currentAngle = rotationMin;
			}
			else if (angle > rotationMax)
			{
				FP toMin = TSMath.Abs((180 - angle) + (180 + rotationMin));
				FP toMax = TSMath.Abs(angle - rotationMax);
				if (toMin > toMax)
					currentAngle = rotationMax;
				else
					currentAngle = rotationMin;
			}
			else
				currentAngle = angle;

			yawRot = TSQuaternion.Euler(new TSVector(0, currentAngle, 0));
		}

		public bool FacingWithInTolerance(in FP deg)
		{
			if (checkAngleLimitation)
			{
				var yawDir = TSQuaternion.FromToRotation(TSVector.forward, new TSVector(localDir.x, 0, localDir.z));
				return State == AimState.Aim && FP.Abs(TSQuaternion.Angle(forward, yawDir)) <= deg;
			}
			else
			{
				return State == AimState.Aim && FP.Abs(TSQuaternion.Angle(forward, yawRot)) <= deg;
			}
		}

		public void InitIK(ref TSMatrix4x4 self)
		{
			//self = TSMatrix4x4.Translate(Transformation.MatPosition(self)) * TSMatrix4x4.Scale(Transformation.MatScale(self));
			self = self * TSMatrix4x4.Rotate(initFacing);
		}
	}

	class BarrelIk : IBonePoseModifier
	{
		readonly World3DRenderer w3dr;
		public WPos TargetPos;
		public FP RotateSpeed;
		public AimState State = AimState.Realign;

		TSQuaternion forward = TSQuaternion.LookRotation(TSVector.forward);
		TSQuaternion initFacing = TSQuaternion.LookRotation(TSVector.forward);

		TSVector dir;
		TSVector start;
		TSVector end;
		TSVector localDir;
		TSQuaternion pitchRot;
		TSQuaternion offsetedBarrelBaseRot;
		FP depression;
		FP elevation;
		readonly bool checkAngleLimitation;

		public InverseKinematicState IKState
		{
			get => ikState;
		}

		InverseKinematicState ikState = InverseKinematicState.Resolving;

		public BarrelIk(in World3DRenderer w3dr, in FP speed, in FP initAngle, in FP depression, in FP elevation, bool angleLimitation)
		{
			this.w3dr = w3dr;
			this.RotateSpeed = speed;
			this.depression = depression;
			this.elevation = elevation;
			checkAngleLimitation = angleLimitation;
			initFacing = TSQuaternion.Euler(new TSVector(-initAngle, 0, 0));
			forward = initFacing;
		}

		public void CalculateIK(ref TSMatrix4x4 self)
		{
			if (State == AimState.Realign)
			{
				if (!initFacing.Equals(forward))
				{
					ikState = InverseKinematicState.Resolving;
					forward = TSQuaternion.RotateTowards(forward, initFacing, RotateSpeed);
				}
				else
				{
					State = AimState.Keep;
					ikState = InverseKinematicState.Keeping;
				}

				self = self * TSMatrix4x4.Rotate(forward);
			}
			else if (State == AimState.Aim)
			{
				ikState = InverseKinematicState.Resolving;
				end = w3dr.Get3DPositionFromWPos(TargetPos);

				offsetedBarrelBaseRot = Transformation.MatRotation(self);
				start = Transformation.MatPosition(self);
				dir = end - start;
				localDir = offsetedBarrelBaseRot * dir;

				CalculateRotate();

				// pitchRot = TSQuaternion.FromToRotation(TSVector.up, new TSVector(0, localDir.y, localDir.z));

				// Console.WriteLine("pitchRot.eulerAngles: " + pitchRot.eulerAngles + " vec: " + (TSMath.Atan(localDir.y / localDir.z) * TSMath.Rad2Deg));

				if (!pitchRot.Equals(forward))
					forward = TSQuaternion.RotateTowards(forward, pitchRot, RotateSpeed);
				self = self * TSMatrix4x4.Rotate(forward);
			}
		}

		void CalculateRotate()
		{
			var angle = (TSMath.Atan(localDir.y / localDir.z) * TSMath.Rad2Deg);

			if (angle < 0)
			{
				// elevation
				angle = 90 + angle;
				if (angle > elevation)
					angle = elevation;
				pitchRot = TSQuaternion.Euler(new TSVector(-angle, 0, 0));
			}
			else
			{
				// depression
				angle = 90 - angle;
				if (angle > depression)
					angle = depression;
				pitchRot = TSQuaternion.Euler(new TSVector(angle, 0, 0));
			}
		}

		public bool FacingWithInTolerance(in FP deg)
		{
			if (checkAngleLimitation)
			{
				var pitchDir = TSQuaternion.FromToRotation(TSVector.up, new TSVector(0, localDir.y, localDir.z));
				return State == AimState.Aim && FP.Abs(TSQuaternion.Angle(forward, pitchDir)) <= deg;
			}
			else
			{
				return State == AimState.Aim && FP.Abs(TSQuaternion.Angle(forward, pitchRot)) <= deg;
			}
		}

		public void InitIK(ref TSMatrix4x4 self)
		{
			//self = TSMatrix4x4.Translate(Transformation.MatPosition(self)) * TSMatrix4x4.Scale(Transformation.MatScale(self));
			self = self * TSMatrix4x4.Rotate(initFacing);
		}
	}

}

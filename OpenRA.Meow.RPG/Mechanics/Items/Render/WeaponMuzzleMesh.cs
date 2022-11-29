using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using TagLib.Ape;
using GlmSharp;
using System;
using System.Linq;
using Microsoft.VisualBasic;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class WeaponMuzzleMeshInfo : TraitInfo, Requires<RenderMeshesInfo>, Requires<WeaponItemInfo>, Requires<WithSkeletonInfo>
	{
		public readonly int MuzzleDuration = 5;
		public readonly float2 AlphaStartToEnd = new float2(1, 0);
		public readonly float2 ScaleStartToEnd = new float2(0.25f, 1);
		public readonly string Mesh = "muzzle";
		public readonly string Image = null;
		[FieldLoader.Require]
		public readonly string MuzzleSkeleton = null;
		[FieldLoader.Require]
		public readonly string MuzzleBone = null;
		public override object Create(ActorInitializer init)
		{
			return new WeaponMuzzleMesh(init.Self, this);
		}
	}

	public class WeaponMuzzleMesh : INotifyBeingEquiped, ITickByItem, INotifyWeaponItemAttack
	{
		readonly WeaponMuzzleMeshInfo info;
		public readonly WithSkeleton MuzzleSkeleton;
		public readonly MeshInstance MeshInstance;
		public readonly RenderMeshes RenderMeshes;
		public readonly WeaponItem WeaponItem;
		readonly int boneId;
		bool renderMuzzle;
		bool meshAdded;
		int muzzleTick = -1;
		float muzzleAlpha = 1;
		float muzzleScale = 1;

		public WeaponMuzzleMesh(Actor self, WeaponMuzzleMeshInfo info)
		{
			this.info = info;
			if (info.MuzzleDuration <= 0)
				throw new Exception("MuzzleDuration Can't be 0 or negative value");

			MuzzleSkeleton = self.TraitsImplementing<WithSkeleton>().Single(s => s.Name == info.MuzzleSkeleton);

			RenderMeshes = self.Trait<RenderMeshes>();
			WeaponItem = self.Trait<WeaponItem>();

			var image = RenderMeshes.Image;
			if (info.Image != null)
			{
				image = info.Image;
			}

			boneId = MuzzleSkeleton.GetBoneId(info.MuzzleBone);

			if (boneId == -1)
				throw new Exception("Can't find MuzzleBone:" + info.MuzzleBone);

			var mesh = self.World.MeshCache.GetMeshSequence(image, info.Mesh);
			MeshInstance = new MeshInstance(mesh,
				() => Transformation.MatWithNewScale(MuzzleSkeleton.GetRenderMatrixFromBoneId(boneId), muzzleScale),
				() => meshAdded && renderMuzzle,
				null)
			{
				GetAlpha = () => muzzleAlpha,
				GetTint = () => new float3(muzzleAlpha, muzzleAlpha, muzzleAlpha),
			};
		}

		public void OnWeaponItemAttack()
		{
			MuzzleSkeleton.SetBoneRenderUpdate(boneId, true);
			renderMuzzle = true;
			muzzleTick = info.MuzzleDuration;
			muzzleAlpha = Math.Clamp(info.AlphaStartToEnd.X, 0, 1);
			muzzleScale = info.ScaleStartToEnd.X;
		}

		public void TickByItem(Item item)
		{
			if (--muzzleTick >= 0)
			{
				muzzleAlpha = Math.Clamp(float2.Lerp(info.AlphaStartToEnd.Y, info.AlphaStartToEnd.X, (float)muzzleTick / info.MuzzleDuration), 0, 1);
				muzzleScale = float2.Lerp(info.ScaleStartToEnd.Y, info.ScaleStartToEnd.X, (float)muzzleTick / info.MuzzleDuration);
				if (muzzleAlpha <= 0)
				{
					muzzleAlpha = 0;
				}
			}
			else
			{
				MuzzleSkeleton.SetBoneRenderUpdate(boneId, false);
				renderMuzzle = false;
			}
		}

		void INotifyBeingEquiped.EquipedBy(Actor user, EquipmentSlot slot)
		{
			if (slot.RenderMeshes == null)
				return;

			meshAdded = true;

			slot.RenderMeshes.Add(MeshInstance);
		}

		void INotifyBeingEquiped.UnequipedBy(Actor user, EquipmentSlot slot)
		{
			meshAdded = false;
			renderMuzzle = false;
			muzzleTick = -1;
			MuzzleSkeleton.SetBoneRenderUpdate(boneId, false);
			if (slot.RenderMeshes == null)
				return;

			slot.RenderMeshes.Remove(MeshInstance);
		}
	}
}

using System;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	public class WeaponItemMuzzleInfo : TraitInfo, Requires<RenderMeshesInfo>, Requires<WeaponItemInfo>, Requires<WithSkeletonInfo>
	{
		public readonly int MuzzleDuration = 5;
		public readonly float2 AlphaStartToEnd = new float2(1, 0);
		public readonly float2 ScaleStartToEnd = new float2(0.25f, 1);
		public readonly string MuzzleFlashMesh = "muzzle";
		public readonly string Image = null;
		[FieldLoader.Require]
		public readonly string MuzzleSkeleton = null;
		[FieldLoader.Require]
		public readonly string MuzzleBone = null;
		public override object Create(ActorInitializer init)
		{
			return new WeaponItemMuzzle(init.Self, this);
		}
	}

	public class WeaponItemMuzzle : INotifyBeingEquiped, ITickByItem, INotifyWeaponItemAttack
	{
		readonly WeaponItemMuzzleInfo info;
		public readonly WithSkeleton MuzzleSkeleton;
		public readonly int MuzzleBoneId = -1;
		public readonly MeshInstance MuzzleFlashMeshInstance;
		public readonly RenderMeshes RenderMeshes;
		public readonly WeaponItem WeaponItem;
		bool renderMuzzle;
		bool meshAdded;
		int muzzleTick = -1;
		float muzzleAlpha = 1;
		float muzzleScale = 1;

		public WeaponItemMuzzle(Actor self, WeaponItemMuzzleInfo info)
		{
			this.info = info;
			if (info.MuzzleDuration <= 0)
				throw new Exception("MuzzleDuration Can't be 0 or negative value");

			RenderMeshes = self.Trait<RenderMeshes>();
			WeaponItem = self.Trait<WeaponItem>();
			WeaponItem.UsingMuzzle = this;

			if (!string.IsNullOrEmpty(info.MuzzleSkeleton))
			{
				MuzzleSkeleton = self.TraitsImplementing<WithSkeleton>().Single(s => s.Name == info.MuzzleSkeleton);

				MuzzleBoneId = MuzzleSkeleton.GetBoneId(info.MuzzleBone);

				if (MuzzleBoneId == -1)
					throw new Exception("Can't find MuzzleBone:" + info.MuzzleBone);
			}
			else
				throw new Exception("The WeaponItemMuzzle need to have a skeleton");

			var image = RenderMeshes.Image;
			if (info.Image != null)
			{
				image = info.Image;
			}

			if (!string.IsNullOrEmpty(info.MuzzleFlashMesh))
			{
				var mesh = self.World.MeshCache.GetMeshSequence(image, info.MuzzleFlashMesh);
				MuzzleFlashMeshInstance = new MeshInstance(mesh,
					() => Transformation.MatWithNewScale(MuzzleSkeleton.GetRenderMatrixFromBoneId(MuzzleBoneId), muzzleScale),
					() => meshAdded && renderMuzzle,
					null)
				{
					GetAlpha = () => muzzleAlpha,
					GetTint = () => new float3(muzzleAlpha, muzzleAlpha, muzzleAlpha),
				};
			}
		}

		public void OnWeaponItemAttack()
		{
			if (MuzzleFlashMeshInstance == null)
				return;

			MuzzleSkeleton.SetBoneRenderUpdate(MuzzleBoneId, true);
			MuzzleSkeleton.Skeleton.TempUpdateRenderSingle(MuzzleBoneId);
			renderMuzzle = true;
			muzzleTick = info.MuzzleDuration;
			muzzleAlpha = Math.Clamp(info.AlphaStartToEnd.X, 0, 1);
			muzzleScale = info.ScaleStartToEnd.X;
		}

		public void TickByItem(Item item)
		{
			if (MuzzleFlashMeshInstance == null)
				return;

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
				MuzzleSkeleton.SetBoneRenderUpdate(MuzzleBoneId, false);
				renderMuzzle = false;
			}
		}

		void INotifyBeingEquiped.EquipedBy(Actor user, EquipmentSlot slot)
		{
			if (MuzzleFlashMeshInstance == null)
				return;

			if (slot.RenderMeshes == null)
				return;

			meshAdded = true;

			slot.RenderMeshes.Add(MuzzleFlashMeshInstance);
		}

		void INotifyBeingEquiped.UnequipedBy(Actor user, EquipmentSlot slot)
		{
			if (MuzzleFlashMeshInstance == null)
				return;

			meshAdded = false;
			renderMuzzle = false;
			muzzleTick = -1;
			MuzzleSkeleton.SetBoneRenderUpdate(MuzzleBoneId, false);
			if (slot.RenderMeshes == null)
				return;

			slot.RenderMeshes.Remove(MuzzleFlashMeshInstance);
		}
	}
}

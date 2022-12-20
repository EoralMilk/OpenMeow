using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Traits.Trait3D;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Mechanics
{
	[Desc("Used for the item which should bind on a bone from the user's skeleton.")]
	public class ItemSkeletonHandlerInfo : TraitInfo, Requires<ItemInfo>
	{
		public readonly string Skeleton = null;
		public override object Create(ActorInitializer init)
		{
			return new ItemSkeletonHandler(init.Self, this);
		}
	}

	public class ItemSkeletonHandler: INotifyBeingEquiped, ITick
	{
		public readonly WithSkeleton Skeleton;
		readonly Item item;
		readonly IFacing itemFacing;
		readonly IPositionable itemPositionable;
		readonly Mobile itemMobile;
		readonly SelectionDecorationsBase itemSelectionDecorations;
		readonly RenderModels itemRenderModels;
		readonly RenderSprites itemRenderSprites;
		readonly bool callForUpdate;

		public ItemSkeletonHandler(Actor self, ItemSkeletonHandlerInfo info)
		{
			if (!string.IsNullOrEmpty(info.Skeleton))
				Skeleton = self.TraitsImplementing<WithSkeleton>().Single(s => s.Name == info.Skeleton);

			item = self.Trait<Item>();

			itemPositionable = item.ItemActor.TraitOrDefault<IPositionable>();
			itemFacing = item.ItemActor.TraitOrDefault<IFacing>();
			itemMobile = item.ItemActor.TraitOrDefault<Mobile>();
			itemSelectionDecorations = item.ItemActor.TraitOrDefault<SelectionDecorationsBase>();
			itemRenderModels = item.ItemActor.TraitOrDefault<RenderModels>();
			itemRenderSprites = item.ItemActor.TraitOrDefault<RenderSprites>();

			callForUpdate = itemPositionable != null || itemFacing != null;
		}

		public void Tick(Actor self)
		{
			if (item.ItemActor.IsDead)
			{
				item.EquipmentSlot?.RemoveItem(item.EquipmentSlot.SlotOwnerActor);
				item.Inventory?.TryRemove(item.Inventory.InventoryActor, item);
				itemPositionable?.BindPoseTo(null);
			}

			if (item.EquipmentSlot == null || !callForUpdate)
			{
				return;
			}

			if (item.EquipmentSlot.SkeletonBind != null && item.EquipmentSlot.BoneId != -1)
			{
				var mat = item.EquipmentSlot.SkeletonBind.GetMatrixFromBoneId(item.EquipmentSlot.BoneId);
				var pos = World3DCoordinate.TSVec3ToWPos(Transformation.MatPosition(mat));

				itemPositionable?.SetPosition(item.ItemActor, pos, true);

				if (itemFacing != null)
				{
					itemFacing.Orientation = World3DCoordinate.GetWRotFromMatrix(mat);
				}

				var modelScale = Transformation.MatScale(mat) / item.EquipmentSlot.SkeletonBind.GetScale();
				var scaleValue = (float)(modelScale.magnitude);

				if (itemRenderModels != null)
				{
					itemRenderModels.ScaleOverride = itemRenderModels.Info.Scale * scaleValue;// MathF.Min(RenderModels.Info.Scale * SelectionDecorations.BoundsScale, RenderModels.Info.Scale);
				}

				if (itemRenderSprites != null)
				{
					itemRenderSprites.ScaleMultiply = scaleValue;
					itemRenderSprites.ShadowEnable = false;
				}

				if (itemSelectionDecorations != null)
				{
					itemSelectionDecorations.BoundsScale = scaleValue;

					itemSelectionDecorations.ForceRenderSelection = true;
				}
			}

		}

		void INotifyBeingEquiped.EquipedBy(Actor user, EquipmentSlot slot)
		{
			item.ItemActor.CancelActivity();
			if (slot.SkeletonBind != null && slot.BoneId != -1)
			{
				Skeleton?.SetParent(slot.SkeletonBind, slot.BoneId, Skeleton.Scale);
				itemPositionable?.BindPoseTo(() =>
					{
						if (slot == null || slot.SkeletonBind == null || slot.SlotOwnerActor.IsDead)
						{
							itemPositionable.BindPoseTo(null);
							return WPos.Zero;
						}

						return World3DCoordinate.TSVec3ToWPos(Transformation.MatPosition(slot.SkeletonBind.GetMatrixFromBoneId(item.EquipmentSlot.BoneId)));
					}
				);

				if (itemMobile != null)
				{
					itemMobile.RemoveInfluence();
					itemMobile.OccupySpace = false;
					itemMobile.TerrainOrientationIgnore = true;
					itemMobile.ForceDisabled = true;
				}
			}

		}

		public void ReleaseFrom(Actor user, EquipmentSlot slot)
		{
			Skeleton?.ReleaseFromParent();
			itemPositionable?.BindPoseTo(null);
			if (itemFacing != null)
			{
				if (itemMobile != null)
				{
					itemMobile.ForceDisabled = false;
					itemMobile.TerrainOrientationIgnore = false;
					itemMobile.UnderControl = true;
					itemMobile.RemoveInfluence();
					itemMobile.OccupySpace = true;
				}

				// itemFacing.Orientation = WRot.None;
			}

			var scaleValue = 1f;

			if (itemRenderModels != null)
			{
				itemRenderModels.ScaleOverride = itemRenderModels.Info.Scale * scaleValue;
			}

			if (itemRenderSprites != null)
			{
				itemRenderSprites.ScaleMultiply = scaleValue;
				itemRenderSprites.ShadowEnable = true;
			}

			if (itemSelectionDecorations != null)
			{
				itemSelectionDecorations.BoundsScale = scaleValue;

				itemSelectionDecorations.ForceRenderSelection = false;
			}
		}

		void INotifyBeingEquiped.UnequipedBy(Actor user, EquipmentSlot slot)
		{
			ReleaseFrom(user, slot);
		}

	}
}

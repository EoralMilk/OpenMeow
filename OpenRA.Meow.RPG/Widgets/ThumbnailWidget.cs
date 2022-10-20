using System;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class ThumbnailWidget : ShadowButtonWidget
	{
		public int2 IconOffset = int2.Zero;

		readonly Item item;
		readonly WorldRenderer worldRenderer;
		readonly Animation thumbnail;

		public ThumbnailWidget(Item item, WorldRenderer worldRenderer)
		{
			this.item = item;
			this.worldRenderer = worldRenderer;
			IsActive = () => this.item.EquipmentSlot != null;

			thumbnail = new Animation(worldRenderer.World, item.Info.ThumbnailImage);
			thumbnail.Play(item.Info.ThumbnailSequence);
		}

		public override void Draw()
		{
			base.Draw();

			// Icons
			Game.Renderer.EnableAntialiasingFilter();

			if (thumbnail != null && thumbnail.Image != null)
			{
				WidgetUtils.DrawSprite(thumbnail.Image,
					worldRenderer.Palette(item.ThumbnailPal),
					IconOffset + RenderBounds.Location);
			}

			Game.Renderer.DisableAntialiasingFilter();
		}
	}
}

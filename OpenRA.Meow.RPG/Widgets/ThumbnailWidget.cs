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
		readonly Item item;
		readonly WorldRenderer worldRenderer;
		readonly Animation thumbnail;

		public ThumbnailWidget(Item item, WorldRenderer worldRenderer, Skin skin)
			: base(skin)
		{
			this.item = item;
			this.worldRenderer = worldRenderer;
			IsActive = () => this.item.EquipmentSlot != null;

			thumbnail = new Animation(worldRenderer.World, item.Info.ThumbnailImage);
			thumbnail.Play(item.Info.ThumbnailSequence);
		}

		public override void Draw()
		{
			// Icons
			Game.Renderer.EnableAntialiasingFilter();

			if (thumbnail != null && thumbnail.Image != null)
			{
				var maxScale = Math.Min((float)RenderBounds.Width / thumbnail.Image.Bounds.Width,
					(float)RenderBounds.Height / thumbnail.Image.Bounds.Height);
				WidgetUtils.DrawSpriteCentered(thumbnail.Image,
					worldRenderer.Palette(item.ThumbnailPal),
					RenderBounds.Location + new int2(RenderBounds.Width / 2, RenderBounds.Height / 2) -
						int2.FromFloat3(maxScale * thumbnail.Image.Offset),
					maxScale);
			}

			Game.Renderer.DisableAntialiasingFilter();
			base.Draw();
		}
	}
}

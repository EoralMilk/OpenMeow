using System;
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public class ShadowScrollContainerWidget : ShadowContainerWidget
	{
		const int ThumbWidth = Skin.ScrollbarWidth - Skin.ScrollbarBorderWidth;

		readonly ContainerWidget scrollContent;

		public int BottomSpacing;

		int thumbHeight;
		int thumbPosition;
		int scroll;

		protected ShadowScrollContainerWidget()
		{
			base.AddChild(scrollContent = new ContainerWidget());
		}

		public override void AddChild(Widget child)
		{
			scrollContent.AddChild(child);
		}

		public override void RemoveChild(Widget child)
		{
			scrollContent.RemoveChild(child);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Scroll)
				return false;

			scroll -= mi.Delta.Y * 20;

			return true;
		}

		void UpdateScroll()
		{
			var innerHeight = scrollContent.Children.Select(child => child.Bounds.Y + child.Bounds.Height).Prepend(0).Max();

			if (innerHeight != 0)
				innerHeight += BottomSpacing;

			var height = Bounds.Height;
			var maxScroll = Math.Max(0, innerHeight - height);
			thumbHeight = maxScroll == 0 ? height : height * height / innerHeight;
			scroll = Math.Clamp(scroll, 0, maxScroll);
			thumbPosition = maxScroll == 0 ? 0 : (height - thumbHeight) * scroll / maxScroll;
			scrollContent.Bounds.Y = - scroll;
		}

		public override void Draw()
		{
			if (!Render)
				return;

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - Skin.ScrollbarWidth,
				RenderBounds.Y, Skin.ScrollbarBorderWidth, RenderBounds.Height),
				Skin.ScrollbarBorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - ThumbWidth,
				RenderBounds.Y + thumbPosition, ThumbWidth, thumbHeight),
				Skin.ScrollbarThumbColor
			);

			base.Draw();
		}

		public override void DrawOuter()
		{
			if (!IsVisible() || !Render)
				return;

			UpdateScroll();

			foreach (var child in Children)
			{
				if (child == scrollContent)
				{
					Game.Renderer.EnableScissor(RenderBounds);
					child.DrawOuter();
					Game.Renderer.DisableScissor();
				}
				else
					child.DrawOuter();
			}

			Draw();
		}
	}
}

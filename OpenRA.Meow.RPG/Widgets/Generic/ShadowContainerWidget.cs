using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public interface ITogglableRenderWidget
	{
		bool Render { get; set; }
	}

	public class ShadowContainerWidget : ContainerWidget, ITogglableRenderWidget
	{
#pragma warning disable IDE1006 // 命名样式
		public readonly Skin Skin;
		protected Func<ShadowSkin> ShadowSkin;
		protected bool Inset;
#pragma warning restore IDE1006 // 命名样式

		public bool Render { get; set; }

		protected ShadowContainerWidget(Skin skin)
		{
			Skin = skin;
			ShadowSkin = () => Skin.BrightShadowSkin;
		}

		public override void Draw()
		{
			if (!Render)
				return;

			if (Inset)
				DrawInset(ShadowSkin());
			else
				DrawOutset(ShadowSkin());
		}

		void DrawInset(ShadowSkin shadowSkin)
		{
			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y, RenderBounds.Width, shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y
					+ RenderBounds.Height
					- shadowSkin.BorderSize, RenderBounds.Width, shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y + shadowSkin.BorderSize, shadowSkin.BorderSize, RenderBounds.Height
					- 2 * shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - shadowSkin.BorderSize, RenderBounds.Y + shadowSkin.BorderSize, shadowSkin.BorderSize,
					RenderBounds.Height - 2 * shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + shadowSkin.ShadowSize, RenderBounds.Y, RenderBounds.Width - 2 * shadowSkin.ShadowSize,
					shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - shadowSkin.ShadowSize, RenderBounds.Y, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - shadowSkin.ShadowSize, RenderBounds.Y + shadowSkin.ShadowSize, shadowSkin.ShadowSize,
					RenderBounds.Height - 2 * shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - shadowSkin.ShadowSize, RenderBounds.Y
					+ RenderBounds.Height
					- shadowSkin.ShadowSize, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + shadowSkin.ShadowSize, RenderBounds.Y + RenderBounds.Height - shadowSkin.ShadowSize, RenderBounds.Width
					- 2 * shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y + RenderBounds.Height - shadowSkin.ShadowSize, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y + shadowSkin.ShadowSize, shadowSkin.ShadowSize, RenderBounds.Height
					- 2 * shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor
			);
		}

		void DrawOutset(ShadowSkin shadowSkin)
		{
			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y, RenderBounds.Width, shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y
					+ RenderBounds.Height
					- shadowSkin.BorderSize, RenderBounds.Width, shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y + shadowSkin.BorderSize, shadowSkin.BorderSize, RenderBounds.Height
					- 2 * shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width - shadowSkin.BorderSize, RenderBounds.Y + shadowSkin.BorderSize, shadowSkin.BorderSize,
					RenderBounds.Height - 2 * shadowSkin.BorderSize),
				shadowSkin.BorderColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X - shadowSkin.ShadowSize, RenderBounds.Y - shadowSkin.ShadowSize, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y - shadowSkin.ShadowSize, RenderBounds.Width, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width, RenderBounds.Y - shadowSkin.ShadowSize, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width, RenderBounds.Y, shadowSkin.ShadowSize, RenderBounds.Height),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X + RenderBounds.Width, RenderBounds.Y + RenderBounds.Height, shadowSkin.ShadowSize, shadowSkin
					.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X, RenderBounds.Y + RenderBounds.Height, RenderBounds.Width, shadowSkin.ShadowSize),
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X - shadowSkin.ShadowSize, RenderBounds.Y + RenderBounds.Height, shadowSkin.ShadowSize, shadowSkin.ShadowSize),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowTransparent
			);

			WidgetUtils.FillRectWithColor(
				new Rectangle(RenderBounds.X - shadowSkin.ShadowSize, RenderBounds.Y, shadowSkin.ShadowSize, RenderBounds.Height),
				shadowSkin.ShadowTransparent,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowColor,
				shadowSkin.ShadowTransparent
			);
		}

	}
}

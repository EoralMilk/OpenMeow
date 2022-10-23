using System;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public class ShadowProgressBarWidget : ShadowContainerWidget
	{
		public Size BarMargin = new Size(2, 2);

		public Func<float> GetProgress;
		public Func<Color> GetColor;

		public ShadowProgressBarWidget(Skin skin)
			: base(skin)
		{
			BarMargin = new Size(ShadowSkin().BorderSize, ShadowSkin().BorderSize);
			GetProgress = () => 1;
			GetColor = () => Color.LightGreen;
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var percentage = GetProgress();

			var maxBarWidth = rb.Width - BarMargin.Width * 2;
			var barWidth = percentage * maxBarWidth;

			var barRect = new Rectangle(rb.X + BarMargin.Width, rb.Y + BarMargin.Height,
				(int)barWidth, rb.Height - 2 * BarMargin.Height);

			WidgetUtils.FillRectWithColor(
				barRect,
				GetColor()
			);
			base.Draw();

		}
	}
}

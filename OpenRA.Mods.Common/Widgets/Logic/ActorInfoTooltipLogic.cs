using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.TA.Widgets.Logic
{
	public class ActorInfoTooltipLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public ActorInfoTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Func<BasicUnitInfo> getTooltipUnit)
		{
			widget.IsVisible = () => getTooltipUnit() != null;
			var nameLabel = widget.Get<LabelWidget>("NAME");
			var descLabel = widget.Get<LabelWidget>("DESC");

			var font = Game.Renderer.Fonts[nameLabel.Font];
			var descFont = Game.Renderer.Fonts[descLabel.Font];

			BasicUnitInfo lastUnitInfo = null;
			var descLabelPadding = descLabel.Bounds.Height;

			tooltipContainer.BeforeRender = () =>
			{
				var unitInfo = getTooltipUnit();

				if (unitInfo == null || unitInfo == lastUnitInfo)
					return;

				var tooltip = unitInfo.TooltipInfo;
				var name = tooltip != null ? tooltip.Name : unitInfo.ActorInfo.Name;
				var buildable = unitInfo.BuildableInfo;

				nameLabel.Text = name;

				var nameSize = font.Measure(name);
				var descSize = int2.Zero;
				if (unitInfo.Description != null)
				{
					descLabel.Text = unitInfo.Description.Replace("\\n", "\n");
					descSize = descFont.Measure(descLabel.Text);
				}
				else if (buildable != null)
				{
					descLabel.Text = buildable.Description.Replace("\\n", "\n");
					descSize = descFont.Measure(descLabel.Text);
				}
				else
				{
					descLabel.Text = null;
					descLabelPadding = 0;
					descSize = int2.Zero;
				}

				descLabel.Bounds.Width = descSize.X;
				descLabel.Bounds.Height = descSize.Y + descLabelPadding;

				var leftWidth = Math.Max(nameSize.X, descSize.X);

				widget.Bounds.Width = leftWidth + 2 * nameLabel.Bounds.X;

				// Set the bottom margin to match the left margin
				var leftHeight = descLabel.Bounds.Bottom + descLabel.Bounds.X;

				widget.Bounds.Height = leftHeight;

				lastUnitInfo = unitInfo;
			};
		}
	}
}

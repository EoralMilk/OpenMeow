using System;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;
using TagLib.Ape;

namespace OpenRA.Meow.RPG.Widgets
{
	public sealed class ToggleButtonWidget : ShadowButtonWidget
	{
		public bool SetActive;
		readonly WorldRenderer worldRenderer;
		public Action OnClick;

		public ToggleButtonWidget(WorldRenderer worldRenderer, Skin skin)
			: base(skin)
		{
			this.worldRenderer = worldRenderer;
			IsActive = () => SetActive;
		}

		public override bool HandleMouseInput(MouseInput mouseInput)
		{
			if (mouseInput.Button != MouseButton.Left && mouseInput.Button != MouseButton.Right)
				return false;

			// ReSharper disable once ConvertIfStatementToSwitchStatement
			if (mouseInput.Button == MouseButton.Left)
			{
				SetActive = true;
				OnClick?.Invoke();
			}
			else if (mouseInput.Button == MouseButton.Right)
			{
				SetActive = false;
				OnClick?.Invoke();
			}

			return true;
		}
	}
}

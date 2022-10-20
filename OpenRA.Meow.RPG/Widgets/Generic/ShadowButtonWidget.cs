using System;
using System.Linq;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public abstract class ShadowButtonWidget : ShadowContainerWidget
	{
		protected bool Hovered { get; private set; }

#pragma warning disable IDE1006 // 命名样式
		protected Func<bool> IsEnabled = () => true;
		protected Func<bool> IsActive = () => false;
#pragma warning restore IDE1006 // 命名样式

		protected ShadowButtonWidget()
		{
			IgnoreMouseOver = false;
			IgnoreChildMouseOver = true;
			Inset = true;

			ShadowSkin = () => !IsEnabled() ? Skin.DisabledShadowSkin :
				IsActive() ? Skin.ActiveShadowSkin :
				Hovered ? Skin.HoverShadowSkin : Skin.DarkShadowSkin;
		}

		public override void MouseEntered()
		{
			Hovered = true;
			base.MouseEntered();
		}

		public override void MouseExited()
		{
			Hovered = false;
			base.MouseExited();
		}

		public override bool HandleMouseInput(MouseInput mouseInput)
		{
			if (mouseInput.Event != MouseInputEvent.Down)
				return false;

			if (!IsEnabled())
				return false;

			Game.Sound.PlayNotification(Game.ModData.DefaultRules, null, "Sounds", "ClickSound", null);

			return true;
		}
	}
}

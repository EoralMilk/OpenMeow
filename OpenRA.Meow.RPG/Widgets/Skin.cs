using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG.Widgets
{
	public class ShadowSkin
	{
		public readonly int BorderSize;
		public readonly Color BorderColor;
		public readonly int ShadowSize;
		public readonly Color ShadowColor;
		public readonly Color ShadowTransparent;

		public ShadowSkin(int borderSize, Color borderColor, int shadowSize, Color shadowColor, Color shadowTransparent)
		{
			BorderSize = borderSize;
			BorderColor = borderColor;
			ShadowSize = shadowSize;
			ShadowColor = shadowColor;
			ShadowTransparent = shadowTransparent;
		}
	}

	public class Skin
	{
		public readonly int InGameUiWidth = 1280;
		public readonly int InGameUiHeight = 200;
		public readonly string InGameUiFont = "Ethnocentric.12";
		public readonly string InGameUiFontSmall = "Ethnocentric.11";
		public readonly string InGameUiFontLittle = "Ethnocentric.10";
		public readonly string InGameUiFontTiny = "Ethnocentric.8";

		public string[] Fontsmall => new string[] { InGameUiFontSmall, InGameUiFontLittle, InGameUiFontTiny};

		public readonly int ScrollbarWidth = 10;
		public readonly int ScrollbarBorderWidth = 1;

		public readonly int ActorIconHeight = 48;
		public readonly int ActorIconWidth = 64;
		public readonly int ActorHealthDiv = 100;
		public readonly int ActorExpDiv = 100;

		public readonly int MinimapWidth = 180;

		public readonly int ResourcesWidth = 105;
		public readonly int ResourceHeight = 25;

		public readonly int SquadsWidth = 235;
		public readonly int SquadLabelHeight = 15;
		public readonly int SquadThumbnailSize = 50;

		public readonly int CharacterWidth = 100;
		public readonly int CharacterPreviewHeight = 110;
		public readonly int CharacterLabelHeight = 14;

		public readonly int ActorNameHeight = 20;

		public readonly int ActionsWidth = 180;
		public readonly int ActionsButtonSize = 50;

		public readonly int InventoryWidth = 150;
		public readonly int InventoryThumbnailSizeX = 40;
		public readonly int InventoryThumbnailSizeY = 40;
		public readonly int InventoryLabelHeight = 16;

		public readonly int SpacingLarge = 10;
		public readonly int SpacingSmall = 5;

		public readonly Color Background = Color.FromArgb(0xff, 0x00, 0x33, 0x44);
		public readonly Color ScrollbarBorderColor = Color.FromArgb(0xff, 0xbb, 0xee, 0xff);
		public readonly Color ScrollbarThumbColor = Color.FromArgb(0x88, 0x00, 0xaa, 0xdd);

		public readonly ShadowSkin BrightShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xbb, 0xee, 0xff),
			shadowColor: Color.FromArgb(0x88, 0x00, 0xaa, 0xdd),
			shadowTransparent: Color.FromArgb(0x00, 0x0, 0xaa, 0xdd)
		);

		public readonly ShadowSkin DarkShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0x00, 0xaa, 0xdd),
			shadowColor: Color.FromArgb(0x88, 0x00, 0xaa, 0xdd),
			shadowTransparent: Color.FromArgb(0x00, 0x00, 0xaa, 0xdd)
		);

		public readonly ShadowSkin ActiveShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xee, 0x77, 0x00),
			shadowColor: Color.FromArgb(0x88, 0xee, 0x77, 0x00),
			shadowTransparent: Color.FromArgb(0x00, 0xee, 0x77, 0x00)
		);

		public readonly ShadowSkin DisabledShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0x88, 0x88, 0x88),
			shadowColor: Color.FromArgb(0x88, 0x88, 0x88, 0x88),
			shadowTransparent: Color.FromArgb(0x00, 0x88, 0x88, 0x88)
		);

		public readonly ShadowSkin HoverShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xff, 0xff, 0x00),
			shadowColor: Color.FromArgb(0x88, 0xff, 0xff, 0x00),
			shadowTransparent: Color.FromArgb(0x00, 0xff, 0xff, 0x00)
		);
	}
}

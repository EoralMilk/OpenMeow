using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Primitives;

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

	public static class Skin
	{
		public const int InGameUiWidth = 1280;
		public const int InGameUiHeight = 200;
		public const string InGameUiFont = "Ethnocentric.12";
		public const string InGameUiFontSmall = "Ethnocentric.11";
		public const string InGameUiFontLittle = "Ethnocentric.10";
		public const string InGameUiFontTiny = "Ethnocentric.8";

		public static string[] Fontsmall = new string[] { Skin.InGameUiFontSmall, Skin.InGameUiFontLittle, Skin.InGameUiFontTiny};

		public const int ScrollbarWidth = 10;
		public const int ScrollbarBorderWidth = 1;

		public const int ActorIconHeight = 48;
		public const int ActorIconWidth = 64;
		public const int ActorHealthDiv = 100;
		public const int ActorExpDiv = 100;

		public const int MinimapWidth = 180;

		public const int ResourcesWidth = 105;
		public const int ResourceHeight = 25;

		public const int SquadsWidth = 235;
		public const int SquadLabelHeight = 15;
		public const int SquadThumbnailSize = 50;

		public const int CharacterWidth = 100;
		public const int CharacterPreviewHeight = 110;
		public const int CharacterLabelHeight = 14;

		public const int ActorNameHeight = 20;

		public const int ActionsWidth = 180;
		public const int ActionsButtonSize = 50;

		public const int InventoryWidth = 150;
		public const int InventoryThumbnailSizeX = 40;
		public const int InventoryThumbnailSizeY = 40;
		public const int InventoryLabelHeight = 16;

		public const int SpacingLarge = 10;
		public const int SpacingSmall = 5;

		public static Color Background = Color.FromArgb(0xff, 0x00, 0x33, 0x44);
		public static readonly Color ScrollbarBorderColor = Color.FromArgb(0xff, 0xbb, 0xee, 0xff);
		public static readonly Color ScrollbarThumbColor = Color.FromArgb(0x88, 0x00, 0xaa, 0xdd);

		public static readonly ShadowSkin BrightShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xbb, 0xee, 0xff),
			shadowColor: Color.FromArgb(0x88, 0x00, 0xaa, 0xdd),
			shadowTransparent: Color.FromArgb(0x00, 0x0, 0xaa, 0xdd)
		);

		public static readonly ShadowSkin DarkShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0x00, 0xaa, 0xdd),
			shadowColor: Color.FromArgb(0x88, 0x00, 0xaa, 0xdd),
			shadowTransparent: Color.FromArgb(0x00, 0x00, 0xaa, 0xdd)
		);

		public static readonly ShadowSkin ActiveShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xee, 0x77, 0x00),
			shadowColor: Color.FromArgb(0x88, 0xee, 0x77, 0x00),
			shadowTransparent: Color.FromArgb(0x00, 0xee, 0x77, 0x00)
		);

		public static readonly ShadowSkin DisabledShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0x88, 0x88, 0x88),
			shadowColor: Color.FromArgb(0x88, 0x88, 0x88, 0x88),
			shadowTransparent: Color.FromArgb(0x00, 0x88, 0x88, 0x88)
		);

		public static readonly ShadowSkin HoverShadowSkin = new ShadowSkin
		(
			borderSize: 1,
			shadowSize: 5,
			borderColor: Color.FromArgb(0xff, 0xff, 0xff, 0x00),
			shadowColor: Color.FromArgb(0x88, 0xff, 0xff, 0x00),
			shadowTransparent: Color.FromArgb(0x00, 0xff, 0xff, 0x00)
		);
	}
}

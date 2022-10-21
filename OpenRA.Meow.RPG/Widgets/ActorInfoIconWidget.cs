using System;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Meow.RPG.Widgets
{
	public class ActorInfoIconWidget : Widget
	{
		public readonly string DefaultIconImage = "icon";
		public readonly string DefaultIconSequence = "none";

		[PaletteReference(nameof(IconPaletteIsPlayerPalette))]
		[Desc("Palette used for the production icon.")]
		public readonly string IconPalette = "chrome";

		[Desc("Custom palette is a player palette BaseName")]
		public readonly bool IconPaletteIsPlayerPalette = false;

		readonly PaletteReference palette;

		public int2 IconOffset = new int2(4,4);

		public string Background = "unitinfo";

		public readonly string TooltipContainer = "TOOLTIP_CONTAINER";
		public readonly string TooltipTemplate = "UNIT_INFO_TOOLTIP";

		public readonly string ActorInfoTemplate = "UNIT_INFO";

		readonly WorldRenderer worldRenderer;

		Animation icon;
		Lazy<TooltipContainerWidget> tooltipContainer;
		Lazy<ActorInfoWidget> actorInfoMainContainer;

		readonly Player player;
		readonly World world;

		[ObjectCreator.UseCtor]
		public ActorInfoIconWidget(ModData modData, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			player = world.LocalPlayer;

			tooltipContainer = Exts.Lazy(() =>
			Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
			actorInfoMainContainer = Exts.Lazy(() =>
			Ui.Root.Get<ActorInfoWidget>(ActorInfoTemplate));

			palette = worldRenderer.Palette(IconPaletteIsPlayerPalette ? IconPalette + player.InternalName : IconPalette);
		}

		public override string GetCursor(int2 pos) { return null; }

		public Func<KeyInput, bool> OnKeyPress = _ => false;

		public override bool HandleKeyPress(KeyInput e) { return OnKeyPress(e); }

		public void RefreshIcons()
		{
			if (actorInfoMainContainer.Value.TooltipUnit == null)
				icon = null;
			else
				icon = actorInfoMainContainer.Value.TooltipUnit.GetDisplayIcon(worldRenderer);

			if (icon == null)
			{
				icon = new Animation(worldRenderer.World, DefaultIconImage);
				icon.Play(DefaultIconSequence);
			}
		}

		public override void Draw()
		{
			if (actorInfoMainContainer.Value.TooltipUnit == null)
				return;

			WidgetUtils.DrawPanel(Background, RenderBounds);

			// Icons
			Game.Renderer.EnableAntialiasingFilter();

			if (icon != null && icon.Image != null)
			{
				WidgetUtils.DrawSpriteCentered(icon.Image,
					actorInfoMainContainer.Value.TooltipUnit.Palette == null ? palette : actorInfoMainContainer.Value.TooltipUnit.Palette,
					RenderBounds.Location + new int2(RenderBounds.Width / 2, RenderBounds.Height / 2), 2f);
			}

			Game.Renderer.DisableAntialiasingFilter();
		}

		public override void Tick()
		{
			RefreshIcons();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return actorInfoMainContainer.Value.TooltipUnit != null && EventBounds.Contains(mi.Location);
		}

		public override void MouseEntered()
		{
			if (TooltipContainer != null)
			{
				tooltipContainer.Value.SetTooltip(TooltipTemplate,
						new WidgetArgs() { { "player", world.LocalPlayer }, { "getTooltipUnit", actorInfoMainContainer.Value.GetTooltipUnit }, { "world", world } });
			}
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null)
			{
				tooltipContainer.Value.RemoveTooltip();
			}
		}
	}
}

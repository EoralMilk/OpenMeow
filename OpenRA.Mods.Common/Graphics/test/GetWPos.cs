using System;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class GetWPosInfo : ConditionalTraitInfo
	{
		public override object Create(ActorInitializer init) { return new GetWPos(init.Self,this); }
	}

	public class GetWPos : ConditionalTrait<GetWPosInfo>, ITick
	{
		IPositionable positionable;
		IFacing facing;
		public GetWPos(Actor self, GetWPosInfo info)
			: base(info) {
			positionable = self.TraitOrDefault< IPositionable>();
			facing = self.TraitOrDefault<IFacing>();
		}

		public void Tick(Actor self)
		{
			if (positionable != null)
			{
				if (Game.Renderer.Standalone3DRenderer != null)
					Game.Renderer.Standalone3DRenderer.TestPos = positionable.CenterPosition;
				else
					Console.WriteLine(self.ActorID + " Game.Renderer.Standalone3DRenderer == null");
			}
			else
				Console.WriteLine(self.ActorID + " has no IPositonable");

			if (facing != null)
			{
				if (Game.Renderer.Standalone3DRenderer != null)
					Game.Renderer.Standalone3DRenderer.TestRot = facing.Orientation;
				else
					Console.WriteLine(self.ActorID + " Game.Renderer.Standalone3DRenderer == null");
			}
			else
				Console.WriteLine(self.ActorID + " has no IFacing");
		}
	}
}

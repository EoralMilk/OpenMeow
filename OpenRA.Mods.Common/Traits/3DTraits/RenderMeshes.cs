using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public class RenderMeshesInfo : TraitInfo, Requires<BodyOrientationInfo>
	{
		[Desc("Defaults to the actor name.")]
		public readonly string Image = null;

		[Desc("Change size.")]
		public readonly float Scale = 1;

		public readonly int ZOffset = 1;

		public override object Create(ActorInitializer init) { return new RenderMeshes(init.Self, this); }
	}

	public class RenderMeshes : IRender, ITick, INotifyOwnerChanged, INotifyCreated
	{
		public readonly RenderMeshesInfo Info;
		readonly List<MeshInstance> meshes = new List<MeshInstance>();
		public readonly World3DRenderer W3dr;
		bool hasSkeleton;
		readonly Dictionary<string, WithSkeleton> withSkeletons = new Dictionary<string, WithSkeleton>();
		readonly Actor self;
		Color remap;
		bool inScreen = false;
		public RenderMeshes(Actor self, RenderMeshesInfo info)
		{
			this.self = self;
			Info = info;
			W3dr = Game.Renderer.World3DRenderer;
		}

		public void Created(Actor self)
		{
			foreach (var ws in self.TraitsImplementing<WithSkeleton>())
			{
				withSkeletons.Add(ws.Name, ws);
			}

			hasSkeleton = withSkeletons.Count > 0;
		}

		bool initializePalettes = true;
		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { initializePalettes = true; }

		void ITick.Tick(Actor self)
		{
			foreach (var wskv in withSkeletons)
			{
				if (!wskv.Value.Draw)
					wskv.Value.Drawtick = 1;
				wskv.Value.Draw = inScreen;
			}

			inScreen = false;
		}

		/// <summary>
		/// call WithSkeletons for update
		/// </summary>
		public void CallWhenInSceen()
		{
			inScreen = true;
		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			if (initializePalettes)
			{
				remap = self.Owner.Color;
				initializePalettes = false;
			}

			if (hasSkeleton)
			{
				foreach (var mesh in meshes)
				{
					if (mesh.SkeletonBinded != null)
					{
						if (withSkeletons.ContainsKey(mesh.SkeletonBinded))
						{
							mesh.DrawId = withSkeletons[mesh.SkeletonBinded].GetDrawId();
							//if (maxDrawId < mesh.DrawId)
							//	maxDrawId = mesh.DrawId;
						}
					}
				}
			}

			return new IRenderable[]
			{
				new MeshRenderable(meshes, self.CenterPosition, Info.ZOffset, remap, Info.Scale, this)
			};
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			var pos = self.CenterPosition;
			foreach (var c in meshes)
				if (c.IsVisible())
					yield return c.ScreenBounds(pos, wr, Info.Scale);
		}

		public string Image => Info.Image ?? self.Info.Name;

		public void Add(MeshInstance m)
		{
			meshes.Add(m);
		}

		public void Remove(MeshInstance m)
		{
			meshes.Remove(m);
		}
	}
}

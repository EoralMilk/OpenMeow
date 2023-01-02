using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Trait3D
{
	public interface IRenderMeshesUpdate
	{
		void RenderUpdateMeshes(Actor self);
	}

	public class RenderMeshesInfo : TraitInfo
	{
		[Desc("Defaults to the actor name.")]
		public readonly string Image = null;

		[Desc("Change size.")]
		public readonly float Scale = 1;

		public readonly int ZOffset = 1;

		public override object Create(ActorInitializer init) { return new RenderMeshes(init.Self, this); }
	}

	public class RenderMeshes : IRender, INotifyOwnerChanged, INotifyCreated
	{
		public readonly RenderMeshesInfo Info;
		readonly List<MeshInstance> meshes = new List<MeshInstance>();
		public readonly World3DRenderer W3dr;
		bool hasSkeleton;
		readonly Dictionary<string, WithSkeleton> withSkeletons = new Dictionary<string, WithSkeleton>();
		readonly Actor self;
		Color remap;
		bool created = false;

		public ITwistActorMesh[] AllTwistor;
		IRenderMeshesUpdate[] renderMeshesUpdates;

		public float RenderAlpha = 1;

		public RenderMeshes(Actor self, RenderMeshesInfo info)
		{
			this.self = self;
			Info = info;
			W3dr = Game.Renderer.World3DRenderer;
		}

		public void Created(Actor self)
		{
			renderMeshesUpdates = self.TraitsImplementing<IRenderMeshesUpdate>().ToArray();
			AllTwistor = self.TraitsImplementing<ITwistActorMesh>().ToArray();

			foreach (var ws in self.TraitsImplementing<WithSkeleton>())
			{
				withSkeletons.Add(ws.Name, ws);
			}

			hasSkeleton = withSkeletons.Count > 0;

			if (hasSkeleton)
			{
				foreach (var mesh in meshes)
				{
					if (mesh.SkeletonBinded != null)
					{
						if (withSkeletons.ContainsKey(mesh.SkeletonBinded))
						{
							mesh.DrawId = () => withSkeletons[mesh.SkeletonBinded].GetDrawId();
							mesh.Matrix = () => withSkeletons[mesh.SkeletonBinded].Skeleton.Offset.ToMat4();
							mesh.UseMatrix = true;
						}
					}
				}
			}

			created = true;
		}

		bool initializePalettes = true;
		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { initializePalettes = true; }

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			if (initializePalettes)
			{
				remap = self.Owner.Color;
				initializePalettes = false;
			}

			foreach (var ru in renderMeshesUpdates)
				ru.RenderUpdateMeshes(self);

			bool twist = false;
			foreach (var t in AllTwistor)
			{
				if (t.IsTwisting)
				{
					twist = true;
					break;
				}
			}

			var tint = float3.Ones;
			if (twist)
			{
				tint = float3.Lerp(0.75f * Color.ToFloat3(remap) + float3.Half, tint, Game.Renderer.TwistMul);
			}

			if (created && meshes != null && meshes.Count > 0)
				yield return new MeshRenderable(meshes, self.CenterPosition, Info.ZOffset, remap, Info.Scale, RenderAlpha, tint, TintModifiers.None, this, twist);
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

			if (hasSkeleton && created)
			{
				foreach (var mesh in meshes)
				{
					if (mesh.SkeletonBinded != null)
					{
						if (withSkeletons.ContainsKey(mesh.SkeletonBinded))
						{
							mesh.DrawId = () => withSkeletons[mesh.SkeletonBinded].GetDrawId();
							mesh.Matrix = () => withSkeletons[mesh.SkeletonBinded].Skeleton.Offset.ToMat4();
							mesh.UseMatrix = true;
						}
					}
				}
			}

		}

		public void Remove(MeshInstance m)
		{
			meshes.Remove(m);
		}


	}
}

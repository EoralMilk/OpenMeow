#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{

	public class WithMeshInfo : ConditionalTraitInfo, Requires<RenderMeshesInfo>
	{
		public readonly string Mesh = "idle";
		public override object Create(ActorInitializer init) { return new WithMesh(init.Self, this); }
	}

	public class WithMesh : ConditionalTrait<WithMeshInfo>
	{
		protected readonly MeshInstance MeshInstance;
		protected readonly RenderMeshes RenderMeshes;

		public WithMesh(Actor self, WithMeshInfo info)
			: base(info)
		{
			var body = self.Trait<BodyOrientation>();
			RenderMeshes = self.Trait<RenderMeshes>();

			var mesh = self.World.MeshCache.GetMeshSequence(RenderMeshes.Image, info.Mesh);
			MeshInstance = new MeshInstance(mesh, () => WVec.Zero,
				() => body.QuantizeOrientation(self.Orientation),
				() => !IsTraitDisabled);

			RenderMeshes.Add(MeshInstance);
		}
	}

	public class WithMeshBodyInfo : WithMeshInfo
	{
		public override object Create(ActorInitializer init) { return new WithMeshBody(init.Self, this); }
	}

	public class WithMeshBody : WithMesh
	{
		/// <summary>
		/// The first 9 bit are 1
		/// </summary>
		int partMask = 0x1FF;

		public WithMeshBody(Actor self, WithMeshBodyInfo info)
			: base(self, info)
		{
		}

		public void SetPartDisable(int mask)
		{
			partMask = partMask & (~mask);
			MeshInstance.DrawMask = partMask;
		}

		public void SetPartEnable(int mask)
		{
			partMask = partMask | mask;
			MeshInstance.DrawMask = partMask;
		}
	}

	public class WithMeshClothInfo : WithMeshInfo, Requires<WithMeshBodyInfo>
	{
		public readonly int Mask = 0;
		public override object Create(ActorInitializer init) { return new WithMeshCloth(init.Self, this); }
	}

	public class WithMeshCloth : WithMesh
	{
		WithMeshBody withMeshBody;
		readonly int mask;

		public WithMeshCloth(Actor self, WithMeshClothInfo info)
			: base(self, info)
		{
			mask = info.Mask;
		}

		protected override void Created(Actor self)
		{
			withMeshBody = self.Trait<WithMeshBody>();

			base.Created(self);
		}

		protected override void TraitEnabled(Actor self)
		{
			base.TraitEnabled(self);

			withMeshBody.SetPartDisable(mask);
		}

		protected override void TraitDisabled(Actor self)
		{
			base.TraitDisabled(self);

			withMeshBody.SetPartEnable(mask);
		}
	}
}

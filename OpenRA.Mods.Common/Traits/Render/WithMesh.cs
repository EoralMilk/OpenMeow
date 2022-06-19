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
		readonly MeshInstance meshInstance;
		readonly RenderMeshes rm;

		public WithMesh(Actor self, WithMeshInfo info)
			: base(info)
		{
			var body = self.Trait<BodyOrientation>();
			rm = self.Trait<RenderMeshes>();

			var mesh = self.World.MeshCache.GetMeshSequence(rm.Image, info.Mesh);
			meshInstance = new MeshInstance(mesh, () => WVec.Zero,
				() => body.QuantizeOrientation(self.Orientation),
				() => !IsTraitDisabled);

			rm.Add(meshInstance);
		}
	}
}

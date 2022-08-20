using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Graphics;

namespace OpenRA
{
	public class TerrainPatch
	{
		public readonly string Name;
		public readonly int TypeId;
		public readonly IOrderedMesh[] Meshes;

		public TerrainPatchInstance[] TerrainPatchInstances { get; private set; }

		public void Draw()
		{
			foreach (var i in TerrainPatchInstances)
			{
				if (i.ToDraw)
				{
					i.AddInstance();
				}

			}
		}

		public void Flush()
		{
			foreach (var i in TerrainPatchInstances)
				i.ToDraw = false;
		}
	}

	public class TerrainPatchInstance
	{
		public readonly TerrainPatch Type;
		public readonly MPos[] CoveringCells;
		public readonly float[] Transform;
		public bool ToDraw = false;

		/// <summary>
		/// wip
		/// </summary>
		public void AddInstance()
		{
			float[] data = new float[0];
			int[] idata = new int[0];

			foreach (var mesh in Type.Meshes)
			{
				mesh.AddInstanceData(Transform, 16, idata, 0);
			}
		}
	}
}

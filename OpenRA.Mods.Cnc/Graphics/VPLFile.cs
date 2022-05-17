using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenRA.Mods.RA2.Graphics
{
	struct VPLHeader
	{
		public int NRemapStart;
		public int NRemapEnd;
		public int NSections;
		public int DwUnk;
	}

	public class VPLSectionTable
	{
		public const int SectionIndexCount = 256;
		public byte[] Table = new byte[SectionIndexCount];
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	struct ColorStruct
	{
		public byte R;
		public byte G;
		public byte B;
	}

	public class VPLFile
	{
		private const int ColorStructCount = 256;
		VPLHeader header;

		List<VPLSectionTable> sections;
		readonly ColorStruct[] containedPal = new ColorStruct[ColorStructCount];
		public void LoadFromFile(string file)
		{
			var f = Game.ModData.DefaultFileSystem.Open(file);
			StreamReader r = new StreamReader(f);
			byte[] header = new byte[System.Runtime.InteropServices.Marshal.SizeOf(typeof(VPLHeader))];
			f.Read(header, 0, header.Length);
			this.header = ByteToStructure<VPLHeader>(header);
			sections = new List<VPLSectionTable>(this.header.NSections);

			byte[] b = new byte[3];
			for (int i = 0; i < ColorStructCount; i++)
			{
				f.Read(b, 0, 3);
				ColorStruct s;
				s.R = (byte)(b[0] << 2);
				s.G = (byte)(b[1] << 2);
				s.B = (byte)(b[2] << 2);
				containedPal[i] = s;
			}

			for (int i = 0; i < this.header.NSections; i++)
			{
				VPLSectionTable table = new VPLSectionTable();
				f.Read(table.Table, 0, table.Table.Length);
				sections.Add(table);
			}

			f.Close();
		}

		private T ByteToStructure<T>(byte[] dataBuffer)
		{
			object structure = null;
			int size = Marshal.SizeOf(typeof(T));
			IntPtr allocIntPtr = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.Copy(dataBuffer, 0, allocIntPtr, size);
				structure = Marshal.PtrToStructure(allocIntPtr, typeof(T));
			}
			finally
			{
				Marshal.FreeHGlobal(allocIntPtr);
			}

			return (T)structure;
		}

		bool IsLoaded()
		{
			return sections.Any();
		}

		public int GetSectionCount()
		{
			return sections.Count;
		}

		public VPLSectionTable this[int index]
		{
			get
			{
				return sections[index];
			}
		}
	}

}

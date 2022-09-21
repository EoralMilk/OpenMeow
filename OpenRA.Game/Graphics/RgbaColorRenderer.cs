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
using System.Linq;
using GlmSharp;
using OpenRA.Primitives;
using OpenRA.Primitives.FixPoint;

namespace OpenRA.Graphics
{
	public class RgbaColorRenderer
	{
		static readonly float3 ScreenOffset = new float3(0.5f, 0.5f, 0);
		static float3 CamUpOffset;
		static float3 CamRightOffset;
		static float3 ViewZOffset;

		readonly SpriteRenderer parent;
		readonly Vertex[] vertices = new Vertex[6];
		readonly Vertex[] verticesDouble = new Vertex[12];

		public RgbaColorRenderer(SpriteRenderer parent)
		{
			this.parent = parent;
		}

		public void UpdateWorldRenderOffset(World3DRenderer wr)
		{
			var offset = wr.InverseCameraFrontMeterPerWPos * 10;

			ViewZOffset = new float3(offset.x, offset.y, offset.z);
			CamUpOffset = new float3(wr.CameraUp.x, wr.CameraUp.y, wr.CameraUp.z);
			CamRightOffset = new float3(wr.CameraRight.x, wr.CameraRight.y, wr.CameraRight.z);
		}

		public void DrawScreenLine(in float3 start, in float3 end, float width, Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawLine(start, end, width, startColor, endColor, blendMode, false);
		}

		public void DrawWorldLine(in float3 start, in float3 end, float width, Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawLine(start, end, width, startColor, endColor, blendMode, true);
		}

		public void DrawWorldPoint(in WPos pos, float width, Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha)
		{
			var offset = new WVec(0, 0, 1);
			var offset2 = new WVec(1, 0, 0);

			DrawLine(Render3DPosition(pos - offset), Render3DPosition(pos + offset), width, startColor, endColor, blendMode, true);
			DrawLine(Render3DPosition(pos - offset2), Render3DPosition(pos + offset2), width, startColor, endColor, blendMode, true);
		}

		public void DrawWorldLine(in WPos start, in WPos end, float width, Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawLine(Render3DPosition(start), Render3DPosition(end), width, startColor, endColor, blendMode, true);
		}

		public void DrawWorldLine(in float3 startUp, in float3 startDown, in float3 endUp, in float3 endDown,
			Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha)
		{
			startColor = Util.PremultiplyAlpha(startColor);
			var sr = startColor.R / 255.0f;
			var sg = startColor.G / 255.0f;
			var sb = startColor.B / 255.0f;
			var sa = startColor.A / 255.0f;

			endColor = Util.PremultiplyAlpha(endColor);
			var er = endColor.R / 255.0f;
			var eg = endColor.G / 255.0f;
			var eb = endColor.B / 255.0f;
			var ea = endColor.A / 255.0f;

			vertices[0] = new Vertex(startDown + ViewZOffset, sr, sg, sb, sa, 0, 0);
			vertices[1] = new Vertex(startUp + ViewZOffset, sr, sg, sb, sa, 0, 0);
			vertices[2] = new Vertex(endUp + ViewZOffset, er, eg, eb, ea, 0, 0);
			vertices[3] = new Vertex(endUp + ViewZOffset, er, eg, eb, ea, 0, 0);
			vertices[4] = new Vertex(endDown + ViewZOffset, er, eg, eb, ea, 0, 0);
			vertices[5] = new Vertex(startDown + ViewZOffset, sr, sg, sb, sa, 0, 0);
			parent.DrawRGBAVertices(vertices, blendMode);
		}

		public void DrawWorldLine(in float3 startUp, in float3 start, in float3 startDown,
			in float3 endUp, in float3 end, in float3 endDown,
			Color startColor, Color endColor,
			Color startColorOuter, Color endColorOuter,
			BlendMode blendMode = BlendMode.Alpha)
		{
			startColor = Util.PremultiplyAlpha(startColor);
			var sr = startColor.R / 255.0f;
			var sg = startColor.G / 255.0f;
			var sb = startColor.B / 255.0f;
			var sa = startColor.A / 255.0f;

			startColorOuter = Util.PremultiplyAlpha(startColorOuter);
			var osr = startColorOuter.R / 255.0f;
			var osg = startColorOuter.G / 255.0f;
			var osb = startColorOuter.B / 255.0f;
			var osa = startColorOuter.A / 255.0f;

			endColor = Util.PremultiplyAlpha(endColor);
			var er = endColor.R / 255.0f;
			var eg = endColor.G / 255.0f;
			var eb = endColor.B / 255.0f;
			var ea = endColor.A / 255.0f;

			endColorOuter = Util.PremultiplyAlpha(endColorOuter);
			var oer = endColorOuter.R / 255.0f;
			var oeg = endColorOuter.G / 255.0f;
			var oeb = endColorOuter.B / 255.0f;
			var oea = endColorOuter.A / 255.0f;

			verticesDouble[0] = new Vertex(start + ViewZOffset, sr, sg, sb, sa, 0, 0);
			verticesDouble[1] = new Vertex(startUp + ViewZOffset, osr, osg, osb, osa, 0, 0);
			verticesDouble[2] = new Vertex(endUp + ViewZOffset, oer, oeg, oeb, oea, 0, 0);
			verticesDouble[3] = new Vertex(endUp + ViewZOffset, oer, oeg, oeb, oea, 0, 0);
			verticesDouble[4] = new Vertex(end + ViewZOffset, er, eg, eb, ea, 0, 0);
			verticesDouble[5] = new Vertex(start + ViewZOffset, sr, sg, sb, sa, 0, 0);

			verticesDouble[6] = new Vertex(startDown + ViewZOffset, osr, osg, osb, osa, 0, 0);
			verticesDouble[7] = new Vertex(start + ViewZOffset, sr, sg, sb, sa, 0, 0);
			verticesDouble[8] = new Vertex(end + ViewZOffset, er, eg, eb, ea, 0, 0);
			verticesDouble[9] = new Vertex(end + ViewZOffset, er, eg, eb, ea, 0, 0);
			verticesDouble[10] = new Vertex(endDown + ViewZOffset, oer, oeg, oeb, oea, 0, 0);
			verticesDouble[11] = new Vertex(startDown + ViewZOffset, osr, osg, osb, osa, 0, 0);
			parent.DrawRGBAVertices(verticesDouble, blendMode);
		}

		public float3 Render3DPosition(WPos pos)
		{
			return new float3(-(float)pos.X / Game.Renderer.World3DRenderer.WDistPerMeter, (float)pos.Y / Game.Renderer.World3DRenderer.WDistPerMeter, (float)pos.Z / Game.Renderer.World3DRenderer.WDistPerMeter);
		}

		void DrawLine(in float3 start, in float3 end, float width, Color startColor, Color endColor, BlendMode blendMode = BlendMode.Alpha, bool world = false)
		{
			startColor = Util.PremultiplyAlpha(startColor);
			var sr = startColor.R / 255.0f;
			var sg = startColor.G / 255.0f;
			var sb = startColor.B / 255.0f;
			var sa = startColor.A / 255.0f;

			endColor = Util.PremultiplyAlpha(endColor);
			var er = endColor.R / 255.0f;
			var eg = endColor.G / 255.0f;
			var eb = endColor.B / 255.0f;
			var ea = endColor.A / 255.0f;

			if (world)
			{
				if (end == start)
					return;

				var dir = World3DCoordinate.Float3toVec3(end - start).Normalized;
				var cam = Game.Renderer.World3DRenderer.InverseCameraFront;
				vec3 cross;
				if (dir == cam)
					cross = Game.Renderer.World3DRenderer.CameraUp;
				else
					cross = vec3.Cross(cam, dir).Normalized;
				var widthOffset = World3DCoordinate.Vec3toFloat3(cross * (width / 2));
				//var widthOffset = (width / 2) * Game.Renderer.World3DRenderer.CameraUp;

				var sup = start + widthOffset;
				var sdown = start - widthOffset;
				var eup = end + widthOffset;
				var edown = end - widthOffset;
				vertices[0] = new Vertex(sdown + ViewZOffset, sr, sg, sb, sa, 0, 0);
				vertices[1] = new Vertex(sup + ViewZOffset, sr, sg, sb, sa, 0, 0);
				vertices[2] = new Vertex(eup + ViewZOffset, er, eg, eb, ea, 0, 0);
				vertices[3] = new Vertex(eup + ViewZOffset, er, eg, eb, ea, 0, 0);
				vertices[4] = new Vertex(edown + ViewZOffset, er, eg, eb, ea, 0, 0);
				vertices[5] = new Vertex(sdown + ViewZOffset, sr, sg, sb, sa, 0, 0);
				parent.DrawRGBAVertices(vertices, blendMode);
			}
			else
			{
				var corner = width / 2 * CamUpOffset;

				vertices[0] = new Vertex(start - corner + ScreenOffset, sr, sg, sb, sa, 0, 0);
				vertices[1] = new Vertex(start + corner + ScreenOffset, sr, sg, sb, sa, 0, 0);
				vertices[2] = new Vertex(end + corner + ScreenOffset, er, eg, eb, ea, 0, 0);
				vertices[3] = new Vertex(end + corner + ScreenOffset, er, eg, eb, ea, 0, 0);
				vertices[4] = new Vertex(end - corner + ScreenOffset, er, eg, eb, ea, 0, 0);
				vertices[5] = new Vertex(start - corner + ScreenOffset, sr, sg, sb, sa, 0, 0);
				parent.DrawRGBAVertices(vertices, blendMode);
			}
		}

		public void DrawScreenLine(in float3 start, in float3 end, float width, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawLine(start, end, width, color, color, blendMode, false);
		}

		public void DrawWorldLine(in float3 start, in float3 end, float width, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawLine(start, end, width, color, color, blendMode, true);
		}

		/// <summary>
		/// Calculate the 2D intersection of two lines.
		/// Will behave badly if the lines are parallel.
		/// Z position is the average of a and b (ignores actual intersection point if it exists)
		/// </summary>
		float3 IntersectionOf(in float3 a, in float3 da, in float3 b, in float3 db)
		{
			var crossA = a.X * (a.Y + da.Y) - a.Y * (a.X + da.X);
			var crossB = b.X * (b.Y + db.Y) - b.Y * (b.X + db.X);
			var x = da.X * crossB - db.X * crossA;
			var y = da.Y * crossB - db.Y * crossA;
			var d = da.X * db.Y - da.Y * db.X;
			return new float3(x / d, y / d, 0.5f * (a.Z + b.Z));
		}

		void DrawDisconnectedLine(IEnumerable<float3> points, float width, Color color, BlendMode blendMode, bool world = false)
		{
			using (var e = points.GetEnumerator())
			{
				if (!e.MoveNext())
					return;

				var lastPoint = e.Current;
				while (e.MoveNext())
				{
					var point = e.Current;
					if (world)
						DrawWorldLine(lastPoint, point, width, color, blendMode);
					else
						DrawScreenLine(lastPoint, point, width, color, blendMode);
					lastPoint = point;
				}
			}
		}

		void DrawConnectedLine(float3[] points, float width, Color color, bool closed, BlendMode blendMode, bool world = false)
		{
			// Not a line
			if (points.Length < 2)
				return;

			// Single segment
			if (points.Length == 2)
			{
				DrawScreenLine(points[0], points[1], width, color, blendMode);
				return;
			}

			color = Util.PremultiplyAlpha(color);
			var r = color.R / 255.0f;
			var g = color.G / 255.0f;
			var b = color.B / 255.0f;
			var a = color.A / 255.0f;

			var start = points[0];
			var end = points[1];
			var dir = (end - start) / (end - start).XY.Length;
			var corner = width / 2 * new float3(-dir.Y, dir.X, dir.Z);

			// Corners for start of line segment
			var ca = start - corner;
			var cb = start + corner;

			// Segment is part of closed loop
			if (closed)
			{
				var prev = points[points.Length - 1];
				var prevDir = (start - prev) / (start - prev).XY.Length;
				var prevCorner = width / 2 * new float3(-prevDir.Y, prevDir.X, prevDir.Z);
				ca = IntersectionOf(start - prevCorner, prevDir, start - corner, dir);
				cb = IntersectionOf(start + prevCorner, prevDir, start + corner, dir);
			}

			var limit = closed ? points.Length : points.Length - 1;
			for (var i = 0; i < limit; i++)
			{
				var next = points[(i + 2) % points.Length];
				var nextDir = (next - end) / (next - end).XY.Length;
				var nextCorner = width / 2 * new float3(-nextDir.Y, nextDir.X, nextDir.Z);

				// Vertices for the corners joining start-end to end-next
				var cc = closed || i < limit - 1 ? IntersectionOf(end + corner, dir, end + nextCorner, nextDir) : end + corner;
				var cd = closed || i < limit - 1 ? IntersectionOf(end - corner, dir, end - nextCorner, nextDir) : end - corner;

				// Fill segment
				if (world)
				{
					vertices[0] = new Vertex(ca + ViewZOffset, r, g, b, a, 0, 0);
					vertices[1] = new Vertex(cb + ViewZOffset, r, g, b, a, 0, 0);
					vertices[2] = new Vertex(cc + ViewZOffset, r, g, b, a, 0, 0);
					vertices[3] = new Vertex(cc + ViewZOffset, r, g, b, a, 0, 0);
					vertices[4] = new Vertex(cd + ViewZOffset, r, g, b, a, 0, 0);
					vertices[5] = new Vertex(ca + ViewZOffset, r, g, b, a, 0, 0);
				}
				else
				{
					vertices[0] = new Vertex(ca + ScreenOffset, r, g, b, a, 0, 0);
					vertices[1] = new Vertex(cb + ScreenOffset, r, g, b, a, 0, 0);
					vertices[2] = new Vertex(cc + ScreenOffset, r, g, b, a, 0, 0);
					vertices[3] = new Vertex(cc + ScreenOffset, r, g, b, a, 0, 0);
					vertices[4] = new Vertex(cd + ScreenOffset, r, g, b, a, 0, 0);
					vertices[5] = new Vertex(ca + ScreenOffset, r, g, b, a, 0, 0);
				}

				parent.DrawRGBAVertices(vertices, blendMode);

				// Advance line segment
				end = next;
				dir = nextDir;
				corner = nextCorner;

				ca = cd;
				cb = cc;
			}
		}

		public void DrawScreenLine(IEnumerable<float3> points, float width, Color color, bool connectSegments = false, BlendMode blendMode = BlendMode.Alpha)
		{
			if (!connectSegments)
				DrawDisconnectedLine(points, width, color, blendMode);
			else
				DrawConnectedLine(points as float3[] ?? points.ToArray(), width, color, false, blendMode);
		}

		public void DrawWorldLine(IEnumerable<float3> points, float width, Color color, bool connectSegments = false, BlendMode blendMode = BlendMode.Alpha)
		{
			if (!connectSegments)
				DrawDisconnectedLine(points, width, color, blendMode, true);
			else
				DrawConnectedLine(points as float3[] ?? points.ToArray(), width, color, false, blendMode, true);
		}

		public void DrawPolygon(float3[] vertices, float width, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawConnectedLine(vertices, width, color, true, blendMode);
		}

		public void DrawPolygon(float2[] vertices, float width, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			DrawConnectedLine(vertices.Select(v => new float3(v, 0)).ToArray(), width, color, true, blendMode);
		}

		public void DrawRect(in float3 tl, in float3 br, float width, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			var tr = new float3(br.X, tl.Y, tl.Z);
			var bl = new float3(tl.X, br.Y, br.Z);
			DrawPolygon(new[] { tl, tr, br, bl }, width, color, blendMode);
		}

		public void FillTriangle(in float3 a, in float3 b, in float3 c, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			color = Util.PremultiplyAlpha(color);
			var cr = color.R / 255.0f;
			var cg = color.G / 255.0f;
			var cb = color.B / 255.0f;
			var ca = color.A / 255.0f;

			vertices[0] = new Vertex(a + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[1] = new Vertex(b + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[2] = new Vertex(c + ScreenOffset, cr, cg, cb, ca, 0, 0);
			parent.DrawRGBAVertices(vertices, blendMode);
		}

		public void FillRect(in float3 tl, in float3 br, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			var tr = new float3(br.X, tl.Y, tl.Z);
			var bl = new float3(tl.X, br.Y, br.Z);
			FillRect(tl, tr, br, bl, color, blendMode);
		}

		public void FillWorldRect(in float3 a, in float3 b, in float3 c, in float3 d, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			color = Util.PremultiplyAlpha(color);
			var cr = color.R / 255.0f;
			var cg = color.G / 255.0f;
			var cb = color.B / 255.0f;
			var ca = color.A / 255.0f;

			vertices[0] = new Vertex(a + ViewZOffset, cr, cg, cb, ca, 0, 0);
			vertices[1] = new Vertex(b + ViewZOffset, cr, cg, cb, ca, 0, 0);
			vertices[2] = new Vertex(c + ViewZOffset, cr, cg, cb, ca, 0, 0);
			vertices[3] = new Vertex(c + ViewZOffset, cr, cg, cb, ca, 0, 0);
			vertices[4] = new Vertex(d + ViewZOffset, cr, cg, cb, ca, 0, 0);
			vertices[5] = new Vertex(a + ViewZOffset, cr, cg, cb, ca, 0, 0);
			parent.DrawRGBAVertices(vertices, blendMode);
		}

		public void FillRect(in float3 a, in float3 b, in float3 c, in float3 d, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			color = Util.PremultiplyAlpha(color);
			var cr = color.R / 255.0f;
			var cg = color.G / 255.0f;
			var cb = color.B / 255.0f;
			var ca = color.A / 255.0f;

			vertices[0] = new Vertex(a + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[1] = new Vertex(b + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[2] = new Vertex(c + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[3] = new Vertex(c + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[4] = new Vertex(d + ScreenOffset, cr, cg, cb, ca, 0, 0);
			vertices[5] = new Vertex(a + ScreenOffset, cr, cg, cb, ca, 0, 0);
			parent.DrawRGBAVertices(vertices, blendMode);
		}

		public void FillRect(in float3 a, in float3 b, in float3 c, in float3 d, Color topLeftColor, Color topRightColor, Color bottomRightColor, Color bottomLeftColor, BlendMode blendMode = BlendMode.Alpha)
		{
			vertices[0] = VertexWithColor(a + ScreenOffset, topLeftColor);
			vertices[1] = VertexWithColor(b + ScreenOffset, topRightColor);
			vertices[2] = VertexWithColor(c + ScreenOffset, bottomRightColor);
			vertices[3] = VertexWithColor(c + ScreenOffset, bottomRightColor);
			vertices[4] = VertexWithColor(d + ScreenOffset, bottomLeftColor);
			vertices[5] = VertexWithColor(a + ScreenOffset, topLeftColor);

			parent.DrawRGBAVertices(vertices, blendMode);
		}

		static Vertex VertexWithColor(in float3 xyz, Color color)
		{
			color = Util.PremultiplyAlpha(color);
			var cr = color.R / 255.0f;
			var cg = color.G / 255.0f;
			var cb = color.B / 255.0f;
			var ca = color.A / 255.0f;

			return new Vertex(xyz, cr, cg, cb, ca, 0, 0);
		}

		public void FillEllipse(in float3 tl, in float3 br, Color color, BlendMode blendMode = BlendMode.Alpha)
		{
			// TODO: Create an ellipse polygon instead
			var a = (br.X - tl.X) / 2;
			var b = (br.Y - tl.Y) / 2;
			var xc = (br.X + tl.X) / 2;
			var yc = (br.Y + tl.Y) / 2;
			for (var y = tl.Y; y <= br.Y; y++)
			{
				var z = float2.Lerp(tl.Z, br.Z, (y - tl.Y) / (br.Y - tl.Y));
				var dx = a * (float)Math.Sqrt(1 - (y - yc) * (y - yc) / b / b);
				DrawScreenLine(new float3(xc - dx, y, z), new float3(xc + dx, y, z), 1, color, blendMode);
			}
		}
	}
}

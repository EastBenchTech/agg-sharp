/*
Copyright (c) 2015, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.QuadTree;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonPathing
{
	using Agg;
	using Pathfinding;
	using MSIntPoint = MSClipperLib.IntPoint;
	using MSPolygons = List<List<MSClipperLib.IntPoint>>;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PolygonPathingDemo : SystemWindow
	{
		MSIntPoint startOverride;
		MSIntPoint endOverride;

		private Vector2 lineStart = Vector2.Zero;
		private Vector2 mousePosition;
		private RGBA_Bytes pathColor = RGBA_Bytes.Green;
		private long scale = 1;
		private MSIntPoint offset = new MSIntPoint(0, 0);

		private RadioButtonGroup pathTypeRadioGroup = new RadioButtonGroup(new Vector2(555, 5), new Vector2(80, 130))
		{
			HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
			VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
			Margin = new BorderDouble(5),
		};

		private MSPolygons polygonsToPathAround;

		private RadioButtonGroup shapeTypeRadioGroup = new RadioButtonGroup(new Vector2(5, 5), new Vector2(205, 110))
		{
			HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
			VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
			Margin = new BorderDouble(5),
		};

		public PolygonPathingDemo()
			: base(740, 520)
		{
			BackgroundColor = RGBA_Bytes.White;

			pathTypeRadioGroup.AddRadioButton("Stay Inside");
			pathTypeRadioGroup.AddRadioButton("Stay Outside");
			pathTypeRadioGroup.SelectedIndex = 0;
			AddChild(pathTypeRadioGroup);

			shapeTypeRadioGroup.AddRadioButton("Boxes");
			shapeTypeRadioGroup.AddRadioButton("Simple Map");
			shapeTypeRadioGroup.AddRadioButton("Thin Middle");
			shapeTypeRadioGroup.AddRadioButton("Circle Holes");
			shapeTypeRadioGroup.AddRadioButton("Raise The Barre");
			shapeTypeRadioGroup.AddRadioButton("Rocktopus");
			shapeTypeRadioGroup.AddRadioButton("Spiral");
			shapeTypeRadioGroup.AddRadioButton("Glyph");
			shapeTypeRadioGroup.SelectedIndex = 0;
			AddChild(shapeTypeRadioGroup);

			AnchorAll();
		}

		private RGBA_Bytes fillColor
		{ get { return RGBA_Bytes.Pink; } }

		[STAThread]
		public static void Main(string[] args)
		{
			PolygonPathingDemo demo = new PolygonPathingDemo();
			demo.ShowAsSystemWindow();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			CreatePolygonData();

			if (polygonsToPathAround?.Count > 0)
			{
				MSIntPoint pathStart = ScreenToObject(new MSIntPoint(lineStart.x, lineStart.y));
				MSIntPoint pathEnd = ScreenToObject(new MSIntPoint(mousePosition.x, mousePosition.y));

				if (startOverride.X != 0 || startOverride.Y != 0)
				{
					pathStart = startOverride;
					pathEnd = endOverride;
				}

				var avoid = new PathFinder(polygonsToPathAround, scale == 1 ? -4 : -600); // -600 is for a .4 nozzle in matterslice

				IVertexSource outlineShape = new VertexSourceApplyTransform(VertexSourceToClipperPolygons.CreatePathStorage(MSPolygonsToPolygons(avoid.OutlinePolygons), scale), Affine.NewTranslation(offset.X, offset.Y));
				graphics2D.Render(outlineShape, RGBA_Bytes.Orange);

				IVertexSource pathingShape = new VertexSourceApplyTransform(VertexSourceToClipperPolygons.CreatePathStorage(MSPolygonsToPolygons(avoid.BoundaryPolygons), scale), Affine.NewTranslation(offset.X, offset.Y));
				graphics2D.Render(pathingShape, fillColor);

				// creat the path
				List<MSIntPoint> pathThatIsInside = new List<MSIntPoint>();
				bool found = avoid.CreatePathInsideBoundary(pathStart, pathEnd, pathThatIsInside);

				foreach (var node in avoid.Waypoints.Nodes)
				{
					foreach (var link in node.Links)
					{
						var pointA = ObjectToScreen(((Pathfinding.IntPointNode)link.nodeA).Position);
						var pointB = ObjectToScreen(((Pathfinding.IntPointNode)link.nodeB).Position);
						graphics2D.Line(pointA.X, pointA.Y, pointB.X, pointB.Y, RGBA_Bytes.Yellow);
					}
					var pos = ObjectToScreen(node.Position);
					graphics2D.Circle(pos.X, pos.Y, 4, RGBA_Bytes.Green);
				}

				if (found)
				{
					MSIntPoint last = ObjectToScreen(pathStart);
					foreach (var inPoint in pathThatIsInside)
					{
						var point = ObjectToScreen(inPoint);
						graphics2D.Line(last.X, last.Y, point.X, point.Y, new RGBA_Bytes(RGBA_Bytes.Black, 128), 2);
						last = point;
					}

					MSIntPoint point2 = ObjectToScreen(pathEnd);
					graphics2D.Line(last.X, last.Y, point2.X, point2.Y, new RGBA_Bytes(RGBA_Bytes.Black, 128), 2);

					graphics2D.DrawString($"Length = {MSClipperLib.CLPolygonExtensions.PolygonLength(pathThatIsInside, false)}", 30, Height - 40);
				}

				// show the crossings
				if (false)
				{
					var crossings = new List<Tuple<int, int, MSIntPoint>>(avoid.BoundaryPolygons.FindCrossingPoints(pathStart, pathEnd, avoid.BoundaryEdgeQuadTrees));
					crossings.Sort(new PolygonAndPointDirectionSorter(pathStart, pathEnd));

					int index = 0;
					foreach (var crossing in crossings)
					{
						graphics2D.Circle(ObjectToScreen(crossing.Item3).X, ObjectToScreen(crossing.Item3).Y, 4, RGBA_Floats.FromHSL((float)index / crossings.Count, 1, .5).GetAsRGBA_Bytes());
						index++;
					}
				}

				// show the thin edges
				if (true && avoid.ThinLinePolygons != null)
				{
					foreach (var polygon in avoid.ThinLinePolygons)
					{
						for(int i=0; i<polygon.Count-1; i++)
						{
							var point = polygon[i];
							var nextPoint = polygon[i+1];
							var start = ObjectToScreen(point);
							var end = ObjectToScreen(nextPoint);
							graphics2D.Line(start.X, start.Y, end.X, end.Y, RGBA_Bytes.Black, 3);
						}
					}
				}

				if (avoid.BoundaryPolygons.PointIsInside(pathEnd))
				{
					graphics2D.DrawString("Inside", 30, Height - 60, color: RGBA_Bytes.Green);
				}
				else
				{
					graphics2D.DrawString("Outside", 30, Height - 60, color: RGBA_Bytes.Red);
				}

				//var triangulated = avoid.BoundaryPolygons.Triangulate();
			}

			base.OnDraw(graphics2D);
		}

		private MSIntPoint ObjectToScreen(MSIntPoint inPoint)
		{
			return inPoint / scale + offset;
		}

		private MSIntPoint ScreenToObject(MSIntPoint inPoint)
		{
			return (inPoint - offset) * scale;
		}

		private MSPolygons PolygonsToMSPolygons(Polygons polygonsToPathAround)
		{
			var otherPolygons = new List<List<MSIntPoint>>();
			foreach (var polygon in polygonsToPathAround)
			{
				otherPolygons.Add(new List<MSIntPoint>());
				for (int i = 0; i < polygon.Count; i++)
				{
					otherPolygons[otherPolygons.Count - 1].Add(new MSIntPoint(polygon[i].X, polygon[i].Y));
				}
			}

			return otherPolygons;
		}

		private Polygons MSPolygonsToPolygons(MSPolygons polygonsToPathAround)
		{
			var otherPolygons = new List<List<ClipperLib.IntPoint>>();
			foreach (var polygon in polygonsToPathAround)
			{
				otherPolygons.Add(new List<ClipperLib.IntPoint>());
				for (int i = 0; i < polygon.Count; i++)
				{
					otherPolygons[otherPolygons.Count - 1].Add(new ClipperLib.IntPoint(polygon[i].X, polygon[i].Y));
				}
			}

			return otherPolygons;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (mouseEvent.Button == MouseButtons.Left && FirstWidgetUnderMouse)
			{
				lineStart = mousePosition = mouseEvent.Position;
				Invalidate();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseCaptured)
			{
				mousePosition = mouseEvent.Position;
				Invalidate();
			}
			base.OnMouseMove(mouseEvent);
		}

		private MSPolygons CreateTravelPath(MSPolygons polygonsToPathAround, MSPolygons travelPolysLine)
		{
			var clipper = new MSClipperLib.Clipper();

			clipper.AddPaths(travelPolysLine, MSClipperLib.PolyType.ptSubject, false);
			clipper.AddPaths(polygonsToPathAround, MSClipperLib.PolyType.ptClip, true);

			var clippedLine = new MSClipperLib.PolyTree();

			//List<List<IntPoint>> intersectedPolys = new List<List<IntPoint>>();
			//clipper.Execute(ClipType.ctDifference, intersectedPolys);

			clipper.Execute(MSClipperLib.ClipType.ctDifference, clippedLine);

			return MSClipperLib.Clipper.OpenPathsFromPolyTree(clippedLine);
		}

		private Polygons FixWinding(Polygons polygonsToPathAround)
		{
			polygonsToPathAround = Clipper.CleanPolygons(polygonsToPathAround);
			Polygon boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToPathAround);
			bounds.left -= 10;
			bounds.bottom += 10;
			bounds.right += 10;
			bounds.top -= 10;

			boundsPolygon.Add(new IntPoint(bounds.left, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.bottom));
			boundsPolygon.Add(new IntPoint(bounds.left, bounds.bottom));

			Clipper clipper = new Clipper();

			clipper.AddPaths(polygonsToPathAround, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			PolyTree intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		private void CreatePolygonData()
		{
			startOverride = new MSIntPoint();
			endOverride = new MSIntPoint();

			IVertexSource pathToUse = null;
			MSPolygons directPolygons = null;
			switch (shapeTypeRadioGroup.SelectedIndex)
			{
				case 0:// simple boxes
					{
						PathStorage ps1 = new PathStorage();

						ps1.MoveTo(100, 100);
						ps1.LineTo(400, 100);
						ps1.LineTo(400, 400);
						ps1.LineTo(100, 400);
						ps1.ClosePolygon();

						ps1.MoveTo(200, 200);
						ps1.LineTo(300, 200);
						ps1.LineTo(300, 300);
						ps1.LineTo(200, 300);
						ps1.ClosePolygon();

						pathToUse = ps1;
					}
					break;

				case 1:// simple polygon map
					{
						PathStorage ps1 = new PathStorage();

						ps1.MoveTo(85, 417);
						ps1.LineTo(338, 428);
						ps1.LineTo(344, 325);
						ps1.LineTo(399, 324);
						ps1.LineTo(400, 421);
						ps1.LineTo(644, 415);
						ps1.LineTo(664, 75);
						ps1.LineTo(98, 81);
						ps1.ClosePolygon();

						ps1.MoveTo(343, 290);
						ps1.LineTo(159, 235);
						ps1.LineTo(154, 162);
						ps1.LineTo(340, 114);
						ps1.ClosePolygon();

						ps1.MoveTo(406, 121);
						ps1.LineTo(587, 158);
						ps1.LineTo(591, 236);
						ps1.LineTo(404, 291);
						ps1.ClosePolygon();

						pathToUse = ps1;
					}
					break;

				case 2: // thin middle
					{
						string thinMiddle = "x:185000, y:48496,x:184599, y:48753,x:184037, y:49167,x:183505, y:49620,x:183007, y:50108,x:182544, y:50631,x:182118, y:51184,x:181732, y:51765,x:181388, y:52372,x:181087, y:53001,x:180830, y:53650,x:180619, y:54315,x:180455, y:54993,x:180339, y:55682,x:180271, y:56376,x:180250, y:57000,x:180274, y:57697,x:180347, y:58391,x:180468, y:59079,x:180637, y:59756,x:180853, y:60420,x:181114, y:61067,x:181420, y:61694,x:181769, y:62299,x:182159, y:62877,x:182589, y:63427,x:183056, y:63946,x:183558, y:64431,x:184093, y:64880,x:184658, y:65290,x:185000, y:65504,x:185000, y:67000,x:175000, y:67000,x:175000, y:65504,x:175342, y:65290,x:175907, y:64880,x:176442, y:64431,x:176944, y:63946,x:177411, y:63427,x:177841, y:62877,x:178231, y:62299,x:178580, y:61694,x:178886, y:61067,x:179147, y:60420,x:179363, y:59756,x:179532, y:59079,x:179653, y:58391,x:179726, y:57697,x:179747, y:56927,x:179718, y:56230,x:179640, y:55537,x:179514, y:54850,x:179340, y:54174,x:179120, y:53512,x:178854, y:52867,x:178543, y:52242,x:178190, y:51640,x:177796, y:51065,x:177362, y:50519,x:176891, y:50003,x:176386, y:49522,x:175848, y:49077,x:175306, y:48688,x:175000, y:48496,x:175000, y:47000,x:185000, y:47000,|";
						directPolygons = MSClipperLib.CLPolygonsExtensions.CreateFromString(thinMiddle);
					}
					break;

				case 3:
					{
						// circle holes
						string circleHoles = "x:189400, y:76400,x:170600, y:76400,x:170600, y:37600,x:189400, y:37600,|x:177346, y:60948,x:175525, y:62137,x:174189, y:63854,x:173482, y:65912,x:173482, y:68087,x:174189, y:70145,x:175525, y:71862,x:177346, y:73051,x:179455, y:73585,x:181621, y:73406,x:183614, y:72532,x:185214, y:71059,x:186249, y:69146,x:186608, y:67000,x:186249, y:64853,x:185214, y:62940,x:183614, y:61468,x:181621, y:60593,x:179455, y:60414,|x:177346, y:40949,x:175525, y:42138,x:174189, y:43855,x:173482, y:45913,x:173482, y:48088,x:174189, y:50146,x:175525, y:51863,x:177346, y:53052,x:179455, y:53586,x:181621, y:53407,x:183614, y:52532,x:185214, y:51060,x:186249, y:49147,x:186608, y:47000,x:186249, y:44854,x:185214, y:42941,x:183614, y:41468,x:181621, y:40594,x:179455, y:40415,|";
						//circleHoles = "x:180987, y:20403, z:1100, width:0,x:181958, y:20720, z:1100, width:0,x:182687, y:21125, z:1100, width:0,x:183185, y:21539, z:1100, width:0,x:183724, y:22089, z:1100, width:0,x:184172, y:22730, z:1100, width:0,x:184432, y:23368, z:1100, width:0,x:184566, y:24089, z:1100, width:0,x:184578, y:24921, z:1100, width:0,x:184441, y:25747, z:1100, width:0,x:184138, y:26475, z:1100, width:0,x:183702, y:27108, z:1100, width:0,x:183167, y:27632, z:1100, width:0,x:182555, y:27938, z:1100, width:0,x:182554, y:27915, z:1100, width:0,x:183212, y:26818, z:1100, width:0,x:183508, y:26189, z:1100, width:0,x:183688, y:25518, z:1100, width:0,x:183702, y:24819, z:1100, width:0,x:183621, y:24184, z:1100, width:0,x:183484, y:23674, z:1100, width:0,x:183255, y:23224, z:1100, width:0,x:182918, y:22778, z:1100, width:0,x:182487, y:22368, z:1100, width:0,x:181976, y:22020, z:1100, width:0,x:181364, y:21750, z:1100, width:0,x:180632, y:21570, z:1100, width:0,x:179830, y:21508, z:1100, width:0,x:178999, y:21597, z:1100, width:0,x:178189, y:21845, z:1100, width:0,x:177449, y:22265, z:1100, width:0,x:176813, y:22859, z:1100, width:0,x:176321, y:23632, z:1100, width:0,x:175995, y:24513, z:1100, width:0,x:175862, y:25429, z:1100, width:0,x:175872, y:26264, z:1100, width:0,x:175972, y:26905, z:1100, width:0,x:176188, y:27451, z:1100, width:0,x:176539, y:27988, z:1100, width:0,x:177591, y:29295, z:1100, width:0,x:178233, y:30002, z:1100, width:0,x:178925, y:30609, z:1100, width:0,x:179753, y:31225, z:1100, width:0,x:181582, y:32508, z:1100, width:0,x:182470, y:33170, z:1100, width:0,x:183251, y:33809, z:1100, width:0,x:184288, y:34722, z:1100, width:0,x:184911, y:35349, z:1100, width:0,x:185137, y:35539, z:1100, width:0,x:186071, y:36681, z:1100, width:0,x:186896, y:38053, z:1100, width:0,x:187406, y:39517, z:1100, width:0,x:187639, y:41002, z:1100, width:0,x:187634, y:42431, z:1100, width:0,x:187518, y:43727, z:1100, width:0,x:187327, y:45100, z:1100, width:0,x:187191, y:46163, z:1100, width:0,x:187244, y:46837, z:1100, width:0,x:187680, y:47318, z:1100, width:0,x:188627, y:47808, z:1100, width:0,x:189985, y:48275, z:1100, width:0,x:191310, y:48512, z:1100, width:0,x:192509, y:48565, z:1100, width:0,x:193422, y:48457, z:1100, width:0,x:194109, y:48238, z:1100, width:0,x:194588, y:47981, z:1100, width:0,x:195075, y:47583, z:1100, width:0,x:195522, y:47082, z:1100, width:0,x:195918, y:46451, z:1100, width:0,x:196200, y:45708, z:1100, width:0,x:196388, y:44868, z:1100, width:0,x:196506, y:43938, z:1100, width:0,x:196730, y:41538, z:1100, width:0,x:196819, y:40094, z:1100, width:0,x:196773, y:37109, z:1100, width:0,x:196834, y:35631, z:1100, width:0,x:196979, y:34313, z:1100, width:0,x:197200, y:33298, z:1100, width:0,x:197487, y:32482, z:1100, width:0,x:197826, y:31759, z:1100, width:0,x:198234, y:31130, z:1100, width:0,x:198724, y:30598, z:1100, width:0,x:199307, y:30160, z:1100, width:0,x:199994, y:29811, z:1100, width:0,x:200755, y:29542, z:1100, width:0,x:201565, y:29346, z:1100, width:0,x:202350, y:29254, z:1100, width:0,x:203038, y:29300, z:1100, width:0,x:203676, y:29448, z:1100, width:0,x:204307, y:29659, z:1100, width:0,x:204899, y:29943, z:1100, width:0,x:205424, y:30309, z:1100, width:0,x:205900, y:30744, z:1100, width:0,x:206341, y:31238, z:1100, width:0,x:206715, y:31833, z:1100, width:0,x:206965, y:32519, z:1100, width:0,x:207143, y:33310, z:1100, width:0,x:207182, y:34036, z:1100, width:0,x:207104, y:34673, z:1100, width:0,x:206913, y:35265, z:1100, width:0,x:206572, y:35843, z:1100, width:0,x:206049, y:36420, z:1100, width:0,x:205435, y:36922, z:1100, width:0,x:205929, y:36318, z:1100, width:0,x:206313, y:35713, z:1100, width:0,x:206555, y:35184, z:1100, width:0,x:206669, y:34662, z:1100, width:0,x:206664, y:34091, z:1100, width:0,x:206565, y:33504, z:1100, width:0,x:206382, y:32797, z:1100, width:0,x:206130, y:32161, z:1100, width:0,x:205799, y:31657, z:1100, width:0,x:205422, y:31244, z:1100, width:0,x:205028, y:30885, z:1100, width:0,x:204600, y:30592, z:1100, width:0,x:204117, y:30379, z:1100, width:0,x:203593, y:30223, z:1100, width:0,x:203038, y:30101, z:1100, width:0,x:202429, y:30065, z:1100, width:0,x:201741, y:30167, z:1100, width:0,x:201062, y:30366, z:1100, width:0,x:200483, y:30623, z:1100, width:0,x:199990, y:30925, z:1100, width:0,x:199574, y:31266, z:1100, width:0,x:199217, y:31675, z:1100, width:0,x:198904, y:32183, z:1100, width:0,x:198633, y:32779, z:1100, width:0,x:198396, y:33458, z:1100, width:0,x:198216, y:34325, z:1100, width:0,x:198104, y:35476, z:1100, width:0,x:198081, y:36793, z:1100, width:0,x:198163, y:38160, z:1100, width:0,x:198353, y:40158, z:1100, width:0,x:198617, y:43362, z:1100, width:0,x:198656, y:44453, z:1100, width:0,x:198615, y:45530, z:1100, width:0,x:198462, y:46622, z:1100, width:0,x:198167, y:47769, z:1100, width:0,x:197709, y:48863, z:1100, width:0,x:197066, y:49776, z:1100, width:0,x:196194, y:50597, z:1100, width:0,x:195038, y:51434, z:1100, width:0,x:193637, y:52208, z:1100, width:0,x:192093, y:52812, z:1100, width:0,x:190610, y:53302, z:1100, width:0,x:189555, y:53699, z:1100, width:0,x:189483, y:53790, z:1100, width:0,x:189240, y:54023, z:1100, width:0,x:189870, y:54330, z:1100, width:0,x:191325, y:54913, z:1100, width:0,x:191948, y:55224, z:1100, width:0,x:192658, y:55617, z:1100, width:0,x:193809, y:56344, z:1100, width:0,x:195832, y:57799, z:1100, width:0,x:196769, y:58278, z:1100, width:0,x:197372, y:58499, z:1100, width:0,x:197647, y:58583, z:1100, width:0,x:198439, y:58743, z:1100, width:0,x:199215, y:58770, z:1100, width:0,x:200046, y:58679, z:1100, width:0,x:200931, y:58398, z:1100, width:0,x:201874, y:57859, z:1100, width:0,x:202756, y:57213, z:1100, width:0,x:203460, y:56618, z:1100, width:0,x:204312, y:55816, z:1100, width:0,x:205986, y:54164, z:1100, width:0,x:206874, y:53342, z:1100, width:0,x:207625, y:52798, z:1100, width:0,x:208545, y:52312, z:1100, width:0,x:209568, y:51966, z:1100, width:0,x:210726, y:51807, z:1100, width:0,x:212015, y:51921, z:1100, width:0,x:213285, y:52307, z:1100, width:0,x:214386, y:52963, z:1100, width:0,x:215257, y:53751, z:1100, width:0,x:215835, y:54533, z:1100, width:0,x:216176, y:55228, z:1100, width:0,x:216419, y:55928, z:1100, width:0,x:216533, y:56583, z:1100, width:0,x:216548, y:57239, z:1100, width:0,x:216479, y:57902, z:1100, width:0,x:216334, y:58558, z:1100, width:0,x:216106, y:59194, z:1100, width:0,x:215789, y:59796, z:1100, width:0,x:215395, y:60348, z:1100, width:0,x:214941, y:60836, z:1100, width:0,x:214419, y:61238, z:1100, width:0,x:213825, y:61537, z:1100, width:0,x:213167, y:61717, z:1100, width:0,x:212305, y:61786, z:1100, width:0,x:211454, y:61716, z:1100, width:0,x:210705, y:61550, z:1100, width:0,x:209916, y:61220, z:1100, width:0,x:209234, y:60749, z:1100, width:0,x:208680, y:60175, z:1100, width:0,x:208307, y:59559, z:1100, width:0,x:208098, y:59019, z:1100, width:0,x:208340, y:59545, z:1100, width:0,x:208769, y:60117, z:1100, width:0,x:209370, y:60593, z:1100, width:0,x:210067, y:60933, z:1100, width:0,x:210763, y:61096, z:1100, width:0,x:211524, y:61154, z:1100, width:0,x:212240, y:61105, z:1100, width:0,x:212828, y:60984, z:1100, width:0,x:213352, y:60798, z:1100, width:0,x:213759, y:60542, z:1100, width:0,x:214152, y:60192, z:1100, width:0,x:214511, y:59770, z:1100, width:0,x:214815, y:59290, z:1100, width:0,x:215054, y:58788, z:1100, width:0,x:215216, y:58298, z:1100, width:0,x:215291, y:57950, z:1100, width:0,x:215359, y:57432, z:1100, width:0,x:215365, y:56936, z:1100, width:0,x:215305, y:56473, z:1100, width:0,x:215150, y:55983, z:1100, width:0,x:214881, y:55426, z:1100, width:0,x:214423, y:54818, z:1100, width:0,x:213710, y:54203, z:1100, width:0,x:212829, y:53688, z:1100, width:0,x:211868, y:53383, z:1100, width:0,x:210909, y:53310, z:1100, width:0,x:210037, y:53500, z:1100, width:0,x:209316, y:53802, z:1100, width:0,x:208618, y:54153, z:1100, width:0,x:207997, y:54541, z:1100, width:0,x:207446, y:54969, z:1100, width:0,x:206021, y:56191, z:1100, width:0,x:204174, y:57856, z:1100, width:0,x:203385, y:58526, z:1100, width:0,x:202461, y:59199, z:1100, width:0,x:201468, y:59819, z:1100, width:0,x:200471, y:60329, z:1100, width:0,x:199478, y:60703, z:1100, width:0,x:198498, y:60915, z:1100, width:0,x:197518, y:60972, z:1100, width:0,x:196524, y:60881, z:1100, width:0,x:195578, y:60684, z:1100, width:0,x:194820, y:60448, z:1100, width:0,x:194437, y:60302, z:1100, width:0,x:192694, y:59529, z:1100, width:0,x:191458, y:59122, z:1100, width:0,x:190389, y:59075, z:1100, width:0,x:189599, y:59243, z:1100, width:0,x:189119, y:59480, z:1100, width:0,x:188967, y:59755, z:1100, width:0,x:189138, y:60058, z:1100, width:0,x:189492, y:60369, z:1100, width:0,x:189982, y:60778, z:1100, width:0,x:190706, y:61527, z:1100, width:0,x:192915, y:64443, z:1100, width:0,x:194141, y:65892, z:1100, width:0,x:195052, y:66844, z:1100, width:0,x:195423, y:67211, z:1100, width:0,x:196758, y:68398, z:1100, width:0,x:198071, y:69403, z:1100, width:0,x:199439, y:70365, z:1100, width:0,x:200717, y:71099, z:1100, width:0,x:202913, y:72157, z:1100, width:0,x:204008, y:72800, z:1100, width:0,x:204968, y:73463, z:1100, width:0,x:205634, y:74031, z:1100, width:0,x:206099, y:74589, z:1100, width:0,x:206454, y:75221, z:1100, width:0,x:206715, y:75884, z:1100, width:0,x:206896, y:76533, z:1100, width:0,x:207018, y:77274, z:1100, width:0,x:207099, y:78211, z:1100, width:0,x:207085, y:79216, z:1100, width:0,x:206922, y:80161, z:1100, width:0,x:206631, y:81044, z:1100, width:0,x:206235, y:81863, z:1100, width:0,x:205756, y:82559, z:1100, width:0,x:205214, y:83077, z:1100, width:0,x:204585, y:83487, z:1100, width:0,x:203844, y:83863, z:1100, width:0,x:203024, y:84143, z:1100, width:0,x:202158, y:84264, z:1100, width:0,x:201327, y:84245, z:1100, width:0,x:200611, y:84099, z:1100, width:0,x:199981, y:83836, z:1100, width:0,x:199208, y:83381, z:1100, width:0,x:198524, y:82824, z:1100, width:0,x:198000, y:82203, z:1100, width:0,x:197634, y:81550, z:1100, width:0,x:197428, y:80900, z:1100, width:0,x:197369, y:80197, z:1100, width:0,x:197444, y:79391, z:1100, width:0,x:197649, y:78570, z:1100, width:0,x:197947, y:77856, z:1100, width:0,x:198417, y:77141, z:1100, width:0,x:199039, y:76555, z:1100, width:0,x:199747, y:76111, z:1100, width:0,x:200465, y:75861, z:1100, width:0,x:201119, y:75768, z:1100, width:0,x:201568, y:75828, z:1100, width:0,x:201370, y:75857, z:1100, width:0,x:200504, y:76041, z:1100, width:0,x:199836, y:76319, z:1100, width:0,x:199224, y:76786, z:1100, width:0,x:198722, y:77383, z:1100, width:0,x:198375, y:78043, z:1100, width:0,x:198143, y:78740, z:1100, width:0,x:198016, y:79457, z:1100, width:0,x:197992, y:80115, z:1100, width:0,x:198066, y:80648, z:1100, width:0,x:198260, y:81141, z:1100, width:0,x:198596, y:81674, z:1100, width:0,x:199057, y:82193, z:1100, width:0,x:199610, y:82627, z:1100, width:0,x:200233, y:82994, z:1100, width:0,x:200788, y:83214, z:1100, width:0,x:201351, y:83321, z:1100, width:0,x:201979, y:83341, z:1100, width:0,x:202645, y:83249, z:1100, width:0,x:203325, y:83020, z:1100, width:0,x:203958, y:82705, z:1100, width:0,x:204484, y:82351, z:1100, width:0,x:204925, y:81918, z:1100, width:0,x:205306, y:81366, z:1100, width:0,x:205608, y:80745, z:1100, width:0,x:205819, y:80110, z:1100, width:0,x:205929, y:79424, z:1100, width:0,x:205930, y:78648, z:1100, width:0,x:205852, y:77906, z:1100, width:0,x:205726, y:77320, z:1100, width:0,x:205538, y:76797, z:1100, width:0,x:205279, y:76245, z:1100, width:0,x:204919, y:75718, z:1100, width:0,x:204431, y:75271, z:1100, width:0,x:203748, y:74854, z:1100, width:0,x:202806, y:74411, z:1100, width:0,x:201708, y:74016, z:1100, width:0,x:200555, y:73744, z:1100, width:0,x:199238, y:73411, z:1100, width:0,x:197642, y:72835, z:1100, width:0,x:195746, y:72077, z:1100, width:0,x:194074, y:71350, z:1100, width:0,x:192410, y:70529, z:1100, width:0,x:191010, y:69738, z:1100, width:0,x:189880, y:69177, z:1100, width:0,x:189033, y:69002, z:1100, width:0,x:188632, y:69143, z:1100, width:0,x:188385, y:69199, z:1100, width:0,x:188137, y:69548, z:1100, width:0,x:187886, y:69735, z:1100, width:0,x:187701, y:70180, z:1100, width:0,x:187481, y:70479, z:1100, width:0,x:187298, y:70942, z:1100, width:0,x:187111, y:71313, z:1100, width:0,x:186769, y:72125, z:1100, width:0,x:186440, y:72800, z:1100, width:0,x:186051, y:73382, z:1100, width:0,x:185528, y:73918, z:1100, width:0,x:184909, y:74379, z:1100, width:0,x:184231, y:74736, z:1100, width:0,x:183561, y:75004, z:1100, width:0,x:182965, y:75199, z:1100, width:0,x:182284, y:75347, z:1100, width:0,x:181327, y:75468, z:1100, width:0,x:180496, y:75700, z:1100, width:0,x:180318, y:75949, z:1100, width:0,x:180141, y:76112, z:1100, width:0,x:180086, y:76664, z:1100, width:0,x:180173, y:77274, z:1100, width:0,x:180368, y:77957, z:1100, width:0,x:180679, y:78667, z:1100, width:0,x:181066, y:79371, z:1100, width:0,x:181480, y:80035, z:1100, width:0,x:182251, y:81168, z:1100, width:0,x:182632, y:81660, z:1100, width:0,x:184193, y:83325, z:1100, width:0,x:184754, y:83985, z:1100, width:0,x:185231, y:84649, z:1100, width:0,x:185585, y:85388, z:1100, width:0,x:185780, y:86273, z:1100, width:0,x:185815, y:87357, z:1100, width:0,x:185684, y:88689, z:1100, width:0,x:185330, y:90050, z:1100, width:0,x:184696, y:91229, z:1100, width:0,x:183903, y:92198, z:1100, width:0,x:183076, y:92932, z:1100, width:0,x:182176, y:93440, z:1100, width:0,x:181171, y:93732, z:1100, width:0,x:180096, y:93801, z:1100, width:0,x:178993, y:93641, z:1100, width:0,x:177995, y:93326, z:1100, width:0,x:177232, y:92923, z:1100, width:0,x:176627, y:92444, z:1100, width:0,x:176105, y:91903, z:1100, width:0,x:175681, y:91306, z:1100, width:0,x:175373, y:90655, z:1100, width:0,x:175171, y:89917, z:1100, width:0,x:175070, y:89059, z:1100, width:0,x:175129, y:88161, z:1100, width:0,x:175405, y:87296, z:1100, width:0,x:175879, y:86467, z:1100, width:0,x:176543, y:85691, z:1100, width:0,x:177298, y:85058, z:1100, width:0,x:178055, y:84644, z:1100, width:0,x:178755, y:84418, z:1100, width:0,x:179373, y:84351, z:1100, width:0,x:179442, y:84380, z:1100, width:0,x:179770, y:84454, z:1100, width:0,x:179747, y:84655, z:1100, width:0,x:179364, y:84745, z:1100, width:0,x:178801, y:84821, z:1100, width:0,x:178187, y:85061, z:1100, width:0,x:177588, y:85492, z:1100, width:0,x:177068, y:86120, z:1100, width:0,x:176648, y:86853, z:1100, width:0,x:176330, y:87663, z:1100, width:0,x:176168, y:88425, z:1100, width:0,x:176134, y:89154, z:1100, width:0,x:176208, y:89794, z:1100, width:0,x:176365, y:90293, z:1100, width:0,x:176618, y:90728, z:1100, width:0,x:176984, y:91168, z:1100, width:0,x:177444, y:91578, z:1100, width:0,x:177986, y:91925, z:1100, width:0,x:178621, y:92198, z:1100, width:0,x:179363, y:92384, z:1100, width:0,x:180167, y:92449, z:1100, width:0,x:180996, y:92362, z:1100, width:0,x:181802, y:92108, z:1100, width:0,x:182541, y:91676, z:1100, width:0,x:183176, y:91062, z:1100, width:0,x:183671, y:90261, z:1100, width:0,x:183998, y:89346, z:1100, width:0,x:184126, y:88390, z:1100, width:0,x:184115, y:87534, z:1100, width:0,x:184021, y:86837, z:1100, width:0,x:183834, y:86301, z:1100, width:0,x:183533, y:85823, z:1100, width:0,x:183188, y:85437, z:1100, width:0,x:182689, y:84952, z:1100, width:0,x:181611, y:83970, z:1100, width:0,x:180398, y:82914, z:1100, width:0,x:179492, y:82110, z:1100, width:0,x:178808, y:81390, z:1100, width:0,x:178359, y:80861, z:1100, width:0,x:177642, y:79951, z:1100, width:0,x:177167, y:79245, z:1100, width:0,x:176767, y:78517, z:1100, width:0,x:176405, y:77628, z:1100, width:0,x:176005, y:76582, z:1100, width:0,x:175847, y:76085, z:1100, width:0,x:175745, y:75690, z:1100, width:0,x:175599, y:75315, z:1100, width:0,x:175501, y:74961, z:1100, width:0,x:175368, y:74780, z:1100, width:0,x:175135, y:74336, z:1100, width:0,x:174982, y:74209, z:1100, width:0,x:174599, y:73774, z:1100, width:0,x:174020, y:73279, z:1100, width:0,x:173611, y:72735, z:1100, width:0,x:173276, y:72076, z:1100, width:0,x:172946, y:71256, z:1100, width:0,x:172661, y:70411, z:1100, width:0,x:172373, y:69309, z:1100, width:0,x:172153, y:68586, z:1100, width:0,x:172033, y:68314, z:1100, width:0,x:171817, y:68071, z:1100, width:0,x:171579, y:67664, z:1100, width:0,x:171237, y:67464, z:1100, width:0,x:170765, y:67107, z:1100, width:0,x:169792, y:66739, z:1100, width:0,x:168840, y:66649, z:1100, width:0,x:167984, y:66723, z:1100, width:0,x:167083, y:66840, z:1100, width:0,x:166292, y:66981, z:1100, width:0,x:165533, y:67146, z:1100, width:0,x:164756, y:67395, z:1100, width:0,x:163912, y:67811, z:1100, width:0,x:163137, y:68366, z:1100, width:0,x:162573, y:69023, z:1100, width:0,x:162218, y:69839, z:1100, width:0,x:162087, y:70880, z:1100, width:0,x:162138, y:72011, z:1100, width:0,x:162333, y:73102, z:1100, width:0,x:162588, y:74209, z:1100, width:0,x:162817, y:75390, z:1100, width:0,x:162978, y:76630, z:1100, width:0,x:163023, y:77913, z:1100, width:0,x:162955, y:79071, z:1100, width:0,x:162775, y:79935, z:1100, width:0,x:162504, y:80635, z:1100, width:0,x:162163, y:81306, z:1100, width:0,x:161700, y:81957, z:1100, width:0,x:161060, y:82599, z:1100, width:0,x:160310, y:83177, z:1100, width:0,x:159517, y:83634, z:1100, width:0,x:158728, y:83939, z:1100, width:0,x:157988, y:84060, z:1100, width:0,x:157236, y:84044, z:1100, width:0,x:156411, y:83935, z:1100, width:0,x:155575, y:83701, z:1100, width:0,x:154792, y:83309, z:1100, width:0,x:154120, y:82823, z:1100, width:0,x:153615, y:82301, z:1100, width:0,x:153215, y:81684, z:1100, width:0,x:152866, y:80916, z:1100, width:0,x:152620, y:80086, z:1100, width:0,x:152541, y:79307, z:1100, width:0,x:152608, y:78582, z:1100, width:0,x:152830, y:77911, z:1100, width:0,x:152905, y:77822, z:1100, width:0,x:153207, y:77342, z:1100, width:0,x:153511, y:77055, z:1100, width:0,x:153775, y:76765, z:1100, width:0,x:154348, y:76321, z:1100, width:0,x:154577, y:76176, z:1100, width:0,x:154461, y:76276, z:1100, width:0,x:153922, y:76893, z:1100, width:0,x:153761, y:77144, z:1100, width:0,x:153558, y:77516, z:1100, width:0,x:153417, y:77865, z:1100, width:0,x:153314, y:78549, z:1100, width:0,x:153308, y:79182, z:1100, width:0,x:153393, y:79812, z:1100, width:0,x:153624, y:80561, z:1100, width:0,x:153923, y:81187, z:1100, width:0,x:154258, y:81676, z:1100, width:0,x:154662, y:82080, z:1100, width:0,x:155168, y:82445, z:1100, width:0,x:155769, y:82741, z:1100, width:0,x:156457, y:82933, z:1100, width:0,x:157156, y:83026, z:1100, width:0,x:157788, y:83028, z:1100, width:0,x:158393, y:82918, z:1100, width:0,x:159015, y:82675, z:1100, width:0,x:159606, y:82322, z:1100, width:0,x:160121, y:81885, z:1100, width:0,x:160548, y:81389, z:1100, width:0,x:160874, y:80861, z:1100, width:0,x:161119, y:80309, z:1100, width:0,x:161301, y:79736, z:1100, width:0,x:161405, y:79006, z:1100, width:0,x:161412, y:77982, z:1100, width:0,x:161296, y:76799, z:1100, width:0,x:161029, y:75583, z:1100, width:0,x:160675, y:74363, z:1100, width:0,x:160299, y:73166, z:1100, width:0,x:160009, y:71911, z:1100, width:0,x:159909, y:70518, z:1100, width:0,x:160011, y:69167, z:1100, width:0,x:160320, y:68050, z:1100, width:0,x:160947, y:67008, z:1100, width:0,x:161956, y:65962, z:1100, width:0,x:163111, y:65032, z:1100, width:0,x:164168, y:64343, z:1100, width:0,x:165180, y:63751, z:1100, width:0,x:166193, y:63128, z:1100, width:0,x:169256, y:61157, z:1100, width:0,x:170457, y:60375, z:1100, width:0,x:170856, y:60050, z:1100, width:0,x:171030, y:59754, z:1100, width:0,x:170876, y:59482, z:1100, width:0,x:170285, y:59194, z:1100, width:0,x:169576, y:58985, z:1100, width:0,x:168448, y:58773, z:1100, width:0,x:167071, y:58584, z:1100, width:0,x:163714, y:58186, z:1100, width:0,x:162743, y:58110, z:1100, width:0,x:161898, y:58082, z:1100, width:0,x:161169, y:58093, z:1100, width:0,x:160519, y:58170, z:1100, width:0,x:159905, y:58340, z:1100, width:0,x:159294, y:58596, z:1100, width:0,x:158653, y:58921, z:1100, width:0,x:157303, y:59682, z:1100, width:0,x:155806, y:60502, z:1100, width:0,x:155027, y:60894, z:1100, width:0,x:154125, y:61309, z:1100, width:0,x:153354, y:61596, z:1100, width:0,x:152453, y:61862, z:1100, width:0,x:151577, y:62081, z:1100, width:0,x:150889, y:62228, z:1100, width:0,x:150177, y:62296, z:1100, width:0,x:149238, y:62279, z:1100, width:0,x:148390, y:62209, z:1100, width:0,x:147601, y:62086, z:1100, width:0,x:146995, y:61903, z:1100, width:0,x:146363, y:61625, z:1100, width:0,x:145752, y:61276, z:1100, width:0,x:145207, y:60881, z:1100, width:0,x:144708, y:60354, z:1100, width:0,x:144229, y:59610, z:1100, width:0,x:143852, y:58830, z:1100, width:0,x:143621, y:58096, z:1100, width:0,x:143523, y:57432, z:1100, width:0,x:143495, y:56752, z:1100, width:0,x:143546, y:56061, z:1100, width:0,x:143692, y:55379, z:1100, width:0,x:143932, y:54723, z:1100, width:0,x:144260, y:54104, z:1100, width:0,x:144661, y:53538, z:1100, width:0,x:145123, y:53043, z:1100, width:0,x:145645, y:52636, z:1100, width:0,x:146227, y:52335, z:1100, width:0,x:146903, y:52145, z:1100, width:0,x:147203, y:52118, z:1100, width:0,x:147709, y:52136, z:1100, width:0,x:148517, y:52310, z:1100, width:0,x:148783, y:52406, z:1100, width:0,x:148506, y:52414, z:1100, width:0,x:147746, y:52565, z:1100, width:0,x:147265, y:52729, z:1100, width:0,x:147102, y:52763, z:1100, width:0,x:146599, y:52959, z:1100, width:0,x:146180, y:53238, z:1100, width:0,x:145787, y:53617, z:1100, width:0,x:145433, y:54071, z:1100, width:0,x:145136, y:54583, z:1100, width:0,x:144911, y:55120, z:1100, width:0,x:144796, y:55563, z:1100, width:0,x:144698, y:56144, z:1100, width:0,x:144649, y:56637, z:1100, width:0,x:144650, y:57123, z:1100, width:0,x:144712, y:57597, z:1100, width:0,x:144870, y:58133, z:1100, width:0,x:145167, y:58806, z:1100, width:0,x:145549, y:59448, z:1100, width:0,x:145965, y:59889, z:1100, width:0,x:146402, y:60213, z:1100, width:0,x:146853, y:60491, z:1100, width:0,x:147311, y:60707, z:1100, width:0,x:147764, y:60865, z:1100, width:0,x:148283, y:60970, z:1100, width:0,x:148942, y:61030, z:1100, width:0,x:149596, y:61038, z:1100, width:0,x:150096, y:60984, z:1100, width:0,x:150239, y:60953, z:1100, width:0,x:151213, y:60695, z:1100, width:0,x:151891, y:60468, z:1100, width:0,x:152537, y:60200, z:1100, width:0,x:153221, y:59868, z:1100, width:0,x:154015, y:59447, z:1100, width:0,x:154833, y:58965, z:1100, width:0,x:155583, y:58449, z:1100, width:0,x:157038, y:57384, z:1100, width:0,x:157803, y:56898, z:1100, width:0,x:158621, y:56497, z:1100, width:0,x:159490, y:56183, z:1100, width:0,x:160405, y:55955, z:1100, width:0,x:161499, y:55781, z:1100, width:0,x:164223, y:55542, z:1100, width:0,x:165291, y:55408, z:1100, width:0,x:167049, y:55136, z:1100, width:0,x:168596, y:54786, z:1100, width:0,x:169718, y:54441, z:1100, width:0,x:170062, y:54350, z:1100, width:0,x:170851, y:53913, z:1100, width:0,x:170864, y:53667, z:1100, width:0,x:170739, y:53147, z:1100, width:0,x:170522, y:52750, z:1100, width:0,x:170385, y:52433, z:1100, width:0,x:170029, y:51889, z:1100, width:0,x:169300, y:51088, z:1100, width:0,x:168388, y:50170, z:1100, width:0,x:167525, y:49462, z:1100, width:0,x:166476, y:48856, z:1100, width:0,x:165272, y:48405, z:1100, width:0,x:164414, y:48024, z:1100, width:0,x:164118, y:47844, z:1100, width:0,x:163719, y:47717, z:1100, width:0,x:163189, y:47476, z:1100, width:0,x:163013, y:47322, z:1100, width:0,x:162616, y:47021, z:1100, width:0,x:162001, y:46134, z:1100, width:0,x:161901, y:45177, z:1100, width:0,x:162149, y:44587, z:1100, width:0,x:162664, y:44069, z:1100, width:0,x:163078, y:43816, z:1100, width:0,x:163365, y:43405, z:1100, width:0,x:163679, y:43268, z:1100, width:0,x:163830, y:43252, z:1100, width:0,x:164332, y:43318, z:1100, width:0,x:164394, y:43314, z:1100, width:0,x:164959, y:43432, z:1100, width:0,x:165109, y:43438, z:1100, width:0,x:165453, y:43533, z:1100, width:0,x:165820, y:43725, z:1100, width:0,x:167689, y:44205, z:1100, width:0,x:169414, y:44990, z:1100, width:0,x:170733, y:45625, z:1100, width:0,x:172387, y:46390, z:1100, width:0,x:173412, y:47007, z:1100, width:0,x:174299, y:47493, z:1100, width:0,x:175117, y:47775, z:1100, width:0,x:175872, y:47756, z:1100, width:0,x:176613, y:47541, z:1100, width:0,x:177312, y:47277, z:1100, width:0,x:177970, y:46982, z:1100, width:0,x:178587, y:46649, z:1100, width:0,x:179042, y:46341, z:1100, width:0,x:179401, y:46069, z:1100, width:0,x:179980, y:45605, z:1100, width:0,x:181197, y:44579, z:1100, width:0,x:182196, y:43578, z:1100, width:0,x:182978, y:42509, z:1100, width:0,x:183391, y:41461, z:1100, width:0,x:183450, y:40434, z:1100, width:0,x:183280, y:39468, z:1100, width:0,x:182999, y:38600, z:1100, width:0,x:182609, y:37809, z:1100, width:0,x:182113, y:37076, z:1100, width:0,x:181582, y:36426, z:1100, width:0,x:181090, y:35879, z:1100, width:0,x:180500, y:35289, z:1100, width:0,x:177903, y:32811, z:1100, width:0,x:177068, y:31941, z:1100, width:0,x:176264, y:30984, z:1100, width:0,x:175554, y:30014, z:1100, width:0,x:174998, y:29106, z:1100, width:0,x:174591, y:28231, z:1100, width:0,x:174329, y:27355, z:1100, width:0,x:174237, y:26350, z:1100, width:0,x:174342, y:25087, z:1100, width:0,x:174687, y:23785, z:1100, width:0,x:175316, y:22660, z:1100, width:0,x:176105, y:21736, z:1100, width:0,x:176931, y:21039, z:1100, width:0,x:177828, y:20559, z:1100, width:0,x:178831, y:20288, z:1100, width:0,x:179898, y:20235, z:1100, width:0,|";
						//startOverride = new MSIntPoint(153099, 78023);
						//endOverride = new MSIntPoint(153104, 77984);
						directPolygons = MSClipperLib.CLPolygonsExtensions.CreateFromString(circleHoles);
					}
					break;

				case 4: // Raise The Barre
					{
						string raiseTheBarreString = "x:208002, y:42352,x:208103, y:42394,x:208296, y:42425,x:208674, y:42572,x:209019, y:42753,x:209581, y:43315,x:209833, y:43813,x:209909, y:44038,x:209942, y:44237,x:209984, y:44338,x:210000, y:44547,x:209982, y:69661,x:209940, y:69762,x:209909, y:69955,x:209762, y:70334,x:209581, y:70678,x:209019, y:71241,x:208521, y:71493,x:208296, y:71568,x:208097, y:71602,x:207996, y:71643,x:207787, y:71659,x:151998, y:71641,x:151897, y:71599,x:151704, y:71568,x:151325, y:71421,x:150981, y:71241,x:150418, y:70678,x:150166, y:70180,x:150091, y:69955,x:150058, y:69756,x:150016, y:69655,x:150000, y:69446,x:150007, y:44404,x:150060, y:44231,x:150091, y:44038,x:150238, y:43660,x:150418, y:43315,x:150981, y:42753,x:151479, y:42501,x:151704, y:42426,x:151903, y:42392,x:152008, y:42350,|x:160016, y:67391,x:159952, y:67402,x:159916, y:67437,x:159883, y:67711,x:159841, y:67812,x:159810, y:68005,x:159663, y:68384,x:159371, y:68914,x:159037, y:69334,x:158375, y:69796,x:157897, y:69993,x:157698, y:70027,x:157597, y:70068,x:157388, y:70084,x:156333, y:70078,x:156204, y:70051,x:156076, y:69964,x:156016, y:69880,x:156000, y:69671,x:155983, y:67435,x:155943, y:67398,x:153866, y:67391,x:153802, y:67402,x:153766, y:67437,x:153750, y:67646,x:153746, y:69594,x:153660, y:70030,x:153474, y:70384,x:153361, y:70483,x:152941, y:70655,x:152663, y:70684,x:152066, y:70691,x:151999, y:70704,x:151919, y:70769,x:151892, y:70818,x:151892, y:70931,x:152079, y:71043,x:152288, y:71059,x:152884, y:71053,x:153190, y:70988,x:153859, y:70978,x:154165, y:70913,x:155811, y:70913,x:156116, y:70978,x:156853, y:70998,x:157091, y:71053,x:158209, y:71053,x:158515, y:70988,x:158950, y:70968,x:159922, y:70743,x:160450, y:70521,x:160645, y:70416,x:161175, y:70046,x:161432, y:69778,x:161788, y:69234,x:161985, y:68830,x:162145, y:68243,x:162168, y:68031,x:162210, y:67930,x:162208, y:67435,x:162172, y:67399,x:161963, y:67384,|x:206421, y:66543,x:206228, y:66575,x:206003, y:66650,x:205580, y:66902,x:205242, y:67239,x:204915, y:67737,x:204840, y:67962,x:204807, y:68161,x:204765, y:68262,x:204750, y:68471,x:204767, y:68911,x:204809, y:69012,x:204840, y:69205,x:205092, y:69703,x:205312, y:70009,x:205505, y:70191,x:205824, y:70396,x:206228, y:70593,x:206427, y:70626,x:206528, y:70668,x:206737, y:70684,x:207177, y:70666,x:207278, y:70625,x:207471, y:70593,x:207696, y:70518,x:208236, y:70157,x:208531, y:69853,x:208784, y:69430,x:208859, y:69205,x:208892, y:69006,x:208934, y:68905,x:208950, y:68696,x:208932, y:68256,x:208890, y:68155,x:208859, y:67962,x:208784, y:67737,x:208531, y:67314,x:208095, y:66888,x:207696, y:66649,x:207471, y:66574,x:207272, y:66541,x:207171, y:66499,x:206594, y:66490,|x:166691, y:65966,x:166624, y:65979,x:166545, y:66041,x:166441, y:66237,x:166210, y:66914,x:166153, y:67004,x:166084, y:67057,x:166018, y:66999,x:165657, y:66083,x:165589, y:66002,x:165543, y:65973,x:164753, y:65976,x:164720, y:66011,x:164721, y:66061,x:166138, y:69509,x:166249, y:69709,x:166401, y:69839,x:166461, y:69834,x:166597, y:69768,x:166785, y:69430,x:168130, y:66058,x:168129, y:66006,x:168096, y:65974,x:167888, y:65959,|x:177942, y:59290,x:177636, y:59354,x:177273, y:59377,x:177172, y:59418,x:176979, y:59450,x:176494, y:59639,x:176324, y:59778,x:176266, y:59862,x:176250, y:60071,x:176256, y:60893,x:176321, y:61198,x:176345, y:61938,x:176417, y:62081,x:176452, y:62114,x:176507, y:62110,x:176582, y:62048,x:176610, y:62005,x:176643, y:61581,x:176685, y:61480,x:176716, y:61287,x:176863, y:60909,x:176968, y:60714,x:177188, y:60409,x:177625, y:60096,x:177879, y:59975,x:178078, y:59941,x:178179, y:59900,x:178388, y:59884,x:179047, y:59899,x:179425, y:60046,x:179770, y:60227,x:180106, y:60564,x:180283, y:60912,x:180359, y:61137,x:180374, y:61346,x:180359, y:61855,x:180162, y:62409,x:179924, y:62770,x:179533, y:63203,x:179325, y:63371,x:179013, y:63660,x:178425, y:64121,x:178188, y:64335,x:177975, y:64496,x:177458, y:65032,x:176988, y:65684,x:176731, y:66249,x:176641, y:66537,x:176608, y:66811,x:176566, y:66912,x:176550, y:67121,x:176568, y:67561,x:176610, y:67662,x:176641, y:67930,x:176788, y:68309,x:176893, y:68503,x:177113, y:68809,x:177381, y:69066,x:177775, y:69346,x:178179, y:69543,x:178404, y:69618,x:178603, y:69651,x:178704, y:69693,x:179053, y:69726,x:179154, y:69768,x:179363, y:69784,x:180177, y:69766,x:180278, y:69725,x:180702, y:69691,x:180803, y:69650,x:181071, y:69618,x:181358, y:69521,x:181477, y:69367,x:181429, y:69144,x:181417, y:67649,x:181405, y:67579,x:181332, y:67436,x:181298, y:67403,x:181243, y:67408,x:181140, y:67512,x:181096, y:67924,x:180775, y:68571,x:180519, y:68841,x:180193, y:69043,x:179803, y:69170,x:179588, y:69184,x:178998, y:69170,x:178555, y:69019,x:178200, y:68771,x:178064, y:68607,x:177852, y:68149,x:177826, y:67846,x:177841, y:67437,x:178093, y:66939,x:178313, y:66634,x:178731, y:66227,x:178950, y:66071,x:179181, y:65852,x:179700, y:65471,x:179856, y:65327,x:180074, y:65171,x:180931, y:64303,x:181212, y:63909,x:181409, y:63580,x:181634, y:62980,x:181667, y:62631,x:181709, y:62530,x:181724, y:62321,x:181707, y:61806,x:181665, y:61705,x:181634, y:61437,x:181437, y:60884,x:181048, y:60298,x:180819, y:60077,x:180424, y:59796,x:179872, y:59524,x:179647, y:59449,x:179448, y:59416,x:179347, y:59374,x:178935, y:59330,x:178759, y:59289,|x:186485, y:59504,x:183040, y:59516,x:182973, y:59528,x:182897, y:59587,x:182876, y:59625,x:182978, y:59718,x:183471, y:59749,x:183675, y:59858,x:183782, y:59968,x:183884, y:60237,x:183915, y:60583,x:183970, y:60823,x:183959, y:68830,x:183797, y:69124,x:183621, y:69244,x:183402, y:69320,x:182883, y:69341,x:182764, y:69374,x:182738, y:69406,x:182720, y:69510,x:182765, y:69544,x:182962, y:69559,x:188421, y:69574,x:188750, y:69688,x:188806, y:69685,x:188881, y:69623,x:188908, y:69574,x:188905, y:69455,x:188865, y:69355,x:188833, y:68934,x:188779, y:68694,x:188756, y:67807,x:188696, y:67731,x:188658, y:67710,x:188565, y:67812,x:188534, y:68230,x:188459, y:68455,x:188286, y:68657,x:188039, y:68782,x:187610, y:68879,x:185676, y:68866,x:185640, y:68830,x:185624, y:68621,x:185642, y:65185,x:185678, y:65150,x:185887, y:65134,x:187977, y:65151,x:188078, y:65193,x:188278, y:65230,x:188600, y:65489,x:188653, y:65487,x:188684, y:65455,x:188694, y:64950,x:188629, y:64644,x:188617, y:63599,x:188605, y:63532,x:188541, y:63454,x:188499, y:63429,x:188446, y:63430,x:188415, y:63463,x:188371, y:63799,x:188295, y:64099,x:188174, y:64270,x:187999, y:64396,x:187671, y:64518,x:187462, y:64534,x:185676, y:64516,x:185640, y:64480,x:185624, y:64271,x:185631, y:60903,x:185715, y:60612,x:185888, y:60410,x:186128, y:60275,x:186327, y:60241,x:186428, y:60200,x:186637, y:60184,x:187977, y:60201,x:188078, y:60243,x:188271, y:60274,x:188625, y:60458,x:188731, y:60564,x:188909, y:60912,x:188942, y:61111,x:188984, y:61212,x:189018, y:61410,x:189078, y:61486,x:189116, y:61507,x:189209, y:61405,x:189219, y:60900,x:189154, y:60594,x:189132, y:60156,x:189090, y:60055,x:189059, y:59787,x:188948, y:59583,x:188825, y:59486,x:188720, y:59449,x:186791, y:59440,|x:166020, y:57640,x:165469, y:57838,x:164780, y:58274,x:164325, y:58646,x:163168, y:59889,x:162546, y:60759,x:162546, y:60811,x:164086, y:64519,x:164118, y:64632,x:164154, y:64668,x:164363, y:64684,x:165097, y:64669,x:165128, y:64643,x:164796, y:63733,x:164797, y:63680,x:164838, y:63645,x:164967, y:63603,x:166944, y:63603,x:167242, y:63615,x:167319, y:63684,x:167354, y:63747,x:167046, y:64584,x:167047, y:64637,x:167079, y:64668,x:167288, y:64684,x:168560, y:64676,x:168627, y:64664,x:168707, y:64598,x:170188, y:60984,x:170443, y:60489,x:170663, y:60184,x:171006, y:59852,x:171204, y:59750,x:171478, y:59716,x:171579, y:59675,x:171997, y:59674,x:172201, y:59783,x:172307, y:59889,x:172410, y:60087,x:172443, y:60511,x:172485, y:60612,x:172500, y:60821,x:172485, y:68830,x:172329, y:69135,x:172260, y:69207,x:172065, y:69306,x:171499, y:69353,x:171423, y:69412,x:171402, y:69450,x:171504, y:69543,x:171713, y:69559,x:175149, y:69541,x:175184, y:69499,x:175182, y:69384,x:175147, y:69349,x:174654, y:69318,x:174384, y:69203,x:174308, y:69137,x:174166, y:68830,x:174150, y:68621,x:174157, y:60528,x:174210, y:60355,x:174241, y:60087,x:174352, y:59883,x:174465, y:59785,x:174804, y:59675,x:175523, y:59641,x:175556, y:59607,x:175551, y:59552,x:175447, y:59449,x:175238, y:59434,x:174566, y:59439,x:174261, y:59504,x:172465, y:59504,x:172159, y:59440,x:169392, y:59440,x:168578, y:59525,x:168545, y:59561,x:168545, y:59610,x:168714, y:59743,x:168773, y:59829,x:168784, y:59905,x:168735, y:60130,x:168616, y:60387,x:167763, y:62684,x:167651, y:62881,x:167572, y:62943,x:167363, y:62959,x:164666, y:62951,x:164597, y:62937,x:164477, y:62845,x:164341, y:62605,x:188737, y:46985,x:188592, y:47140,x:186667, y:49877,x:186546, y:49969,x:186337, y:49985,x:185226, y:49967,x:185190, y:49931,x:185174, y:49722,x:185179, y:47099,x:185244, y:46794,x:185265, y:46513,x:185423, y:46208,x:185551, y:46123,x:185828, y:46026,x:186472, y:45992,x:186505, y:45958,x:186500, y:45903,x:186396, y:45800,x:186187, y:45785,x:185590, y:45790,x:185285, y:45855,x:183564, y:45855,x:183258, y:45791,x:182665, y:45790,x:182481, y:45815,x:179816, y:45867,x:179749, y:45879,x:179648, y:45970,x:179874, y:46103,x:179913, y:46147,x:179955, y:46272,x:179910, y:46481,x:178929, y:49113,x:178832, y:49236,x:178747, y:49294,x:178538, y:49310,x:175833, y:49303,x:175710, y:49277,x:175539, y:49008,x:174935, y:47423,x:174867, y:47383,x:174839, y:47415,x:174811, y:47762,x:174756, y:48001,x:174735, y:48281,x:174543, y:48844,x:174542, y:48957,x:175368, y:50983,x:175404, y:51019,x:175613, y:51035,x:176272, y:51020,x:176303, y:50994,x:176191, y:50756,x:175974, y:50094,x:175988, y:50043,x:176017, y:50016,x:176140, y:49989,x:178385, y:49992,x:178452, y:50003,x:178553, y:50098,x:178225, y:50996,x:178256, y:51022,x:178463, y:51035,x:179735, y:51027,x:179802, y:51015,x:179881, y:50953,x:179985, y:50756,x:181437, y:47185,x:181688, y:46763,x:181912, y:46460,x:182105, y:46278,x:182453, y:46101,x:182662, y:46085,x:183253, y:46106,x:183376, y:46204,x:183434, y:46288,x:183509, y:46513,x:183542, y:46937,x:183584, y:47038,x:183599, y:47247,x:183617, y:52333,x:183653, y:52369,x:183862, y:52385,x:185123, y:52367,x:185159, y:52331,x:185174, y:52122,x:185182, y:50850,x:185198, y:50783,x:185378, y:50676,x:185652, y:50642,x:185753, y:50601,x:185962, y:50585,x:186709, y:50592,x:186996, y:50675,x:187311, y:50886,x:187625, y:51319,x:187859, y:51913,x:187966, y:52332,x:188003, y:52368,x:188212, y:52385,x:189246, y:52370,x:189277, y:52344,x:189031, y:51865,x:188512, y:51185,x:188011, y:50698,x:187877, y:50610,x:187816, y:50525,x:187821, y:50405,x:188056, y:50129,x:188437, y:49610,x:188581, y:49454,x:188737, y:49235,x:188881, y:49079,x:189262, y:48560,x:189406, y:48404,x:189562, y:48185,x:189937, y:47735,x:190156, y:47504,x:190312, y:47285,x:191030, y:46578,x:191424, y:46297,x:191828, y:46101,x:192027, y:46067,x:192128, y:46026,x:192546, y:46025,x:192750, y:46134,x:192857, y:46244,x:192959, y:46513,x:192992, y:46937,x:193034, y:47038,x:193049, y:47247,x:193067, y:52333,x:193103, y:52369,x:193312, y:52385,x:194573, y:52367,x:194609, y:52331,x:194624, y:52122,x:194632, y:50850,x:194648, y:50783,x:194828, y:50676,x:195102, y:50642,x:195203, y:50601,x:195412, y:50585,x:196159, y:50592,x:196446, y:50675,x:196761, y:50886,x:197075, y:51319,x:197309, y:51913,x:197416, y:52332,x:197453, y:52368,x:197662, y:52385,x:198696, y:52370,x:198727, y:52344,x:198481, y:51865,x:197962, y:51185,x:197461, y:50698,x:197327, y:50610,x:197266, y:50525,x:197271, y:50405,x:197506, y:50129,x:197887, y:49610,x:198031, y:49454,x:198187, y:49235,x:198331, y:49079,x:198712, y:48560,x:198856, y:48404,x:199012, y:48185,x:199387, y:47735,x:199606, y:47504,x:199762, y:47285,x:200480, y:46578,x:200874, y:46297,x:201278, y:46101,x:201477, y:46067,x:201585, y:46026,x:201777, y:46027,x:201878, y:46069,x:202371, y:46100,x:202575, y:46209,x:202682, y:46319,x:202784, y:46588,x:202815, y:46934,x:202870, y:47174,x:202859, y:55181,x:202697, y:55475,x:202512, y:55599,x:202364, y:55658,x:201651, y:55703,x:201616, y:55744,x:201618, y:55859,x:201653, y:55894,x:201862, y:55910,x:207321, y:55925,x:207650, y:56039,x:207706, y:56036,x:207781, y:55974,x:207808, y:55925,x:207805, y:55806,x:207765, y:55706,x:207733, y:55285,x:207679, y:55045,x:207656, y:54158,x:207596, y:54082,x:207558, y:54061,x:207465, y:54163,x:207434, y:54581,x:207359, y:54806,x:207186, y:55008,x:206939, y:55133,x:206510, y:55230,x:204576, y:55217,x:204540, y:55181,x:204524, y:54972,x:204542, y:51536,x:204578, y:51501,x:204787, y:51485,x:206877, y:51502,x:206978, y:51544,x:207178, y:51581,x:207500, y:51840,x:207553, y:51838,x:207584, y:51806,x:207594, y:51301,x:207529, y:50995,x:207517, y:49950,x:207505, y:49883,x:207441, y:49805,x:207399, y:49780,x:207346, y:49781,x:207315, y:49814,x:207271, y:50150,x:207195, y:50450,x:207074, y:50621,x:206899, y:50747,x:206571, y:50869,x:206362, y:50885,x:204576, y:50867,x:204540, y:50831,x:204524, y:50622,x:204531, y:47254,x:204615, y:46963,x:204788, y:46761,x:205028, y:46626,x:205227, y:46592,x:205328, y:46551,x:205537, y:46535,x:206877, y:46552,x:206978, y:46594,x:207171, y:46625,x:207525, y:46809,x:207631, y:46915,x:207809, y:47263,x:207918, y:47761,x:207978, y:47837,x:208016, y:47858,x:208109, y:47756,x:208119, y:47326,x:208100, y:47095,x:208065, y:47006,x:208032, y:46507,x:207990, y:46406,x:207959, y:46138,x:207848, y:45934,x:207725, y:45837,x:207624, y:45801,x:207412, y:45785,|x:187990, y:53667,x:187923, y:53684,x:187838, y:53811,x:187712, y:54135,x:187531, y:54479,x:187290, y:54757,x:186960, y:55034,x:186615, y:55206,x:186321, y:55294,x:186112, y:55310,x:185365, y:55302,x:185298, y:55290,x:185190, y:55181,x:185174, y:54972,x:185157, y:53711,x:185117, y:53674,x:183715, y:53667,x:183651, y:53678,x:183615, y:53713,x:183599, y:53922,x:183584, y:55181,x:183422, y:55475,x:183237, y:55599,x:183089, y:55658,x:182812, y:55685,x:182508, y:55692,x:182389, y:55725,x:182363, y:55757,x:182345, y:55861,x:182378, y:55894,x:182829, y:55910,x:186927, y:55892,x:187028, y:55851,x:187452, y:55817,x:187553, y:55776,x:187746, y:55744,x:188124, y:55597,x:188619, y:55342,x:189031, y:54929,x:189237, y:54610,x:189359, y:54356,x:189434, y:54131,x:189467, y:53932,x:189507, y:53825,x:189506, y:53710,x:189471, y:53675,x:189262, y:53660,|x:197440, y:53667,x:197373, y:53684,x:197288, y:53811,x:197162, y:54135,x:196981, y:54479,x:196740, y:54757,x:196410, y:55034,x:196065, y:55206,x:195771, y:55294,x:195562, y:55310,x:194815, y:55302,x:194748, y:55290,x:194640, y:55181,x:194624, y:54972,x:194607, y:53711,x:194567, y:53674,x:193165, y:53667,x:193101, y:53678,x:193065, y:53713,x:193049, y:53922,x:193034, y:55181,x:192872, y:55475,x:192687, y:55599,x:192539, y:55658,x:192262, y:55685,x:191958, y:55692,x:191839, y:55725,x:191813, y:55757,x:191795, y:55861,x:191840, y:55895,x:192037, y:55910,x:196377, y:55892,x:196478, y:55851,x:196902, y:55817,x:197003, y:55776,x:197196, y:55744,x:197574, y:55597,x:198069, y:55342,x:198481, y:54929,x:198687, y:54610,x:198809, y:54356,x:198884, y:54131,x:198917, y:53932,x:198957, y:53825,x:198956, y:53710,x:198921, y:53675,x:198712, y:53660,|x:172016, y:47667,x:171949, y:47680,x:171841, y:47788,x:171635, y:48333,x:171112, y:49156,x:170505, y:49755,x:170150, y:49997,x:169747, y:50194,x:169522, y:50269,x:169011, y:50355,x:167966, y:50352,x:167899, y:50340,x:167791, y:50231,x:167775, y:50022,x:167758, y:47711,x:167718, y:47674,x:165641, y:47667,x:165577, y:47678,x:165541, y:47713,x:165525, y:47922,x:165543, y:53533,x:165579, y:53569,x:165788, y:53585,x:167724, y:53567,x:167760, y:53531,x:167775, y:53322,x:167783, y:51450,x:167797, y:51380,x:167879, y:51243,x:167910, y:51216,x:168444, y:51154,x:169190, y:51156,x:169522, y:51200,x:169900, y:51347,x:170095, y:51453,x:170507, y:51865,x:170759, y:52363,x:170835, y:52588,x:170868, y:52787,x:170910, y:52888,x:170943, y:53533,x:170979, y:53569,x:171188, y:53585,x:172749, y:53567,x:172784, y:53525,x:172783, y:53412,x:172269, y:52358,x:171938, y:51935,x:171402, y:51416,x:171075, y:51143,x:171025, y:51072,x:171058, y:51046,x:171849, y:50721,x:172075, y:50572,x:172270, y:50467,x:172800, y:50097,x:172956, y:49953,x:173175, y:49797,x:173432, y:49529,x:174013, y:48685,x:174135, y:48431,x:174285, y:47981,x:174317, y:47784,x:174351, y:47699,x:174319, y:47672,x:174113, y:47660,|x:168342, y:42941,x:167663, y:43010,x:164288, y:42984,x:164073, y:43024,x:164015, y:43049,x:163989, y:43082,x:163969, y:43184,x:164154, y:43294,x:164722, y:43325,x:165054, y:43452,x:165135, y:43513,x:165285, y:43738,x:165360, y:43963,x:165393, y:44162,x:165435, y:44263,x:165465, y:44760,x:165521, y:45072,x:165543, y:46408,x:165579, y:46444,x:165788, y:46460,x:167724, y:46442,x:167760, y:46406,x:167775, y:46197,x:167780, y:45074,x:167866, y:44563,x:167968, y:44365,x:168231, y:44103,x:168579, y:43926,x:168804, y:43851,x:169078, y:43817,x:169179, y:43776,x:169388, y:43760,x:170136, y:43764,x:170572, y:43850,x:171220, y:44178,x:171707, y:44665,x:171959, y:45163,x:172035, y:45388,x:172124, y:45972,x:172143, y:46408,x:172179, y:46444,x:172388, y:46460,x:174324, y:46442,x:174357, y:46409,x:174210, y:45763,x:173958, y:45187,x:173699, y:44774,x:173438, y:44435,x:173052, y:44066,x:172674, y:43773,x:172147, y:43475,x:171397, y:43175,x:171198, y:43142,x:171097, y:43100,x:170134, y:42940,|";
						directPolygons = MSClipperLib.CLPolygonsExtensions.CreateFromString(raiseTheBarreString);
					}
					break;

				case 5: // Roctopus
					{
						string roctopus = "x:182191, y:-16586,x:184260, y:-15883,x:185751, y:-15050,x:186868, y:-14088,x:187811, y:-12999,x:188571, y:-11769,x:189136, y:-10381,x:189505, y:-8810,x:189674, y:-7028,x:189543, y:-5239,x:189009, y:-3643,x:188088, y:-2217,x:186794, y:-939,x:185283, y:9,x:183711, y:446,x:182218, y:538,x:180945, y:444,x:180056, y:205,x:179715, y:-142,x:179706, y:-524,x:179821, y:-865,x:180190, y:-1094,x:181944, y:-1198,x:183026, y:-1464,x:184094, y:-1996,x:185047, y:-2854,x:185841, y:-3904,x:186435, y:-5013,x:186796, y:-6178,x:186893, y:-7398,x:186772, y:-8528,x:186481, y:-9423,x:186024, y:-10222,x:185403, y:-11065,x:184614, y:-11865,x:183653, y:-12536,x:182506, y:-13065,x:181213, y:-13427,x:179709, y:-13589,x:178114, y:-13447,x:176530, y:-12976,x:175047, y:-12153,x:173766, y:-10979,x:172803, y:-9449,x:172188, y:-7738,x:171954, y:-6018,x:171997, y:-4461,x:172214, y:-3243,x:172653, y:-2186,x:173361, y:-1111,x:174161, y:-120,x:175512, y:1473,x:176854, y:2853,x:178258, y:3990,x:184039, y:7973,x:185760, y:9243,x:187367, y:10537,x:189094, y:12036,x:190914, y:13868,x:192808, y:16170,x:194477, y:18951,x:195481, y:21984,x:195989, y:25205,x:196039, y:28321,x:195779, y:31128,x:195488, y:32822,x:195071, y:35343,x:195168, y:36467,x:196014, y:37326,x:197970, y:38348,x:200445, y:39242,x:202850, y:39713,x:204994, y:39807,x:206677, y:39569,x:207972, y:39126,x:208952, y:38605,x:209767, y:37927,x:210569, y:37016,x:211276, y:35857,x:211804, y:34439,x:212170, y:32808,x:212355, y:31311,x:212551, y:29285,x:212866, y:26275,x:213080, y:23383,x:213091, y:21028,x:213073, y:17337,x:213203, y:14314,x:213487, y:11603,x:213932, y:9496,x:214521, y:7793,x:215241, y:6294,x:216115, y:4988,x:217164, y:3867,x:218397, y:2908,x:219823, y:2087,x:221406, y:1447,x:223111, y:1031,x:224774, y:861,x:226230, y:961,x:227587, y:1280,x:228953, y:1767,x:230251, y:2419,x:231403, y:3229,x:232438, y:4173,x:233386, y:5224,x:234178, y:6468,x:234744, y:7993,x:235086, y:9583,x:235204, y:11023,x:235073, y:12393,x:234666, y:13778,x:233962, y:15191,x:232941, y:16645,x:231655, y:17972,x:230153, y:19006,x:228423, y:19753,x:226450, y:20218,x:224502, y:20290,x:222847, y:19859,x:221464, y:19115,x:220330, y:18245,x:219626, y:17500,x:219527, y:17134,x:219918, y:16789,x:220306, y:16824,x:220742, y:17076,x:222154, y:17832,x:223374, y:18231,x:224737, y:18356,x:226224, y:18100,x:227698, y:17579,x:229026, y:16907,x:230177, y:16080,x:231116, y:15095,x:231794, y:14090,x:232162, y:13206,x:232323, y:12320,x:232380, y:11315,x:232306, y:10198,x:232073, y:8982,x:231684, y:7834,x:231139, y:6925,x:230496, y:6172,x:229811, y:5494,x:229049, y:4927,x:228176, y:4506,x:227205, y:4187,x:226152, y:3928,x:224983, y:3837,x:223665, y:4024,x:222373, y:4381,x:221281, y:4798,x:220367, y:5295,x:219611, y:5890,x:218956, y:6630,x:218342, y:7558,x:217790, y:8677,x:217324, y:9994,x:216962, y:11693,x:216726, y:13959,x:216653, y:16559,x:216785, y:19261,x:216831, y:19777,x:217218, y:24715,x:217439, y:27304,x:217704, y:29672,x:217873, y:31909,x:217805, y:34102,x:217448, y:36337,x:216751, y:38698,x:215716, y:40923,x:214353, y:42752,x:212531, y:44409,x:210115, y:46125,x:207303, y:47687,x:204303, y:48877,x:201729, y:49765,x:200204, y:50418,x:199977, y:50796,x:199904, y:50890,x:200166, y:50990,x:201940, y:51557,x:202966, y:51851,x:205263, y:52920,x:207591, y:54350,x:211797, y:57714,x:213673, y:58859,x:215354, y:59592,x:216896, y:60023,x:218417, y:60133,x:220112, y:59926,x:221932, y:59303,x:223830, y:58161,x:225593, y:56864,x:227008, y:55770,x:228243, y:54723,x:229351, y:53671,x:232094, y:50935,x:233747, y:49436,x:235344, y:48300,x:237102, y:47387,x:239136, y:46658,x:241453, y:46319,x:244058, y:46571,x:246638, y:47396,x:248878, y:48778,x:250650, y:50417,x:251824, y:52013,x:252558, y:53526,x:253009, y:54915,x:253230, y:56254,x:253274, y:57616,x:253161, y:58979,x:252910, y:60319,x:252488, y:61605,x:251875, y:62793,x:251086, y:63907,x:250181, y:64884,x:249116, y:65720,x:247851, y:66397,x:246362, y:66899,x:244623, y:67211,x:242809, y:67236,x:241093, y:66882,x:239457, y:66146,x:237887, y:65024,x:236595, y:63652,x:235793, y:62167,x:235363, y:60707,x:235189, y:59417,x:235223, y:58467,x:235420, y:58029,x:235704, y:57936,x:235999, y:58018,x:236284, y:58400,x:236770, y:59940,x:236882, y:60249,x:237418, y:61350,x:238207, y:62402,x:239303, y:63306,x:240561, y:64027,x:241838, y:64535,x:243124, y:64801,x:244409, y:64800,x:245551, y:64593,x:246415, y:64244,x:247110, y:63784,x:247903, y:63150,x:248660, y:62373,x:249302, y:61458,x:249797, y:60520,x:250146, y:59631,x:250374, y:58757,x:250500, y:57866,x:250519, y:56984,x:250424, y:56136,x:250146, y:55205,x:249614, y:54077,x:248704, y:52849,x:247291, y:51617,x:245560, y:50593,x:243701, y:49985,x:241853, y:49843,x:240160, y:50210,x:238658, y:50831,x:237385, y:51446,x:236209, y:52187,x:234996, y:53190,x:230258, y:57527,x:228737, y:58982,x:227186, y:60389,x:225276, y:61896,x:223300, y:63202,x:221254, y:64270,x:219188, y:65048,x:217152, y:65484,x:215204, y:65607,x:213139, y:65463,x:211205, y:65087,x:209614, y:64552,x:205461, y:62737,x:203070, y:61891,x:201350, y:61751,x:200228, y:62008,x:199604, y:62347,x:199408, y:62763,x:199462, y:62897,x:199565, y:63253,x:199699, y:63404,x:199970, y:63774,x:200515, y:64281,x:201601, y:65494,x:206078, y:71356,x:208475, y:74334,x:209829, y:75847,x:210957, y:77073,x:212207, y:78247,x:213661, y:79574,x:216451, y:81729,x:219199, y:83469,x:221781, y:84826,x:224071, y:85813,x:226242, y:86783,x:228467, y:88089,x:230413, y:89464,x:231748, y:90646,x:232679, y:91806,x:233411, y:93115,x:233960, y:94489,x:234337, y:95840,x:234583, y:97379,x:234739, y:99317,x:234692, y:101405,x:234325, y:103396,x:233691, y:105267,x:232842, y:106995,x:231823, y:108460,x:230677, y:109541,x:229358, y:110393,x:227817, y:111171,x:226089, y:111743,x:224210, y:111971,x:222403, y:111908,x:220888, y:111600,x:219478, y:111016,x:217985, y:110121,x:216610, y:109000,x:215553, y:107736,x:214797, y:106371,x:214325, y:104944,x:214115, y:103385,x:214149, y:101622,x:214490, y:99849,x:215202, y:98257,x:216289, y:96828,x:217755, y:95546,x:219416, y:94589,x:221088, y:94136,x:222625, y:94018,x:223879, y:94068,x:224728, y:94243,x:225061, y:94506,x:225061, y:94810,x:224910, y:95110,x:224512, y:95325,x:222753, y:95451,x:221555, y:95740,x:220309, y:96302,x:219151, y:97196,x:218143, y:98286,x:217350, y:99437,x:216807, y:100647,x:216546, y:101914,x:216528, y:103087,x:216710, y:104016,x:217090, y:104886,x:217667, y:105883,x:218447, y:106877,x:219437, y:107737,x:220495, y:108404,x:221483, y:108817,x:222510, y:109039,x:223689, y:109136,x:224967, y:109025,x:226296, y:108622,x:227540, y:108034,x:228566, y:107364,x:229422, y:106548,x:230155, y:105520,x:230735, y:104377,x:231132, y:103218,x:231340, y:101952,x:231351, y:100487,x:231213, y:99072,x:230971, y:97954,x:230605, y:96955,x:230097, y:95892,x:229387, y:94877,x:228416, y:94016,x:227070, y:93208,x:225239, y:92348,x:223092, y:91579,x:220793, y:91048,x:218108, y:90385,x:214798, y:89220,x:211218, y:87698,x:207730, y:85954,x:201828, y:82734,x:200480, y:82153,x:199580, y:81744,x:197779, y:81453,x:196305, y:81744,x:196133, y:81876,x:195046, y:82494,x:194927, y:82657,x:194004, y:83644,x:193181, y:85138,x:192590, y:86487,x:191927, y:87923,x:191256, y:88979,x:190331, y:89912,x:189234, y:90698,x:188046, y:91309,x:187094, y:91692,x:185949, y:92095,x:184820, y:92362,x:183161, y:92637,x:181988, y:93095,x:181648, y:93208,x:180982, y:94370,x:180830, y:95752,x:180871, y:97004,x:181129, y:98257,x:181619, y:99644,x:182306, y:101102,x:183138, y:102562,x:184642, y:104935,x:185356, y:105876,x:186304, y:106875,x:187017, y:107580,x:188548, y:109163,x:189654, y:110472,x:190614, y:111878,x:191343, y:113491,x:191753, y:115425,x:191834, y:117748,x:191575, y:120531,x:190847, y:123334,x:189523, y:125716,x:187871, y:127656,x:186158, y:129132,x:184320, y:130171,x:182291, y:130804,x:180106, y:130965,x:177798, y:130593,x:175707, y:129890,x:174171, y:129057,x:172992, y:128095,x:171972, y:127007,x:171125, y:125776,x:170469, y:124388,x:170013, y:122815,x:169765, y:121030,x:169859, y:119188,x:170428, y:117447,x:171441, y:115768,x:172869, y:114115,x:174517, y:112745,x:176186, y:111916,x:177775, y:111508,x:179184, y:111405,x:180141, y:111619,x:180374, y:112171,x:180000, y:112723,x:179133, y:112937,x:177992, y:113127,x:176798, y:113609,x:175625, y:114435,x:174551, y:115663,x:173646, y:117095,x:172980, y:118539,x:172589, y:119960,x:172509, y:121321,x:172674, y:122514,x:173018, y:123429,x:173541, y:124228,x:174244, y:125071,x:175124, y:125872,x:176173, y:126542,x:177396, y:127071,x:178794, y:127448,x:180313, y:127596,x:181897, y:127439,x:183470, y:126948,x:184953, y:126090,x:186234, y:124862,x:187197, y:123262,x:187811, y:121469,x:188046, y:119664,x:188011, y:118027,x:187814, y:116737,x:187427, y:115677,x:186815, y:114730,x:185978, y:113778,x:184916, y:112704,x:183775, y:111660,x:182699, y:110800,x:181508, y:109894,x:180024, y:108714,x:178474, y:107341,x:177085, y:105857,x:175841, y:104347,x:174724, y:102898,x:173744, y:101485,x:172911, y:100084,x:172112, y:98383,x:171245, y:96089,x:170550, y:93676,x:170313, y:91627,x:170236, y:91214,x:170099, y:90084,x:169966, y:89933,x:169517, y:89268,x:168804, y:88662,x:168177, y:87763,x:167578, y:86544,x:166956, y:84984,x:166390, y:83347,x:165963, y:81894,x:165530, y:80613,x:165019, y:79647,x:164946, y:79483,x:164017, y:78605,x:163861, y:78429,x:162243, y:77571,x:161925, y:77384,x:159690, y:76604,x:157699, y:76346,x:155903, y:76419,x:154252, y:76631,x:152712, y:76974,x:151247, y:77437,x:149765, y:78069,x:148181, y:78933,x:146734, y:80013,x:145673, y:81290,x:145010, y:82881,x:144761, y:84903,x:144858, y:87106,x:145232, y:89245,x:145717, y:91431,x:146153, y:93780,x:146460, y:96259,x:146561, y:98838,x:146435, y:101180,x:146054, y:102946,x:145479, y:104394,x:144766, y:105780,x:143796, y:107138,x:142449, y:108502,x:140866, y:109735,x:139189, y:110701,x:137521, y:111340,x:135965, y:111590,x:134395, y:111552,x:132682, y:111329,x:130932, y:110831,x:129250, y:109962,x:127790, y:108892,x:126710, y:107786,x:125873, y:106510,x:125141, y:104931,x:124634, y:103231,x:124470, y:101592,x:124612, y:100038,x:125024, y:98592,x:125728, y:97184,x:126747, y:95745,x:128025, y:94470,x:129509, y:93554,x:131211, y:92984,x:133145, y:92747,x:135056, y:92888,x:136693, y:93454,x:138005, y:94242,x:138942, y:95053,x:139413, y:95751,x:139324, y:96200,x:138862, y:96308,x:138212, y:95984,x:137330, y:95477,x:136178, y:95043,x:134832, y:94808,x:133371, y:94896,x:131925, y:95231,x:130622, y:95738,x:129492, y:96433,x:128566, y:97334,x:127892, y:98294,x:127520, y:99164,x:127345, y:100098,x:127263, y:101246,x:127349, y:102507,x:127684, y:103774,x:128185, y:104920,x:128770, y:105816,x:129494, y:106576,x:130414, y:107319,x:131534, y:107945,x:132859, y:108358,x:134218, y:108570,x:135442, y:108593,x:136610, y:108398,x:137794, y:107955,x:138906, y:107308,x:139857, y:106501,x:140648, y:105576,x:141284, y:104576,x:141768, y:103516,x:142104, y:102411,x:142281, y:100995,x:142289, y:99000,x:142055, y:96680,x:141501, y:94291,x:140832, y:92061,x:140039, y:89508,x:139470, y:86999,x:139263, y:84193,x:139473, y:81495,x:140165, y:79156,x:141496, y:77058,x:143633, y:74933,x:146062, y:73004,x:148266, y:71486,x:151191, y:69626,x:154462, y:67477,x:158347, y:64821,x:159387, y:64139,x:159980, y:63704,x:160274, y:63362,x:160413, y:63225,x:160583, y:62757,x:160391, y:62349,x:159756, y:61958,x:158598, y:61545,x:156765, y:61123,x:154123, y:60699,x:152279, y:60454,x:149652, y:60118,x:147440, y:59875,x:145026, y:59662,x:143322, y:59571,x:141958, y:59574,x:140794, y:59716,x:139688, y:60044,x:138729, y:60449,x:137467, y:61037,x:134669, y:62442,x:129589, y:65097,x:128143, y:65789,x:126645, y:66387,x:124902, y:66938,x:123213, y:67390,x:121880, y:67699,x:120472, y:67847,x:118561, y:67825,x:116635, y:67669,x:115179, y:67417,x:113930, y:67021,x:112630, y:66437,x:111376, y:65714,x:110266, y:64898,x:109248, y:63822,x:108269, y:62316,x:107469, y:60678,x:106996, y:59203,x:106771, y:57799,x:106726, y:56371,x:106839, y:54942,x:107089, y:53540,x:107512, y:52193,x:108114, y:50973,x:108914, y:49784,x:109819, y:48762,x:110884, y:47886,x:112149, y:47178,x:113638, y:46652,x:115377, y:46326,x:117191, y:46299,x:118908, y:46673,x:120545, y:47468,x:122115, y:48650,x:123405, y:50060,x:124207, y:51607,x:124627, y:53136,x:124676, y:53776,x:124760, y:54494,x:124694, y:55132,x:124678, y:55485,x:124507, y:55910,x:124294, y:55986,x:124074, y:55925,x:123816, y:55562,x:123713, y:55237,x:123509, y:54723,x:123305, y:54085,x:123128, y:53618,x:122582, y:52462,x:121793, y:51360,x:120697, y:50414,x:119439, y:49659,x:118162, y:49127,x:116876, y:48848,x:115591, y:48850,x:114449, y:49067,x:113585, y:49432,x:112890, y:49913,x:112095, y:50580,x:111337, y:51394,x:110698, y:52348,x:110204, y:53330,x:109855, y:54260,x:109628, y:55173,x:109502, y:56103,x:109487, y:57036,x:109593, y:57959,x:109904, y:59022,x:110503, y:60373,x:111277, y:61664,x:112115, y:62549,x:112988, y:63188,x:113863, y:63743,x:114737, y:64189,x:115603, y:64502,x:116595, y:64710,x:117845, y:64838,x:119088, y:64870,x:120055, y:64785,x:121017, y:64583,x:122247, y:64259,x:123580, y:63830,x:124853, y:63312,x:126202, y:62659,x:127762, y:61831,x:129359, y:60890,x:130823, y:59902,x:133669, y:57816,x:135202, y:56813,x:136898, y:55948,x:138724, y:55235,x:140646, y:54693,x:142636, y:54268,x:144234, y:53994,x:146301, y:53652,x:148410, y:53280,x:153228, y:52379,x:155650, y:51904,x:158053, y:51458,x:159019, y:51255,x:160238, y:50648,x:160376, y:49456,x:160279, y:49226,x:159856, y:47941,x:159087, y:46380,x:158081, y:44872,x:157697, y:44398,x:156909, y:43476,x:155932, y:42575,x:155225, y:41841,x:154396, y:41429,x:152281, y:40432,x:151287, y:40284,x:150630, y:40081,x:149858, y:39361,x:149914, y:38498,x:149916, y:36556,x:150565, y:32349,x:150976, y:30265,x:151553, y:30300,x:153922, y:30926,x:155554, y:31405,x:159500, y:32983,x:162611, y:34323,x:164904, y:35509,x:166826, y:36623,x:168544, y:37474,x:170094, y:37967,x:171510, y:38004,x:172912, y:37695,x:174413, y:37154,x:175785, y:36523,x:176798, y:35941,x:177650, y:35340,x:178536, y:34652,x:181749, y:31995,x:183759, y:30041,x:185190, y:28039,x:186028, y:26014,x:186204, y:24049,x:185984, y:22238,x:185629, y:20674,x:185037, y:19242,x:184105, y:17838,x:183034, y:16546,x:182032, y:15461,x:179368, y:12777,x:177611, y:11055,x:175800, y:9357,x:174020, y:7591,x:172350, y:5650,x:170923, y:3683,x:169783, y:1745,x:168938, y:-182,x:168394, y:-2120,x:168205, y:-4304,x:168427, y:-6969,x:169153, y:-9668,x:170477, y:-11950,x:172129, y:-13805,x:173842, y:-15221,x:175642, y:-16198,x:177708, y:-16821,x:179892, y:-16964,|";
						directPolygons = MSClipperLib.CLPolygonsExtensions.CreateFromString(roctopus);
					}
					break;

				case 6: // Spiral
					{
						spiral sp = new spiral(Width / 2, Height / 2, 10, 150, 30, 0.0);
						Stroke stroke = new Stroke(sp);
						stroke.width(15.0);

						Affine mtx = Affine.NewIdentity(); ;
						mtx *= Affine.NewTranslation(-1150, -1150);
						mtx *= Affine.NewScaling(2.0);

						pathToUse = stroke;
					}
					break;

				case 7: // Glyph
					{
						//------------------------------------
						// Spiral and glyph
						//
						PathStorage glyph = new PathStorage();
						glyph.MoveTo(28.47, 6.45);
						glyph.curve3(21.58, 1.12, 19.82, 0.29);
						glyph.curve3(17.19, -0.93, 14.21, -0.93);
						glyph.curve3(9.57, -0.93, 6.57, 2.25);
						glyph.curve3(3.56, 5.42, 3.56, 10.60);
						glyph.curve3(3.56, 13.87, 5.03, 16.26);
						glyph.curve3(7.03, 19.58, 11.99, 22.51);
						glyph.curve3(16.94, 25.44, 28.47, 29.64);
						glyph.LineTo(28.47, 31.40);
						glyph.curve3(28.47, 38.09, 26.34, 40.58);
						glyph.curve3(24.22, 43.07, 20.17, 43.07);
						glyph.curve3(17.09, 43.07, 15.28, 41.41);
						glyph.curve3(13.43, 39.75, 13.43, 37.60);
						glyph.LineTo(13.53, 34.77);
						glyph.curve3(13.53, 32.52, 12.38, 31.30);
						glyph.curve3(11.23, 30.08, 9.38, 30.08);
						glyph.curve3(7.57, 30.08, 6.42, 31.35);
						glyph.curve3(5.27, 32.62, 5.27, 34.81);
						glyph.curve3(5.27, 39.01, 9.57, 42.53);
						glyph.curve3(13.87, 46.04, 21.63, 46.04);
						glyph.curve3(27.59, 46.04, 31.40, 44.04);
						glyph.curve3(34.28, 42.53, 35.64, 39.31);
						glyph.curve3(36.52, 37.21, 36.52, 30.71);
						glyph.LineTo(36.52, 15.53);
						glyph.curve3(36.52, 9.13, 36.77, 7.69);
						glyph.curve3(37.01, 6.25, 37.57, 5.76);
						glyph.curve3(38.13, 5.27, 38.87, 5.27);
						glyph.curve3(39.65, 5.27, 40.23, 5.62);
						glyph.curve3(41.26, 6.25, 44.19, 9.18);
						glyph.LineTo(44.19, 6.45);
						glyph.curve3(38.72, -0.88, 33.74, -0.88);
						glyph.curve3(31.35, -0.88, 29.93, 0.78);
						glyph.curve3(28.52, 2.44, 28.47, 6.45);
						glyph.ClosePolygon();

						glyph.MoveTo(28.47, 9.62);
						glyph.LineTo(28.47, 26.66);
						glyph.curve3(21.09, 23.73, 18.95, 22.51);
						glyph.curve3(15.09, 20.36, 13.43, 18.02);
						glyph.curve3(11.77, 15.67, 11.77, 12.89);
						glyph.curve3(11.77, 9.38, 13.87, 7.06);
						glyph.curve3(15.97, 4.74, 18.70, 4.74);
						glyph.curve3(22.41, 4.74, 28.47, 9.62);
						glyph.ClosePolygon();

						Affine mtx = Affine.NewIdentity();
						mtx *= Affine.NewScaling(4.0);
						mtx *= Affine.NewTranslation(220, 200);
						VertexSourceApplyTransform trans = new VertexSourceApplyTransform(glyph, mtx);
						FlattenCurves curve = new FlattenCurves(trans);

						pathToUse = curve;
					}
					break;
			}

			if (directPolygons == null)
			{
				polygonsToPathAround = PolygonsToMSPolygons(FixWinding(VertexSourceToClipperPolygons.CreatePolygons(pathToUse, 1)));
				scale = 1;
				offset = new MSIntPoint(0, 0);
			}
			else
			{
				polygonsToPathAround = directPolygons;
				var bounds = MSClipperLib.CLPolygonsExtensions.GetBounds(directPolygons);
				long width = (bounds.maxX - bounds.minX) / (long)Width * 4 / 3;
				long height = (bounds.maxY - bounds.minY) / (long)Height * 4 / 3;
				scale = Math.Max(width, height);
				offset = new MSIntPoint(-(bounds.maxX + bounds.minX) / 2 / scale + Width / 2, -(bounds.maxY + bounds.minY) / 2 / scale + Height / 2);
			}
		}
	}

	public class spiral : IVertexSource
	{
		private double m_angle;
		private double m_curr_r;
		private double m_da;
		private double m_dr;
		private double m_r1;
		private double m_r2;
		private bool m_start;
		private double m_start_angle;
		private double m_step;
		private double m_x;
		private double m_y;

		public spiral(double x, double y, double r1, double r2, double step, double start_angle = 0)
		{
			m_x = x;
			m_y = y;
			m_r1 = r1;
			m_r2 = r2;
			m_step = step;
			m_start_angle = start_angle;
			m_angle = start_angle;
			m_da = agg_basics.deg2rad(4.0);
			m_dr = m_step / 90.0;
		}

		public void rewind(int index)
		{
			m_angle = m_start_angle;
			m_curr_r = m_r1;
			m_start = true;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (m_curr_r > m_r2)
			{
				return ShapePath.FlagsAndCommand.CommandStop;
			}

			x = m_x + Math.Cos(m_angle) * m_curr_r;
			y = m_y + Math.Sin(m_angle) * m_curr_r;
			m_curr_r += m_dr;
			m_angle += m_da;
			if (m_start)
			{
				m_start = false;
				return ShapePath.FlagsAndCommand.CommandMoveTo;
			}
			return ShapePath.FlagsAndCommand.CommandLineTo;
		}

		public IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}
	}

	internal class conv_poly_counter
	{
		private int m_contours;
		private int m_points;

		private conv_poly_counter(IVertexSource src)
		{
			m_contours = 0;
			m_points = 0;

			foreach (VertexData vertexData in src.Vertices())
			{
				if (ShapePath.is_vertex(vertexData.command))
				{
					++m_points;
				}

				if (ShapePath.is_move_to(vertexData.command))
				{
					++m_contours;
				}
			}
		}
	}
}
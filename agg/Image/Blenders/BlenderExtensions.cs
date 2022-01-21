﻿//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// Adaptation for high precision colors has been sponsored by
// Liberty Technology Systems, Inc., visit http://lib-sys.com
//
// Liberty Technology Systems, Inc. is the provider of
// PostScript and PDF technology for software developers.
//
//----------------------------------------------------------------------------
namespace MatterHackers.Agg.Image
{
    public static class BlenderExtensions
	{
		// Compute a fixed color from a source and a target alpha
		public static Color Blend(this IRecieveBlenderByte blender, Color start, Color blend)
		{
			var result = new byte[] { start.blue, start.green, start.red, start.alpha };
			blender.BlendPixel(result, 0, blend);

			return new Color(result[2], result[1], result[0], result[3]);
		}
	}
}

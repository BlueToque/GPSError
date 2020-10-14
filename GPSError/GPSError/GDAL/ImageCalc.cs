using System;

namespace GPSError.GDAL
{
    static class ImageCalc
    {
        private static bool IsSizeTilePower2(int sizeTile)
        {
            double dbSizeTile = Math.Log(sizeTile, 2);

            return (dbSizeTile - Math.Floor(dbSizeTile) != 0) ? false : true;
        }

        /// <summary>
        /// Calculate the levels for a raster of the given resolution
        /// </summary>
        /// <param name="sizeTile"></param>
        /// <param name="rasterXSize"></param>
        /// <param name="rasterYSize"></param>
        /// <returns></returns>
        public static int GetLevels(int sizeTile, int rasterXSize, int rasterYSize)
        {
            if (!IsSizeTilePower2(sizeTile))
                throw (new Exception(string.Format("{0}/{1}: SizeTile({2}) not power 2 ", "ImageCalculus", "GetTotalLevelForPyramid", sizeTile)));

            double xLevel = Math.Log((double)(rasterXSize / sizeTile), 2),
                   yLevel = Math.Log((double)(rasterYSize / sizeTile), 2);

            double xLevelFloor = Math.Floor(xLevel),
                   yLevelFloor = Math.Floor(yLevel);

            int xLevelInt = (int)xLevelFloor,
                yLevelInt = (int)yLevelFloor;

            if (xLevelFloor < xLevel) xLevelInt++;
            if (yLevelFloor < yLevel) yLevelInt++;

            return xLevelInt > yLevelInt ? xLevelInt : yLevelInt;
        }

    }
}

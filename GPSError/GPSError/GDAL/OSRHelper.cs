using OSGeo.OGR;
using System;
using System.Diagnostics;
using SpatialReference = OSGeo.OGR.SpatialReference;

namespace GPSError.GDAL
{
    static class OSRHelper
    {
        public static SpatialReference SRSFromEPSG(string epsg)
        {
            if (epsg.StartsWith("EPSG:"))
                epsg = epsg.Replace("EPSG:", "");

            if (!Int32.TryParse(epsg, out int code))
            {
                Trace.TraceError("OsrHelper.SRSFromEPSG: Can't parse EPSG code {0}, not an integer", epsg);
                return null;
            }

            return SRSFromEPSG(code);
        }

        public static SpatialReference SRSFromEPSG(int epsg)
        {
            try
            {
                SpatialReference srs = new SpatialReference(null);
                int code = srs.ImportFromEPSG(epsg);
                if (code != Ogr.OGRERR_NONE)
                {
                    Trace.TraceError("OsrHelper.SRSFromEPSG: Can't get SRS from EPSG Code \"{0}\": {1}", epsg, OGRHelper.ReportOgrError(code));
                    return null;
                }

                return srs;
            }
            catch (Exception ex)
            {
                Trace.TraceError("OsrHelper.SRSFromEPSG: Error trying to create SpatialReference from EPSG code {0}:\r\n{1}", epsg, ex);
                return null;
            }
        }

        /// <summary>
        /// Convert a spatial reference system to Well Known Text
        /// </summary>
        /// <param name="srs"></param>
        /// <returns></returns>
        public static string ToWkt(this SpatialReference srs)
        {
            srs.ExportToWkt(out string result);
            return result;
        }

        public static PointDType TransformPoint(this CoordinateTransformation tx, PointDType point)
        {
            if (point.X > 180) point.X = 180.0;
            if (point.X < -180) point.X = -180.0;
            if (point.Y > 90) point.Y = 90.0;
            if (point.Y < -90) point.Y = -90.0;
            double[] arr = new double[3] { point.X, point.Y, point.Z };
            tx.TransformPoint(arr);

            return new PointDType(arr[0], arr[1], arr[2]);
        }
    }
}

using OSGeo.OGR;
using System.Text;

namespace GPSError.GDAL
{
    public static class OGRHelper
    {
        /// <summary>
        /// Take an OGR Error and convert it to a human readable error string
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string ReportOgrError(int code)
        {
            StringBuilder sb = new StringBuilder();

            if (code == Ogr.OGRERR_CORRUPT_DATA) sb.Append("Corrupt data");
            else if (code == Ogr.OGRERR_FAILURE) sb.Append("Failure");
            else if (code == Ogr.OGRERR_NONE) sb.Append("No Error");
            else if (code == Ogr.OGRERR_NOT_ENOUGH_DATA) sb.Append("Not enough data");
            else if (code == Ogr.OGRERR_NOT_ENOUGH_MEMORY) sb.Append("Not enough memory");
            else if (code == Ogr.OGRERR_UNSUPPORTED_GEOMETRY_TYPE) sb.Append("Unsupported Geometry Type");
            else if (code == Ogr.OGRERR_UNSUPPORTED_OPERATION) sb.Append("Unsupported Operation");
            else if (code == Ogr.OGRERR_UNSUPPORTED_SRS) sb.Append("Unsupported Spatial Reference");
            else sb.AppendFormat("Unknown error code {0}", code);

            //if (code != Ogr.OGRERR_NONE)
            //    Trace.TraceError("OGR Error: {0}", sb);

            return sb.ToString();
        }
    }
}

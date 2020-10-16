using OSGeo.GDAL;
using OSGeo.OGR;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SpatialReference = OSGeo.OSR.SpatialReference;

namespace GPSError.GDAL
{
    /// <summary>
    /// Assists setting up GDAL
    /// </summary>
    public static partial class GDALHelper
    {
        private static bool s_configuredOgr;
        private static bool s_configuredGdal;

        /// <summary>
        /// Function to determine which platform we're on
        /// </summary>
        private static string GetPlatform() => IntPtr.Size == 4 ? "x86" : "x64";

        public static string GDALPath { get; private set; }

        public static string GDALData { get; private set; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetDllDirectory(string lpPathName);

        /// <summary> Construction of Gdal/Ogr </summary>
        static GDALHelper()
        {
            var executingAssemblyFile = new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath;
            var executingDirectory = Path.GetDirectoryName(executingAssemblyFile);

            if (string.IsNullOrEmpty(executingDirectory))
                throw new InvalidOperationException("cannot get executing directory");

            var gdalPath = Path.Combine(executingDirectory, "gdal");
            GDALPath = Path.Combine(gdalPath, GetPlatform());

            if (!Directory.Exists(GDALPath))
                throw new DirectoryNotFoundException("Could not find GDAL directory");

            SetDllDirectory(GDALPath);

            // Prepend native path to environment path, to ensure the right libs are being used.
            var path = Environment.GetEnvironmentVariable("PATH");
            path = GDALPath + ";" + Path.Combine(GDALPath, "plugins") + ";" + path;
            Environment.SetEnvironmentVariable("PATH", path);

            // Set the additional GDAL environment variables.
            GDALData = Path.Combine(gdalPath, "data");
            Environment.SetEnvironmentVariable("GDAL_DATA", GDALData);
            Gdal.SetConfigOption("GDAL_DATA", GDALData);

            var driverPath = Path.Combine(GDALPath, "gdalplugins");
            Environment.SetEnvironmentVariable("GDAL_DRIVER_PATH", driverPath);
            Gdal.SetConfigOption("GDAL_DRIVER_PATH", driverPath);

            Environment.SetEnvironmentVariable("GEOTIFF_CSV", GDALData);
            Gdal.SetConfigOption("GEOTIFF_CSV", GDALData);

            var projSharePath = Path.Combine(gdalPath, "projlib");
            Environment.SetEnvironmentVariable("PROJ_LIB", projSharePath);
            Gdal.SetConfigOption("PROJ_LIB", projSharePath);
        }

        /// <summary>
        /// Method to ensure the static constructor is being called.
        /// </summary>
        /// <remarks>Be sure to call this function before using Gdal/Ogr/Osr</remarks>
        public static void ConfigureOgr()
        {
            if (s_configuredOgr) return;

            // Register drivers
            Ogr.RegisterAll();
            s_configuredOgr = true;

            Trace.TraceInformation(PrintDriversOgr());
        }

        /// <summary>
        /// Method to ensure the static constructor is being called.
        /// </summary>
        /// <remarks>Be sure to call this function before using Gdal/Ogr/Osr</remarks>
        public static void ConfigureGdal()
        {
            if (s_configuredGdal) return;

            // Register drivers
            Gdal.AllRegister();
            s_configuredGdal = true;

            Trace.TraceInformation(PrintDriversGdal());
        }

        static string s_version;

        /// <summary>
        /// Return the version of GDAL that is installed
        /// </summary>
        public static string Version => s_version ??= Gdal.VersionInfo("RELEASE_NAME");

        private static string PrintDriversOgr()
        {
            StringBuilder sb = new StringBuilder();
            var num = Ogr.GetDriverCount();
            for (var i = 0; i < num; i++)
            {
                var driver = Ogr.GetDriver(i);
                sb.AppendFormat("OGR {0}: {1}\r\n", i, driver.name);
            }
            return sb.ToString();
        }

        public static string PrintDriversGdal()
        {
            StringBuilder sb = new StringBuilder();
            var num = Gdal.GetDriverCount();
            for (var i = 0; i < num; i++)
            {
                var driver = Gdal.GetDriver(i);
                sb.AppendFormat("GDAL {0}: {1}-{2}\r\n", i, driver.ShortName, driver.LongName);
            }
            return sb.ToString();
        }

        public static string GetGeogCS(this SpatialReference srs) =>
            //PROJCS, GEOGCS, DATUM, SPHEROID, and PROJECTION
            srs.GetAttrValue("GEOGCS", 0);//string wkt = srs.ToWkt();//return GetValueOf(wkt, "GEOGCS");

        public static string GetProjectionName(this SpatialReference srs) => srs.GetAttrValue("PROJECTION", 0);//string wkt = srs.ToWkt();//return GetValueOf(wkt, "DATUM");
        
        public static string GetDatumName(this SpatialReference srs) => srs.GetAttrValue("DATUM", 0);//string wkt = srs.ToWkt();//return GetValueOf(wkt, "DATUM");

        public static string GetProjCS(this SpatialReference srs) => srs.GetAttrValue("PROJCS", 0);//string wkt = srs.ToWkt();//return GetValueOf(wkt, "PROJCS");

        public static string GetSpheroidName(this SpatialReference srs) => srs.GetAttrValue("SPHEROID", 0);//string wkt = srs.ToWkt();//return GetValueOf(wkt, "SPHEROID");

        public static string GetAuthorityCode(this SpatialReference srs) => srs.GetAuthorityCode(null);

        public static string GetAuthorityName(this SpatialReference srs) => srs.GetAuthorityName(null);

        public static SpatialReference SRSFromEPSG(string epsg)
        {
            if (epsg.StartsWith("EPSG:"))
                epsg = epsg.Replace("EPSG:", "");

            if (!int.TryParse(epsg, out int code))
            {
                Trace.TraceError("Can't parse EPSG code {0}, not an integer", epsg);
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
                    Trace.TraceError("Can't get SRS from EPSG Code \"{0}\": {1}", epsg, GDALHelper.ReportOgrError(code));
                    return null;
                }

                return srs;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error trying to create SpatialReference from EPSG code {0}:\r\n{1}", epsg, ex);
                return null;
            }
        }

        /// <summary>
        /// Create an SRS from user input
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static SpatialReference SRSFromUserInput(string input)
        {
            if (input.StartsWith("EPSG:"))
            {
                int code = Int32.Parse(input.Replace("EPSG:", ""));
                return SRSFromEPSG(code);
            }

            SpatialReference srs = new SpatialReference(null);
            srs.SetFromUserInput(input);

            //srs.SetWellKnownGeogCS(input);
            return srs;
        }

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
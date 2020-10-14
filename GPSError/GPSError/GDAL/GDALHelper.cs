using OSGeo.GDAL;
using OSGeo.OGR;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using SpatialReference = OSGeo.OGR.SpatialReference;

namespace GPSError.GDAL
{
    /// <summary>
    /// Assists setting up GDAL
    /// </summary>
    public static partial class GDALHelper
    {
        /// <summary>
        /// structure holds dataset parameters and descriptors
        /// </summary>
        public class DatasetParameters
        {
            public int ChannelSize = 8;
            public bool HasAlpha = false;
            public int[] BandMap = new int[4] { 1, 1, 1, 1 };
            public int ChannelCount = 1;
            public bool IsIndexed = false;
            public ColorTable ColorTable = null;
            public PixelFormat PixelFormat = PixelFormat.DontCare;
            public DataType DataType = DataType.GDT_Unknown;
            public int PixelSpace = 1;
        }

        private static bool s_configuredOgr;
        private static bool s_configuredGdal;

        public static long ALPHA_MASK = 0xFFFFFFFFL;
        public static byte ALPHA_TRANSPARENT = (byte)0x00;
        public static byte ALPHA_OPAQUE = (byte)0xFF;

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

            PrintDriversOgr();
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
#pragma warning disable IDE0074 // Use compound assignment
        public static string Version => s_version ?? (s_version = Gdal.VersionInfo("RELEASE_NAME"));
#pragma warning restore IDE0074 // Use compound assignment

        /// <summary>
        /// A temp path where we can manage files
        /// </summary>
        public static string TempPath
        {
            get
            {
                string path = Path.Combine(Path.GetTempPath(), "BlueToque.GDAL");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Get an extension depending on the driver
        /// </summary>
        /// <param name="driver"></param>
        /// <returns></returns>
        public static string GetExtension(string driver)
        {
            switch (driver)
            {
                case "MEM": return null;
                case "GTiff": return ".tif";
                case "PNG": return ".png";
                case "ECW": return ".ecw";
                case "MrSID": return ".sid";
                case "JPEG": return ".jpg";
                case "VRT": return ".vrt";
                case "PDF": return ".pdf";
                default: return null;
            }
        }

        /// <summary>
        /// Get the well know text from the dataset
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static string GetWkt(this Dataset ds) => ds.GetProjectionRef();

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

        /// <summary>
        /// Open a dataset
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static Dataset OpenDataSet(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Trace.TraceError("Error, file \"{0}\" does not exist when attempting to open as GDAL Dataset", fileName);
                return null;
            }

            Dataset ds = Gdal.OpenShared(fileName, Access.GA_ReadOnly);

            if (ds == null)
            {
                Trace.TraceError("Can't open {0}", fileName);
                return null;
            }

            Trace.TraceInformation("Raster dataset parameters:");
            Trace.TraceInformation("  Projection: {0}", ds.GetProjectionRef());
            Trace.TraceInformation("  RasterCount: {0}", ds.RasterCount);
            Trace.TraceInformation("  RasterSize ({0}.{1})", ds.RasterXSize, ds.RasterYSize);

            // Get driver
            using (OSGeo.GDAL.Driver drv = ds.GetDriver())
                Trace.TraceInformation("  Using driver {0}", drv == null ? "<null>" : drv.LongName);
            return ds;
        }

        /// <summary>
        /// Copy the dataset using the given driver, file name and options
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="driver"></param>
        /// <param name="filename"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        [HandleProcessCorruptedStateExceptions()]
        public static Dataset Copy(this Dataset ds, string filename, string driver = "GTiff", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            try
            {
                using (var drv = Gdal.GetDriverByName(driver))
                {
                    if (driver == null)
                    {
                        Trace.TraceError("GDALHelper.Copy: Could not load driver {0}", driver);
                        return null;
                    }

                    Dataset dataset = drv.CreateCopy(filename, ds, 0, options,
                        delegate (double complete, IntPtr message, IntPtr data)
                        {
                            return GDALHelper.HandleProgress(progress, complete, message, data);
                        },
                    null);

                    if (dataset == null)
                    {
                        Trace.TraceError("GDALHelper.Copy: Error saving dataset to file {0}", filename);
                        return null;
                    }

                    return dataset;
                }
            }
            catch (AccessViolationException ex)
            {
                Trace.TraceError("GDALHelper.Copy: Error copying dataset {0}:\r\n{1}", filename, ex);
                throw new GDALException("Error copying dataset", ex);
            }
            catch (Exception ex)
            {
                Trace.TraceError("GDALHelper.Copy: Error copying dataset {0}:\r\n{1}", filename, ex);
                throw new GDALException("Error copying dataset", ex);
            }
        }

        internal static string GetVersion() => Gdal.VersionInfo("RELEASE_NAME");

        #region extension utility methods

        /// <summary>
        /// Get the nodata value from the band
        /// </summary>
        /// <param name="band"></param>
        /// <returns></returns>
        public static double? GetNoData(this Band band)
        {
            if (band == null) return null;
            band.GetNoDataValue(out double value, out int hasValue);
            return (hasValue == 1) ? value : (double?)null;
        }

        /// <summary>
        /// Retrieve dataset parameters from the dataset
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static DatasetParameters GetBitmapParameters(this Dataset ds)
        {
            DatasetParameters p = new DatasetParameters();

            // Evaluate the bands and find out a proper image transfer format
            for (int i = 0; i < ds.RasterCount; i++)
            {
                Band band = ds.GetRasterBand(i + 1);
                if (Gdal.GetDataTypeSize(band.DataType) > 8)
                    p.ChannelSize = 16;

                switch (band.GetRasterColorInterpretation())
                {
                    case ColorInterp.GCI_AlphaBand:
                        p.ChannelCount = i + 1;
                        p.HasAlpha = true;
                        p.BandMap[3] = i + 1;
                        break;
                    case ColorInterp.GCI_BlueBand:
                        if (p.ChannelCount < 3)
                            p.ChannelCount = 3;
                        p.BandMap[0] = i + 1;
                        break;
                    case ColorInterp.GCI_RedBand:
                        if (p.ChannelCount < 3)
                            p.ChannelCount = 3;
                        p.BandMap[2] = i + 1;
                        break;
                    case ColorInterp.GCI_GreenBand:
                        if (p.ChannelCount < 3)
                            p.ChannelCount = 3;
                        p.BandMap[1] = i + 1;
                        break;
                    case ColorInterp.GCI_PaletteIndex:
                        p.ColorTable = band.GetRasterColorTable();
                        p.IsIndexed = true;
                        p.BandMap[0] = i + 1;
                        break;
                    case ColorInterp.GCI_GrayIndex:
                        p.IsIndexed = true;
                        p.BandMap[0] = i + 1;
                        break;
                    default:
                        // we create the bandmap using the dataset ordering by default
                        if (i < 4 && p.BandMap[i] == 0)
                        {
                            if (p.ChannelCount < i)
                                p.ChannelCount = i;
                            p.BandMap[i] = i + 1;
                        }
                        break;
                }
            }

            // find out the pixel format based on the gathered information
            if (p.IsIndexed)
            {
                p.PixelFormat = PixelFormat.Format8bppIndexed;
                p.DataType = DataType.GDT_Byte;
                p.PixelSpace = 1;
                if (p.HasAlpha)
                {
                    p.ChannelCount = 2;
                    p.BandMap[2] = 2;
                }
            }
            else
            {
                if (p.ChannelCount == 1)
                {
                    if (p.ChannelSize > 8)
                    {
                        p.PixelFormat = PixelFormat.Format16bppGrayScale;
                        p.DataType = DataType.GDT_Int16;
                        p.PixelSpace = 2;
                    }
                    else
                    {
                        p.PixelFormat = PixelFormat.Format24bppRgb;
                        p.ChannelCount = 3;
                        p.DataType = DataType.GDT_Byte;
                        p.PixelSpace = 3;
                    }
                }
                else
                {
                    if (p.HasAlpha)
                    {
                        if (p.ChannelSize > 8)
                        {
                            p.PixelFormat = PixelFormat.Format64bppArgb;
                            p.DataType = DataType.GDT_UInt16;
                            p.PixelSpace = 8;
                        }
                        else
                        {
                            p.PixelFormat = PixelFormat.Format32bppArgb;
                            p.DataType = DataType.GDT_Byte;
                            p.PixelSpace = 4;
                        }
                        p.ChannelCount = 4;
                    }
                    else
                    {
                        if (p.ChannelSize > 8)
                        {
                            p.PixelFormat = PixelFormat.Format48bppRgb;
                            p.DataType = DataType.GDT_UInt16;
                            p.PixelSpace = 6;
                        }
                        else
                        {
                            p.PixelFormat = PixelFormat.Format24bppRgb;
                            p.DataType = DataType.GDT_Byte;
                            p.PixelSpace = 3;
                        }
                        p.ChannelCount = 3;
                    }
                }
            }

            return p;
        }

        /// <summary>
        /// Get the extents of a dataset in the cordinate system of the dataset
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static Extents GetExtents(this Dataset dataset)
        {
            if (dataset == null) return null;

            double[] geoTrans = new double[6];
            dataset.GetGeoTransform(geoTrans);

            // no rotation...use default transform
            if (geoTrans[0] == 0 && geoTrans[3] == 0)
                geoTrans = new[] { 999.5, 1, 0, 1000.5, 0, -1 };

            GeoTransform xform = new GeoTransform(geoTrans, dataset.RasterXSize, dataset.RasterYSize);

            Size imagesize = new Size(dataset.RasterXSize, dataset.RasterYSize);
            return Extents.FromLTRB(
                xform.EnvelopeLeft(imagesize.Width, imagesize.Height),
                xform.EnvelopeTop(imagesize.Width, imagesize.Height),
                xform.EnvelopeRight(imagesize.Width, imagesize.Height),
                xform.EnvelopeBottom(imagesize.Width, imagesize.Height));
        }

        /// <summary>
        /// Use a variety of methods to determine the EPSG
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public static string GetEPSG(this Dataset dataset)
        {
            string epsgCode = string.Empty;
            OSGeo.OSR.SpatialReference sr = new OSGeo.OSR.SpatialReference(dataset.GetProjection());

            string cstype;
            if (sr.IsLocal() == 1)
                cstype = string.Empty;
            else if (sr.IsGeographic() == 1)
                cstype = "GEOGCS";
            else
                cstype = "PROJCS";

            if (sr.AutoIdentifyEPSG() == 0)
                epsgCode = sr.GetAuthorityCode(cstype);

            return epsgCode;
        }

        /// <summary>
        /// Get the spatial reference system for the given dataset
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public static SpatialReference GetSRS(this Dataset dataset)
        {
            string wkt = dataset.GetProjection();
            SpatialReference srs = new SpatialReference(wkt);
            return srs;
        }

        /// <summary>
        /// Get the neatline from the dataset if it exists
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static Envelope GetNeatLine(this Dataset ds)
        {
            // get the neatline from the metadata
            string neatline = ds.GetMetadataItem("NEATLINE", string.Empty);
            if (string.IsNullOrEmpty(neatline))
                return null;

            Envelope envelope = new Envelope();
            Geometry g = Geometry.CreateFromWkt(neatline);
            g.GetEnvelope(envelope);

            // check if the neatline is the same as the dataset extents
            Extents ext = g.GetExtents();
            if (ext == ds.GetExtents())
                return null;

            return envelope;
        }

        /// <summary>
        /// Get the extents of this DataSource
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="srs"></param>
        /// <returns></returns>
        public static Extents GetExtents(this DataSource dataSource)
        {
            try
            {
                if (dataSource == null)
                {
                    Trace.TraceError("OGRHelper.GetExtents: Error, DataSource is null when getting extents");
                    return new Extents();
                }

                Extents extents = new Extents();
                for (int i = 0; i < dataSource.GetLayerCount(); i++)
                {
                    Layer layer = dataSource.GetLayerByIndex(i);
                    if (layer == null)
                    {
                        Trace.TraceWarning("OGRHelper.GetExtents: Could not get Layer {0} extents for {1}", 0, dataSource.GetName());
                        continue;
                    }

                    Envelope env = new Envelope();
                    if (layer.GetExtent(env, 1) != 0)
                    {
                        Trace.TraceWarning("OGRHelper.GetExtents: Could not get extents for {0}", dataSource.GetName());
                        return new Extents();
                    }

                    OSGeo.OSR.SpatialReference srs = layer.GetSpatialRef();
                    srs.ExportToWkt(out string srs_string);
                    extents.SRS = srs_string;

                    extents.Intersect(env.ToExtents(srs_string));
                }

                return extents;
            }
            catch (Exception ex)
            {
                Trace.TraceError("OGRHelper.GetExtents: Error getting extent from datasource:\r\n{0}", ex);
                return new Extents();
            }

        }

        /// <summary>
        /// Convert the envelope to an extents structure
        /// </summary>
        /// <param name="env"></param>
        /// <returns></returns>
        public static Extents ToExtents(this Envelope env, string srs = "") =>
            Extents.FromMinMax(env.MinX, env.MaxX, env.MinY, env.MaxY, srs);

        /// <summary>
        /// Get the extents of the geometry
        /// </summary>
        /// <param name="geom"></param>
        /// <returns></returns>
        public static Extents GetExtents(this Geometry geom)
        {
            try
            {
                if (geom == null) return Extents.Empty;
                Envelope e = new Envelope();
                geom.GetEnvelope(e);
                return e.ToExtents();
            }
            catch (Exception ex)
            {
                Trace.TraceError("OGRHelper.GetExtents: Error getting extents of geometry:\r\n{0}", ex);
                return Extents.Empty;
            }
        }

        /// <summary>
        /// GdalInfo
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static string GdalInfo(this Dataset ds)
        {
            StringBuilder sb = new StringBuilder();

            if (ds == null)
                return "dataset cannot be null";

            sb.AppendFormat("Raster dataset parameters:");
            var projection = ds.GetProjectionRef();
            if (string.IsNullOrEmpty(projection))
                projection = "No Projection";
            sb.AppendFormat("  Projection:  {0}", projection);
            sb.AppendFormat("  RasterCount: {0}", ds.RasterCount);
            sb.AppendFormat("  Raster Size: ({0}, {1})", ds.RasterXSize, ds.RasterYSize);

            /* -------------------------------------------------------------------- */
            /*      Get driver                                                      */
            /* -------------------------------------------------------------------- */
            OSGeo.GDAL.Driver drv = ds.GetDriver();
            if (drv == null)
            {
                sb.AppendFormat("Can't get driver.");
                return sb.ToString(); ;
            }

            sb.AppendFormat("Driver: {0}", drv.LongName);

            /* -------------------------------------------------------------------- */
            /*      Get metadata                                                    */
            /* -------------------------------------------------------------------- */
            string[] metadata = ds.GetMetadata("");
            if (metadata.Length > 0)
            {
                sb.AppendFormat("  Metadata:");
                for (int iMeta = 0; iMeta < metadata.Length; iMeta++)
                    sb.AppendFormat("    {0}: {1}", iMeta, metadata[iMeta]);
                sb.AppendFormat("");
            }
            else
                sb.AppendFormat("  No Metadata");

            /* -------------------------------------------------------------------- */
            /*      Report "IMAGE_STRUCTURE" metadata.                              */
            /* -------------------------------------------------------------------- */
            metadata = ds.GetMetadata("IMAGE_STRUCTURE");
            if (metadata.Length > 0)
            {
                sb.AppendFormat("  Image Structure Metadata:");
                for (int iMeta = 0; iMeta < metadata.Length; iMeta++)
                    sb.AppendFormat("    {0}: {1}", iMeta, metadata[iMeta]);
                sb.AppendFormat("");
            }
            else
                sb.AppendFormat("  No Image Structure Metadata");

            /* -------------------------------------------------------------------- */
            /*      Report subdatasets.                                             */
            /* -------------------------------------------------------------------- */
            metadata = ds.GetMetadata("SUBDATASETS");
            if (metadata.Length > 0)
            {
                sb.AppendFormat("  Subdatasets:");
                for (int iMeta = 0; iMeta < metadata.Length; iMeta++)
                    sb.AppendFormat("    {0}: {1}", iMeta, metadata[iMeta]);
                sb.AppendFormat("");
            }
            else
                sb.AppendFormat("  No Subdatasets");

            /* -------------------------------------------------------------------- */
            /*      Report geolocation.                                             */
            /* -------------------------------------------------------------------- */
            metadata = ds.GetMetadata("GEOLOCATION");
            if (metadata.Length > 0)
            {
                sb.AppendFormat("  Geolocation:");
                for (int iMeta = 0; iMeta < metadata.Length; iMeta++)
                    sb.AppendFormat("    {0}: {1}", iMeta, metadata[iMeta]);
                sb.AppendFormat("");
            }
            else
                sb.AppendFormat("  No Geolocation");

            /* -------------------------------------------------------------------- */
            /*      Report corners.                                                 */
            /* -------------------------------------------------------------------- */
            sb.AppendFormat("Corner Coordinates:");
            sb.AppendFormat("  Upper Left:  ({0})", GDALInfoGetPosition(ds, 0.0, 0.0));
            sb.AppendFormat("  Lower Left:  ({0})", GDALInfoGetPosition(ds, 0.0, ds.RasterYSize));
            sb.AppendFormat("  Upper Right: ({0})", GDALInfoGetPosition(ds, ds.RasterXSize, 0.0));
            sb.AppendFormat("  Lower Right: ({0})", GDALInfoGetPosition(ds, ds.RasterXSize, ds.RasterYSize));
            sb.AppendFormat("  Center:      ({0})", GDALInfoGetPosition(ds, ds.RasterXSize / 2, ds.RasterYSize / 2));
            sb.AppendFormat("");

            /* -------------------------------------------------------------------- */
            /*      Report projection.                                              */
            /* -------------------------------------------------------------------- */
            //string projection = ds.GetProjectionRef();
            if (!string.IsNullOrEmpty(projection))
            {
                SpatialReference srs = new SpatialReference(null);
                if (srs.ImportFromWkt(ref projection) == 0)
                {
                    srs.ExportToPrettyWkt(out string wkt, 0);
                    sb.AppendFormat("Coordinate System is:\r\n{0}", wkt);
                }
                else
                {
                    sb.AppendFormat("Coordinate System is:\r\n{0}", projection);
                }
            }

            /* -------------------------------------------------------------------- */
            /*      Report GCPs.                                                    */
            /* -------------------------------------------------------------------- */
            if (ds.GetGCPCount() > 0)
            {
                sb.AppendFormat("GCP Projection: {0}", ds.GetGCPProjection());
                GCP[] GCPs = ds.GetGCPs();
                for (int i = 0; i < ds.GetGCPCount(); i++)
                {
                    sb.AppendFormat("GCP[{0}]: Id={1}, Info={2}", i, GCPs[i].Id, GCPs[i].Info);
                    sb.AppendFormat("          ({0},{1}) => ({2}, {3}, {4})", GCPs[i].GCPPixel, GCPs[i].GCPLine, GCPs[i].GCPX, GCPs[i].GCPY, GCPs[i].GCPZ);
                    sb.AppendFormat("");
                }
                sb.AppendFormat("");

                double[] transform = new double[6];
                Gdal.GCPsToGeoTransform(GCPs, transform, 0);
                sb.AppendFormat("GCP Equivalent geotransformation parameters: ", ds.GetGCPProjection());
                for (int i = 0; i < 6; i++)
                    sb.AppendFormat("t[{0}] = {1}", i, transform[i]);
                sb.AppendFormat("");
            }

            sb.AppendFormat("===== Bands =====");

            /* -------------------------------------------------------------------- */
            /*      Get raster band                                                 */
            /* -------------------------------------------------------------------- */
            for (int iBand = 1; iBand <= ds.RasterCount; iBand++)
            {
                sb.AppendFormat("Band {0}:", iBand);
                Band band = ds.GetRasterBand(iBand);
                sb.AppendFormat(band.GetInfo());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get a position from an x and y pixel 
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static string GDALInfoGetPosition(Dataset ds, double x, double y)
        {
            double[] adfGeoTransform = new double[6];
            double dfGeoX, dfGeoY;
            ds.GetGeoTransform(adfGeoTransform);

            dfGeoX = adfGeoTransform[0] + adfGeoTransform[1] * x + adfGeoTransform[2] * y;
            dfGeoY = adfGeoTransform[3] + adfGeoTransform[4] * x + adfGeoTransform[5] * y;

            return dfGeoX.ToString() + ", " + dfGeoY.ToString();
        }

        /// <summary>
        /// Get an info string from the band
        /// </summary>
        /// <param name="band"></param>
        /// <returns></returns>
        public static string GetInfo(this Band band)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("   DataType:            {0}", Gdal.GetDataTypeName(band.DataType));
            sb.AppendFormat("   ColorInterpretation: {0}", Gdal.GetColorInterpretationName(band.GetRasterColorInterpretation()));
            ColorTable ct = band.GetRasterColorTable();
            if (ct != null)
                sb.AppendFormat("   Band has a color table with {0} entries.", ct.GetCount());

            sb.AppendFormat("   Description:         {0}", band.GetDescription());
            sb.AppendFormat("   Size:               ({0},{1})", band.XSize, band.YSize);
            band.GetBlockSize(out int BlockXSize, out int BlockYSize);
            sb.AppendFormat("   BlockSize:          ({0},{1})", BlockXSize, BlockYSize);
            band.GetMinimum(out double val, out int hasval);
            if (hasval != 0) sb.AppendFormat("   Minimum:             {0}", val);
            band.GetMaximum(out val, out hasval);
            if (hasval != 0) sb.AppendFormat("   Maximum:             {0}", val);
            band.GetNoDataValue(out val, out hasval);
            if (hasval != 0) sb.AppendFormat("   NoDataValue:         {0}", val);
            band.GetOffset(out val, out hasval);
            if (hasval != 0) sb.AppendFormat("   Offset:              {0}", val);
            band.GetScale(out val, out hasval);
            if (hasval != 0) sb.AppendFormat("   Scale:               {0}", val);

            for (int iOver = 0; iOver < band.GetOverviewCount(); iOver++)
            {
                Band over = band.GetOverview(iOver);
                sb.AppendFormat("      OverView {0}:", iOver);
                sb.AppendFormat("         DataType:      {0}", over.DataType);
                sb.AppendFormat("         Size:         ({0},{1})", over.XSize, over.YSize);
                sb.AppendFormat("         PaletteInterp: {0}", over.GetRasterColorInterpretation());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get the transform from the dataset
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static double[] GetTransform(this Dataset ds)
        {
            double[] geoTransform = new double[6];
            ds.GetGeoTransform(geoTransform);
            return geoTransform;
        }

        /// <summary>
        /// Get the envelope of the given dataset
        /// project the envelope to the designated spatial reference if it is not null
        /// </summary>
        /// <param name="dataset"></param>
        /// <param name="dst_sr"></param>
        /// <returns></returns>
        public static Envelope GetEnvelope(this Dataset dataset, OSGeo.OGR.SpatialReference dst_sr = null)
        {
            if (dst_sr == null)
            {
                double[] geo_t = dataset.GetTransform();
                return new Envelope()
                {
                    MinX = geo_t[0],
                    MaxX = geo_t[0] + geo_t[1] * dataset.RasterXSize,
                    MinY = geo_t[3] + geo_t[5] * dataset.RasterYSize,
                    MaxY = geo_t[3]
                };
            }

            using (SpatialReference src_sr = dataset.GetSRS())
            using (OSGeo.OGR.CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr))
            {
                // Up to here, all  the projection have been defined, as well as a 
                // transformation from the source to the destination 
                double[] geo_t = dataset.GetTransform();

                // Work out the boundaries of the new dataset in the target projection
                // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
                // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
                PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
                PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * dataset.RasterXSize, geo_t[3] + geo_t[5] * dataset.RasterYSize, 0.0);
                //PointDType lr = tx.Transform(geo_t[0] +  dataset.RasterXSize, geo_t[3] +  dataset.RasterYSize, 0.0);

                Envelope envelope = new Envelope()
                {
                    MinX = ul.X,    // envelope.MinX
                    MaxX = lr.X,
                    MinY = lr.Y,
                    MaxY = ul.Y     // envelope.MaxY
                };

                return envelope;
            }
        }

        /// <summary>
        /// Internal static method to handle the GDAL progress callback and marshall strings.
        /// This will call the provided progress callback in c#
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="complete"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static int HandleProgress(Func<double, string, string, bool> progress, double complete, IntPtr message, IntPtr data)
        {
            if (progress == null)
                return 1;

            string l_message = (message == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(message);
            string l_data = data == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(data);

            bool cancel = progress(complete, l_message, l_data);
            if (cancel)
                System.Diagnostics.Debugger.Break();
            return cancel ? 0 : 1;
        }

        /// <summary>
        /// Save a dataset
        /// http://stackoverflow.com/questions/3469368/how-to-handle-accessviolationexception
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="path"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        [HandleProcessCorruptedStateExceptions]
        public static bool Save(this Dataset ds, string path, string driver = "GTiff", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            try
            {
                using (OSGeo.GDAL.Driver drv = Gdal.GetDriverByName(driver))
                {
                    if (driver == null)
                    {
                        Trace.TraceError("GDALHelper.Save: Could not load driver {0}", driver);
                        return false;
                    }

                    using (Dataset save_ds = drv.CreateCopy(path, ds, 0, options,
                        delegate (double complete, IntPtr message, IntPtr data)
                        {
                            return HandleProgress(progress, complete, message, data);
                        }, null))
                    {
                        if (save_ds == null)
                        {
                            Trace.TraceError("GDALHelper.Save: Error saving dataset to file {0}", path);
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (AccessViolationException ave)
            {
                Trace.TraceError("GDALHelper.Save: Error saving {0}:\r\n{1}", path, ave);
                throw new GDALException("Error copying dataset", ave);
            }
            catch (Exception ex)
            {
                Trace.TraceError("GDALHelper.Save: Error saving {0}:\r\n{1}", path, ex);
                throw new GDALException("Error copying dataset", ex);
            }
        }

        /// <summary>
        /// Generate an image from the dataset using direct memory access to the image bytes
        /// http://stackoverflow.com/questions/3469368/how-to-handle-accessviolationexception
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="xOff">source raster X offset</param>
        /// <param name="yOff">source raster Y offset</param>
        /// <param name="srcWidth">source raster width</param>
        /// <param name="srcHeight">source raster heigh</param>
        /// <param name="dstWidth">destination raster width</param>
        /// <param name="dstHeight">destination raster height</param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        public static Bitmap GetBitmapDirect(this Dataset ds, int xOff, int yOff, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
        {
            if (ds == null || ds.RasterCount == 0) return null;

            if (dstWidth == 0 || dstHeight == 0) return null;

            if (srcWidth > ds.RasterXSize) srcWidth = ds.RasterXSize;
            if (srcHeight > ds.RasterYSize) srcHeight = ds.RasterYSize;

            DatasetParameters p = ds.GetBitmapParameters();

            // Create a Bitmap to store the GDAL image in
            Bitmap bitmap = new Bitmap(dstWidth, dstHeight, p.PixelFormat);

            #region set up palettes
            if (p.IsIndexed)
            {
                // setting up the color table
                if (p.ColorTable != null)
                {
                    int iCol = p.ColorTable.GetCount();
                    ColorPalette pal = bitmap.Palette;
                    for (int i = 0; i < iCol; i++)
                    {
                        ColorEntry ce = p.ColorTable.GetColorEntry(i);
                        pal.Entries[i] = Color.FromArgb(ce.c4, ce.c1, ce.c2, ce.c3);
                    }
                    bitmap.Palette = pal;
                }
                else
                {
                    // grayscale
                    ColorPalette pal = bitmap.Palette;
                    for (int i = 0; i < 256; i++)
                        pal.Entries[i] = Color.FromArgb(255, i, i, i);
                    bitmap.Palette = pal;
                }
            }
            #endregion

            // Use GDAL raster reading methods to read the image data directly into the Bitmap
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, dstWidth, dstHeight), ImageLockMode.WriteOnly, p.PixelFormat);

            try
            {
                ds.ReadRaster(xOff, yOff, srcWidth, srcHeight, bitmapData.Scan0, dstWidth, dstHeight, p.DataType, p.ChannelCount, p.BandMap, p.PixelSpace, bitmapData.Stride, 1);
            }
            catch (AccessViolationException ave)
            {
                Trace.TraceError("GDALHelper.GetBitmapDirect: Error getting bitmaps:{0}\r\n", ave);
                throw new GDALException("Error getting bitmap", ave);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        /// <summary>
        /// Generate an image from the dataset using direct memory access to the image bytes
        /// http://stackoverflow.com/questions/3469368/how-to-handle-accessviolationexception
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="xOff">source raster X offset</param>
        /// <param name="yOff">source raster Y offset</param>
        /// <param name="srcWidth">source raster width</param>
        /// <param name="srcHeight">source raster heigh</param>
        /// <param name="dstWidth">destination raster width</param>
        /// <param name="dstHeight">destination raster height</param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions]
        public static byte[] GetBitmapData(this Dataset ds, int xOff, int yOff, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
        {
            if (ds == null || ds.RasterCount == 0) return null;

            if (dstWidth == 0 || dstHeight == 0) return null;

            if (srcWidth > ds.RasterXSize) srcWidth = ds.RasterXSize;
            if (srcHeight > ds.RasterYSize) srcHeight = ds.RasterYSize;

            DatasetParameters p = ds.GetBitmapParameters();

            if (p.IsIndexed) return null;

            byte[] data = new byte[dstWidth * dstHeight * 4];

            try
            {
                ds.ReadRaster(xOff, yOff, srcWidth, srcHeight, data, dstWidth, dstHeight, p.ChannelCount, p.BandMap, p.PixelSpace, 0, 0);
            }
            catch (AccessViolationException ave)
            {
                Trace.TraceError("GDALHelper.GetBitmapDirect: Error getting bitmaps:{0}\r\n", ave);
                throw new GDALException("Error getting bitmap", ave);
            }

            return data;
        }

        #endregion

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
            int code = 0;
            if (epsg.StartsWith("EPSG:"))
                epsg = epsg.Replace("EPSG:", "");

            if (!Int32.TryParse(epsg, out code))
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
                    Trace.TraceError("Can't get SRS from EPSG Code \"{0}\": {1}", epsg, OGRHelper.ReportOgrError(code));
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

        public static PointDType Transform(this CoordinateTransformation ct, double x, double y, double z)
        {
            double[] o = new double[3];
            ct.TransformPoint(o, x, y, z);
            return new PointDType(o);
        }

        /// <summary>
        /// Project the given array of points with the given coordinate transformation
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="points"></param>
        /// <returns></returns>
        public static PointDType Transform(this CoordinateTransformation ct, PointDType point)
        {
            ct.TransformPoint(point.Array);
            return point;
        }

    }
}
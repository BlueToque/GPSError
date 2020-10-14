using OSGeo.GDAL;
using OSGeo.OGR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using SpatialReference = OSGeo.OGR.SpatialReference;

namespace GPSError.GDAL
{
    /// <summary>
    /// Encapsulate a GDAL dataset with methods that operate on it
    /// </summary>
    public class GDALImage : IDisposable
    {
        public GDALImage()
        {
            Cancelled = false;
            ResampleAlgorithm = ResampleAlg.GRA_Cubic;
        }

        private Dataset m_dataset;

        readonly string[] reprojectOptions = { "NUM_THREADS = ALL_CPUS", "INIT_DEST = NO_DATA", "WRITE_FLUSH = YES" };

        /// <summary> The dataset </summary>
        protected Dataset Dataset
        {
            get => m_dataset;
            set
            {
                if (value == null) throw new ArgumentNullException("Dataset");

                // Free internal dataset reference 
                if (!object.ReferenceEquals(m_dataset, value) && m_dataset != null)
                {
                    m_dataset.FlushCache();
                    m_dataset.Dispose();
                }

                m_dataset = value;

                GDALHelper.DatasetParameters p = m_dataset.GetBitmapParameters();

                DataType = p.DataType;
                ChannelCount = p.ChannelCount;
                IsIndexed = p.IsIndexed;
                BandMap = p.BandMap;
                ColorTable = p.ColorTable;
                PixelFormat = p.PixelFormat;
                PixelSpace = p.PixelSpace;
            }
        }

        /// <summary> Was the last operation cancelled </summary>
        protected bool Cancelled { get; set; }

        #region properties

        /// <summary> ResampingResampling algorithm </summary>
        public ResampleAlg ResampleAlgorithm { get; set; }

        /// <summary> Name of this dataset </summary>
        public string Name { get; set; }

        /// <summary> File this dataset was loaded from </summary>
        public string FileName { get; set; }

        /// <summary> Number of rasters or bands in the dataset </summary>
        public int Rasters => Dataset.RasterCount;

        /// <summary> Description of the dataset </summary>
        public string Description => Dataset.GetDescription();

        /// <summary>
        /// Get the bounding box of the dataset in the dataset's projection system
        /// </summary>
        /// <returns></returns>
        public double[] GetBBox()
        {
            // Array -> north[0], south[1], west[2], east[3];
            double[] bbox = new double[4];

            double[] argTransform = new double[6];
            Dataset.GetGeoTransform(argTransform);

            //argTransform[0] top left x
            //argTransform[1] w-e pixel resolution
            //argTransform[2] rotation, 0 if image is "north up"
            //argTransform[3] top left y
            //argTransform[4] rotation, 0 if image is "north up"
            //argTransform[5] n-s pixel resolution (is negative)

            bbox[0] = argTransform[3]; // north
            bbox[1] = argTransform[3] + (argTransform[5] * Dataset.RasterYSize); // south
            bbox[2] = argTransform[0]; // west
            bbox[3] = argTransform[0] + (argTransform[1] * Dataset.RasterXSize); // east

            return bbox;
        }

        /// <summary> Get a list of all of the metadata in the dataset </summary>
        public string[] Metadata
        {
            get
            {
                List<string> metadata = new List<string>();
                string[] domains = Dataset.GetMetadataDomainList();
                foreach (var domain in domains)
                    metadata.AddRange(Dataset.GetMetadata(domain));
                metadata.AddRange(Dataset.GetMetadata(""));
                return metadata.ToArray();
            }
        }

        /// <summary> List of metadata domains </summary>
        public string[] MetadataDomains => Dataset.GetMetadataDomainList();

        /// <summary> Get the well known text </summary>
        public string Wkt => Dataset.GetProjectionRef();

        /// <summary> Get the image size </summary>
        public Size Size => new Size(Dataset.RasterXSize, Dataset.RasterYSize);

        /// <summary> Get the image width </summary>
        public float Width => Dataset.RasterXSize;

        /// <summary> Get the image height </summary>
        public float Height => Dataset.RasterYSize;

        /// <summary> X resolution </summary>
        public double XResolution => Transform[1];

        /// <summary> Y Resolution </summary>
        public double YResolution => Transform[5] * -1.0;

        /// <summary> The driver used to load this dataset </summary>
        public string Driver => Dataset.GetDriver().ShortName;

        /// <summary> The format of this dataset </summary>
        public string Format => Dataset.GetDriver().LongName;

        /// <summary> Get the geographic transform for the image </summary>
        public GeoTransform Transform
        {
            get
            {
                double[] geoTrans = new double[6];
                Dataset.GetGeoTransform(geoTrans);
                return new GeoTransform(geoTrans, Dataset.RasterXSize, Dataset.RasterYSize);
            }
        }

        /// <summary> Get the image extents in the image's projection </summary>
        public Extents Extents => Dataset.GetExtents();

        /// <summary> The transparent colour </summary>
        public Color TransparentColor { get; set; }

        /// <summary> Spatial reference systen in EPSG format </summary>
        public string EPSG => Dataset.GetEPSG();

        /// <summary> Does this dataset have an alpha band </summary>
        public bool HasAlpha
        {
            get
            {
                for (int i = 0; i < Dataset.RasterCount; i++)
                {
                    Band band = Dataset.GetRasterBand(i + 1);
                    ColorInterp interp = band.GetRasterColorInterpretation();
                    if (interp == ColorInterp.GCI_AlphaBand)
                        return true;
                }
                return false;
            }
        }

        /// <summary> Is this datast indexed </summary>
        public bool IsIndexed { get; protected set; }

        /// <summary> How many channels in the dataset (same as rasters) </summary>
        public int ChannelCount { get; protected set; }

        /// <summary> The datatype of this dataset </summary>
        public DataType DataType { get; protected set; }

        /// <summary> Dataset's band map </summary>
        public int[] BandMap { get; set; }

        /// <summary> The dataset's colour table if it is indexed </summary>
        public ColorTable ColorTable { get; set; }

        /// <summary> The pixel format of this dataset </summary>
        public PixelFormat PixelFormat { get; set; }

        /// <summary> The pixel space of this dataset </summary>
        public int PixelSpace { get; set; }

        /// <summary> Does this dataset have a cutline for trimming non map data </summary>
        public bool HasNeatLine => Dataset.GetNeatLine() != null;//string neatline = Dataset.GetMetadataItem("NEATLINE", string.Empty);//return !string.IsNullOrEmpty(neatline);

        /// <summary> Get the spatial reference for this image </summary>
        public SpatialReference SRS => Dataset.GetSRS();

        /// <summary> Raster count </summary>
        public int RasterCount => Dataset.RasterCount;

        /// <summary> Get info about this dataset </summary>
        public string Info => Dataset.GdalInfo();

        #endregion

        ///// <summary>
        ///// Resampling algorithms
        ///// </summary>
        //public enum ResampleAlg
        //{
        //    /// <summary>
        //    /// nearest neighbour resampling (default, fastest algorithm, worst interpolation quality).
        //    /// </summary>
        //    NearestNeighbour = 0,

        //    /// <summary>
        //    /// bilinear resampling
        //    /// </summary>
        //    Bilinear = 1,

        //    /// <summary>
        //    /// cubic resampling
        //    /// </summary>
        //    Cubic = 2,

        //    /// <summary>
        //    /// cubic spline resampling
        //    /// </summary>
        //    CubicSpline = 3,

        //    /// <summary>
        //    /// Lanczos windowed sinc resampling
        //    /// </summary>
        //    Lanczos = 4,

        //    /// <summary>
        //    /// average resampling, computes the average of all non-NODATA contributing pixels. (GDAL >= 1.10.0)
        //    /// </summary>
        //    Average = 5,

        //    /// <summary>
        //    /// mode resampling, selects the value which appears most often of all the sampled points. (GDAL >= 1.10.0)
        //    /// </summary>
        //    Mode = 6,
        //}

        #region methods

        /// <summary>
        /// Set the geographic transform
        /// </summary>
        /// <param name="geoTransform"></param>
        public void SetGeoTransform(double[] geoTransform) => m_dataset.SetGeoTransform(geoTransform);

        /// <summary>
        /// Set the nodata value for all bands 
        /// </summary>
        /// <param name="d"></param>
        public void SetNoData(double d)
        {
            for (int nBand = 1; nBand < Dataset.RasterCount; nBand++)
                Dataset.GetRasterBand(nBand).SetNoDataValue(d);
        }

        #region transforms

        /// <summary>
        /// Warp the dataset to a new projection
        /// This creates a virtual 
        /// </summary>
        /// <param name="srs">Target spatial reference system</param>
        /// <param name="alg"></param>
        [HandleProcessCorruptedStateExceptions()]
        public void Warp(string srs, ResampleAlg alg = ResampleAlg.GRA_Bilinear)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            try
            {
                SpatialReference dst_sr = OSRHelper.SRSFromEPSG(srs);
                Dataset dsWarp = Gdal.AutoCreateWarpedVRT(Dataset, Dataset.GetProjection(), dst_sr.ToWkt(), alg, 0.125);
                // ???
                Dataset.Dispose();
                Dataset = dsWarp;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GDALImage.Warp: Error warping\r\n{0}", ex);
                throw new GDALException(ex);
            }
        }

        /// <summary>
        /// Extract and save the alpha channel from the dataset
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="filename"></param>
        /// <param name="epsg_to"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        [HandleProcessCorruptedStateExceptions()]
        public void ExtractAlpha(string filename = @"/vsimem/tiffinmem", string driver = "GTiff", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            try
            {
                DataType dataType = DataType.GDT_Unknown;
                int index = -1;
                for (int i = 1; i <= Dataset.RasterCount; i++)
                {
                    Band band = Dataset.GetRasterBand(i);
                    if (band.GetColorInterpretation() == ColorInterp.GCI_AlphaBand)
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1) return;

                dataType = Dataset.GetRasterBand(index).DataType;
                Dataset mask = CreateMaskDataset("source", Dataset.RasterXSize, Dataset.RasterYSize, dataType);

                Copy(Dataset.GetRasterBand(index), mask.GetRasterBand(1));

                double[] arg = new double[2];
                mask.GetRasterBand(1).ComputeRasterMinMax(arg, 1);

                mask.Save(filename, driver, options, progress);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        /// <summary>
        /// Convert the source dataset to a 32bit
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        public Dataset ConvertTo32Bits(double pixel_spacing = 0, string fileName = "32bits", string driver = "GTiff", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            Dataset dest = null;

            try
            {
                fileName = GetTempFile(fileName, driver);

                Dataset source = Dataset;

                Envelope envelope = source.GetEnvelope(source.GetSRS());
                if (envelope == null) return null;

                //Size newSize = GetNewSize(pixel_spacing, envelope);
                pixel_spacing = GetPixelSpacing(pixel_spacing);

                string tempFile = GetTempFile(Path.GetFileName(fileName), driver);

                // Create new tiff   
                using (dest = CreateDS2(tempFile, driver))
                {
                    double[] argin = new double[] { envelope.MinX, pixel_spacing, Transform[2], envelope.MaxY, Transform[4], -pixel_spacing };
                    dest.SetGeoTransform(argin);
                    dest.SetProjection(source.GetProjection());

                    HandleProgress(progress, 0, "Converting to 32 bits");

                    CPLErr res = Gdal.ReprojectImage(source, dest, null, source.GetProjection(), ResampleAlgorithm, 0, 0.0125, (double complete, IntPtr message, IntPtr data)=> HandleProgress(progress, complete, message, data), null, reprojectOptions);

                    if (Cancelled) return null;

                    dest.Save(fileName, driver, options);

                    if (res != CPLErr.CE_None) throw new GDALException(string.Format("Error trimming image: {0}", Gdal.GetLastErrorMsg()));
                }
                return dest;
            }
            catch (Exception ex)
            {
                HandleError(ex);
                return null;
            }
        }

        /// <summary>
        /// Trim the internal dataset to the neat line if there is one.
        /// Returns a new in memory dataset.
        /// Caller will manage memory
        /// Inspired by http://www.gisremotesensing.com/2015/09/clip-raster-with-shapefile-using-c-and.html#.VfiVehFVhHw
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public Dataset TrimNeatLine(double pixel_spacing = 0, Func<double, string, string, bool> progress = null)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            Dataset dest = null;

            try
            {
                Dataset source = Dataset;

                Envelope e = source.GetNeatLine();
                if (e == null) return null;

                // Compute the out raster cell resolutions  

                // get the size of the new raster from the envelope
                Size newSize = GetNewSize(pixel_spacing, e);
                pixel_spacing = GetPixelSpacing(pixel_spacing);

                // Create new raster at the new size
                dest = CreateCompatibleDataset("neatLine", newSize.Width, newSize.Height);
                double[] argin = new double[] { e.MinX, pixel_spacing, Transform[2], e.MaxY, Transform[4], -pixel_spacing };
                dest.SetGeoTransform(argin);
                dest.SetProjection(source.GetProjection());

                HandleProgress(progress, 0, "Trimming image");

                // string[] reprojectOptions = {"NUM_THREADS = ALL_CPUS"," INIT_DEST = NO_DATA","WRITE_FLUSH = YES" };  
                // Gdal.ReprojectImage(oldRasterDataset, outputDataset, null, inputShapeSrs, ResampleAlg.GRA_NearestNeighbour, 1.0,1.0, null, null, reprojectOptions);  

                // "reproject" the image into the new raster - this basically creates a raster with the same projection
                CPLErr res = Gdal.ReprojectImage(source, dest, null, source.GetProjection(), ResampleAlg.GRA_NearestNeighbour, 0, 0.0125, (double complete, IntPtr message, IntPtr data)=>HandleProgress(progress, complete, message, data), null, reprojectOptions);

                if (Cancelled) return null;

                if (res != CPLErr.CE_None) throw new GDALException(string.Format("Error trimming image: {0}", Gdal.GetLastErrorMsg()));

                return dest;
            }
            catch (Exception ex)
            {
                HandleError(ex);
                return null;
            }
        }

        #endregion

        #region Get Image from dataset

        /// <summary>
        /// Return the entire dataset as an image
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");
            return Dataset.GetBitmapDirect(0, 0, Dataset.RasterXSize, Dataset.RasterYSize, Dataset.RasterXSize, Dataset.RasterYSize);
        }

        /// <summary>
        /// Return the dataset in an image of the given size
        /// </summary>
        /// <param name="dstWidth"></param>
        /// <param name="dstHeight"></param>
        /// <returns></returns>
        public Bitmap GetImage(int dstWidth, int dstHeight)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");
            return Dataset.GetBitmapDirect(0, 0, Dataset.RasterXSize, Dataset.RasterYSize, dstWidth, dstHeight);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dstSize"></param>
        /// <returns></returns>
        public Bitmap GetImage(Size dstSize)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");
            return Dataset.GetBitmapDirect(0, 0, Dataset.RasterXSize, Dataset.RasterYSize, dstSize.Width, dstSize.Height);
        }

        /// <summary>
        /// Get a sub-image from the dataset
        /// </summary>
        /// <param name="xOff">start x offset</param>
        /// <param name="yOff">start y offset</param>
        /// <param name="srcWidth">width of source bitmap</param>
        /// <param name="srcHeight">height of source bitmap</param>
        /// <param name="dstWidth"></param>
        /// <param name="dstHeight"></param>
        /// <returns></returns>
        public Bitmap GetImage(Point offset, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");
            return Dataset.GetBitmapDirect(offset.X, offset.Y, srcWidth, srcHeight, dstWidth, dstHeight);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xOff"></param>
        /// <param name="yOff"></param>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public Bitmap GetImage(int xOff, int yOff, Size source, Size destination)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");
            return Dataset.GetBitmapDirect(xOff, yOff, source.Width, source.Height, destination.Width, destination.Height);
        }

        ///// <summary>
        ///// Get an image of the given size at the given extents
        ///// </summary>
        ///// <param name="extents"></param>
        ///// <param name="size"></param>
        ///// <returns></returns>
        //public Bitmap GetImage(Extents extents, Size size)
        //{
        //    if (Dataset == null) throw new ArgumentNullException("Dataset");

        //    if (!GetParameters(extents, size, out int offX, out int offY, out int imgPixWidth, out int imgPixHeight, out int actualImageW, out int actualImageH)) return null;

        //    Bitmap bitmap = Dataset.GetBitmapDirect(offX, offY, imgPixWidth, imgPixHeight, actualImageW, actualImageH);
        //    if (bitmap == null) return null;

        //    // if the bitmap is not of the requested size then draw it into one of the requested size
        //    // with a transparent background.
        //    // This is so tiling operations work since they assume the requested size when drawing
        //    if (bitmap.Size.Width != size.Width || bitmap.Size.Height != size.Height)
        //    {
        //        int x = 0;
        //        int y = 0;
        //        int width = bitmap.Size.Width;
        //        int height = bitmap.Size.Height;

        //        if (extents.Top > Extents.Top)
        //            y = size.Height - bitmap.Size.Height;
        //        if (extents.Left < Extents.Left)
        //            x = size.Width - bitmap.Size.Width;

        //        Bitmap copy;
        //        if (Image.GetPixelFormatSize(bitmap.PixelFormat) != 32)
        //            copy = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        //        else
        //            copy = new Bitmap(size.Width, size.Height);//, bitmap.PixelFormat);

        //        Rectangle destRectangle = new Rectangle(x, y, width, height);
        //        using (Graphics graphics = Graphics.FromImage(copy))
        //        {
        //            using (var br = new SolidBrush(Color.FromArgb(0, 255, 255, 255)))
        //                graphics.FillRectangle(br, new Rectangle(0, 0, size.Width, size.Height));
        //            graphics.DrawImage(bitmap, destRectangle, new Rectangle(0, 0, bitmap.Size.Width, bitmap.Size.Height), GraphicsUnit.Pixel);
        //        }

        //        bitmap.Dispose();
        //        bitmap = copy;
        //    }
        //    else if (Image.GetPixelFormatSize(bitmap.PixelFormat) != 32)
        //    {
        //        Bitmap copy = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        //        using (Graphics graphics = Graphics.FromImage(copy))
        //            graphics.DrawImageUnscaled(bitmap, 0,0);
        //        bitmap.Dispose();
        //        bitmap = copy;
        //    }

        //    if (TransparentColor != Color.Empty)
        //        bitmap.MakeTransparent(TransparentColor);

        //    return bitmap;
        //}

        #endregion

        /// <summary>
        /// Save the dataset using a new driver and options
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public bool Save(string fileName, string driver = "GTiff", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            progress?.Invoke(0, "Saving...", null);

            try
            {
                if (!Dataset.Save(fileName, driver, options, progress))
                {
                    string erro = Gdal.GetLastErrorMsg();
                    Trace.TraceError("Error saving \"{0}\": {1}", fileName, erro);
                    string v = GDALHelper.Version;
                    return false;
                }

                //Dataset = Gdal.Open(fileName, Access.GA_Update);
                return true;
            }
            catch (GDALException ex)
            {
                Trace.TraceError("Error saving dataset:\r\n{0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Copy the dataset, returning a handle to the new dataset
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        public GDALImage Copy(string fileName = "filememory", string driver = "MEM", string[] options = null, Func<double, string, string, bool> progress = null)
        {
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            progress?.Invoke(0, "Saving...", null);

            try
            {
                Dataset copy = Dataset.Copy(fileName, driver, options, progress);
                if (copy == null)
                {
                    string erro = Gdal.GetLastErrorMsg();
                    Trace.TraceError("Error saving \"{0}\": {1}", fileName, erro);
                    return null;
                }
                return new GDALImage() { Dataset = copy, };
            }
            catch (GDALException ex)
            {
                Trace.TraceError("Error copying dataset:\r\n{0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Create image overviews
        /// </summary>
        /// <param name="progress"></param>
        /// <returns></returns>
        public bool GenerateOverviews(Func<double, string, string, bool> progress) => GenerateOverviews(Dataset, progress);

        #region reprojection methods

        /// <summary>
        /// Project and trim in one step
        /// Does not leave transparency properly as the mask is not created 
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="filename"></param>
        /// <param name="epsg_to"></param>
        /// <param name="driver"></param>
        /// <param name="options"></param>
        /// <param name="progress"></param>
        [HandleProcessCorruptedStateExceptions()]
        public void ReprojectWithAlpha(
            double pixel_spacing = 0,
            string filename = @"/vsimem/tiffinmem",
            string epsg_to = "3785",
            string driver = "GTiff",
            string[] options = null,
            Func<double, string, string, bool> progress = null,
            bool generateOverviews = false,
            bool createMask = true)
        {
            Cancelled = false;
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            Dataset source = Dataset;

            try
            {
                #region get source and destination spatial references
                SpatialReference src_sr = source.GetSRS();
                string src_wkt = src_sr.ToWkt();

                SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
                string dst_wkt = dst_sr.ToWkt();
                #endregion

                #region trim to neat line
                int inc = 0;
                int steps = 3;
                if (generateOverviews)
                    steps++;
                if (HasNeatLine)
                {
                    HandleProgress(progress, inc / steps, "Trimming image");
                    steps++;
                    Dataset trimmed = TrimNeatLine(pixel_spacing, (complete, message, data) =>(progress == null) || progress((complete + inc) / steps, message, data));

                    if (Cancelled) return;

                    if (trimmed != null)
                    {
                        inc++;
                        source = trimmed;
                    }
                }
                #endregion

                // if we're already in our target EPSG, return here.
                if (EPSG == epsg_to)
                {
                    Dataset.Copy(filename, driver, options, progress);
                    return;
                }

                #region set up parameters for new dataset

                // get the envelope of the trimmed dataset
                Envelope envelope = source.GetEnvelope(dst_sr);

                //Size newSize = GetNewSize(pixel_spacing, envelope);
                Size newSize = new Size(Size.Width, Size.Height);

                //pixel_spacing = GetPixelSpacing(pixel_spacing);
                double pixel_spacingX = (envelope.MaxX - envelope.MinX) / Size.Width;
                double pixel_spacingY = (envelope.MaxY - envelope.MinY) / Size.Height;

                GeoTransform new_geo = new GeoTransform(
                    envelope.MinX,
                    pixel_spacingX, //pixel_spacing, 
                    Transform[2],
                    envelope.MaxY,
                    Transform[4],
                    -pixel_spacingY);// - pixel_spacing);

                #endregion

                #region mask
                Dataset dstMask = null;
                if (createMask)
                {
                    HandleProgress(progress, (float)inc / (float)steps, "Projecting alpha");
                    dstMask = CreateMask(source, dst_wkt, newSize, new_geo, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    inc++;
                    if (Cancelled) return;
                }
                #endregion

                using (Dataset dest = CreateCompatibleDataset(string.Format("dest-{0}", Name), newSize.Width, newSize.Height))
                {
                    #region set new transforms
                    dest.SetGeoTransform(new_geo.m_transform);
                    dest.SetProjection(dst_wkt);
                    #endregion

                    #region reproject the source image

                    HandleProgress(progress, (float)inc / (float)steps, "Projecting image");

                    CPLErr res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, ResampleAlgorithm, 0.0, 0.125, (double complete, IntPtr message, IntPtr data)=>HandleProgress(progress, (complete + inc) / steps, message, data), null, reprojectOptions);

                    if (Cancelled) return;

                    if (res != CPLErr.CE_None) throw new GDALException("Error reprojecting image");
                    inc++;

                    #endregion

                    #region apply mask
                    if (dstMask != null)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Applying mask");
                        ApplyMask(dest, dstMask, true);
                        dstMask.Dispose();
                    }
                    #endregion

                    #region generate overivews
                    if (generateOverviews)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Generate overviews");
                        GenerateOverviews(dest, (complete, message, data) => HandleProgress(progress, (complete + inc) / steps, message, data));
                        if (Cancelled) return;
                        inc++;
                    }
                    #endregion

                    #region save image
                    HandleProgress(progress, (float)inc / (float)steps, "Saving image");
                    dest.Save(filename, driver, options, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    if (Cancelled) return;
                    inc++;
                    #endregion
                }
            }
            catch (Exception ex)
            {
                if (Cancelled) return;
                HandleError(ex);
            }
            finally
            {
                if (source != null && source != Dataset)
                {
                    source.Dispose();
                    source = null;
                }
            }
        }

        /// <summary>
        /// This seems to work well for an indexed tiff
        /// This uses the autocreate / copy method
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="filename"></param>
        /// <param name="epsg_to"></param>
        /// <param name="driver"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        public void ReprojectTiff(
            double pixel_spacing = 0,
            string filename = @"/vsimem/tiffinmem",
            string epsg_to = "3785",
            string driver = "GTiff",
            string[] options = null,
            Func<double, string, string, bool> progress = null,
            bool generateOverviews = false,
            bool createMask = true)
        {
            Cancelled = false;
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            Dataset source = Dataset;

            try
            {
                #region get source and destination spatial references
                SpatialReference src_sr = source.GetSRS();
                string src_wkt = src_sr.ToWkt();

                SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
                string dst_wkt = dst_sr.ToWkt();
                #endregion

                #region trim to neat line
                int inc = 0;
                int steps = 3;
                if (generateOverviews) steps++;
                if (HasNeatLine)
                {
                    HandleProgress(progress, inc / steps, "Trimming image");
                    steps++;
                    Dataset trimmed = TrimNeatLine(pixel_spacing, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    if (trimmed != null)
                    {
                        inc++;
                        if (Cancelled) return;
                        source = trimmed;
                    }
                }
                #endregion

                // if we're already in our target EPSG, return here.
                if (source.GetEPSG() == epsg_to) return;

                #region set up parameters for new dataset
                // get the envelope of the trimmed dataset
                Envelope envelope = source.GetEnvelope(dst_sr);
                Size newSize = GetNewSize(pixel_spacing, envelope);
                pixel_spacing = GetPixelSpacing(pixel_spacing);
                GeoTransform new_geo = new GeoTransform(envelope.MinX, pixel_spacing, Transform[2], envelope.MaxY, Transform[4], -pixel_spacing);
                #endregion

                #region mask
                Dataset dstMask = null;
                if (createMask)
                {
                    HandleProgress(progress, (float)inc / (float)steps, "Projecting alpha");
                    dstMask = CreateMask(source, dst_wkt, newSize, new_geo, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data), "GTiff");
                    inc++;
                    if (Cancelled) return;
                }
                #endregion

                using (Dataset dest = CreateCompatibleDataset(string.Format("dest-{0}", Name), newSize.Width, newSize.Height, "GTiff"))
                {
                    #region set new transforms

                    dest.SetGeoTransform(new_geo.m_transform);
                    dest.SetProjection(dst_wkt);

                    #endregion

                    #region reproject the source image

                    HandleProgress(progress, (float)inc / (float)steps, "Projecting image");

                    CPLErr res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, ResampleAlgorithm, 0.0, 0.125, (complete, message, data) => HandleProgress(progress, (complete + inc) / steps, message, data), null, reprojectOptions);
                    if (Cancelled) return;

                    if (res != CPLErr.CE_None) throw new Exception(string.Format("Error reprojecting image: {0}", Gdal.GetLastErrorMsg()));
                    inc++;

                    #endregion

                    #region apply mask
                    if (dstMask != null)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Applying mask");
                        ApplyMask(dest, dstMask, true);
                        dstMask.Dispose();
                    }
                    #endregion

                    #region generate overivews
                    if (generateOverviews)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Generate overviews");
                        GenerateOverviews(dest, (complete, message, data) =>HandleProgress(progress, (complete + inc) / steps, message, data));
                        if (Cancelled) return;
                        inc++;
                    }
                    #endregion

                    #region save image
                    HandleProgress(progress, (float)inc / (float)steps, "Saving image");
                    dest.Save(filename, driver, options, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    if (Cancelled) return;
                    inc++;
                    #endregion
                }
            }
            catch (Exception ex)
            {
                if (Cancelled) return;
                HandleError(ex);
            }
            finally
            {
                if (source != null && source != Dataset)
                {
                    source.Dispose();
                    source = null;
                }
            }
        }

        /// <summary>
        /// Reprojection for indexed tiff 
        /// requires to create a special indexed dummy to get the alpha 
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="filename"></param>
        /// <param name="epsg_to"></param>
        /// <param name="driver"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        public void ReprojectIndexedTiff(
            double pixel_spacing = 0,
            string filename = @"/vsimem/tiffinmem",
            string epsg_to = "3785",
            string driver = "GTiff",
            string[] options = null,
            Func<double, string, string, bool> progress = null,
            bool generateOverviews = false,
            bool createMask = true)
        {
            Cancelled = false;
            if (Dataset == null) throw new ArgumentNullException("Dataset");

            Dataset source = Dataset;

            try
            {
                #region get source and destination spatial references
                SpatialReference src_sr = source.GetSRS();
                string src_wkt = src_sr.ToWkt();

                SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
                string dst_wkt = dst_sr.ToWkt();
                #endregion

                #region trim to neat line
                int inc = 0;
                int steps = 3;
                if (generateOverviews) steps++;
                if (HasNeatLine)
                {
                    HandleProgress(progress, inc / steps, "Trimming image");
                    steps++;
                    Dataset trimmed = TrimNeatLine(pixel_spacing, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    if (Cancelled) return;
                    if (trimmed != null)
                    {
                        inc++;
                        if (Cancelled) return;
                        source = trimmed;
                    }
                }
                #endregion

                // if we're already in our target EPSG, return here.
                if (EPSG == epsg_to) return;

                #region set up parameters for new dataset
                // get the envelope of the trimmed dataset
                Envelope envelope = source.GetEnvelope(dst_sr);
                Size newSize = GetNewSize(pixel_spacing, envelope);
                pixel_spacing = GetPixelSpacing(pixel_spacing);
                GeoTransform new_geo = new GeoTransform(envelope.MinX, pixel_spacing, Transform[2], envelope.MaxY, Transform[4], -pixel_spacing);
                #endregion

                #region mask
                Dataset dstMask = null;
                if (createMask)
                {
                    HandleProgress(progress, (float)inc / (float)steps, "Projecting alpha");
                    dstMask = CreateMask(source, dst_wkt, newSize, new_geo, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data), "GTiff");
                    if (Cancelled) return;
                    inc++;
                }
                #endregion

                using (Dataset dest = CreateCompatibleDataset(string.Format("dest-{0}", Name), newSize.Width, newSize.Height, "GTiff"))
                {
                    #region set new transforms

                    dest.SetGeoTransform(new_geo.m_transform);
                    dest.SetProjection(dst_wkt);

                    #endregion

                    #region reproject the source image

                    HandleProgress(progress, (float)inc / (float)steps, "Projecting image");

                    CPLErr res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, ResampleAlgorithm, 0.0, 0.125, (double complete, IntPtr message, IntPtr data) => HandleProgress(progress, (complete + inc) / steps, message, data), null, reprojectOptions);

                    if (Cancelled) return;
                    if (res != CPLErr.CE_None) throw new Exception(string.Format("Error reprojecting image: {0}", Gdal.GetLastErrorMsg()));
                    inc++;

                    #endregion

                    #region apply mask
                    if (dstMask != null)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Applying mask");
                        ApplyMask(dest, dstMask, true);
                        dstMask.Dispose();
                    }
                    #endregion

                    #region generate overivews
                    if (generateOverviews)
                    {
                        HandleProgress(progress, (float)inc / (float)steps, "Generate overviews");
                        GenerateOverviews(dest, (complete, message, data) => HandleProgress(progress, (complete + inc) / steps, message, data) );
                        if (Cancelled) return;
                        inc++;
                    }
                    #endregion

                    #region save image
                    HandleProgress(progress, (float)inc / (float)steps, "Saving image");
                    dest.Save(filename, driver, options, (complete, message, data) => (progress == null) || progress((complete + inc) / steps, message, data));
                    if (Cancelled) return;
                    inc++;
                    #endregion
                }
            }
            catch (Exception ex)
            {
                if (Cancelled) return;
                HandleError(ex);
            }
            finally
            {
                if (source != null && source != Dataset)
                {
                    source.Dispose();
                    source = null;
                }
            }
        }

        #endregion

        #endregion

        #region private methods

        /// <summary>
        /// Generate overivew levels for the existing dataset
        /// </summary>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        private static bool GenerateOverviews(Dataset dataset, Func<double, string, string, bool> progress)
        {
            if (dataset == null) throw new ArgumentNullException("Dataset");

            try
            {
                int nLevel = ImageCalc.GetLevels(256, dataset.RasterXSize, dataset.RasterYSize);
                int levels = dataset.GetRasterBand(1).GetOverviewCount();
                if (levels < 1)
                {
                    // Example:
                    // Level = 3
                    // overviews = { 2, 4, 8 }
                    int[] overviews = new int[nLevel];
                    for (int i = 0; i < nLevel; i++)
                        overviews[i] = (int)Math.Pow(2.0, (double)(i + 1));

                    dataset.BuildOverviews("NEAREST", overviews, (double complete, IntPtr message, IntPtr data) => GDALHelper.HandleProgress(progress, complete, message, data), null);
                }
                else if (levels != nLevel)
                {
                    Trace.TraceError("The number of overviews in this dataset ({0}) is different that calculated {1}", levels, nLevel);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error building overviews:\r\n{0}", ex);
                throw new GDALException("Error building overviews", ex);
            }
        }

        /// <summary>
        /// Get a temporary file based on the file name given
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected string GetTempFile(string name, string driver)
        {
            if (driver == "MEM") return name;

            if (Path.IsPathRooted(name)) return name;

            // create a file in the temporary directory with the given name
            string fileName = Path.Combine(GDALHelper.TempPath, Path.GetFileName(name));
            fileName = Path.ChangeExtension(fileName, GDALHelper.GetExtension(driver));

            if (File.Exists(fileName))
            {
                // if the file exists try to delete it
                try { File.Delete(fileName); }
                // if you can't delete it, then just return a new temporary file name
                catch
                {
                    fileName = Path.ChangeExtension(Path.GetTempFileName(), GDALHelper.GetExtension(driver));
                }
            }

            return fileName;
        }

        /// <summary>
        /// Create a mask dataset for the source dataset
        /// </summary>
        /// <param name="source">Data source</param>
        /// <param name="dst_wkt">Destination SRS</param>
        /// <param name="newSize">Image size</param>
        /// <param name="new_geo">Geometric transform</param>
        /// <param name="progress">Progress</param>
        /// <param name="driver">Driver</param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        private Dataset CreateMask(Dataset source, string dst_wkt, Size newSize, GeoTransform new_geo, Func<double, string, string, bool> progress, string driver = "MEM")
        {
            try
            {
                // Create a mask dataset with the same dimensions as the source dataset, 
                // and another with the same dimensions as the destination
                // warp the blank source to the destination and the new pixels should be "black"
                using (Dataset srcMask = CreateCompatibleDataset(string.Format("srcMask-{0}", Name), source.RasterXSize, source.RasterYSize))
                {
                    Dataset dstMask = CreateMaskDataset(string.Format("destMask-{0}", Name), newSize.Width, newSize.Height, DataType, driver);

                    srcMask.SetGeoTransform(source.GetTransform());
                    srcMask.SetProjection(source.GetWkt());

                    dstMask.SetGeoTransform(new_geo.m_transform);
                    dstMask.SetProjection(dst_wkt);

                    CPLErr res = Gdal.ReprojectImage(srcMask, dstMask, source.GetWkt(), dst_wkt, ResampleAlgorithm, 0.0, 0.125, (double complete, IntPtr message, IntPtr data) => HandleProgress(progress, complete, message, data), null, reprojectOptions);

                    if (Cancelled) return null;

                    if (res != CPLErr.CE_None) throw new GDALException("Error creating mask");

                    //srcMask.Save(Path.Combine(GetTempPath(), "sourceMask.tif"), "GTiff", null, progress);

                    //dstMask.Save(Path.Combine(GetTempPath(), "destMask.tif"), "GTiff", null, progress);
                    return dstMask;
                }
            }
            catch
            {
                if (Cancelled) return null;
                throw;
            }
        }

        /// <summary>
        /// Internal method to handle the GDAL progress callback and marshall strings.
        /// This will call the provided progress callback in c#
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="complete"></param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private int HandleProgress(Func<double, string, string, bool> progress, double complete, IntPtr message, IntPtr data)
        {
            if (progress == null) return 1;

            string l_message = (message == IntPtr.Zero) ? string.Empty : Marshal.PtrToStringAnsi(message);
            string l_data = data == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(data);

            Cancelled = progress(complete, l_message, l_data);
            return Cancelled ? 0 : 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="complete">percent complete (between 0 and 1.0)</param>
        /// <param name="message"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool HandleProgress(Func<double, string, string, bool> progress, double complete, string message, string data = null)
        {
            if (progress == null) return true;
            Cancelled = progress(complete, message, data);
            return Cancelled;
        }

        ///// <summary>
        ///// Get parameters for the existing dataset in preparation for generating an image
        ///// </summary>
        ///// <param name="extents"></param>
        ///// <param name="size"></param>
        ///// <param name="offX"></param>
        ///// <param name="offY"></param>
        ///// <param name="imgPixWidth"></param>
        ///// <param name="imgPixHeight"></param>
        ///// <param name="actualImageW"></param>
        ///// <param name="actualImageH"></param>
        ///// <returns></returns>
        //private bool GetParameters(Extents extents, Size size, out int offX, out int offY, out int imgPixWidth, out int imgPixHeight, out int actualImageW, out int actualImageH)
        //{
        //    offX = 0;
        //    offY = 0;
        //    imgPixWidth = 0;
        //    imgPixHeight = 0;
        //    actualImageW = 0;
        //    actualImageH = 0;

        //    Extents intersection = Extents.Intersect(extents, Extents);
        //    if (intersection.IsNullOrEmpty()) return false;

        //    GeoTransform transform = Transform;

        //    // this is the size of the image to read from the data store
        //    offX = Math.Abs((int)Math.Round(transform.PixelX(intersection.Left)));
        //    offY = Math.Abs((int)Math.Round(transform.PixelY(intersection.Top)));
        //    imgPixWidth = (int)Math.Round( transform.PixelXwidth(intersection.Width));
        //    imgPixHeight = (int)Math.Round(transform.PixelYwidth(intersection.Height));

        //    // get screen pixels image should fill 
        //    double dblBBoxtoImgPixX = imgPixWidth / extents.Width;
        //    double dblImginMapW = size.Width * dblBBoxtoImgPixX * transform.HorizontalPixelResolution;

        //    double dblBBoxtoImgPixY = imgPixHeight / extents.Height;
        //    double dblImginMapH = size.Height * dblBBoxtoImgPixY * -transform.VerticalPixelResolution;

        //    #region resolution fixes

        //    actualImageW = (int)Math.Round(dblImginMapW);
        //    actualImageH = (int)Math.Round(dblImginMapH);

        //    #endregion

        //    if ((dblImginMapH == 0) || (dblImginMapW == 0)) return false;

        //    return true;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <returns></returns>
        private double GetPixelSpacing(double pixel_spacing)
        {
            if (pixel_spacing <= 0)
                pixel_spacing = (float)XResolution;
            return pixel_spacing;
        }

        /// <summary>
        /// Get the pixel spacing.
        /// If the input is 0, then just use the X resolution
        /// if the input is less than 0 then use the current size of the raster
        /// </summary>
        /// <param name="pixel_spacing"></param>
        /// <param name="envelope"></param>
        /// <returns></returns>
        private Size GetNewSize(double pixel_spacing, Envelope envelope)
        {
            if (pixel_spacing == 0)
                pixel_spacing = (float)XResolution;

            return new Size(Size.Width, Size.Height);

            //Size newSize = (pixel_spacing < 0) ?
            //    new Size(this.Size.Width, this.Size.Height) :
            //    new Size((int)((envelope.MaxX - envelope.MinX) / pixel_spacing), (int)((envelope.MaxY - envelope.MinY) / pixel_spacing));
            //return newSize;
        }

        /// <summary>
        /// Get the envelope of the current dataset if transformed into the destination 
        /// spatial reference system
        /// </summary>
        /// <param name="src_sr"></param>
        /// <param name="dst_sr"></param>
        /// <returns></returns>
        public Envelope GetEnvelope(SpatialReference dst_sr = null) => Dataset.GetEnvelope(dst_sr);

        /// <summary>
        /// Get the neatline of the current dataset and transform it to the destination coordinate system
        /// </summary>
        /// <param name="src_sr"></param>
        /// <param name="dst_sr"></param>
        /// <returns></returns>
        public Envelope GetNeatLine(SpatialReference dst_sr = null)
        {
            if (dst_sr == null)
                return Dataset.GetNeatLine();
            using (SpatialReference src_sr = Dataset.GetSRS())
            using (CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr))
            {
                Envelope envelope = Dataset.GetNeatLine(); ;
                if (envelope == null) return null;

                // Work out the boundaries of the new dataset in the target projection
                PointDType ul = tx.Transform(envelope.MinX, envelope.MaxY, 0.0);
                PointDType lr = tx.Transform(envelope.MaxX, envelope.MinY, 0.0);

                envelope = new Envelope()
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
        /// Get the envelope of the current dataset if transformed into the destination 
        /// spatial reference system
        /// This method gets the trimline if it exists
        /// </summary>
        /// <param name="src_sr"></param>
        /// <param name="dst_sr"></param>
        /// <returns></returns>
        public Envelope GetBorder(SpatialReference dst_sr)
        {
            using (SpatialReference src_sr = Dataset.GetSRS())
            {
                using (CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr))
                {

                    Envelope envelope = Dataset.GetNeatLine(); ;
                    if (envelope != null)
                    {
                        // Work out the boundaries of the new dataset in the target projection
                        PointDType ul = tx.Transform(envelope.MinX, envelope.MaxY, 0.0);
                        PointDType lr = tx.Transform(envelope.MaxX, envelope.MinY, 0.0);

                        envelope = new Envelope()
                        {
                            MinX = ul.X,    // envelope.MinX
                            MaxX = lr.X,
                            MinY = lr.Y,
                            MaxY = ul.Y     // envelope.MaxY
                        };
                    }
                    else
                    {
                        // Up to here, all  the projection have been defined, as well as a 
                        // transformation from the source to the destination 
                        GeoTransform geo_t = Transform;

                        // Work out the boundaries of the new dataset in the target projection
                        // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
                        // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
                        PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
                        PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * Dataset.RasterXSize, geo_t[3] + geo_t[5] * Dataset.RasterYSize, 0.0);

                        envelope = new Envelope()
                        {
                            MinX = ul.X,    // envelope.MinX
                            MaxX = lr.X,
                            MinY = lr.Y,
                            MaxY = ul.Y     // envelope.MaxY
                        };

                        // The size of the raster is given the new projection and pixel spacing
                        //size.Width = (int)((lr.X - ul.X) / pixel_spacing);
                        //size.Height= (int)((ul.Y - lr.Y) / pixel_spacing);
                    }

                    return envelope;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        private static void HandleError(Exception ex)
        {
            if (ex is GDALException)
            {
                Trace.TraceError("GDALImage.HandleError: Error reprojecting:\r\n{0}", ex);
                throw ex;
            }
            else
            {
                string gdalError = Gdal.GetLastErrorMsg();
                Trace.TraceError("GDALImage.HandleError: Error reprojecting {0}:\r\n{1}", gdalError, ex);
                //throw new GDALException(ex);
            }
        }

        /// <summary>
        /// Copy a band from the source to the destination, optionally inverting the bytes
        /// This operation assumes the bands are of type GDT_Byte
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <param name="invert"></param>
        private void Copy(Band source, Band dest, bool invert = false)
        {
            int maskDataSize = source.XSize * (Gdal.GetDataTypeSize(source.DataType) / 8);
            byte[] maskData = new byte[maskDataSize];

            // break into chunks
            for (int y = 0; y < source.YSize; y++)
            {
                CPLErr returnVal = source.ReadRaster(0, y, source.XSize, 1, maskData, source.XSize, 1, (int)source.DataType, 0);

                if (returnVal != CPLErr.CE_None) throw new Exception("Error reading raster");

                if (invert)
                    for (int i = 0; i < maskDataSize; i++)
                        maskData[i] = (byte)(maskData[i] ^ (byte)0xFF);

                returnVal = dest.WriteRaster(0, y, dest.XSize, 1, maskData, dest.XSize, 1, (int)dest.DataType, 0);

                if (returnVal != CPLErr.CE_None) throw new GDALException();
            }
        }

        /// <summary>
        /// Apply the mast dataset to the given dataset
        /// The Mask consists of a single band which is copied into the fourth band of the destination dataset
        /// This could go very wrong if the data sets are not of the same size.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="mask"></param>
        /// <param name="invert"></param>
        private void ApplyMask(Dataset dest, Dataset mask, bool invert = false)
        {
            Band maskBand = mask.GetRasterBand(1);
            Band destBand = null;
            if (dest.RasterCount == 4)
                destBand = dest.GetRasterBand(4);
            else if (dest.RasterCount == 2)
                destBand = dest.GetRasterBand(2);

            Copy(maskBand, destBand, invert);
        }

        /// <summary>
        /// Create a mask dataset with the given in memory name, width, height and datatype.
        /// The Mask Dataset is a single alpha band set to be fully opaque
        /// </summary>
        /// <param name="name"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="datatype"></param>
        /// <param name="driver"></param>
        /// <returns></returns>
        protected Dataset CreateMaskDataset(string name, int width, int height, DataType datatype = DataType.GDT_Byte, string driver = "MEM")
        {
            if (driver == "MEM") driver = CheckRequredMemory(width, height, 1);

            // if we are not using the memory driver make sure the filename is something reasonable
            name = GetTempFile(name, driver);

            using (var drvMem = Gdal.GetDriverByName(driver))
            {
                // create a dataset with a single band and the appropriate data type
                Dataset ds = drvMem.Create(name, width, height, 1, datatype, null);
                Band band = ds.GetRasterBand(1);
                band.SetColorInterpretation(ColorInterp.GCI_AlphaBand);

                //band.SetNoDataValue(255);
                //band.Fill(GDALHelper.ALPHA_MASK, 0);
                band.Fill(GDALHelper.ALPHA_OPAQUE, 0);

                return ds;
            }
        }

        /// <summary>
        /// Create a compatible dataset for indexed projections
        /// </summary>
        /// <param name="name"></param>
        /// <param name="driver"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Dataset CreateDS2(string name, string driver, int width = -1, int height = -1)
        {
            name = GetTempFile(name, driver);
            if (width == -1) width = Size.Width;
            if (height == -1) height = Size.Height;

            //int srcNumOfBands = Dataset.RasterCount;
            Band srcBand1 = Dataset.GetRasterBand(1);
            DataType bandDataType = srcBand1.DataType;

            ColorInterp[] bandColorInt = new ColorInterp[] { ColorInterp.GCI_RedBand, ColorInterp.GCI_GreenBand, ColorInterp.GCI_BlueBand, ColorInterp.GCI_AlphaBand };

            int destNumOfBands = 4; // RGBA by default

            if (driver == "MEM") driver = CheckRequredMemory(width, height, destNumOfBands);

            using (var drv = Gdal.GetDriverByName(driver))
            {
                Dataset ds = drv.Create(name, width, height, destNumOfBands, bandDataType, null);

                for (int i = 0; i < destNumOfBands; i++)
                {
                    Band band = ds.GetRasterBand(i + 1);
                    //Band srcBand = (i < srcNumOfBands) ? this.Dataset.GetRasterBand(i + 1) : null;

                    band.SetColorInterpretation(bandColorInt[i]);
                    if (bandColorInt[i] == ColorInterp.GCI_AlphaBand)
                        band.Fill((double)GDALHelper.ALPHA_MASK, 0);
                }

                ds.SetProjection(Wkt);
                ds.SetGeoTransform(Transform.m_transform);

                return ds;
            }
        }

        /// <summary>
        /// Create a dataset in memory that is compatible in size, datatype and composition with the source dataset.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="driver"></param>
        /// <param name="noIndex">Don't generate an indexed dataset</param>
        /// <param name="copyNodata">Don't copy the nodata</param>
        /// <returns></returns>
        protected Dataset CreateCompatibleDataset(string name, int width = -1, int height = -1, string driver = "MEM", bool noIndex = false, bool copyNodata = false)
        {
            int srcNumOfBands = Dataset.RasterCount;
            int destNumOfBands = 4; // RGBA by default
            Band srcBand1 = Dataset.GetRasterBand(1);
            DataType bandDataType = srcBand1.DataType;
            if (width == -1) width = Size.Width;
            if (height == -1) height = Size.Height;
            ColorInterp[] bandColorInt = new ColorInterp[] { ColorInterp.GCI_RedBand, ColorInterp.GCI_GreenBand, ColorInterp.GCI_BlueBand, ColorInterp.GCI_AlphaBand };

            if (!noIndex)
            {
                if (bandDataType == DataType.GDT_Byte && srcBand1.GetColorInterpretation() == ColorInterp.GCI_PaletteIndex)
                {
                    bandColorInt = new ColorInterp[] { ColorInterp.GCI_PaletteIndex, ColorInterp.GCI_AlphaBand };
                    destNumOfBands = 2; // RGBA by default
                }
            }

            if (driver == "MEM") driver = CheckRequredMemory(width, height, destNumOfBands);

            name = GetTempFile(name, driver);

            using (var drvMem = Gdal.GetDriverByName(driver))
            {
                Dataset ds = drvMem.Create(name, width, height, destNumOfBands, bandDataType, null);

                // MFC cancel nodata
                double? missingDataSignal = null;
                if (copyNodata)
                {
                    missingDataSignal = Dataset.GetRasterBand(1).GetNoData();
                    if (!missingDataSignal.HasValue)
                        missingDataSignal = 255;
                }

                for (int i = 0; i < destNumOfBands; i++)
                {
                    Band band = ds.GetRasterBand(i + 1);

                    if (missingDataSignal != null)
                        band.SetNoDataValue(missingDataSignal.Value);

                    Band srcBand = (i < srcNumOfBands) ? Dataset.GetRasterBand(i + 1) : null;

                    ColorInterp colorInt;

                    if (null != srcBand)
                    {
                        colorInt = srcBand.GetColorInterpretation();

                        if (colorInt == ColorInterp.GCI_Undefined)
                            colorInt = bandColorInt[i];

                        band.SetColorInterpretation(colorInt);

                        if (colorInt == ColorInterp.GCI_PaletteIndex && !noIndex)
                            band.SetColorTable(srcBand.GetColorTable());
                    }
                    else
                    {
                        colorInt = bandColorInt[i];
                        band.SetColorInterpretation(colorInt);
                    }

                    if (colorInt == ColorInterp.GCI_AlphaBand)
                        band.Fill((double)GDALHelper.ALPHA_OPAQUE, 0);

                    if (null != missingDataSignal && colorInt == ColorInterp.GCI_GrayIndex)
                        band.Fill(missingDataSignal.Value, 0);
                }

                ds.SetProjection(Wkt);
                ds.SetGeoTransform(Transform.m_transform);

                return ds;
            }
        }

        /// <summary>
        /// Check the memory required for a new dataset with the width, height and number of bands
        /// If the memory exceeds the amount we have then use the GeoTIFF driver
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="driver"></param>
        /// <param name="destNumOfBands"></param>
        /// <returns></returns>
        private static string CheckRequredMemory(int width, int height, int destNumOfBands, string driver = "MEM")
        {
            // bands * width * height * bytes per sample
            int size = (destNumOfBands * width * height * 1) + 100000;
            try
            {
                // try to determine how much memory is needed
                MemoryFailPoint memFailPoint = new MemoryFailPoint(size);
            }
            catch (InsufficientMemoryException)
            {
                Trace.TraceInformation("System does not have {0} bytes for this operation, falling back to disk", size);
                // change the driver to GTiff and we can allocate on disk
                driver = "GTiff";
            }
            return driver;
        }

        /// <summary>
        /// Get the nodata value for a band
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static double GetNoData(Band source)
        {
            double? nodata = source.GetNoData();
            if (!nodata.HasValue)
            {
                if (source.DataType == DataType.GDT_Byte)
                    nodata = 255;
            }
            return nodata.Value;
        }

        #endregion

        #region static methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsValid(string path) => Gdal.IdentifyDriver(path, null) != null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HandleProcessCorruptedStateExceptions()]
        public static GDALImage FromFile(string fileName, bool readOnly = true)
        {
            try
            {
                Access access = readOnly ? Access.GA_ReadOnly : Access.GA_Update;

                if (!fileName.StartsWith("/vsimem"))
                {
                    if (!File.Exists(fileName))
                    {
                        Trace.TraceError("GDALImage.FromFile: Error, file \"{0}\" does not exist when attempting to open as GDAL Dataset", fileName);
                        return null;
                    }
                }

                if (!IsValid(fileName))
                {
                    Trace.TraceError("GDALImage.FromFile: Error, file \"{0}\" cannot be read by GDAL", fileName);
                    return null;
                }

                return new GDALImage()
                {
                    FileName = fileName,
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Dataset = Gdal.OpenShared(fileName, access)
                };
            }
            catch (Exception ex)
            {
                Trace.TraceError("GDALImage.FromFile: Error:\r\n{0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Create a GDALImage from a raw dataset
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        public static GDALImage FromDataset(Dataset dataset)
        {
            string description = dataset.GetDescription();
            string fileName = File.Exists(description) ? description : string.Empty;
            string name = string.IsNullOrEmpty(fileName) ? string.Empty : Path.GetFileNameWithoutExtension(fileName);
            return new GDALImage()
            {
                Name = name,
                FileName = fileName,
                Dataset = dataset
            };
        }

        #endregion

        #region Disposers and finalizers

        private bool m_disposed;

        /// <summary>
        /// Disposes the GdalRasterLayer and release the raster file
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                    if (m_dataset != null)
                    {
                        try
                        {
                            m_dataset.Dispose();
                        }
                        finally
                        {
                            m_dataset = null;
                        }
                    }
                m_disposed = true;
            }
        }

        /// <summary> Finalizer </summary>
        ~GDALImage() { Dispose(true); }

        #endregion

        #region not used
        ///// <summary>
        ///// A sample function to reproject and resample a GDAL dataset. 
        ///// The idea here is to reproject from one system to another, as well
        ///// as to change the pixel size. The procedure is slightly long-winded, but
        ///// goes like this:
        ///// https://jgomezdans.github.io/gdal_notes/reprojection.html
        ///// 1. Set up the two Spatial Reference systems.
        ///// 2. Open the original dataset, and get the geotransform
        ///// 3. Calculate bounds of new geotransform by projecting the UL corners 
        ///// 4. Calculate the number of pixels with the new projection & spacing
        ///// 5. Create an in-memory raster dataset
        ///// 6. Perform the projection
        ///// </summary>
        //[HandleProcessCorruptedStateExceptions()]
        //public GDALImage ReprojectDataset(
        //    float pixel_spacing = 5000,
        //    string filename = @"/vsimem/tiffinmem",     // default to in memory file
        //    string epsg_to = "3785",
        //    string driver = "GTiff",
        //    Func<double, string, string, bool> progress = null)
        //{
        //    try
        //    {
        //        if (pixel_spacing <= 0)
        //            pixel_spacing = (float)this.XResolution;

        //        #region get the wkt of the destination CS
        //        string dst_wkt;

        //        SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
        //        dst_sr.ExportToWkt(out dst_wkt);
        //        #endregion

        //        #region get the wkt of the source CS
        //        string src_wkt = Dataset.GetProjection();
        //        SpatialReference src_sr = new SpatialReference(src_wkt);
        //        #endregion

        //        #region set up parameters for new dataset
        //        CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr);

        //        // Up to here, all  the projection have been defined, as well as a 
        //        // transformation from the source to the destination 
        //        GeoTransform geo_t = this.Transform;

        //        var x_size = Dataset.RasterXSize;   // Raster xsize
        //        var y_size = Dataset.RasterYSize;   // Raster ysize

        //        // Work out the boundaries of the new dataset in the target projection
        //        // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
        //        // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
        //        PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
        //        PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * x_size, geo_t[3] + geo_t[5] * y_size, 0.0);

        //        DataType dataType = this.DataType;
        //        int bands = Dataset.RasterCount;
        //        //if (bands == 3)
        //        //    bands += 1;

        //        // The size of the raster is given the new projection and pixel spacing
        //        // Using the values we calculated above. Also, setting it to store one band and to use Float32 data type.
        //        int new_xSize = (int)((lr.X - ul.X) / pixel_spacing);
        //        int new_ySize = (int)((ul.Y - lr.Y) / pixel_spacing);
        //        #endregion

        //        #region export to new dataset
        //        //Driver driver = Gdal.GetDriverByName("MEM");
        //        //Dataset dest = mem_drv.Create("", new_xSize, new_ySize, bands, dataType, null);

        //        CPLErr res;
        //        //Dataset dest = mem_drv.Create(@"/vsimem/tiffinmem", new_xSize, new_ySize, bands, dataType, null);
        //        Dataset dest = Gdal.GetDriverByName(driver).Create(filename, new_xSize, new_ySize, bands, dataType, null);
        //        GeoTransform new_geo = new GeoTransform(ul.X, pixel_spacing, geo_t[2], ul.Y, geo_t[4], -pixel_spacing);

        //        // Set the geotransform
        //        dest.SetGeoTransform(new_geo._transform);
        //        dest.SetProjection(dst_wkt);

        //        ////Set no data  
        //        //const double noDataValue = 256;
        //        //Band band = dest.GetRasterBand(3);
        //        //band.SetNoDataValue(noDataValue);

        //        res = Gdal.ReprojectImage(this.Dataset, dest, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_Bilinear, 0.0, 0.125,
        //            delegate(double complete, IntPtr message, IntPtr data)
        //            {
        //                return HandleProgress(progress, complete, message, data);
        //            }, null);

        //        dest.FlushCache();

        //        #endregion

        //        #region reload dataset

        //        if (res != CPLErr.CE_None)
        //            return null;
        //        if (driver == "MEM" || filename.StartsWith(@"/vsimem"))
        //        {
        //            return new GDALImage() { Dataset = dest };
        //        }
        //        else
        //        {
        //            dest.Dispose();
        //            return GDALImage.FromFile(filename);
        //        }
        //        #endregion

        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError("Error reprojecting:\r\n{0}", ex);
        //        throw ex;
        //    }
        //}

        //[HandleProcessCorruptedStateExceptions()]
        //public GDALImage ReprojectDataset2(
        //    float pixel_spacing = 5000,
        //    string filename = @"/vsimem/tiffinmem",     // default to in memory file
        //    string epsg_to = "3785",
        //    string driver = "GTiff",
        //    Func<double, string, string, bool> progress = null)
        //{
        //    try
        //    {
        //        if (pixel_spacing <= 0)
        //            pixel_spacing = (float)this.XResolution;

        //        #region get the wkt of the destination CS
        //        string dst_wkt;

        //        SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
        //        dst_sr.ExportToWkt(out dst_wkt);
        //        #endregion

        //        #region get the wkt of the source CS
        //        string src_wkt = Dataset.GetProjection();
        //        SpatialReference src_sr = new SpatialReference(src_wkt);
        //        #endregion

        //        #region set up parameters for new dataset
        //        CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr);

        //        // Up to here, all  the projection have been defined, as well as a 
        //        // transformation from the source to the destination 
        //        GeoTransform geo_t = this.Transform;

        //        var x_size = Dataset.RasterXSize;   // Raster xsize
        //        var y_size = Dataset.RasterYSize;   // Raster ysize

        //        // Work out the boundaries of the new dataset in the target projection
        //        // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
        //        // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
        //        PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
        //        PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * x_size, geo_t[3] + geo_t[5] * y_size, 0.0);

        //        DataType dataType = this.DataType;
        //        int bands = Dataset.RasterCount;
        //        if (!this.HasAlpha)
        //            bands += 1;

        //        // The size of the raster is given the new projection and pixel spacing
        //        // Using the values we calculated above. Also, setting it to store one band and to use Float32 data type.
        //        int new_xSize = (int)((lr.X - ul.X) / pixel_spacing);
        //        int new_ySize = (int)((ul.Y - lr.Y) / pixel_spacing);
        //        #endregion

        //        #region export to new dataset
        //        //Driver driver = Gdal.GetDriverByName("MEM");
        //        //Dataset dest = mem_drv.Create("", new_xSize, new_ySize, bands, dataType, null);

        //        CPLErr res;
        //        //Dataset dest = mem_drv.Create(@"/vsimem/tiffinmem", new_xSize, new_ySize, bands, dataType, null);
        //        Dataset dest = Gdal.GetDriverByName(driver).Create(filename, new_xSize, new_ySize, bands, dataType, null);
        //        dest.GetRasterBand(bands).SetColorInterpretation(ColorInterp.GCI_AlphaBand);

        //        GeoTransform new_geo = new GeoTransform(ul.X, pixel_spacing, geo_t[2], ul.Y, geo_t[4], -pixel_spacing);

        //        // Set the geotransform
        //        dest.SetGeoTransform(new_geo._transform);
        //        dest.SetProjection(dst_wkt);

        //        res = Gdal.ReprojectImage(this.Dataset, dest, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_Bilinear, 0.0, 0.125,
        //            delegate(double complete, IntPtr message, IntPtr data)
        //            {
        //                return HandleProgress(progress, complete, message, data);
        //            }, null);

        //        dest.FlushCache();

        //        #endregion

        //        if (res != CPLErr.CE_None)
        //            return null;
        //        if (driver == "MEM" || filename.StartsWith(@"/vsimem"))
        //        {
        //            return new GDALImage() { Dataset = dest };
        //        }
        //        else
        //        {
        //            dest.Dispose();
        //            return GDALImage.FromFile(filename);
        //        }


        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.TraceError("Error reprojecting:\r\n{0}", ex);
        //        throw ex;
        //    }
        //}


        //[HandleProcessCorruptedStateExceptions()]
        //public void ReprojectDataset4(
        //    double pixel_spacing = 5000,
        //    string filename = @"/vsimem/tiffinmem",     // default to in memory file
        //    string epsg_to = "3785",
        //    string driver = "GTiff",
        //    string[] options = null,
        //    Func<double, string, string, bool> progress = null)
        //{

        //    Dataset dest = null;
        //    try
        //    {
        //        if (pixel_spacing <= 0)
        //            pixel_spacing = (float)this.XResolution;

        //        Dataset source = this.Dataset;
        //        for (int i = 0; i < this.Rasters; i++)
        //        {
        //            string info = source.GetRasterBand(i + 1).GetInfo();
        //            Trace.TraceInformation(info);
        //        }

        //        #region get the wkt of the destination CS
        //        string dst_wkt;
        //        SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
        //        dst_sr.ExportToWkt(out dst_wkt);
        //        #endregion

        //        #region get the wkt of the source CS
        //        string src_wkt = source.GetProjection();
        //        SpatialReference src_sr = new SpatialReference(src_wkt);
        //        #endregion

        //        #region set up parameters for new dataset
        //        CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr);

        //        // Up to here, all  the projection have been defined, as well as a 
        //        // transformation from the source to the destination 
        //        GeoTransform geo_t = this.Transform;

        //        var x_size = source.RasterXSize;   // Raster xsize
        //        var y_size = source.RasterYSize;   // Raster ysize

        //        // Work out the boundaries of the new dataset in the target projection
        //        // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
        //        // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
        //        PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
        //        PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * x_size, geo_t[3] + geo_t[5] * y_size, 0.0);

        //        DataType dataType = this.DataType;
        //        int bands = source.RasterCount;
        //        if (bands == 3)
        //            bands += 1;
        //        else if (bands == 1)
        //            bands += 1;

        //        // The size of the raster is given the new projection and pixel spacing
        //        // Using the values we calculated above. Also, setting it to store one band and to use Float32 data type.
        //        int new_xSize = (int)((lr.X - ul.X) / pixel_spacing);
        //        int new_ySize = (int)((ul.Y - lr.Y) / pixel_spacing);
        //        #endregion

        //        #region create new dataset
        //        // create the dataset to export to
        //        dest = CreateCompatibleDataset(new_xSize, new_ySize);

        //        // Set the geotransform
        //        GeoTransform new_geo = new GeoTransform(ul.X, pixel_spacing, geo_t[2], ul.Y, geo_t[4], -pixel_spacing);
        //        dest.SetGeoTransform(new_geo._transform);
        //        dest.SetProjection(dst_wkt);

        //        #endregion

        //        #region reproject the source image

        //        HandleProgress(progress, 0, "Projecting image");

        //        CPLErr res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_Bilinear, 0.0, 0.125,
        //            delegate(double complete, IntPtr message, IntPtr data)
        //            {
        //                return HandleProgress(progress, complete, message, data);
        //            }, null);

        //        if (res != CPLErr.CE_None)
        //        {
        //            Trace.TraceError("Error reprojecting: {0}", Gdal.GetLastErrorMsg());
        //            return;
        //        }
        //        #endregion

        //        #region mask
        //        // Create a mask dataset with the same dimensions as the source dataset, 
        //        // and another with the same dimensions as the destination
        //        // warp the blank source to the destination and the new pixels should be "black"
        //        HandleProgress(progress, 1, "Projecting alpha");
        //        using (Dataset sourceMask = CreateCompatibleDataset(this.Size.Width, this.Size.Height))
        //        using (Dataset destMask = CreateMaskDataset("dest", new_xSize, new_ySize, this.DataType))
        //        {
        //            sourceMask.SetGeoTransform(this.Transform._transform);
        //            sourceMask.SetProjection(src_wkt);

        //            destMask.SetGeoTransform(new_geo._transform);
        //            destMask.SetProjection(dst_wkt);

        //            res = Gdal.ReprojectImage(sourceMask, destMask, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_NearestNeighbour, 0.0, 0.125,
        //                delegate(double complete, IntPtr message, IntPtr data)
        //                {
        //                    return HandleProgress(progress, complete + 1, message, data);
        //                }, null);

        //            if (res != CPLErr.CE_None)
        //            {
        //                Trace.TraceError("Error reprojecting: {0}", Gdal.GetLastErrorMsg());
        //                return;
        //            }
        //            //Copy(sourceMask, "GTiff", Path.Combine(Path.GetDirectoryName(filename), "sourceMask.tif"), null, progress);

        //            //Copy(destMask, "GTiff", Path.Combine(Path.GetDirectoryName(filename), "destMask.tif"), null, progress);

        //            // write the mask band into band 4, inverting the pixels
        //            ApplyMask(dest, destMask, true);

        //        }
        //        #endregion

        //        // save the resulting dataset
        //        dest.Save(filename, driver, options, progress);
        //    }
        //    catch (Exception ex)
        //    {
        //        HandleError(ex);
        //    }
        //    finally
        //    {
        //        if (dest != null)
        //        {
        //            dest.Dispose();
        //            dest = null;
        //        }
        //    }
        //}

        ///// <summary>
        ///// Project and trim in one step
        ///// Does not leave transparency properly as the mask is not created 
        ///// </summary>
        ///// <param name="pixel_spacing"></param>
        ///// <param name="filename"></param>
        ///// <param name="epsg_to"></param>
        ///// <param name="driver"></param>
        ///// <param name="options"></param>
        ///// <param name="progress"></param>
        //[HandleProcessCorruptedStateExceptions()]
        //public void ReprojectDataset5(
        //    double pixel_spacing = 5000,
        //    string filename = @"/vsimem/tiffinmem",     // default to in memory file
        //    string epsg_to = "3785",
        //    string driver = "GTiff",
        //    string[] options = null,
        //    Func<double, string, string, bool> progress = null)
        //{

        //    Dataset dest = null;
        //    try
        //    {
        //        if (pixel_spacing <= 0)
        //            pixel_spacing = (float)this.XResolution;

        //        Dataset source = this.Dataset;
        //        for (int i = 0; i < this.Rasters; i++)
        //        {
        //            string info = source.GetRasterBand(i + 1).GetInfo();
        //            Trace.TraceInformation(info);
        //        }

        //        #region get the wkt of the destination CS
        //        string dst_wkt;
        //        SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
        //        dst_sr.ExportToWkt(out dst_wkt);
        //        #endregion

        //        #region get the wkt of the source CS
        //        string src_wkt = source.GetProjection();
        //        SpatialReference src_sr = new SpatialReference(src_wkt);
        //        #endregion

        //        #region set up parameters for new dataset
        //        DataType dataType = this.DataType;
        //        int bands = source.RasterCount;
        //        if (bands == 3)
        //            bands += 1;
        //        else if (bands == 1)
        //            bands += 1;

        //        Envelope envelope = GetBorder(dst_sr);
        //        Size newSize = new Size(0, 0);
        //        newSize.Width = (int)((envelope.MaxX - envelope.MinX) / pixel_spacing);
        //        newSize.Height = (int)((envelope.MaxY - envelope.MinY) / pixel_spacing);

        //        #endregion

        //        #region create new dataset
        //        // create the dataset to export to
        //        dest = CreateCompatibleDataset(newSize.Width, newSize.Height);

        //        // Set the geotransform
        //        GeoTransform new_geo = new GeoTransform(envelope.MinX, pixel_spacing, this.Transform[2], envelope.MaxY, this.Transform[4], -pixel_spacing);
        //        dest.SetGeoTransform(new_geo._transform);
        //        dest.SetProjection(dst_wkt);

        //        #endregion

        //        #region reproject the source image

        //        HandleProgress(progress, 0, "Projecting image");

        //        CPLErr res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_Bilinear, 0.0, 0.125,
        //            delegate(double complete, IntPtr message, IntPtr data)
        //            {
        //                return HandleProgress(progress, complete, message, data);
        //            }, null);

        //        if (res != CPLErr.CE_None)
        //        {
        //            Trace.TraceError("Error reprojecting: {0}", Gdal.GetLastErrorMsg());
        //            return;
        //        }
        //        #endregion

        //        #region mask
        //        // Create a mask dataset with the same dimensions as the source dataset, 
        //        // and another with the same dimensions as the destination
        //        // warp the blank source to the destination and the new pixels should be "black"
        //        HandleProgress(progress, 1, "Projecting alpha");
        //        using (Dataset srcMask = CreateCompatibleDataset(this.Size.Width, this.Size.Height))
        //        using (Dataset dstMask = CreateMaskDataset("dest", newSize.Width, newSize.Height, this.DataType))
        //        {
        //            srcMask.SetGeoTransform(this.Transform._transform);
        //            srcMask.SetProjection(src_wkt);

        //            dstMask.SetGeoTransform(new_geo._transform);
        //            dstMask.SetProjection(dst_wkt);

        //            res = Gdal.ReprojectImage(srcMask, dstMask, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_NearestNeighbour, 0.0, 0.125,
        //                delegate(double complete, IntPtr message, IntPtr data)
        //                {
        //                    return HandleProgress(progress, complete + 1, message, data);
        //                }, null);

        //            if (res != CPLErr.CE_None)
        //            {
        //                Trace.TraceError("Error reprojecting: {0}", Gdal.GetLastErrorMsg());
        //                return;
        //            }

        //            srcMask.Save(Path.Combine(Path.GetDirectoryName(filename), "sourceMask.tif"), "GTiff", null, progress);
        //            dstMask.Save(Path.Combine(Path.GetDirectoryName(filename), "destMask.tif"), "GTiff", null, progress);

        //            // write the mask band into band 4, inverting the pixels
        //            ApplyMask(dest, dstMask, true);
        //        }
        //        #endregion

        //        dest.Save(filename, driver, options, progress);
        //    }
        //    catch (Exception ex)
        //    {
        //        HandleError(ex);
        //    }
        //    finally
        //    {
        //        if (dest != null)
        //        {
        //            dest.Dispose();
        //            dest = null;
        //        }
        //    }
        //}

        //[HandleProcessCorruptedStateExceptions()]
        //public GDALImage ReprojectDataset3( double pixel_spacing = 5000, string filename = @"/vsimem/tiffinmem", string epsg_to = "3785",string driver = "GTiff",
        //    Func<double, string, string, bool> progress = null)
        //{
        //    try
        //    {

        //        Dataset source = this.Dataset;
        //        for (int i = 0; i < this.Rasters; i++)
        //        {
        //            string info = source.GetRasterBand(i + 1).GetInfo();
        //            Trace.TraceInformation(info);
        //        }

        //        #region get the wkt of the destination CS
        //        string dst_wkt;

        //        SpatialReference dst_sr = OSRHelper.SRSFromEPSG(epsg_to);
        //        dst_sr.ExportToWkt(out dst_wkt);
        //        #endregion

        //        #region get the wkt of the source CS
        //        string src_wkt = source.GetProjection();
        //        SpatialReference src_sr = new SpatialReference(src_wkt);
        //        #endregion

        //        #region set up parameters for new dataset
        //        CoordinateTransformation tx = Osr.CreateCoordinateTransformation(src_sr, dst_sr);

        //        // Up to here, all  the projection have been defined, as well as a 
        //        // transformation from the source to the destination 
        //        GeoTransform geo_t = this.Transform;

        //        var x_size = source.RasterXSize;   // Raster xsize
        //        var y_size = source.RasterYSize;   // Raster ysize

        //        // Work out the boundaries of the new dataset in the target projection
        //        // (ulx, uly, ulz ) = tx.TransformPoint( geo_t[0], geo_t[3])
        //        // (lrx, lry, lrz ) = tx.TransformPoint( geo_t[0] + geo_t[1]*x_size, geo_t[3] + geo_t[5]*y_size )
        //        PointDType ul = tx.Transform(geo_t[0], geo_t[3], 0.0);
        //        PointDType lr = tx.Transform(geo_t[0] + geo_t[1] * x_size, geo_t[3] + geo_t[5] * y_size, 0.0);

        //        DataType dataType = this.DataType;
        //        int bands = source.RasterCount;
        //        if (bands == 3)
        //            bands += 1;
        //        else if (bands == 1)
        //            bands += 1;

        //        // The size of the raster is given the new projection and pixel spacing
        //        // Using the values we calculated above. Also, setting it to store one band and to use Float32 data type.
        //        pixel_spacing = GetPixelSpacing(pixel_spacing);
        //        int new_xSize = (int)((lr.X - ul.X) / pixel_spacing);
        //        int new_ySize = (int)((ul.Y - lr.Y) / pixel_spacing);
        //        #endregion

        //        #region export to new dataset

        //        CPLErr res;
        //        using (var drv = Gdal.GetDriverByName(driver))
        //        {
        //            Dataset dest = drv.Create(filename, new_xSize, new_ySize, bands, dataType, null);

        //            // set the colour of the last raster to alpha
        //            dest.GetRasterBand(bands).SetColorInterpretation(ColorInterp.GCI_AlphaBand);
        //            dest.GetRasterBand(bands).Fill(255, 0);

        //            GeoTransform new_geo = new GeoTransform(ul.X, pixel_spacing, geo_t[2], ul.Y, geo_t[4], -pixel_spacing);

        //            // Set the geotransform
        //            dest.SetGeoTransform(new_geo._transform);
        //            dest.SetProjection(dst_wkt);

        //            res = Gdal.ReprojectImage(source, dest, src_wkt, dst_wkt, OSGeo.GDAL.ResampleAlg.GRA_Bilinear, 0.0, 0.125,
        //                delegate(double complete, IntPtr message, IntPtr data)
        //                {
        //                    return HandleProgress(progress, complete, message, data);
        //                }, null);

        //            dest.FlushCache();

        //            if (res != CPLErr.CE_None)
        //                throw new GDALException("Error reprojecting");

        //            if (driver == "MEM" || filename.StartsWith(@"/vsimem"))
        //            {
        //                return new GDALImage() { Dataset = dest };
        //            }
        //            else
        //            {
        //                dest.Dispose();
        //                return GDALImage.FromFile(filename);
        //            }
        //        }
        //        #endregion

        //    }
        //    catch (Exception ex)
        //    {
        //        string gdalError = Gdal.GetLastErrorMsg();

        //        if (string.IsNullOrEmpty(gdalError))
        //        {
        //            Trace.TraceError("Error reprojecting {0}:\r\n{1}", Gdal.GetLastErrorMsg(), ex);
        //            throw ex;
        //        }
        //        else
        //        {
        //            Trace.TraceError("Error reprojecting {0}:\r\n{1}", gdalError, ex);
        //            throw new Exception(gdalError, ex);
        //        }
        //    }
        //}
        #endregion

    }
}

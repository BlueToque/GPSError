using System;
using System.Drawing;

namespace GPSError.GDAL
{
    /// <summary>
    /// The coefficients for transforming between pixel/line (X,Y) raster space, and projection coordinates (Xp,Yp) space.<br/>
    /// Xp = T[0] + T[1]*X + T[2]*Y<br/>
    /// Yp = T[3] + T[4]*X + T[5]*Y<br/>
    /// In a north up image, T[1] is the pixel width, and T[5] is the pixel height.
    /// The upper left corner of the upper left pixel is at position (T[0],T[3]).
    /// </summary>
    public class GeoTransform
    {
        private readonly double[] m_inverseTransform = new double[6];
        internal double[] m_transform = new double[6];

        #region public properties

        /// <summary>
        /// returns value of the transform array
        /// </summary>
        /// <param name="i">place in array</param>
        /// <returns>value depedent on i</returns>
        public double this[int i] { get => m_transform[i]; set => m_transform[i] = value; }

        /// <summary> </summary>
        public double XSize { get; }

        /// <summary> </summary>
        public double YSize { get; }

        public double[] Inverse => m_inverseTransform;

        /// <summary> returns true if no values were fetched </summary>
        public bool IsTrivial =>
            m_transform[0] == 0 && m_transform[1] == 1 &&
            m_transform[2] == 0 && m_transform[3] == 0 &&
            m_transform[4] == 0 && m_transform[5] == 1;

        /// <summary> left value of the image </summary>       
        public double Left { get => m_transform[0]; set => m_transform[0] = value; }

        /// <summary> right value of the image </summary>       
        public double Right => Left + (HorizontalPixelResolution * XSize);

        /// <summary> top value of the image </summary>
        public double Top { get => m_transform[3]; set => m_transform[3] = value; }

        /// <summary> bottom value of the image </summary>
        public double Bottom => Top + (VerticalPixelResolution * YSize);

        /// <summary> west to east pixel resolution </summary>
        public double HorizontalPixelResolution { get => m_transform[1]; set => m_transform[1] = value; }

        /// <summary> north to south pixel resolution </summary>
        public double VerticalPixelResolution { get => m_transform[5]; set => m_transform[5] = value; }

        public double XRotation { get => m_transform[2]; set => m_transform[2] = value; }

        public double YRotation { get => m_transform[4]; set => m_transform[4] = value; }

        public Extents Extents
        {
            get
            {
                SizeF imagesize = new SizeF((float)XSize, (float)YSize);
                return Extents.FromLTRB(
                    EnvelopeLeft(imagesize.Width, imagesize.Height),
                    EnvelopeTop(imagesize.Width, imagesize.Height),
                    EnvelopeRight(imagesize.Width, imagesize.Height),
                    EnvelopeBottom(imagesize.Width, imagesize.Height));
            }
        }

        #endregion

        #region constructors

        /// <summary> Constructor </summary>
        public GeoTransform()
        {
            m_transform = new double[6]
            {
                999.5,  // x origin
                1,      // horizontal pixel resolution 
                0,      // rotation, 0 if image is "north up" 
                1000.5, // y origin
                0,      // rotation, 0 if image is "north up" 
                -1,     // vertical pixel resolution
            };
            CreateInverse();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="array"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public GeoTransform(double[] array, double width, double height)
        {
            if (array == null) throw new ArgumentNullException("array");

            if (array.Length != 6) throw new ApplicationException("GeoTransform constructor invoked with invalid sized array");

            XSize = width;
            YSize = height;

            m_transform = array;
            CreateInverse();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p1">x origin</param>
        /// <param name="p2">horizontal pixel resolution</param>
        /// <param name="p3">rotation, 0 if image is "north up"</param>
        /// <param name="p4">y origin</param>
        /// <param name="p5">rotation, 0 if image is "north up"</param>
        /// <param name="p6">vertical pixel resolution</param>
        public GeoTransform(double p1, double p2, double p3, double p4, double p5, double p6)
        {
            m_transform = new double[6] { p1, p2, p3, p4, p5, p6, };
            CreateInverse();
        }

        private void CreateInverse()
        {
            // compute determinant
            double det = m_transform[1] * m_transform[5] - m_transform[2] * m_transform[4];
            if (det == 0.0) return;

            // inverse rot/scale portion
            m_inverseTransform[1] = m_transform[5] / det;
            m_inverseTransform[2] = -m_transform[2] / det;
            m_inverseTransform[4] = -m_transform[4] / det;
            m_inverseTransform[5] = m_transform[1] / det;

            // compute translation elements
            m_inverseTransform[0] = -m_inverseTransform[1] * m_transform[0] - m_inverseTransform[2] * m_transform[3];
            m_inverseTransform[3] = -m_inverseTransform[4] * m_transform[0] - m_inverseTransform[5] * m_transform[3];
        }

        #endregion

        #region public methods

        /// <summary>
        /// converts image point into projected point
        /// </summary>
        /// <param name="imgX">image x value</param>
        /// <param name="imgY">image y value</param>
        /// <returns>projected x coordinate</returns>
        public double ProjectedX(double imgX, double imgY) => m_transform[0] + (m_transform[1] * imgX) + (m_transform[2] * imgY);

        /// <summary>
        /// converts image point into projected point
        /// </summary>
        /// <param name="imgX">image x value</param>
        /// <param name="imgY">image y value</param>
        /// <returns>projected y coordinate</returns>
        public double ProjectedY(double imgX, double imgY) => m_transform[3] + m_transform[4] * imgX + m_transform[5] * imgY;

        public PointDType ImageToGround(PointDType imagePoint) => new PointDType
        {
            X = m_transform[0] + m_transform[1] * imagePoint.X + m_transform[2] * imagePoint.Y,
            Y = m_transform[3] + m_transform[4] * imagePoint.X + m_transform[5] * imagePoint.Y
        };

        public PointDType GroundToImage(PointDType groundPoint) => new PointDType
        {
            X = m_inverseTransform[0] + m_inverseTransform[1] * groundPoint.X + m_inverseTransform[2] * groundPoint.Y,
            Y = m_inverseTransform[3] + m_inverseTransform[4] * groundPoint.X + m_inverseTransform[5] * groundPoint.Y
        };

        public double GndW(double imgWidth, double imgHeight) =>
            (m_transform[2] < 0 && m_transform[4] < 0 && m_transform[5] < 0) ?
                Math.Abs((m_transform[1] * imgWidth) + (m_transform[2] * imgHeight)) :
                Math.Abs((m_transform[1] * imgWidth) - (m_transform[2] * imgHeight));

        public double GndH(double imgWidth, double imgHeight) =>
            (m_transform[2] < 0 && m_transform[4] < 0 && m_transform[5] < 0) ?
                Math.Abs((m_transform[4] * imgWidth) - (m_transform[5] * imgHeight)) :
                Math.Abs((m_transform[4] * imgWidth) - (m_transform[5] * imgHeight));

        /// <summary>
        /// finds leftmost pixel location (handles rotation)
        /// </summary>
        /// <param name="imgWidth"></param>
        /// <param name="imgHeight"></param>
        /// <returns></returns>
        public double EnvelopeLeft(double imgWidth, double imgHeight)
        {
            double left = Math.Min(m_transform[0], m_transform[0] + (m_transform[1] * imgWidth));
            left = Math.Min(left, m_transform[0] + (m_transform[2] * imgHeight));
            left = Math.Min(left, m_transform[0] + (m_transform[1] * imgWidth) + (m_transform[2] * imgHeight));
            return left;
        }

        /// <summary>
        /// finds rightmost pixel location (handles rotation)
        /// </summary>
        /// <param name="imgWidth"></param>
        /// <param name="imgHeight"></param>
        /// <returns></returns>
        public double EnvelopeRight(double imgWidth, double imgHeight)
        {
            double right = Math.Max(m_transform[0], m_transform[0] + (m_transform[1] * imgWidth));
            right = Math.Max(right, m_transform[0] + (m_transform[2] * imgHeight));
            right = Math.Max(right, m_transform[0] + (m_transform[1] * imgWidth) + (m_transform[2] * imgHeight));
            return right;
        }

        /// <summary>
        /// finds topmost pixel location (handles rotation)
        /// </summary>
        /// <param name="imgWidth"></param>
        /// <param name="imgHeight"></param>
        /// <returns></returns>
        public double EnvelopeTop(double imgWidth, double imgHeight)
        {
            double top = Math.Max(m_transform[3], m_transform[3] + (m_transform[4] * imgWidth));
            top = Math.Max(top, m_transform[3] + (m_transform[5] * imgHeight));
            top = Math.Max(top, m_transform[3] + (m_transform[4] * imgWidth) + (m_transform[5] * imgHeight));
            return top;
        }

        /// <summary>
        /// finds bottommost pixel location (handles rotation)
        /// </summary>
        /// <param name="imgWidth"></param>
        /// <param name="imgHeight"></param>
        /// <returns></returns>
        public double EnvelopeBottom(double imgWidth, double imgHeight)
        {
            double bottom = Math.Min(m_transform[3], m_transform[3] + (m_transform[4] * imgWidth));
            bottom = Math.Min(bottom, m_transform[3] + (m_transform[5] * imgHeight));
            bottom = Math.Min(bottom, m_transform[3] + (m_transform[4] * imgWidth) + (m_transform[5] * imgHeight));
            return bottom;
        }

        /// <summary> image was flipped horizontally </summary>
        public bool HorzFlip => m_transform[4] > 0;

        /// <summary> image was flipped vertically </summary>
        public bool VertFlip => m_transform[2] > 0;

        public bool IsFlipped => m_transform[5] > 0;

        public double PixelX(double lat) => (m_transform[0] - lat) / (m_transform[1] - m_transform[2]);

        public double PixelY(double lon) => Math.Abs(m_transform[3] - lon) / (m_transform[4] + m_transform[5]);

        public double PixelXwidth(double lat) => Math.Abs(lat / (m_transform[1] - m_transform[2]));

        public double PixelYwidth(double lon) => Math.Abs(lon / (m_transform[5] + m_transform[4]));

        public double RotationAngle => (m_transform[5] != 0) ? Math.Atan(m_transform[2] / m_transform[5]) * 57.2957795 : 0;

        #endregion
    }
}

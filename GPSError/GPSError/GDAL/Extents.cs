using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Xml.Serialization;

namespace GPSError.GDAL
{
    /// <summary>
    /// Extents structure represents a rectangular area on a map
    /// expressed in real world coordinates
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [System.Diagnostics.DebuggerDisplay("LTRB: {Left}, {Top}, {Right}, {Bottom} {SRS}")]
    public partial class Extents :
        IEquatable<Extents>,
        ICloneable//, //ISpatialRef
    {
        #region constructors

        /// <summary>
        /// Static constructors
        /// </summary>
        static Extents() => m_empty = new Extents();

        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public Extents() { Top = 0; Bottom = 0; Left = 0; Right = 0; SRS = ""; }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public Extents(Extents extents)
        {
            Trace.Assert(!(extents is null));

            Top = extents.Top;
            Bottom = extents.Bottom;
            Left = extents.Left;
            Right = extents.Right;
            SRS = extents.SRS;
        }

        /// <summary>
        /// Create extents from a position and a size
        /// </summary>
        /// <remarks>
        /// This method does not function like <see cref="Inflate(double,double)"/>
        /// the size is the total dimension of the extents, so
        /// it's like "inflate" size/2
        /// </remarks>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="srs"></param>
        public Extents(PointF center, SizeF size, string srs = "")
        {
            Top = center.Y + (size.Height / 2.0);
            Left = center.X - (size.Width / 2.0);
            Bottom = Top - (size.Height);
            Right = Left + (size.Width);
            SRS = srs;
        }

        /// <summary>
        /// Create extents from a position and a value indicates the size
        /// </summary>
        /// <remarks>
        /// This method does not function like <see cref="Inflate(double,double)"/>
        /// the size is the total dimension of the extents, so
        /// it's like "inflate" size/2
        /// </remarks>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="srs"></param>
        public Extents(PointF center, float size, string srs = "") : this(center, new SizeF(size, size), srs) { }

        /// <summary>
        /// Create an extents using the given rectagle structure
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="srs"></param>
        public Extents(RectangleF rect, string srs = "")
        {
            Top = rect.Top;
            Left = rect.Left;
            Bottom = rect.Bottom;
            Right = rect.Right;
            SRS = srs;
        }

        ///// <summary>
        ///// Create extents from a position and a size
        ///// </summary>
        ///// <remarks>
        ///// This method does not function like <see cref="Inflate(double,double)"/>
        ///// the size is the total dimension of the extents, so
        ///// it's like "inflate" size/2
        ///// </remarks>
        ///// <param name="center"></param>
        ///// <param name="size"></param>
        ///// <param name="srs"></param>
        //public Extents(IPointD center, SizeF size, string srs = "") : this(center, size.Width, size.Height, (center is ISpatialRef) ? (center as ISpatialRef).SRS : srs) { }

        ///// <summary>
        ///// Create extents from a position and a size
        ///// the size is the total dimension of the extents, so
        ///// it's like "inflate" size/2
        ///// </summary>
        ///// <param name="center"></param>
        ///// <param name="size"></param>
        ///// <param name="srs"></param>
        //public Extents(IPointD center, double size, string srs = "") : this(center, size, size, (center is ISpatialRef) ? (center as ISpatialRef).SRS : srs) { }

        ///// <summary>
        ///// Create extents from a position and a size
        ///// </summary>
        ///// <param name="center"></param>
        ///// <param name="width"></param>
        ///// <param name="height"></param>
        ///// <param name="srs"></param>
        //public Extents(IPointD center, double width, double height, string srs = "") => SetCentre(center, width, height, (center is ISpatialRef) ? (center as ISpatialRef).SRS : srs);

        /// <summary>
        /// Create extents from a position, width and height
        /// </summary>
        /// <param name="x">X coordinate of bottom left of the Extents</param>
        /// <param name="y">Y coordinate of the bottom left</param>
        /// <param name="width">Width of the extents</param>
        /// <param name="height">Height of the extents</param>
        /// <param name="srs"></param>
        public Extents(double x, double y, double width, double height, string srs = "")
        {
            Top = y + height;
            Left = x;
            Bottom = y;
            Right = x + width;
            SRS = srs;
        }

        ///// <summary>
        ///// Create extents from a top.left point and a bottom, right point
        ///// </summary>
        ///// <param name="topLeft"></param>
        ///// <param name="bottomRight"></param>
        ///// <param name="srs"></param>
        //public Extents(IPointD topLeft, IPointD bottomRight, string srs = "")
        //{
        //    if (topLeft is null || bottomRight is null)
        //        return;

        //    if ((topLeft is ISpatialRef) && (bottomRight is ISpatialRef) &&
        //        (topLeft as ISpatialRef).SRS != (bottomRight as ISpatialRef).SRS)
        //        return;

        //    if (topLeft.IsEmpty || bottomRight.IsEmpty)
        //    {
        //        Clear();
        //        return;
        //    }

        //    Top = topLeft.Y;
        //    Left = topLeft.X;
        //    Bottom = bottomRight.Y;
        //    Right = bottomRight.X;
        //    SRS = (topLeft is ISpatialRef) ? (topLeft as ISpatialRef).SRS : srs;
        //}

        /// <summary>
        /// Create extents from a top left point and a bottom right point
        /// </summary>
        /// <param name="topLeft"></param>
        /// <param name="bottomRight"></param>
        /// <param name="srs"></param>
        public Extents(PointF topLeft, PointF bottomRight, string srs = "")
        {
            Top = topLeft.Y;
            Left = topLeft.X;
            Bottom = bottomRight.Y;
            Right = bottomRight.X;
            SRS = srs;
        }

        #endregion

        #region fields

        private static readonly Extents m_empty;

        #endregion

        #region properties

        /// <summary> An empty extents structure </summary>
        public static Extents Empty => Extents.m_empty;

        ///// <summary> The extents of EPSG 3785 (Popular Visualisation CRS / Mercator) </summary>
        //public static Extents EPSG_3785 = Extents.FromBounds(-20037508.3428, -19971868.8804, 20037508.3428, 19971868.8804, ProjectionDefinition.WebMercator);

        ///// <summary> The extents of EPSG 3857 (Web Mercator) -20037508.342789, 20037508.342789, 20037508.342789, -20037508.342789 </summary>
        //public static Extents EPSG_3857 = Extents.FromBounds(-20026376.39, -20048966.10, 20026376.39, 20048966.10, ProjectionDefinition.WebMercator);

        ///// <summary> The extents of EPSG 4326 (Geographic projection) </summary>
        //public static Extents EPSG_4326 = Extents.FromBounds(-180.0, -89.9, 180.0, 89.9, ProjectionDefinition.Geographic);

        /// <summary>
        /// Width of extents
        /// </summary>
        [XmlIgnore]
        [Category("Data"), Description("Height of the rectagular region")]
        public double Height { get => (Top - Bottom); set => Top = Bottom + value; }

        /// <summary> Width of extents </summary>
        [XmlIgnore]
        [Category("Data"), Description("Width of the rectagular region")]
        public double Width { get => (Right - Left); set => Right = Left + value; }

        /// <summary> Size of extents </summary>
        [XmlIgnore]
        [Category("Data"), Description("Size of the extents")]
        public SizeF Size
        {
            get => new SizeF((float)Width, (float)Height);
            set { Width = value.Width; Height = value.Height; }
        }

        ///// <summary>
        ///// The area of this extents
        ///// If this is a UTM Extents then this is KM squared
        ///// </summary>
        //[Category("Info"), Description("Area of the rectagular region")]
        //public Area Area => new Area(Width * Height);

        /// <summary>
        /// The location of the center of the rectagular extents
        /// </summary>
        [XmlIgnore]
        [Category("Info"), Description("Co-ordinates of the center of the rectagular region")]
        public PointDType Center
        {
            get => new PointDType(X + Width / 2.0, Y + Height / 2.0);
            set => SetCentre(value, Width, Height);
        }

        /// <summary>
        /// Average of half of the width and height of the rectangle
        /// </summary>
        [Category("Info"), Description("Average of half of the width and height")]
        public double Radius => (Width + Height / 2.0) / 2.0;

        ///// <summary>
        ///// Location of extents
        ///// For extents this is the BOTTOM LEFT corner
        ///// </summary>
        //[XmlIgnore]
        //[Category("Data"), Description("Coordinates of the bottom left corner")]
        //public Coordinate Location { get => new Coordinate(X, Y, 0.0, SRS); set { Left = value.X; Bottom = value.Y; } }

        /// <summary> The X position of the bottom left corner </summary>
        [XmlIgnore]
        [Category("Data"), Description("The X position of the bottom left corner")]
        public double X { get => Left; set => Left = value; }

        /// <summary> The Y position of the bottom left corner </summary>
        [XmlIgnore]
        [Category("Data"), Description("The Y position of the bottom left corner")]
        public double Y { get => Bottom; set => Bottom = value; }

        /// <summary> The RectangleF for this extents </summary>
        [Browsable(false)]
        public RectangleF RectangleF => RectangleF.FromLTRB((float)Left, (float)Bottom, (float)Right, (float)Top);

        /// <summary> The rectangle for this extents </summary>
        [Browsable(false)]
        public Rectangle Rectangle => Rectangle.FromLTRB((int)Math.Round(Left), (int)Math.Round(Bottom), (int)Math.Round(Right), (int)Math.Round(Top));

        /// <summary>
        /// Tests whether the Width or Height property of this RectangleF has a value of zero.
        /// </summary>
        [Category("State"), Description("Are the extents empty")]
        public bool IsEmpty => (Left == 0) && (Right == 0) && (Bottom == 0) && (Top == 0);

        /// <summary>
        /// Have the extents become numerically unstable?
        /// </summary>
        [Category("State"), Description("Are any of the values in the extents NaN (Not a Number)")]
        public bool IsNan => (double.IsNaN(Top) || double.IsNaN(Bottom) || double.IsNaN(Left) || double.IsNaN(Right));

        ///// <summary> Minimum point </summary>
        //[Category("Info")]
        //public Coordinate Min => BottomLeft;

        ///// <summary> Maximum point </summary>
        //[Category("Info")]
        //public Coordinate Max => TopRight;

        ///// <summary>The top left corner </summary>
        //[Category("Info")]
        //public Coordinate TopLeft => new Coordinate(Left, Top, srs: SRS);

        ///// <summary> The top right corner </summary>
        //[Category("Info")]
        //public Coordinate TopRight => new Coordinate(Right, Top, srs: SRS);

        ///// <summary>The bottom right corner </summary>
        //[Category("Info")]
        //public Coordinate BottomRight => new Coordinate(Right, Bottom, srs: SRS);

        ///// <summary>The bottom left corner </summary>
        //[Category("Info")]
        //public Coordinate BottomLeft => new Coordinate(Left, Bottom, srs: SRS);

        #endregion

        #region static methods

        #region convert from other formats

        /// <summary>
        /// Convert from a "Bounds" format like the EPSG uses for Spatial Reference System bounds
        /// </summary>
        /// <param name="right"></param>
        /// <param name="bottom"></param>
        /// <param name="top"></param>
        /// <param name="left"></param>
        /// <param name="srs"></param>
        /// <returns></returns>
        public static Extents FromBounds(double left, double bottom, double right, double top, string srs = "") => new Extents() { Top = top, Left = left, Bottom = bottom, Right = right, SRS = srs };

        /// <summary>
        /// Creates a Extents structure with the specified edge locations.
        /// </summary>
        /// <param name="left">The x-coordinate of the upper-left corner of this Extents</param>
        /// <param name="top">The y-coordinate of the upper-left corner of this Extents</param>
        /// <param name="right">The x-coordinate of the lower-right corner of this Extents</param>
        /// <param name="bottom">The y-coordinate of the lower-right corner of this Extents</param>
        /// <param name="srs"></param>
        /// <returns>The new Extents that this method creates.</returns>
        public static Extents FromLTRB(float left, float top, float right, float bottom, string srs = "") => new Extents() { Top = top, Left = left, Bottom = bottom, Right = right, SRS = srs };

        /// <summary>
        /// Creates a Extents structure with the specified edge locations.
        /// MinX, MaxY, MaxX, MinY
        /// </summary>
        /// <param name="left">MinX The x-coordinate of the upper-left corner of this Extents</param>
        /// <param name="top">MaxY The y-coordinate of the upper-left corner of this Extents</param>
        /// <param name="right">MinThe x-coordinate of the lower-right corner of this Extents</param>
        /// <param name="bottom">The y-coordinate of the lower-right corner of this Extents</param>
        /// <param name="srs"></param>
        /// <returns>The new Extents that this method creates.</returns>
        public static Extents FromLTRB(double left, double top, double right, double bottom, string srs = "") => new Extents() { Top = top, Left = left, Bottom = bottom, Right = right, SRS = srs };

        /// <summary>
        /// Create an Extents structure from minimum and maximum values
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="srs"></param>
        /// <returns></returns>
        public static Extents FromMinMax(float minX, float maxX, float minY, float maxY, string srs = "") => FromLTRB(minX, maxY, maxX, minY, srs);

        /// <summary>
        /// Create an Extents structure from minimum and maximum values
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="srs"></param>
        /// <returns></returns>
        public static Extents FromMinMax(double minX, double maxX, double minY, double maxY, string srs = "") => FromLTRB(minX, maxY, maxX, minY, srs);

        ///// <summary>
        ///// Generate an extents from a collection of points
        ///// </summary>
        ///// <param name="points"></param>
        ///// <returns></returns>
        //public static Extents FromPoints(IEnumerable<IPointF> points)
        //{
        //    if (points == null) return Extents.Empty;
        //    Extents extents = new Extents();
        //    foreach (IPointF point in points)
        //        extents.Union(point);
        //    return extents;
        //}

        ///// <summary>
        ///// Generate an extents from a collection of points
        ///// </summary>
        ///// <param name="points"></param>
        ///// <returns></returns>
        //public static Extents FromPoints(IEnumerable<IPointD> points)
        //{
        //    if (points == null || points.Count() == 0) return Extents.Empty;
        //    Extents extents = new Extents();
        //    foreach (IPointD point in points)
        //        extents.Union(point);
        //    return extents;
        //}

        ///// <summary>
        ///// Generate an extents from a collection of points
        ///// </summary>
        ///// <param name="points"></param>
        ///// <returns></returns>
        //public static Extents FromPoints(IEnumerable<Coordinate> points)
        //{
        //    if (points == null) return Extents.Empty;
        //    Extents extents = new Extents();
        //    foreach (Coordinate point in points)
        //        extents.Union(point);
        //    return extents;
        //}

        /// <summary>
        /// Create the extents from XML
        /// </summary>
        /// <param name="xmlString"></param>
        /// <returns></returns>
        //public static Extents FromXml(string xmlString) => xmlString.IsNullOrEmpty() ? new Extents() : Serializer.Deserialize<Extents>(xmlString);

        #endregion

        #region modify an extents

        ///// <summary>
        ///// Converts the specified ExtentsF structure to a Extents
        /////     structure by rounding the ExtentsF values to the next higher
        /////     integer values.
        ///// </summary>
        ///// <param name="extents">The ExtentsF structure to be converted.</param>
        ///// <returns>Converts the specified ExtentsF structure to a Extents
        /////     structure by rounding the ExtentsF values to the next higher
        /////     integer values.
        /////     </returns>
        //public static Extents Ceiling(Extents extents) => extents.IsNullOrEmpty()
        //        ? null : Extents.FromLTRB(
        //            (float)Math.Ceiling(extents.Left),
        //            (float)Math.Ceiling(extents.Top),
        //            (float)Math.Ceiling(extents.Right),
        //            (float)Math.Ceiling(extents.Bottom),
        //            extents.SRS);

        /// <summary>
        /// Creates and returns an inflated copy of the specified Extents
        /// structure. The copy is inflated by the specified amount. The original Extents
        /// structure remains unmodified.
        /// </summary>
        /// <param name="extents">The extents with which to start. This rectangle is not modified.</param>
        /// <param name="x">The amount to inflate this Extents horizontally.</param>
        /// <param name="y">The amount to inflate this Extents vertically.</param>
        /// <returns>The inflated Extents.</returns>
        public static Extents Inflate(Extents extents, float x, float y) => extents is null
                ? null : Extents.FromLTRB(
                    extents.Left - x, extents.Top + y, extents.Right + x, extents.Bottom - y, extents.SRS);

        /// <summary>
        ///  Returns a third Extents structure that represents the intersection
        ///     of two other Extents structures. If there is no intersection,
        ///     an empty Extents is returned.
        /// </summary>
        /// <param name="a">A Extents to intersect.</param>
        /// <param name="b">A Extents to intersect.</param>
        /// <returns>A Extents that represents the intersection of a and b.</returns>
        public static Extents Intersect(Extents a, Extents b)
        {
            if (a is null | b is null) return null;
            if (a.SRS != b.SRS) return null;

            if (a.Equals(Extents.Empty)) return b;
            if (b.Equals(Extents.Empty)) return a;

            double left = Math.Max(a.Left, b.Left);
            double right = Math.Min(a.Right, b.Right);
            double top = Math.Min(a.Top, b.Top);
            double bottom = Math.Max(a.Bottom, b.Bottom);

            if (right >= left && top >= bottom)
                return Extents.FromLTRB(left, top, right, bottom, a.SRS);

            return new Extents();
        }

        ///// <summary>
        ///// Converts the specified ExtentsF to a Extents
        /////     by rounding the ExtentsF values to the nearest integer values.
        ///// </summary>
        ///// <param name="extents">The ExtentsF to be converted.</param>
        ///// <returns>A Extents.</returns>
        //public static Extents Round(Extents extents) => extents.IsNullOrEmpty()
        //        ? null : Extents.FromLTRB(
        //            (float)Math.Round(extents.Left),
        //            (float)Math.Round(extents.Top),
        //            (float)Math.Round(extents.Right),
        //            (float)Math.Round(extents.Bottom),
        //            extents.SRS);

        ///// <summary>
        ///// Converts the specified ExtentsF to a Extents
        /////     by truncating the ExtentsF values.
        ///// </summary>
        ///// <param name="extents">The ExtentsF to be converted.</param>
        ///// <returns> A Extents.</returns>
        //public static Extents Truncate(Extents extents) => extents.IsNullOrEmpty()
        //        ? null : Extents.FromLTRB(
        //            (float)Math.Truncate(extents.Left),
        //            (float)Math.Truncate(extents.Top),
        //            (float)Math.Truncate(extents.Right),
        //            (float)Math.Truncate(extents.Bottom),
        //            extents.SRS);

        ///// <summary>
        /////    Gets a Extents structure that contains the union of two
        /////     Extents structures.
        ///// </summary>
        ///// <param name="a">A rectangle to union.</param>
        ///// <param name="b">A rectangle to union.</param>
        ///// <returns>A Extents structure that bounds the union of the two Extents
        /////     structures.</returns>
        //public static Extents Union(Extents a, Extents b)
        //{
        //    if (a.IsNullOrEmpty()) return b;
        //    if (b.IsNullOrEmpty()) return a;
        //    if (a.SRS != b.SRS) return null;

        //    double left = Math.Min(a.Left, b.Left);
        //    double right = Math.Max(a.Right, b.Right);
        //    double top = Math.Max(a.Top, b.Top);
        //    double bottom = Math.Min(a.Bottom, b.Bottom);

        //    return Extents.FromLTRB(left, top, right, bottom, a.SRS);
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="extents"></param>
        ///// <param name="point"></param>
        ///// <returns></returns>
        //public static Extents Union(Extents extents, Coordinate point)
        //{
        //    if (extents == null && point == null) return null;
        //    if (extents.SRS != point.SRS) return null;
        //    if (extents.IsNullOrEmpty())
        //        return new Extents(point, point) { SRS = point.SRS };
        //    if (point is null || point.Equals(Coordinate.Empty))
        //        return extents;

        //    double left = Math.Min(extents.Left, point.X);
        //    double right = Math.Max(extents.Right, point.X);
        //    double top = Math.Max(extents.Top, point.Y);
        //    double bottom = Math.Min(extents.Bottom, point.Y);

        //    return Extents.FromLTRB(left, top, right, bottom, extents.SRS);
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extents"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Extents Union(Extents extents, PointF point)
        {
            if (extents is null || extents.Equals(Extents.Empty))
                return new Extents(point, point);
            if (point.Equals(PointF.Empty))
                return extents;

            double left = Math.Min(extents.Left, point.X);
            double right = Math.Max(extents.Right, point.X);
            double top = Math.Max(extents.Top, point.Y);
            double bottom = Math.Min(extents.Bottom, point.Y);

            return Extents.FromLTRB(left, top, right, bottom, extents.SRS);
        }

        #endregion

        #endregion

        #region methods

        ///// <summary>
        ///// Determines if the specified point is contained within this Extents
        /////     structure.
        ///// </summary>
        ///// <param name="pt">The System.Drawing.Point to test</param>
        ///// <returns>true if the point represented by pt is contained within this Rectangle structure; otherwise false.</returns>
        //public bool Contains(IPointD pt) => (pt is ISpatialRef) && (pt as ISpatialRef).SRS != SRS ? false : Contains(pt.X, pt.Y);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public bool Contains(PointF pt) => Contains(pt.X, pt.Y);

        /// <summary>
        /// Determines if the rectangular region represented by rect is entirely contained
        /// within this Extents structure
        /// </summary>
        /// <param name="extents">The Extents to test.</param>
        /// <returns>This method returns true if the rectangular region represented by rect is
        ///     entirely contained within this Extents structure; otherwise
        ///     false.</returns>
        public bool Contains(Extents extents) => extents.SRS == this.SRS && extents.Left >= Left &&
            extents.Right <= Right &&
            extents.Bottom >= Bottom &&
            extents.Top <= Top;

        /// <summary>
        /// Determines if the specified point is contained within this Extents structure
        /// </summary>
        /// <param name="x">The x-coordinate of the point to test.</param>
        /// <param name="y">The y-coordinate of the point to test.</param>
        /// <returns>This method returns true if the point defined by x and y is contained within
        ///     this Extents structure; otherwise false.
        ///     </returns>
        public bool Contains(double x, double y) => y >= Bottom && y <= Top && x >= Left && x <= Right;

        /// <summary>
        /// Determines if the specified point is contained within this Extents structure
        /// </summary>
        /// <param name="x">The x-coordinate of the point to test.</param>
        /// <param name="y">The y-coordinate of the point to test.</param>
        /// <returns>This method returns true if the point defined by x and y is contained within
        ///     this Extents structure; otherwise false.
        ///     </returns>
        public bool Contains(float x, float y) => Contains((double)x, (double)y);

        /// <summary>
        /// Clear the values of the extents
        /// This makes it equal to Empty
        /// </summary>
        public void Clear()
        {
            Top = Empty.Top;
            Left = Empty.Left;
            Bottom = Empty.Bottom;
            Right = Empty.Right;
        }

        /// <summary>
        /// Inflates this Extents by the specified amount.
        /// </summary>
        /// <param name="size">The amount to inflate this rectangle</param>
        /// <returns>this object, inflated by the given amount</returns>
        public Extents Inflate(SizeF size) => Inflate(size.Width, size.Height);

        /// <summary>
        /// Inflates this Extents by the specified amount
        /// returns this object
        /// </summary>
        /// <param name="w">The amount to inflate this Extents horizontally</param>
        /// <param name="h">The amount to inflate this Extents vertically.</param>
        /// <returns>this object, inflated by the given amount</returns>
        public Extents Inflate(double w, double h) { Top += h; Bottom -= h; Left -= w; Right += w; return this; }

        /// <summary>
        /// Replaces this Extents with the intersection of itself and
        ///     the specified Extents.
        /// </summary>
        /// <param name="extents">The Extents with which to intersect.</param>
        /// <returns>this object, intersected with the given object</returns>
        public Extents Intersect(Extents extents)
        {
            Extents ex = Extents.Intersect(extents, this);
            if (ex == null) return null;

            Left = ex.Left;
            Right = ex.Right;
            Top = ex.Top;
            Bottom = ex.Bottom;
            return this;
        }

        /// <summary>
        /// Determines if this rectangle intersects with rect.
        /// </summary>
        /// <param name="extents">The rectangle to test.</param>
        /// <returns>This method returns true if there is any intersection, otherwise false</returns>
        public bool IntersectsWith(Extents extents)
        {
            if (extents is null) return false;
            if (extents.SRS != this.SRS) return false;
            return ((extents.Left < Right) &&
                    (Left < extents.Right) &&
                    (extents.Bottom < Top) &&
                    (Bottom < extents.Top));
        }

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amount.
        /// </summary>
        /// <param name="pos">Amount to offset the location.</param>
        /// <returns>this object, offset by the given amount</returns>
        public Extents Offset(PointF pos) => Offset(pos.X, pos.Y);

        ///// <summary>
        ///// Adjusts the location of this rectangle by the specified amount.
        ///// </summary>
        ///// <param name="pos">Amount to offset the location.</param>
        ///// <returns>this object, offset by the given amount</returns>
        //public Extents Offset(IPointD pos) => Offset(pos.X, pos.Y);

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amount.
        /// </summary>
        /// <param name="x">The horizontal offset.</param>
        /// <param name="y">The vertical offset.</param>
        /// <returns>this object, offset by the given amount</returns>
        public Extents Offset(double x, double y)
        {
            Top += y;
            Bottom += y;
            Left += x;
            Right += x;
            return this;
        }

        /// <summary>
        /// Adjusts the location of this rectangle by the specified amount.
        /// </summary>
        /// <param name="x">The horizontal offset.</param>
        /// <param name="y">The vertical offset.</param>
        /// <returns>this object, offset by the given amount</returns>
        public Extents Offset(float x, float y) => Offset(x, (double)y);

        ///// <summary>
        ///// Save the extents as XML
        ///// </summary>
        ///// <returns></returns>
        //public virtual string ToXml()
        //{
        //    XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
        //    ns.Add("tng", "http://BlueToque.ca/TrueNorth/Geographic");
        //    ns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
        //    return Serializer.Serialize<Extents>(this);
        //}

        ///// <summary>
        ///// Convert the extents to a list of points, clockwise from top left
        ///// </summary>
        ///// <returns></returns>
        //public List<PointDType> ToPoints() => new List<PointDType>() { TopLeft, TopRight, BottomRight, BottomLeft };

        ///// <summary>
        ///// Convert the extents to a list of points, clockwise from top left
        ///// </summary>
        ///// <returns></returns>
        //public List<Coordinate> ToCoordinates() => new List<Coordinate>() { TopLeft, TopRight, BottomRight, BottomLeft };

        ///// <summary>
        ///// Convert the extents to a list of points, clockwise from top left
        ///// </summary>
        ///// <returns></returns>
        //public List<PointFType> ToPointsF() => new List<PointFType>() { TopLeft.ToPointFType(), TopRight.ToPointFType(), BottomRight.ToPointFType(), BottomLeft.ToPointFType() };

        ///// <summary>
        ///// A very dangerous method that modified this extents by performing a union with the given extents.
        ///// </summary>
        ///// <param name="ex"></param>
        //public void Union(Extents ex)
        //{
        //    if (ex.IsNullOrEmpty())
        //    {
        //        return;
        //    }
        //    else if (this.IsNullOrEmpty())
        //    {
        //        Left = ex.Left;
        //        Right = ex.Right;
        //        Top = ex.Top;
        //        Bottom = ex.Bottom;
        //        SRS = ex.SRS;
        //    }
        //    else if (ex.SRS != SRS)
        //    {
        //        return;
        //    }
        //    else
        //    {
        //        Left = Math.Min(Left, ex.Left);
        //        Right = Math.Max(Right, ex.Right);
        //        Top = Math.Max(Top, ex.Top);
        //        Bottom = Math.Min(Bottom, ex.Bottom);
        //    }
        //}

        ///// <summary>
        ///// A very dangerous method that modified this extents by performing a union with the given extents.
        ///// </summary>
        ///// <param name="coordinate"></param>
        //public void Union(Coordinate coordinate)
        //{
        //    if (object.ReferenceEquals(this, Extents.Empty))
        //        return;
        //    if (coordinate.SRS != this.SRS) return;

        //    if (this.Equals(Extents.Empty))
        //    {
        //        Left = coordinate.X;
        //        Right = coordinate.X;
        //        Top = coordinate.Y;
        //        Bottom = coordinate.Y;
        //        return;
        //    }

        //    if (coordinate.IsNullOrEmpty())
        //        return;

        //    Left = Math.Min(Left, coordinate.X);
        //    Right = Math.Max(Right, coordinate.X);
        //    Top = Math.Max(Top, coordinate.Y);
        //    Bottom = Math.Min(Bottom, coordinate.Y);
        //}

        ///// <summary>
        ///// A very dangerous method that modified this extents by performing a union with the given extents.
        ///// </summary>
        ///// <param name="coordinate"></param>
        //public void Union(IPointD coordinate)
        //{
        //    if (object.ReferenceEquals(this, Extents.Empty)) return;
        //    if (this.Equals(Extents.Empty))
        //    {
        //        Left = coordinate.X;
        //        Right = coordinate.X;
        //        Top = coordinate.Y;
        //        Bottom = coordinate.Y;
        //        SRS = (coordinate as ISpatialRef)?.SRS;
        //        return;
        //    }

        //    if ((coordinate is ISpatialRef) && ((coordinate as ISpatialRef).SRS != this.SRS))
        //        return;

        //    if (coordinate.IsNullOrEmpty())
        //        return;

        //    Left = Math.Min(Left, coordinate.X);
        //    Right = Math.Max(Right, coordinate.X);
        //    Top = Math.Max(Top, coordinate.Y);
        //    Bottom = Math.Min(Bottom, coordinate.Y);
        //}

        ///// <summary>
        ///// A very dangerous method that modified this extents by performing a union with the given extents.
        ///// </summary>
        ///// <param name="coordinate"></param>
        //public void Union(IPointF coordinate)
        //{
        //    if (object.ReferenceEquals(this, Extents.Empty)) return;
        //    if ((coordinate is ISpatialRef) && ((coordinate as ISpatialRef)?.SRS != this.SRS))
        //        return;

        //    if (this.Equals(Extents.Empty))
        //    {
        //        Left = coordinate.X;
        //        Right = coordinate.X;
        //        Top = coordinate.Y;
        //        Bottom = coordinate.Y;
        //        SRS = (coordinate as ISpatialRef)?.SRS;
        //        return;
        //    }

        //    if (coordinate.IsNullOrEmpty())
        //        return;

        //    Left = Math.Min(Left, coordinate.X);
        //    Right = Math.Max(Right, coordinate.X);
        //    Top = Math.Max(Top, coordinate.Y);
        //    Bottom = Math.Min(Bottom, coordinate.Y);
        //}

        ///// <summary>
        ///// Fix the extents to be within the bounds of the world.
        ///// </summary>
        ///// <returns></returns>
        //public Extents FixGeo() => Fix(EPSG_4326);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public Extents Fix(Extents ex)
        {
            if (Top > ex.Top) Top = ex.Top;
            if (Bottom < ex.Bottom) Bottom = ex.Bottom;
            if (Left < ex.Left) Left = ex.Left;
            if (Right > ex.Right) Right = ex.Right;
            return this;
        }

        #endregion

        #region object

        /// <summary>
        ///      Converts the attributes of this Extents to a human-readable string
        /// </summary>
        /// <returns>     A string that contains the position, width, and height of this Extents
        ///     structure ¾ for example, {X=20, Y=20, Width=100, Height=50}</returns>
        public override string ToString() => string.Format("X={0}, Y={1}, Width={2}, Height={3}", X, Y, Height, Width);

        /// <summary>
        /// Checks whether the values of this instance is equal to the values of another instance.
        /// </summary>
        /// <param name="other"><see cref="Extents"/> to compare to.</param>
        /// <returns>True if equal</returns>
        public bool Equals(Extents other) => !(other is null) && (
                other.SRS == SRS &&
                other.Top == Top &&
                other.Left == Left &&
                other.Bottom == Bottom &&
                other.Right == Right);

        /// <summary>
        /// Tests whether obj is a Extents structure with the same location
        ///     and size of this Extents structure.
        /// </summary>
        /// <param name="obj">The System.Object to test</param>
        /// <returns>This method returns true if obj is a Extents structure and
        ///     its Extents.X, Extents.Y, Extents.Width,
        ///     and Extents.Height properties are equal to the corresponding
        ///     properties of this Extents structure; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals(obj as Extents);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool Equals(Extents left, Extents right)
        {
            if (((object)left) == ((object)right))
                return true;

            if ((((object)left) == null) || (((object)right) == null))
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Returns the hash code for this Extents structure. For information
        ///    about the use of hash codes, see <see cref="System.Object.GetHashCode()"/> .
        /// </summary>
        /// <returns> An integer that represents the hash code for this rectangle.</returns>
        public override int GetHashCode() => (int)((UInt32)X ^
                (((UInt32)Y << 13) | ((UInt32)Y >> 19)) ^
                (((UInt32)Width << 26) | ((UInt32)Width >> 6)) ^
                (((UInt32)Height << 7) | ((UInt32)Height >> 25)));

        #endregion

        #region operators

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(Extents left, Extents right) => Equals(left, right);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(Extents left, Extents right) => !(left == right);

        #endregion

        #region private methods

        /// <summary>
        /// Set the centre of the extents
        /// </summary>
        /// <param name="center"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="srs"></param>
        void SetCentre(PointDType center, double width, double height)//, string srs = "")
        {
            if (center is null) return;

            Top = center.Y + (height / 2);
            Left = center.X - (width / 2);
            Bottom = Top - height;
            Right = Left + width;

            //SRS = (center is ISpatialRef) ? (center as ISpatialRef)?.SRS : srs;
        }

        #endregion

        #region ICloneable Members

        /// <summary>
        /// Clone an extents
        /// </summary>
        /// <returns></returns>
        public Extents Clone() => new Extents(this);

        object ICloneable.Clone() => Clone();

        #endregion

        /// <summary> Minimum point </summary>
        [Category("Info")]
        public PointDType Min => BottomLeft;

        /// <summary> Maximum point </summary>
        [Category("Info")]
        public PointDType Max => TopRight;

        /// <summary>The top left corner </summary>
        [Category("Info")]
        public PointDType TopLeft => new PointDType(Left, Top);

        /// <summary> The top right corner </summary>
        [Category("Info")]
        public PointDType TopRight => new PointDType(Right, Top);

        /// <summary>The bottom right corner </summary>
        [Category("Info")]
        public PointDType BottomRight => new PointDType(Right, Bottom);

        /// <summary>The bottom left corner </summary>
        [Category("Info")]
        public PointDType BottomLeft => new PointDType(Left, Bottom);
    }

}

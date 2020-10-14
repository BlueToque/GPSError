using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace GPSError.GDAL
{
    /// <summary>
    /// 
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [System.Diagnostics.DebuggerDisplay("({X}, {Y}, {Z})")]
    public partial class PointDType :
        ICloneable,
        //IPointD,
        //IPoint<double>,
        IEquatable<PointDType>,
        IComparable,
        IComparable<PointDType>
    {
        #region constructors

        /// <summary> </summary>
        public PointDType() : this(0, 0, 0) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public PointDType(double x, double y, double z = 0) { X = x; Y = y; Z = z; }

        public double X;
        public double Y;
        public double Z;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        public PointDType(PointDType point) { X = point.X; Y = point.Y; Z = point.Z; }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="point"></param>
        //public PointDType(IPointD point) : this(point.X, point.Y, point.Z) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        public PointDType(PointF point) { X = point.X; Y = point.Y; }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="point"></param>
        //public PointDType(IPointF point) { X = point.X; Y = point.Y; Z = point.Z; }

        /// <summary>
        /// Create a point from an array of double of length three
        /// </summary>
        /// <param name="points"></param>
        public PointDType(double[] points)
        {
            if (points.Length == 2)
            {
                X = points[0];
                Y = points[1];
                Z = 0;
            }
            else if (points.Length == 3)
            {
                X = points[0];
                Y = points[1];
                Z = points[2];
            }
            else
            {
                throw new ArgumentException("point array must be of length 2 or 3");
            }
        }

        #endregion

        /// <summary>
        /// return true if this is an empty point
        /// </summary>
        public bool IsEmpty => this == Empty;

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public PointType Round() => new PointType((int)Math.Round(X), (int)Math.Round(Y));

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="point"></param>
        ///// <returns></returns>
        //public static PointDType FromPointF(PointF point) => new PointDType(point);

        /// <summary>
        /// return the point structire in array format
        /// </summary>
        [XmlIgnore]
        public double[] Array
        {
            get => ToArray();
            set
            {
                if (value.Length == 2)
                {
                    X = value[0];
                    Y = value[1];
                }
                else if (value.Length == 3)
                {
                    X = value[0];
                    Y = value[1];
                    Z = value[2];
                }
                else
                {
                    throw new ArgumentException("point array must be of length 2 or 3");
                }
            }
        }

        private static readonly PointDType m_empty = new PointDType(PointF.Empty);

        /// <summary> </summary>
        public static PointDType Empty => m_empty;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool Equals(PointDType left, PointDType right)
        {
            if (((object)left) == ((object)right))
                return true;

            if ((((object)left) == null) || (((object)right) == null))
                return false;

            return
                Math.Abs(left.X - right.X) <= double.Epsilon &&
                Math.Abs(left.Y - right.Y) <= double.Epsilon &&
                Math.Abs(left.Z - right.Z) <= double.Epsilon;
        }

        #region operators

        /// <summary>
        /// Returns part of coordinate. Index 0 = X, Index 1 = Y
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual double this[uint index]
        {
            get
            {
                if (index == 0)
                    return X;
                else if (index == 1)
                    return Y;
                else if (index == 2)
                    return Z;
                else
                    throw (new Exception("Point index out of bounds"));
            }
            set
            {
                if (index == 0)
                    X = value;
                else if (index == 1)
                    Y = value;
                else if (index == 2)
                    Z = value;
                else
                    throw (new Exception("Point index out of bounds"));
            }
        }

        ///// <summary>
        ///// implicit operator converting from Point to PointType
        ///// </summary>
        ///// <param name="point"></param>
        ///// <returns></returns>
        //public static implicit operator PointDType(PointF point)
        //{
        //    if (point == null) throw new ArgumentNullException("point");
        //    return new PointDType(point);
        //}

        ///// <summary>
        ///// implicit operator converting from PointFType to PointDType
        ///// </summary>
        ///// <param name="point"></param>
        ///// <returns></returns>
        //public static implicit operator PointDType(PointFType point)
        //{
        //    if (point == null) throw new ArgumentNullException("point");
        //    return new PointDType()
        //    {
        //        X = point.X,
        //        Y = point.Y,
        //        Z = point.Z
        //    };
        //}

        /// <summary>
        /// implicit operator converting from PointType to Point
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static explicit operator PointF(PointDType point)
        {
            if (point == null) throw new ArgumentNullException("point");
            return new PointF((float)point.X, (float)point.Y);
        }

        ///// <summary>
        ///// implicit operator converting from PointType to Point
        ///// </summary>
        ///// <param name="point"></param>
        ///// <returns></returns>
        //public static explicit operator PointFType(PointDType point)
        //{
        //    if (point == null) throw new ArgumentNullException("point");
        //    return new PointFType((float)point.X, (float)point.Y);
        //}

        /// <summary>
        /// Vector + Vector
        /// </summary>
        /// <param name="left">Vector</param>
        /// <param name="right">Vector</param>
        /// <returns></returns>
        public static PointDType operator +(PointDType left, PointDType right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return new PointDType(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        /// <summary>
        /// Vector - Vector
        /// </summary>
        /// <param name="left">Vector</param>
        /// <param name="right">Vector</param>
        /// <returns>Cross product</returns>
        public static PointDType operator -(PointDType left, PointDType right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return new PointDType(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        /// <summary>
        /// Vector * Scalar
        /// </summary>
        /// <param name="value">Vector</param>
        /// <param name="d">Scalar (double)</param>
        /// <returns></returns>
        public static PointDType operator *(PointDType value, double d)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return new PointDType(value.X * d, value.Y * d, value.Z * d);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="d"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PointDType operator *(double d, PointDType value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return new PointDType(value.X * d, value.Y * d, value.Z * d);
        }

        /// <summary>
        /// Vector / Scalar
        /// </summary>
        /// <param name="value">Vector</param>
        /// <param name="d">Scalar (double)</param>
        /// <returns></returns>
        public static PointDType operator /(PointDType value, double d)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return new PointDType(value.X / d, value.Y / d, value.Z / d);
        }

        /// <summary>
        /// Unary minus
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PointDType operator -(PointDType value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            return new PointDType(-value.X, -value.Y, -value.Z);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(PointDType left, PointDType right) => Equals(left, right);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(PointDType left, PointDType right) => !Equals(left, right);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static bool operator <(PointDType v1, PointDType v2) => v1.SumComponentSqrs() < v2.SumComponentSqrs();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static bool operator >(PointDType v1, PointDType v2) => v1.SumComponentSqrs() > v2.SumComponentSqrs();

        #endregion

        #region component operations

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <returns></returns>
        public static PointDType SqrComponents(PointDType v1) => new PointDType(v1.X * v1.X, v1.Y * v1.Y, v1.Z * v1.Z);

        /// <summary>
        /// 
        /// </summary>
        public void SqrComponents() => Value = SqrtComponents(this);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <returns></returns>
        public static PointDType SqrtComponents(PointDType v1) => new PointDType(Math.Sqrt(v1.X), Math.Sqrt(v1.Y), Math.Sqrt(v1.Z));

        /// <summary>
        /// 
        /// </summary>
        public void SqrtComponents() => Value = SqrtComponents(this);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <returns></returns>
        public static double SumComponentSqrs(PointDType v1) => SqrComponents(v1).SumComponents();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public double SumComponentSqrs() => SumComponentSqrs(this);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v1"></param>
        /// <returns></returns>
        public static double SumComponents(PointDType v1) => (v1.X + v1.Y + v1.Z);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public double SumComponents() => SumComponents(this);

        #endregion

        /// <summary> </summary>
        [XmlIgnore]
        internal PointDType Value { get => new PointDType(Value); set { X = value.X; Y = value.Y; Z = value.Z; } }

        /// <summary>
        /// Return the point structure in array format
        /// </summary>
        /// <returns></returns>
        public double[] ToArray() => new double[3] { this.X, this.Y, this.Z };

        #region ICloneable Members

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public PointDType Clone() => new PointDType(X, Y, Z);

        object ICloneable.Clone() => new PointDType(X, Y, Z);

        #endregion

        #region IEquatable<PointDType> Members

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(PointDType other) => Equals(this, other);

        #endregion

        #region overrides

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals(obj as PointDType);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => (int)((X + Y + Z) % Int32.MaxValue);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format("{0}, {1}, {2}", X, Y, Z);

        #endregion

        #region IComparable

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PointDType other)
        {
            if (this < other)
                return -1;
            else if (this > other)
                return 1;
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(object other)
        {
            if (!(other is PointDType))
                throw new ArgumentException("not a point");
            return CompareTo((PointDType)other);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        public void FromArray(double[] array) => Array = array;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Offset(double x, double y, double z = 0) { X += x; Y += y; Z += z; }

        #endregion
    }
}

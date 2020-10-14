using GPSError.GDAL;
using System.Collections.Generic;
using System.Linq;

namespace GPSError
{
    public abstract class ProjectionBase //: IProjection
    {
        #region IProjection Members

        public virtual string Name => string.Empty;

        public virtual string Datum => string.Empty;

        public virtual bool IsProjected { get; protected set; }

        public virtual bool IsGeographic { get; protected set; }

        public virtual bool IsLocal { get; protected set; }

        /// <summary> The identifier for this projection </summary>
        public abstract string Code { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public abstract PointDType Project(double x, double y);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordinate"></param>
        /// <returns></returns>
        public virtual PointDType Project(PointDType coordinate)
        {
            return Project(coordinate.X, coordinate.Y);
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="extents"></param>
        ///// <returns></returns>
        //public virtual Extents Project(Extents extents)
        //{
        //    if (extents == null)
        //        return new Extents();

        //    return new Extents(
        //        Project(extents.TopLeft),
        //        Project(extents.BottomRight));
        //}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public abstract PointDType Unproject(double x, double y);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordinate"></param>
        /// <returns></returns>
        public virtual PointDType Unproject(PointDType coordinate)
        {
            return Unproject(coordinate.X, coordinate.Y);
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="extents"></param>
        ///// <returns></returns>
        //public virtual Extents Unproject(Extents extents)
        //{
        //    return new Extents(
        //        Unproject(extents.TopLeft),
        //        Unproject(extents.BottomRight));
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public virtual IEnumerable<PointDType> Project(IEnumerable<PointDType> coordinates)
        {
            List<PointDType> list = coordinates.ToList<PointDType>();

            // jump out here if there is just one
            return (list.Count == 1) ?
                new List<PointDType>().AddItem(Project(list[0])) :
                list.Select<PointDType, PointDType>(x => Project(x));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public virtual IEnumerable<PointDType> Unproject(IEnumerable<PointDType> coordinates)
        {
            List<PointDType> list = coordinates.ToList<PointDType>();

            // jump out here if there is just one
            return (list.Count == 1) ?
                new List<PointDType>().AddItem(Unproject(list[0])) :
                list.Select<PointDType, PointDType>(x => this.Unproject(x));
        }

        #endregion
    }

}

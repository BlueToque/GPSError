using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GPSError.GDAL;
using SpatialReference = OSGeo.OSR.SpatialReference;
using CoordinateTransformation = OSGeo.OSR.CoordinateTransformation;

namespace GPSError
{
    public class SphericalMercatorProjection// : ProjectionBase
    {
        public SphericalMercatorProjection() => Initialize("EPSG:3785");

        #region fields

        SpatialReference m_sr;

        string m_code;

        CoordinateTransformation m_transform;

        CoordinateTransformation m_inverseTransform;

        #endregion

        /// <summary>
        /// Parameter can be EPSG code or WKT
        /// </summary>
        /// <param name="parameter"></param>
        private void Initialize(string parameter)
        {
            m_code = parameter;

            m_sr = parameter.ToUpper().StartsWith("EPSG") ? GDALHelper.SRSFromEPSG(parameter) : GDALHelper.SRSFromUserInput(parameter);

            // initialize this to the geographic coordinate system
            SpatialReference geog_sr = GDALHelper.SRSFromEPSG("EPSG:4326"); //new SpatialReference(null);

            try
            {
                m_transform = new CoordinateTransformation(geog_sr, m_sr);
                m_inverseTransform = new CoordinateTransformation(m_sr, geog_sr);
            }
            catch (Exception ex)
            {
                Trace.TraceError("error creating projection:\r\n{0}", ex);
            }
        }

        #region IProjection Members

        public string Name => m_sr.GetGeogCS();

        public string Datum => m_sr.GetDatumName();

        public bool IsGeographic => m_sr.IsGeographic() == 1;

        public bool IsProjected => m_sr.IsProjected() == 1;

        public string Code => m_code;

        /// <summary>
        /// Project from geographic to UTM
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public PointDType Project(double x, double y)
        {
            if (x > 180) x = 180.0;
            if (x < -180) x = -180.0;
            if (y > 90) y = 90.0;
            if (y < -90) y = -90.0;
            double[] point = new double[3] { x, y, 0 };
            m_transform.TransformPoint(point);
            return new PointDType(point[0], point[1]);
            //return (IsUTM(m_sr.GetAuthorityCode())) ?
            //    new UTMCoordinate(point[0], point[1], UTMZone.FromLatLon(y, x).ToString()) :
            //    new PointDType(point[0], point[1]);// { IsProjected = true };
        }

        public virtual PointDType Project(PointDType coordinate) => Project(coordinate.X, coordinate.Y);

        /// <summary>
        /// Unproject from UTM to Geographic
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public PointDType Unproject(double x, double y)
        {
            // do the transform
            double[] point = new double[3] { x, y, 0.0 };
            m_inverseTransform.TransformPoint(point);
            return new PointDType(point[1], point[0]);
        }

        public virtual PointDType Unproject(PointDType coordinate) => Unproject(coordinate.X, coordinate.Y);

        public IEnumerable<PointDType> Unproject(IEnumerable<PointDType> coordinates)
        {
            List<PointDType> list = coordinates.ToList<PointDType>();
            if (list.Count == 1)
                return new List<PointDType>().AddItem(Unproject(list[0]));

            double[] x = list.Select(s => s.X).ToArray();
            double[] y = list.Select(s => s.Y).ToArray();
            double[] z = list.Select(s => s.Z).ToArray();

            m_inverseTransform.TransformPoints(x.Length, x, y, z);

            List<PointDType> coords = new List<PointDType>(list.Count);
            for (int i = 0; i < list.Count; i++)
                coords.Add(new PointDType(y[i], x[i], z[i]));// { IsProjected = true });

            return coords;
        }

        public IEnumerable<PointDType> Project(IEnumerable<PointDType> coordinates)
        {
            //List<Coordinate> list = new List<Coordinate>();
            //foreach (var c in coordinates)
            //    list.Add(Project(c));
            //return list;

            int count = coordinates.ToList().Count;

            // jump out here if there is just one
            if (count == 1)
                return new List<PointDType>().AddItem(Project(coordinates.First()));

            double[] x = new double[count];
            double[] y = new double[count];
            double[] z = new double[count];
            {
                int i = 0;
                foreach (var c in coordinates)
                {
                    x[i] = c.X;
                    x[i] = (x[i] >= 180.0) ? 179.9 : x[i];
                    x[i] = (x[i] <= -180.0) ? -179.9 : x[i];
                    y[i] = c.Y;
                    y[i] = (y[i] >= 90.0) ? 89.9 : y[i];
                    y[i] = (y[i] <= -90.0) ? -89.9 : y[i];
                    z[i] = c.Z;
                    i++;
                }
            }

            m_transform.TransformPoints(count, x, y, z);

            PointDType[] coords = new PointDType[count];
            for (int i = 0; i < count; i++)
                coords[i] = new PointDType(x[i], y[i], z[i]);
            return coords;
        }

        #endregion
    }
}

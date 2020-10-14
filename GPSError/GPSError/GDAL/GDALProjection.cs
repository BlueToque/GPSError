using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GPSError.GDAL;
using OSGeo.OGR;
using SpatialReference = OSGeo.OGR.SpatialReference;

namespace GPSError
{
    public class GDALProjection : ProjectionBase
    {
        public GDALProjection() => Initialize("EPSG:3785");

        public GDALProjection(string parameter) => Initialize(parameter);

        #region fields

        SpatialReference m_sr;

        string m_code;

        CoordinateTransformation m_transform;

        CoordinateTransformation m_inverseTransform;

        #endregion

        #region IProjection Members

        public override string Name => m_sr.GetGeogCS();

        public override string Datum => m_sr.GetDatumName();

        public override bool IsGeographic => m_sr.IsGeographic() == 1;

        public override bool IsProjected => m_sr.IsProjected() == 1;

        /// <summary>
        /// Parameter can be EPSG code or WKT
        /// </summary>
        /// <param name="parameter"></param>
        private void Initialize(string parameter)
        {
            //Helper.Initialize();
            m_code = parameter;

            if (parameter.ToUpper().StartsWith("EPSG"))
                m_sr = GDALHelper.SRSFromEPSG(parameter);
            else
                m_sr = GDALHelper.SRSFromUserInput(parameter);
            //if (parameter.ToUpper().StartsWith("EPSG"))
            //    m_sr = new SpatialReference(ProjectionDefinition.Create(parameter).Definition);
            //else
            //    m_sr = OSRHelper.SRSFromEPSG(parameter);

            // initialize this to the geographic coordinate system
            SpatialReference geog_sr = GDALHelper.SRSFromEPSG("EPSG:4326"); //new SpatialReference(null);
            //SpatialReference geog_sr = new SpatialReference("EPSG:4326"); 
            //geog_sr.SetWellKnownGeogCS("EPSG:4326");


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

        public override string Code { get { return m_code; } }

        /// <summary>
        /// Project from geographic to UTM
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public override PointDType Project(double x, double y)
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

        private bool IsUTM(string code)
        {

            return (m_sr.GetProjectionName() == "Transverse_Mercator");
            //int intCode = 0;
            //if (!Int32.TryParse(code, out intCode))
            //    return false;
            //return (
            //    (intCode >= 26900 && intCode <= 26960) ||
            //    (intCode >= 27001 && intCode <= 27060) ||
            //    (intCode >= 32600 && intCode <= 32660) ||
            //    (intCode >= 32701 && intCode <= 32760) ||
            //    (intCode >= 26700 && intCode <= 26760)||
            //    (intCode >= 26801 && intCode <= 26860)
            //    );
        }

        public override IEnumerable<PointDType> Unproject(IEnumerable<PointDType> coordinates)
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

        public override IEnumerable<PointDType> Project(IEnumerable<PointDType> coordinates)
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
            x = null;
            y = null;
            z = null;
            return coords;
        }

        //public override void Project(PointFType[] coordinates)
        //{
        //    int count = coordinates.Length;
        //    // jump out here if there is just one
        //    if (count == 1)
        //    {
        //        coordinates[0] = this.Project(coordinates[0]);
        //        return;
        //    }

        //    double[] x = new double[count];
        //    double[] y = new double[count];
        //    double[] z = new double[count];
        //    {
        //        for (int i = 0; i < count; i++)
        //        {
        //            x[i] = coordinates[i].X;
        //            x[i] = (x[i] >= 180.0) ? 179.9 : x[i];
        //            x[i] = (x[i] <= -180.0) ? -179.9 : x[i];
        //            y[i] = coordinates[i].Y;
        //            y[i] = (y[i] >= 90.0) ? 89.9 : y[i];
        //            y[i] = (y[i] <= -90.0) ? -89.9 : y[i];
        //            z[i] = coordinates[i].Z;
        //        }
        //    }
        //    m_transform.TransformPoints(count, x, y, z);

        //    for (int i = 0; i < count; i++)
        //    {
        //        coordinates[i].X = (float)x[i];
        //        coordinates[i].Y = (float)y[i];
        //        coordinates[i].Z = (float)z[i];
        //    }

        //    x = null;
        //    y = null;
        //    z = null;

        //}

        /// <summary>
        /// Unproject from UTM to Geographic
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public override PointDType Unproject(double x, double y)
        {
            // do the transform
            double[] point = new double[3] { x, y, 0.0 };
            m_inverseTransform.TransformPoint(point);
            return new PointDType(point[1], point[0]);
        }

        #endregion
    }
}

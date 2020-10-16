using GPSError.GDAL;
using GPSError.Models;
using SharpGPX;
using SharpGPX.GPX1_1;
using System;
using System.Diagnostics;
using System.IO;

namespace GPSError
{

    public static class Processor
    {
        static SphericalMercatorProjection m_projection;

        /// <summary>
        /// Process the file name
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static TestData Process(string fileName)
        {
            if (m_projection == null)
                m_projection = new SphericalMercatorProjection();

            TestData error = new TestData();

            var ext = Path.GetExtension(fileName).ToLower();
            if (ext == ".gpx")
                error = ProcessGPX(fileName);
            else if ( ext == ".kml" || ext == ".kmz")
                error = ProcessKML(fileName);

            if (error == null)
                return null;

            // process the data set
            error.Process();

            return error;
        }

        private static TestData ProcessKML(string fileName)
        {
            try
            {
                TestData data = new TestData();
                //TrueNorth.Geographic.CoordinateCollection collection = new KMLReader().Read(file);

                //foreach (var c in collection)
                //{
                //    Coordinate UTM = m_projection.Project(c);
                //    data.Entries.Add(new Entry()
                //    {
                //        Lat = c.Y,
                //        Lon = c.X,
                //        X = UTM.X,
                //        Y = UTM.Y,
                //        Z = c.Z
                //    });
                //}
                return data;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error\r\n{0}", ex);
                return null;
            }
        }

        private static TestData ProcessGPX(string file) => ProcessGPX(GpxClass.FromFile(file));

        //private static TestData ProcessGPX(Stream fileStream) => ProcessGPX(GpxClass.FromStream(fileStream));

        private static TestData ProcessGPX(GpxClass gpx)
        {
            TestData data = new TestData();

            // get all of the points in the tracks and convert them to spherical mercator
            foreach (var track in gpx.Tracks)
            {
                foreach (var seg in track.trkseg)
                {
                    foreach (var pt in seg.trkpt)
                    {
                        PointDType geo = pt.ToCoordinate();
                        PointDType mercator = m_projection.Project(geo);

                        // the start time of the track
                        if (seg.trkpt.IndexOf(pt) == 0 && pt.timeSpecified)
                            data.Start = pt.time;

                        // the end time of the track
                        if (seg.trkpt.IndexOf(pt) == (seg.trkpt.Count - 1) && pt.timeSpecified)
                            data.End = pt.time;

                        data.Entries.Add(new Entry()
                        {
                            Lat = geo.Y,
                            Lon = geo.X,
                            X = mercator.X,
                            Y = mercator.Y,
                            Z = geo.Z
                        });
                    }
                }
            }

            if (data.Start.HasValue && data.End.HasValue)
                data.Duration = (float)(data.End.Value - data.Start.Value).TotalMinutes;

            return data;
        }

        /// <summary>
        /// Convert a waypoint to a double precision point
        /// </summary>
        /// <param name="wpt"></param>
        /// <returns></returns>
        public static PointDType ToCoordinate(this wptType wpt)
        {
            PointDType c = new PointDType(
                Convert.ToDouble(wpt.lon),
                Convert.ToDouble(wpt.lat));

            if (wpt.eleSpecified)
                c.Z = Convert.ToDouble(wpt.ele);
            return c;
        }

    }
}
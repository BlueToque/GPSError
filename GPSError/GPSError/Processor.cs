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
        static GDALProjection m_projection;

        internal static TestData Process(string fileName)
        {
            if (m_projection == null)
                m_projection = new GDALProjection();

            TestData error = new TestData();

            //Stream stream = new Storage("gpserror").DownloadToStream(device.GetFileName());

            if (Path.GetExtension(fileName).ToLower() == ".gpx")
                error = ProcessGPX(fileName);
            else if (
                Path.GetExtension(fileName).ToLower() == ".kml" ||
                Path.GetExtension(fileName).ToLower() == ".kmz")
                error = ProcessKML(fileName);
            else
                return null;

            if (error == null)
                return null;

            if (error.Entries.Count != 0)
                error.Initialize();

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

        private static TestData ProcessGPX(Stream fileStream) => ProcessGPX(GpxClass.FromStream(fileStream));

        private static TestData ProcessGPX(GpxClass gpx)
        {
            TestData data = new TestData();
            foreach (var track in gpx.Tracks)
            {
                foreach (var seg in track.trkseg)
                {
                    foreach (var pt in seg.trkpt)
                    {
                        PointDType c = pt.ToCoordinate();
                        PointDType UTM = m_projection.Project(c);

                        if (seg.trkpt.IndexOf(pt) == 0 &&
                            pt.timeSpecified)
                            data.Start = pt.time;
                        if (seg.trkpt.IndexOf(pt) == (seg.trkpt.Count - 1) &&
                            pt.timeSpecified)
                            data.End = pt.time;

                        data.Entries.Add(new Entry()
                        {
                            Lat = c.Y,
                            Lon = c.X,
                            X = UTM.X,
                            Y = UTM.Y,
                            Z = c.Z
                        });
                    }
                }
            }

            if (data.Start.HasValue && data.End.HasValue)
                data.Duration = (float)(data.End.Value - data.Start.Value).TotalMinutes;
            return data;
        }

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
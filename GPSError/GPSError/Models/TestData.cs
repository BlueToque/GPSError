using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GPSError.Models
{
    public class TestData
    {
        public TestData()
        {
            Entries = new List<Entry>();
        }

        public int TestDataID { get; set; }

        #region test statistics

        /// <summary>
        /// Minutes
        /// </summary>
        public float Duration { get; set; }

        public DateTime? Start { get; set; }

        public DateTime? End { get; set; }

        #endregion

        public ICollection<Entry> Entries { get; set; }

        public double AvgX { get; set; }
        public double AvgY { get; set; }
        public double StdvX { get; set; }
        public double StdvY { get; set; }

        /// <summary>
        /// CEP (50%)
        /// </summary>
        public double CEP { get; set; }

        /// <summary>
        /// 2DRMS (95%)
        /// </summary>
        public double TDRMS { get; set; }

        public double MaxX { get; set; }
        public double MaxY { get; set; }

        /// <summary>
        /// # Samples
        /// </summary>
        public int Count { get; set; }
        public double LocationX { get; set; }
        public double LocationY { get; set; }

        public List<Entry> Circle1 { get; set; }

        public List<Entry> Circle2 { get; set; }

        public string Platform { get; set; }

        public string UserAgent { get; set; }

        void CalculateAverage()
        {
            LocationX = Entries.Average(x => x.Lon);
            LocationY = Entries.Average(x => x.Lat);
            AvgX = Entries.Average(x => x.X);
            AvgY = Entries.Average(x => x.Y);
        }

        void CalculateStdev()
        {
            double sum = Entries.Sum(x => (x.X - AvgX) * (x.X - AvgX));
            StdvX = Math.Sqrt(sum / Entries.Count());
            sum = Entries.Sum(x => (x.Y - AvgY) * (x.Y - AvgY));
            StdvY = Math.Sqrt(sum / Entries.Count());
        }

        void CalculateCI()
        {
            CEP = 0.59 * (StdvX + StdvY);
            TDRMS = 2.0 * Math.Sqrt(StdvX * StdvX + StdvY * StdvY);
        }

        void Circles()
        {
            try
            {
                // do the circle
                var p50 = Math.PI / 50.0;

                Circle1 = new List<Entry>();
                Circle2 = new List<Entry>();
                for (int i = 0; i < 100; i++)
                {
                    var ipv = i * p50;
                    Circle1.Add(new Entry()
                    {
                        Y = CEP * Math.Cos(ipv),
                        X = CEP * Math.Sin(ipv)
                    });

                    Circle2.Add(new Entry()
                    {
                        Y = TDRMS * Math.Cos(ipv),
                        X = TDRMS * Math.Sin(ipv)
                    });
                }

            }
            catch (Exception ex)
            {
                Trace.TraceError("Error\r\n{0}", ex);
            }

        }

        void Normalize()
        {
            foreach (var entry in Entries)
            {
                entry.X -= AvgX;
                entry.Y -= AvgY;
            }
        }

        void Centre()
        {
            MaxX = Entries.Max(x => Math.Abs(x.X));
            MaxY = Entries.Max(x => Math.Abs(x.Y));
        }

        public void Process()
        {
            this.Circles();
        }

        public void Initialize()
        {
            this.Count = Entries.Count;
            this.CalculateAverage();
            this.CalculateStdev();
            this.CalculateCI();
            this.Normalize(); // only call this once
            this.Centre();
            this.Circles();
        }
    }

    public class Entry
    {
        public int EntryID { get; set; }

        public int TestDataID { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

}

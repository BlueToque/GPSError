using GPSError.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace GPSError
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            ChartArea chartArea1 = new ChartArea();

            chart1 = new Chart();
            ((System.ComponentModel.ISupportInitialize)(chart1)).BeginInit();
            // 
            // myElevationProfile
            // 
            //chartArea1.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea1.AxisX.Title = "latitude (m)";
            chartArea1.AxisX.Interval = 0.5;

            //chartArea1.AxisY.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea1.AxisY.Title = "longitude (m)";
            chartArea1.AxisX.Interval = 0.5;

            chartArea1.Name = "myChartArea";

            chart1.ChartAreas.Add(chartArea1);
            //chart1.ContextMenuStrip = this.contextMenuStrip1;
            chart1.Dock = DockStyle.Fill;
            chart1.Location = new Point(0, 0);
            chart1.Name = "myElevationProfile";


            chart1.Size = new Size(466, 368);
            chart1.SuppressExceptions = true;
            chart1.TabIndex = 0;
            chart1.Text = "Elevation Profile";
            splitContainer1.Panel2.Controls.Add(chart1);
            ((System.ComponentModel.ISupportInitialize)(chart1)).EndInit();

        }
        private readonly Chart chart1;

        TestData m_testData;

        private void LoadButton_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog()
            {
                Filter = "GPX File|*.gpx|KML File|*.kml"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            m_testData = Processor.Process(ofd.FileName);

            chart1.Series.Clear();

            chart1.Series.Add(new Series()
            {
                ChartArea = "myChartArea",
                ChartType = SeriesChartType.Point,
                MarkerColor = Color.Red,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 5,
                Name = "Series1",
                IsValueShownAsLabel = false,
            });

            var maxX = Math.Max(Math.Abs(m_testData.MaxX), Math.Abs(m_testData.MinX));
            var maxY = Math.Max(Math.Abs(m_testData.MaxY), Math.Abs(m_testData.MinY));
            var max = Math.Max(Math.Abs(maxX), Math.Abs(maxY));

            max=Math.Round(max, 0, MidpointRounding.AwayFromZero); // Output: 2

            chart1.ChartAreas[0].AxisY.Minimum = -max;
            chart1.ChartAreas[0].AxisY.Maximum = max;
            chart1.ChartAreas[0].AxisX.Minimum = -max;
            chart1.ChartAreas[0].AxisX.Maximum = max;

            chart1.ChartAreas[0].AxisX.Interval = 1;
            chart1.ChartAreas[0].AxisY.Interval = 1;

            chart1.ChartAreas[0].AxisX.MinorGrid.Interval = 5;
            chart1.ChartAreas[0].AxisY.MinorGrid.Interval = 5;
            chart1.ChartAreas[0].AxisX.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart1.ChartAreas[0].AxisY.MinorGrid.LineDashStyle = ChartDashStyle.Dot;
            chart1.ChartAreas[0].AxisX.MinorGrid.LineColor = Color.LightBlue;
            chart1.ChartAreas[0].AxisY.MinorGrid.LineColor = Color.LightBlue;

            chart1.ChartAreas[0].AxisX.MajorGrid.Interval = 1;
            chart1.ChartAreas[0].AxisY.MajorGrid.Interval = 1;
            chart1.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightBlue;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightBlue;

            foreach (var entry in m_testData.Entries)
                chart1.Series["Series1"].Points.AddXY(entry.X, entry.Y);

            // Draw the circle
            DrawCircle(m_testData.Circle1, Color.LightGreen, "Circle1");
            DrawCircle(m_testData.Circle2, Color.Red, "Circle2");
        }

        private void DrawCircle(List<Entry> circle, Color color, string text)
        {
            chart1.Series.Add(text);
            // Set the type to line      
            chart1.Series[text].ChartType = SeriesChartType.Line;
            chart1.Series[text].Color = color;
            chart1.Series[text].BorderWidth = 3;
            //This function cannot include zero, and we walk through it in steps of 0.1 to add coordinates to our series
            foreach (var c in circle)
                chart1.Series[text].Points.AddXY(c.X, c.Y);
            var first = circle.First();
            chart1.Series[text].Points.AddXY(first.X, first.Y);

            chart1.Series[text].LegendText = text;
        }

        private void SaveChartButton_Click(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog()
            {
                Filter = "Chart Image|*.png",
                DefaultExt=".png"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            chart1.SaveImage(sfd.FileName, ChartImageFormat.Png);
        }
    }
}

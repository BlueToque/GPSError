using GPSError.Models;
using System;
using System.IO;
using System.Windows.Forms;

namespace GPSError
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        TestData m_testData;
        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog() {
                Filter="GPX File|*.gpx|KML File|*.kml" 
            })
                m_testData = Processor.Process(ofd.FileName);

        }
    }
}

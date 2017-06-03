using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using SharpCompress.Archive;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            var archive = ArchiveFactory.Open(@"C:\test.rar");
            foreach (var rae in archive.Entries)
            {
                rae.WriteToDirectory(@"C:\");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppTest2
{
    public partial class CaptureForm : Form
    {
        private Point startPoint;
        private Rectangle selection;
        private bool isDragging = false;

        public Rectangle SelectedRegion { get; private set; }

        public CaptureForm()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.Opacity = 0.3;
            this.TopMost = true;
            this.Cursor = Cursors.Cross;

            // 이벤트 연결
            this.MouseDown += CaptureForm_MouseDown;
            this.MouseMove += CaptureForm_MouseMove;
            this.MouseUp += CaptureForm_MouseUp;
            this.Paint += CaptureForm_Paint;
        }

        private void CaptureForm_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            startPoint = e.Location;
        }

        private void CaptureForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                selection = new Rectangle(
                    Math.Min(startPoint.X, e.X),
                    Math.Min(startPoint.Y, e.Y),
                    Math.Abs(e.X - startPoint.X),
                    Math.Abs(e.Y - startPoint.Y));
                Invalidate();
            }
        }

        private void CaptureForm_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            SelectedRegion = selection;
            DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CaptureForm_Paint(object sender, PaintEventArgs e)
        {
            if (isDragging)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    e.Graphics.DrawRectangle(pen, selection);
                }
            }
        }
    }
}

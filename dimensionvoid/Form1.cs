using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace dimension
{
    public partial class Form1 : System.Windows.Forms.Form
    {
        private UIDocument _uiDoc;
        private Document _doc;
        private List<ClashResult> _clashes;

        // Constructor with parameters
        public Form1(UIDocument uiDoc, List<ClashResult> clashes)
        {
            InitializeComponent();
            _uiDoc = uiDoc;
            _doc = uiDoc.Document; 
            _clashes = clashes;

            foreach (var clash in _clashes)
            {
                comboBox2.Items.Add(clash.Description);
            }

            if (comboBox2.Items.Count > 0)
            {
                comboBox2.SelectedIndex = 0; // Select the first item by default
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Get the selected clash
            int selectedIndex = comboBox2.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _clashes.Count)
            {
                ClashResult selectedClash = _clashes[selectedIndex];
                ZoomToClash(selectedClash);
            }
        }

        private void ZoomToClash(ClashResult clash)
        {
            BoundingBoxXYZ clashBoundingBox = clash.BoundingBox;

            // Get the active view
            Autodesk.Revit.DB.View activeView = _doc.ActiveView;

            using (Transaction tx = new Transaction(_doc, "Zoom to Clash"))
            {
                tx.Start();

                // Adjust the section box of the view to zoom into the clash area
                if (activeView is View3D view3D)
                {
                    view3D.SetSectionBox(clashBoundingBox);
                }
                else if (activeView is ViewPlan || activeView is ViewSection)
                {
                    // For 2D views, you can adjust the view's crop box
                    if (activeView.CropBoxActive)
                    {
                        activeView.CropBox = clashBoundingBox;
                    }
                }

                tx.Commit();
            }
        }

        private void ClashViewer_Load(object sender, EventArgs e)
        {

        }
    }

    public class ClashResult
    {
        public string Description { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }

        public XYZ IntersectionPoint { get; set; }
        public int PipeDiameter { get; internal set; }
    }

    public class XYZComparer : IEqualityComparer<XYZ>
    {
        public bool Equals(XYZ x, XYZ y)
        {
            return x.IsAlmostEqualTo(y);
        }

        public int GetHashCode(XYZ obj)
        {
            return obj.GetHashCode();
        }
    }
}



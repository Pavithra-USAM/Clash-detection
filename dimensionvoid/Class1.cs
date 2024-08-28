using System;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;
using Autodesk.Revit.DB.Structure;
using dimension;
using System.IO;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace dimensionvoid
{
    [Transaction(TransactionMode.Manual)]

    public class class1 : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "Custom Tab";
            string panelName = "dimensionvoid";

            // Create a custom ribbon tab
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Tab already exists
            }

            // Create a ribbon panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, panelName);

            //Get the path to the assembly
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData("Buttom1", "Click", assemblyPath, typeof(dimensionvoid.Print4Command).FullName);

            ribbonPanel.AddItem(buttonData);


            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
    public static class BoundingBoxExtensions
    {
        public static bool IntersectsBoundingBox(this BoundingBoxXYZ boundingBox1, BoundingBoxXYZ boundingBox2)
        {
            XYZ min1 = boundingBox1.Min;
            XYZ max1 = boundingBox1.Max;
            XYZ min2 = boundingBox2.Min;
            XYZ max2 = boundingBox2.Max;

            return (min1.X <= max2.X && max1.X >= min2.X) &&
                   (min1.Y <= max2.Y && max1.Y >= min2.Y) &&
                   (min1.Z <= max2.Z && max1.Z >= min2.Z);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class Print4Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get the application and document from the commandData
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // List of specific BuiltInCategories to include for clash detection
                List<BuiltInCategory> targetCategories = new List<BuiltInCategory>
         {
             BuiltInCategory.OST_CableTray,
             BuiltInCategory.OST_Walls,
             BuiltInCategory.OST_Conduit,
             BuiltInCategory.OST_PipeSegments,
             BuiltInCategory.OST_PipeAccessory,
             BuiltInCategory.OST_PipeCurves,
             BuiltInCategory.OST_PlumbingEquipment,
             BuiltInCategory.OST_PlumbingFixtures,
             BuiltInCategory.OST_FlexPipeCurves,
         };

                // Collect only the elements from the specified categories
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ICollection<Element> allElements = collector
                    .WhereElementIsNotElementType()
                    .Where(e =>
                    {
                        var category = e.Category;
                        if (category == null) return false;
                        return targetCategories.Contains((BuiltInCategory)category.Id.IntegerValue);
                    })
                    .ToList();

                // List to hold clash results
                List<ClashResult> clashes = new List<ClashResult>();

                // Spatial Element Geometry Options
                Options geomOptions = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true
                };

                // Loop through each element to check for clashes
                for (int i = 0; i < allElements.Count; i++)
                {
                    Element element1 = allElements.ElementAt(i);
                    GeometryElement geometryElement1 = element1.get_Geometry(geomOptions);
                    if (geometryElement1 == null) continue;

                    for (int j = i + 1; j < allElements.Count; j++) // Note: j starts from i + 1
                    {
                        Element element2 = allElements.ElementAt(j);
                        GeometryElement geometryElement2 = element2.get_Geometry(geomOptions);
                        if (geometryElement2 == null) continue;

                        // Check for intersection between the two geometries
                        bool isClash = CheckGeometryIntersection(geometryElement1, geometryElement2);
                        if (isClash)
                        {
                            BuiltInCategory category1 = (BuiltInCategory)element1.Category.Id.IntegerValue;
                            BuiltInCategory category2 = (BuiltInCategory)element2.Category.Id.IntegerValue;

                            // Focus on clashes between pipes/cable trays and walls only, avoid vice versa
                            if (((category2 == BuiltInCategory.OST_CableTray || category2 == BuiltInCategory.OST_PipeCurves || category2 == BuiltInCategory.OST_Conduit || category2 == BuiltInCategory.OST_FlexPipeCurves) && category1 == BuiltInCategory.OST_Walls))
                            {
                                BoundingBoxXYZ bbox2 = element2.get_BoundingBox(null);
                                XYZ clashPoint1 = GetGeometricCenter(geometryElement2);

                                // Ensure bbox2 is not null
                                if (bbox2 == null) continue;

                                XYZ clashPoint = new XYZ(
                                    (bbox2.Max.X + bbox2.Min.X) / 2,
                                    (bbox2.Max.Y + bbox2.Min.Y) / 2,
                                    (bbox2.Max.Z + bbox2.Min.Z) / 2
                                );

                                if (clashPoint != null)
                                {
                                    clashes.Add(new ClashResult
                                    {
                                        Description = $"Clash detected between {element1.Name ?? element1.Category.Name} and {element2.Name ?? element2.Category.Name}",
                                        IntersectionPoint = clashPoint
                                    });
                                }
                            }
                        }
                    }
                }

                // If clashes are found, display the clash viewer
                if (clashes.Count > 0)
                {
                    Form1 viewer = new Form1(uiDoc, clashes);
                    viewer.ShowDialog();

                    // Path to the family file
                    string familyPath = @"C:\Users\Pavithra\Documents\Familynew.rfa";

                    // Check if the family file exists
                    if (!File.Exists(familyPath))
                    {
                        TaskDialog.Show("Error", "Family file does not exist.");
                        return Result.Failed;
                    }

                    // Start a transaction
                    using (Transaction trans = new Transaction(doc, "Load and Place Family"))
                    {
                        trans.Start();

                        // Load the family
                        Family family = null;
                        if (doc.LoadFamily(familyPath, out family))
                        {
                            TaskDialog.Show("Success", "Family loaded successfully.");
                        }
                        else
                        {
                            TaskDialog.Show("Error", "Failed to load family.");
                            trans.RollBack();
                            return Result.Failed;
                        }

                        // Create a FamilySymbol (type) from the loaded Family
                        FamilySymbol familySymbol = null;
                        foreach (ElementId id in family.GetFamilySymbolIds())
                        {
                            familySymbol = doc.GetElement(id) as FamilySymbol;
                            break; // Assume the first one
                        }

                        // Make sure the FamilySymbol is activated
                        if (!familySymbol.IsActive)
                        {
                            familySymbol.Activate();
                            doc.Regenerate();
                        }

                        // Set to track used placement points
                        HashSet<XYZ> usedPoints = new HashSet<XYZ>(new XYZComparer());

                        // Find a wall in the current view to place the family instance
                        foreach (var clash in clashes)
                        {
                            // Find the closest wall to the clash point
                            Wall closestWall = FindClosestWall(doc, clash.IntersectionPoint);

                            if (closestWall != null)
                            {
                                PlanarFace closestFace = FindClosestWallFace(closestWall, clash.IntersectionPoint);

                                if (closestFace != null)
                                {
                                    XYZ placementPoint = TransformToWallFaceCoordinate(closestFace, clash.IntersectionPoint);

                                    // Adjust placementPoint by moving it downwards by the radius of element2
                                    if (placementPoint != null && !usedPoints.Contains(placementPoint))
                                    {
                                        // Assuming you have a way to calculate or retrieve the radius of element2
                                        // Pass element2 as a parameter to GetElementRadius method

                                        double element2Radius = GetElementRadius(closestWall); // Fix here: use closestWall or another valid element
                                        placementPoint -= new XYZ(0, 0, element2Radius); // Move downwards

                                        FamilyInstance familyInstance = doc.Create.NewFamilyInstance(
                                            placementPoint, familySymbol, closestWall, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                        TaskDialog.Show("Success", $"Family instance placed successfully at {placementPoint}.");
                                        usedPoints.Add(placementPoint); // Mark this point as used
                                    }
                                    else
                                    {
                                        TaskDialog.Show("Error", "Failed to transform intersection point for family placement.");
                                    }
                                }
                                else
                                {
                                    TaskDialog.Show("Error", "Failed to find a valid wall face for family placement.");
                                }
                            }
                            else
                            {
                                TaskDialog.Show("Error", "No suitable wall found for family placement.");
                            }
                        }

                        // Commit the transaction
                        trans.Commit();
                    }

                    return Result.Succeeded;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Exception", ex.Message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }



        private XYZ GetGeometricCenter(GeometryElement geometryElement)
        {
            XYZ totalCenter = XYZ.Zero;
            int count = 0;

            foreach (GeometryObject geomObj in geometryElement)
            {
                if (geomObj is Solid solid)
                {
                    totalCenter += solid.ComputeCentroid();
                    count++;
                }
            }

            return count > 0 ? totalCenter / count : null;
        }

        private bool CheckGeometryIntersection(GeometryElement geom1, GeometryElement geom2)
        {
            foreach (GeometryObject obj1 in geom1)
            {
                Solid solid1 = obj1 as Solid;
                if (solid1 == null || solid1.Volume == 0) continue;

                foreach (GeometryObject obj2 in geom2)
                {
                    Solid solid2 = obj2 as Solid;
                    if (solid2 == null || solid2.Volume == 0) continue;

                    try
                    {
                        Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                        if (intersection != null && intersection.Volume > 0)
                        {
                            return true;
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        BoundingBoxXYZ bbox1 = solid1.GetBoundingBox();
                        BoundingBoxXYZ bbox2 = solid2.GetBoundingBox();
                        if (bbox1.IntersectsBoundingBox(bbox2))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private XYZ TransformToWallFaceCoordinate(PlanarFace wallFace, XYZ intersectionPoint)
        {
            XYZ projectedPoint = wallFace.Project(intersectionPoint).XYZPoint;
            return projectedPoint;
        }

        private PlanarFace FindClosestWallFace(Wall wall, XYZ point)
        {
            Options geomOptions = new Options { ComputeReferences = true, IncludeNonVisibleObjects = true };
            GeometryElement geomElement = wall.get_Geometry(geomOptions);

            PlanarFace closestFace = null;
            double minDistance = double.MaxValue;

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            IntersectionResult result = planarFace.Project(point);
                            if (result != null)
                            {
                                XYZ closestPointOnFace = result.XYZPoint;
                                double distance = point.DistanceTo(closestPointOnFace);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    closestFace = planarFace;
                                }
                            }
                        }
                    }
                }
            }
            return closestFace;
        }

        private Wall FindClosestWall(Document doc, XYZ point)
        {
            Wall closestWall = null;
            double minDistance = double.MaxValue;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                Wall wall = elem as Wall;
                if (wall != null)
                {
                    BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        XYZ wallCenter = (bbox.Max + bbox.Min) / 2;
                        double distance = wallCenter.DistanceTo(point);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestWall = wall;
                        }
                    }
                }
            }

            return closestWall;
        }

        private double GetElementRadius(Element element)
        {
            // Check if the element is a FamilyInstance or other category-specific element
            if (element is FamilyInstance familyInstance)
            {
                // Handle specific cases based on family category
                switch (familyInstance.Symbol.Family.FamilyCategory.Id.IntegerValue)
                {
                    case (int)BuiltInCategory.OST_PipeAccessory:
                    case (int)BuiltInCategory.OST_PipeCurves:
                    case (int)BuiltInCategory.OST_FlexPipeCurves:
                    case (int)BuiltInCategory.OST_PipeSegments:
                    case (int)BuiltInCategory.OST_PlumbingEquipment:
                    case (int)BuiltInCategory.OST_PlumbingFixtures:
                        // Extract radius for pipes and similar elements from their diameter property
                        Parameter diameterParam = familyInstance.LookupParameter("Diameter");
                        if (diameterParam != null && diameterParam.HasValue)
                        {
                            return diameterParam.AsDouble() / 2; // Diameter / 2 = Radius
                        }
                        break;

                    case (int)BuiltInCategory.OST_DuctAccessory:
                    case (int)BuiltInCategory.OST_DuctCurves:
                    case (int)BuiltInCategory.OST_FlexDuctCurves:
                        // Handle ducts similarly by looking for the "Diameter" or equivalent parameter
                        Parameter ductDiameterParam = familyInstance.LookupParameter("Diameter");
                        if (ductDiameterParam != null && ductDiameterParam.HasValue)
                        {
                            return ductDiameterParam.AsDouble() / 2;
                        }
                        break;

                    case (int)BuiltInCategory.OST_Conduit:
                        // Conduit elements usually have a "Diameter" parameter
                        Parameter conduitDiameterParam = familyInstance.LookupParameter("Diameter");
                        if (conduitDiameterParam != null && conduitDiameterParam.HasValue)
                        {
                            return conduitDiameterParam.AsDouble() / 2;
                        }
                        break;
                }
            }

            // For non-FamilyInstance elements, use BoundingBoxXYZ for approximation
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox != null)
            {
                XYZ min = bbox.Min;
                XYZ max = bbox.Max;

                double width = max.X - min.X;
                //double depth = max.Y - min.Y;
                //double height = max.Z - min.Z;

                double depth = max.Y - min.Y;
                double height = max.Z - min.Z;

                // Assuming a cylindrical or circular profile, the radius can be approximated
                double radius = Math.Min(width, Math.Min(depth, height)) / 2;

                return radius;

                // Use the largest dimension divided by 2 as an approximation for radius
                double maxDimension = Math.Max(width, Math.Max(depth, height));
                return maxDimension / 2;
            }

            // Default radius if not calculated
            return 1.0; // Default or fallback value, adjust as needed
        }


        public class LoadFamilyBasedOnElementType
        {
            public void LoadFamily(UIDocument uiDoc, Document doc, Element intersectingElement)
            {
                string familyPath = string.Empty;

                // Determine the type of the intersecting element
                if (intersectingElement is Pipe)
                {
                    // Path to the family file for circular openings (for pipes)
                    familyPath = @"C:\Users\Pavithra\Familynew.rfa";
                }
                else if (intersectingElement is Duct)
                {
                    // Path to the family file for rectangular openings (for ducts)
                    familyPath = @"C:\Users\Pavithra\Familynew2.rfa";
                }
                else if (intersectingElement is CableTray)
                {
                    // Path to the family file for tray openings
                    familyPath = @"C:\Users\Pavithra\Familynew1.rfa";
                }
                else
                {
                    TaskDialog.Show("Error", "Unsupported element type.");
                    return;
                }

                // Load the family file
                Family family = null;
                if (!doc.LoadFamily(familyPath, out family))
                {
                    TaskDialog.Show("Error", "Failed to load family.");
                    return;
                }

                // Place family instance at the intersection point
                PlaceFamilyInstance(doc, intersectingElement, family);
            }

            private void PlaceFamilyInstance(Document doc, Element intersectingElement, Family family)
            {
                // Get the intersection point (you need to define how you get this point)
                XYZ intersectionPoint = GetIntersectionPoint(intersectingElement);

                // Get the family symbol
                FamilySymbol familySymbol = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;

                if (familySymbol != null)
                {
                    // Activate the family symbol
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                        doc.Regenerate();
                    }

                    // Place the family instance at the intersection point
                    using (Transaction trans = new Transaction(doc, "Place Family Instance"))
                    {
                        trans.Start();
                        doc.Create.NewFamilyInstance(intersectionPoint, familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        trans.Commit();
                    }
                }
            }

            private XYZ GetIntersectionPoint(Element intersectingElement)
            {
                // Implement your logic to calculate the intersection point
                // For example, you might use bounding boxes or geometry intersections
                return new XYZ(0, 0, 0); // Placeholder value
            }


        }

    }

}


    










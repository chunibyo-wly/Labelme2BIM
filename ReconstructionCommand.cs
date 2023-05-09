using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace UBILOC
{
    public class LabelMe
    {
        public List<Shape> shapes { get; set; }
        public List<List<double>> boundary { get; set; }
    }

    public class Shape
    {
        public string label { get; set; }
        public string shape_type { get; set; }
        public List<List<double>> points { get; set; }
    }

    [Transaction(TransactionMode.Manual)]
    public class ReconstructionCommand : IExternalCommand
    {
        public string Prefix = "Floor";
        public List<LabelMe> labelMeList = new List<LabelMe>();
        public List<Level> LevelsList = new List<Level>();
        public List<Tuple<Line, Wall>> WindowLinesList = new List<Tuple<Line, Wall>>();
        public List<Tuple<Line, Wall>> DoorLinesList = new List<Tuple<Line, Wall>>();
        public int num = 6;
        public double FloorHeight = UnitUtils.ConvertToInternalUnits(3.5, UnitTypeId.Meters);

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Init
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            var app = uiapp.Application;
            var doc = uidoc.Document;

            LevelsList.Clear();
            LevelsList.Add(null);
            labelMeList.Clear();
            labelMeList.Add(null);
            // Init

            for (int index = 1; index <= num; ++index)
            {
                string filePath = String.Format("D:\\workspace\\cad2labelme\\output\\F{0}.json", index);
                Debug.Print(filePath);
                string jsonString = File.ReadAllText(filePath);
                labelMeList.Add(JsonConvert.DeserializeObject<LabelMe>(jsonString));
            }

            // 添加 Level
            if (!AddLevels(doc))
                return Result.Failed;

            // 添加墙
            if (!AddWalls(doc))
                return Result.Failed;

            // 添加窗户
            if (!AddWindows(doc))
                return Result.Failed;

            // 添加门
            if (!AddDoors(doc))
                return Result.Failed;

            if (!AddFloors(doc))
                return Result.Failed;
            return Result.Succeeded;
        }

        public double convert(double pixelCoordinate)
        {
            return UnitUtils.ConvertToInternalUnits(pixelCoordinate / 77.6, UnitTypeId.Meters);
        }

        public bool AddLevels(Document doc)
        {
            var collector = new FilteredElementCollector(doc);

            collector.OfCategory(BuiltInCategory.OST_Levels);
            IEnumerable<Element> levels = collector.ToElements();
            using (Transaction transaction = new Transaction(doc, "Clear Levels"))
            {
                // 清理原有的 Level 避免命名重复
                if (TransactionStatus.Started == transaction.Start())
                {
                    foreach (Element level in levels)
                    {
                        if (level.Name.StartsWith(Prefix))
                            doc.Delete(level.Id);
                    }
                    transaction.Commit();
                }
            }

            using (Transaction transaction = new Transaction(doc, "Create Levels"))
            {
                // 创建 1-6 楼
                if (TransactionStatus.Started == transaction.Start())
                {
                    for (int i = 1; i <= num + 1; ++i)
                    {
                        var level = Level.Create(doc, FloorHeight * (i - 1));
                        if (null != level)
                        {
                            level.Name = Prefix + i.ToString();
                            LevelsList.Add(level);
                            Debug.Print("Level {0} created.", i);
                        }
                    }
                    transaction.Commit();
                }
            }

            return (LevelsList.Count == num + 2);
        }

        public bool AddWalls(Document doc)
        {
            using (Transaction transaction = new Transaction(doc, "Clear Walls"))
            {
                // 清理原有的 Wall
                if (TransactionStatus.Started == transaction.Start())
                {
                    try
                    {
                        var collector = new FilteredElementCollector(doc);
                        collector.OfCategory(BuiltInCategory.OST_Walls);
                        ICollection<ElementId> walls = collector.ToElementIds();
                        doc.Delete(walls);
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.RollBack();
                    }
                }
            }

            using (Transaction transaction = new Transaction(doc, "Create Walls"))
            {
                if (TransactionStatus.Started == transaction.Start())
                {
                    for (int index = 1; index <= num; ++index)
                    {
                        var shapes = labelMeList[index].shapes;

                        var lineList = new List<Tuple<Line, string>>();
                        foreach (Shape shape in shapes)
                        {
                            if (shape.label == "wall" || shape.label == "window")
                            {
                                var pts = new List<XYZ>();
                                for (int i = 0; i < 4; i++)
                                {
                                    var a = new XYZ(convert(shape.points[i][0]), convert(shape.points[i][1]), LevelsList[index].Elevation);
                                    var b = new XYZ(convert(shape.points[i + 1][0]), convert(shape.points[i + 1][1]), LevelsList[index].Elevation);
                                    pts.Add((a + b) / 2);
                                }
                                if (pts[0].DistanceTo(pts[2]) > pts[1].DistanceTo(pts[3]))
                                    lineList.Add(new Tuple<Line, string>(Line.CreateBound(pts[0], pts[2]), shape.label));
                                else
                                    lineList.Add(new Tuple<Line, string>(Line.CreateBound(pts[1], pts[3]), shape.label));
                            }
                            else if (shape.label == "door")
                            {
                                var o = new XYZ(convert(shape.points[0][0]), convert(shape.points[0][1]), LevelsList[index].Elevation);
                                var a = new XYZ(convert(shape.points[1][0]), convert(shape.points[1][1]), LevelsList[index].Elevation);
                                lineList.Add(new Tuple<Line, string>(Line.CreateBound(o, a), shape.label));
                            }
                        }

                        try
                        {
                            foreach (var lineTuple in lineList)
                            {
                                var wall = Wall.Create(doc, lineTuple.Item1, LevelsList[index].Id, true);
                                if (null != wall)
                                {
                                    var parameter = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                                    parameter.Set(LevelsList[index + 1].Id);
                                    if (lineTuple.Item2 == "window")
                                        WindowLinesList.Add(new Tuple<Line, Wall>(lineTuple.Item1, wall));
                                    else if (lineTuple.Item2 == "door")
                                        DoorLinesList.Add(new Tuple<Line, Wall>(lineTuple.Item1, wall));
                                }
                            }
                        }
                        catch (Exception)
                        {
                            transaction.RollBack();
                            return false;
                        }
                    }
                    transaction.Commit();
                }
            }

            return true;
        }

        public bool AddWindows(Document doc)
        {
            using (Transaction transaction = new Transaction(doc, "Clear Windows"))
            {
                // 清理原有的 Window
                if (TransactionStatus.Started == transaction.Start())
                {
                    try
                    {
                        ICollection<ElementId> windows = new FilteredElementCollector(doc)
                                                            .OfCategory(BuiltInCategory.OST_Windows)
                                                            .OfClass(typeof(FamilyInstance))
                                                            .ToElementIds();
                        doc.Delete(windows);
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.RollBack();
                    }
                }
            }

            List<FamilySymbol> symbolsList = new FilteredElementCollector(doc)
                                           .OfClass(typeof(FamilySymbol))
                                           .OfCategory(BuiltInCategory.OST_Windows)
                                           .OfType<FamilySymbol>()
                                           .ToList<FamilySymbol>();

            using (Transaction transaction = new Transaction(doc, "Modify Windows Family Type"))
            {
                // 统一窗户高度
                if (TransactionStatus.Started == transaction.Start())
                {
                    try
                    {
                        foreach (var _symbol in symbolsList)
                        {
                            _symbol.Activate();
                            _symbol.get_Parameter(BuiltInParameter.CASEWORK_HEIGHT).Set(FloorHeight * 0.5);
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.RollBack();
                        return false;
                    }
                }
            }

            if (symbolsList.Count == 0)
                return false;

            using (Transaction transaction = new Transaction(doc, "Create Windows"))
            {
                if (TransactionStatus.Started == transaction.Start())
                {
                    foreach (var WindowLine in WindowLinesList)
                    {
                        var line = WindowLine.Item1;
                        var wall = WindowLine.Item2;

                        double width = 0.0;
                        FamilySymbol symbol = null;
                        foreach (var _symbol in symbolsList)
                        {
                            _symbol.Activate();
                            var familyWidthParam = _symbol.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsValueString();
                            double _width = Double.PositiveInfinity;
                            if (double.TryParse(familyWidthParam, out _width))
                            {
                                if (width < _width && UnitUtils.ConvertToInternalUnits(_width / 1000, UnitTypeId.Meters) < line.Length)
                                {
                                    width = _width;
                                    symbol = _symbol;
                                }
                            }
                        }

                        if (symbol == null) continue;

                        try
                        {
                            Level level = doc.GetElement(wall.LevelId) as Level;
                            symbol.Activate();
                            XYZ location = (line.GetEndPoint(0) + line.GetEndPoint(1)) / 2;
                            FamilyInstance instance = doc.Create.NewFamilyInstance(location, symbol, wall, level, StructuralType.NonStructural);

                            var windowHeight = symbol.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble();
                            instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set((FloorHeight - windowHeight) / 2);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    transaction.Commit();
                }
            }
            return true;
        }

        public bool AddDoors(Document doc)
        {
            using (Transaction transaction = new Transaction(doc, "Clear Doors"))
            {
                // 清理原有的 Door
                if (TransactionStatus.Started == transaction.Start())
                {
                    try
                    {
                        ICollection<ElementId> doors = new FilteredElementCollector(doc)
                                                            .OfCategory(BuiltInCategory.OST_Doors)
                                                            .OfClass(typeof(FamilyInstance))
                                                            .ToElementIds();
                        doc.Delete(doors);
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.RollBack();
                    }
                }
            }

            List<FamilySymbol> symbolsList = new FilteredElementCollector(doc)
                               .OfClass(typeof(FamilySymbol))
                               .OfCategory(BuiltInCategory.OST_Doors)
                               .OfType<FamilySymbol>()
                               .ToList<FamilySymbol>();
            if (symbolsList.Count == 0)
                return false;

            using (Transaction transaction = new Transaction(doc, "Create Doors"))
            {
                if (TransactionStatus.Started == transaction.Start())
                {
                    foreach (var DoorLine in DoorLinesList)
                    {
                        var line = DoorLine.Item1;
                        var wall = DoorLine.Item2;

                        double width = 0.0;
                        FamilySymbol symbol = null;
                        foreach (var _symbol in symbolsList)
                        {
                            _symbol.Activate();
                            var familyWidthParam = _symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsValueString();
                            double _width = Double.PositiveInfinity;
                            if (double.TryParse(familyWidthParam, out _width))
                            {
                                if (width < _width && UnitUtils.ConvertToInternalUnits(_width / 1000, UnitTypeId.Meters) < line.Length)
                                {
                                    width = _width;
                                    symbol = _symbol;
                                }
                            }
                        }

                        if (symbol == null) continue;

                        try
                        {
                            Level level = doc.GetElement(wall.LevelId) as Level;
                            symbol.Activate();
                            XYZ location = (line.GetEndPoint(0) + line.GetEndPoint(1)) / 2;
                            FamilyInstance instance = doc.Create.NewFamilyInstance(location, symbol, wall, level, StructuralType.NonStructural);
                            instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(0);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    transaction.Commit();
                }
            }
            return true;
        }

        public bool AddFloors(Document doc)
        {
            using (Transaction transaction = new Transaction(doc, "Clear Floors"))
            {
                // 清理原有的 Floor
                if (TransactionStatus.Started == transaction.Start())
                {
                    try
                    {
                        ICollection<ElementId> floors = new FilteredElementCollector(doc)
                                                            .OfCategory(BuiltInCategory.OST_Floors)
                                                            .OfClass(typeof(Floor))
                                                            .ToElementIds();
                        doc.Delete(floors);
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.RollBack();
                    }
                }
            }

            var floorType = new FilteredElementCollector(doc)
                   .OfClass(typeof(FloorType))
                   .OfCategory(BuiltInCategory.OST_Floors)
                   .FirstElement() as FloorType;

            if (floorType == null)
                return false;

            using (Transaction transaction = new Transaction(doc, "Create Floors"))
            {
                if (TransactionStatus.Started == transaction.Start())
                {
                    for (int index = 1; index <= num; ++index)
                    {
                        var item = labelMeList[index];
                        if (item == null) continue;
                        CurveArray boundings = new CurveArray();
                        var points = item.boundary;
                        var n = points.Count;
                        for (int i = 0; i < n - 1; ++i)
                        {
                            var a = new XYZ(convert(points[i][0]), convert(points[i][1]), LevelsList[index].Elevation);
                            var b = new XYZ(convert(points[(i + 1) % n][0]), convert(points[(i + 1) % n][1]), LevelsList[index].Elevation);
                            boundings.Append(Line.CreateBound(a, b));
                        }
                        XYZ normal = XYZ.BasisZ;
                        doc.Create.NewFloor(boundings, floorType, LevelsList[index], true, normal);
                    }
                    transaction.Commit();
                }
            }

            return true;
        }
    }
}
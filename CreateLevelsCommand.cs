#region Namespaces

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion

namespace UBILOC
{
    [Transaction(TransactionMode.Manual)]
    public class CreateLevelsCommand : IExternalCommand
    {
        public ArrayList levelList;
        public FilteredElementCollector collector;
        public string prefix = "Floor";

        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            /*
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            var app = uiapp.Application;
            var doc = uidoc.Document;

            // Init
            LevelsList = new ArrayList();
            collector = new FilteredElementCollector(doc);
            // Init

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

            var height = UnitUtils.ConvertToInternalUnits(3.0, UnitTypeId.Meters);
            using (Transaction transaction = new Transaction(doc, "Creating Levels"))
            {
                // 创建 1-6 楼
                if (TransactionStatus.Started == transaction.Start())
                {
                    for (int i = 1; i <= 7; ++i)
                    {
                        var level = Level.Create(doc, height * (i - 1));
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
            */
            return Result.Succeeded;
        }
    }
}
#region Namespaces

using System;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

#endregion

namespace UBILOC
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            string tabName = "UBILOC";
            string panelName = "Automation";
            app.CreateRibbonTab(tabName);
            var panel = app.CreateRibbonPanel(tabName, panelName);

            // var levelBtn = new PushButtonData("Levels", "CreateLevels", typeof(CreateLevelsCommand).Assembly.Location, "UBILOC.CreateLevelsCommand");
            // BitmapImage levelBtnImage = new BitmapImage(new Uri("pack://application:,,,/UBILOC;component/Resources/level.png"));
            // levelBtn.LargeImage = levelBtnImage;
            // panel.AddItem(levelBtn);

            var wallBtn = new PushButtonData("Construct", "START", typeof(ReconstructionCommand).Assembly.Location, "UBILOC.ReconstructionCommand");
            BitmapImage wallBtnImage = new BitmapImage(new Uri("pack://application:,,,/UBILOC;component/Resources/robot.png"));
            wallBtn.LargeImage = wallBtnImage;
            panel.AddItem(wallBtn);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
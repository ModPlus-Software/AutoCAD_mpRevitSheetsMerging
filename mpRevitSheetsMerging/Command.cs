namespace mpRevitSheetsMerging;

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using ModPlusAPI.Windows;
using Services;
using MessageBox = System.Windows.MessageBox;

public class Command
{
    private const string PluginName = "Объединение листов";

    [CommandMethod("mpRevitSheetsMerging")]
    public void SheetsMergingCommand()
    {
        try
        {
            var dwgFiles = SelectMergingFiles();
            var sheetImportService = new SheetsImportService();
            var maxX = 0.0;

            using var progress = new ProgressMeter();
            progress.SetLimit(dwgFiles.Length);
            progress.Start("Обработка файлов...");

            foreach (var dwgFile in dwgFiles)
            {
                progress.MeterProgress();

                try
                {
                    sheetImportService.ImportSheets(dwgFile, ref maxX);
                }
                catch (System.Exception ex)
                {
                    var result = MessageBox.Show(
                        $"Ошибка импорта листов из файла '{Path.GetFileName(dwgFile)}':{Environment.NewLine}" +
                        $"{ex.Message}{Environment.NewLine}" +
                        $"{Environment.NewLine}" +
                        "Продолжить?",
                        PluginName,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error,
                        MessageBoxResult.Yes);

                    if (result == MessageBoxResult.No)
                        throw new OperationCanceledException();
                }
            }

            progress.Stop();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (System.Exception ex)
        {
            ex.ShowInExceptionBox();
        }
    }

    private string[] SelectMergingFiles()
    {
        var selectFilesDialog = new OpenFileDialog(
            "Выбор файлов для объединения листов",
            string.Empty,
            "dwg",
            string.Empty,
            OpenFileDialog.OpenFileDialogFlags.AllowMultiple);
        if (selectFilesDialog.ShowModal() == true)
            return selectFilesDialog.GetFilenames();

        throw new OperationCanceledException();
    }
}
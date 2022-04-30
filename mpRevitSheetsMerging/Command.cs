namespace mpRevitSheetsMerging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using ModPlusAPI;
using ModPlusAPI.IO;
using ModPlusAPI.Windows;
using Services;

/// <summary>
/// Command class
/// </summary>
public class Command
{
    /// <summary>
    /// Start command
    /// </summary>
    [CommandMethod("mpRevitSheetsMerging")]
    public void SheetsMergingCommand()
    {
        try
        {
            var pluginName = Language.GetPluginLocalName(new ModPlusConnector());
            var dwgFiles = SelectMergingFiles();
            var sheetImportService = new SheetsImportService();
            var maxX = 0.0;

            using var progress = new ProgressMeter();
            progress.SetLimit(dwgFiles.Length);

            // Обработка файлов...
            progress.Start(Language.GetItem("h1"));

            var commonNamePart = GetCommonNamePart(dwgFiles);

            foreach (var dwgFile in dwgFiles.OrderBy(i => i, new OrdinalStringComparer()))
            {
                progress.MeterProgress();

                try
                {
                    sheetImportService.ImportSheets(dwgFile, commonNamePart, ref maxX);
                }
                catch (System.Exception ex)
                {
                    // Ошибка импорта листов из файла
                    if (!MessageBox.ShowYesNo(
                            $"{Language.GetItem("h2")} \"{Path.GetFileName(dwgFile)}\":{Environment.NewLine}" +
                            $"{ex.Message}{Environment.NewLine}" +
                            $"{Environment.NewLine}" +
                            $"{Language.GetCommonItem("continue")}?",
                            pluginName,
                            MessageBoxIcon.Alert))
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
            Language.GetItem("h3"),
            string.Empty,
            "dwg",
            string.Empty,
            OpenFileDialog.OpenFileDialogFlags.AllowMultiple);
        if (selectFilesDialog.ShowModal() == true)
            return selectFilesDialog.GetFilenames();

        throw new OperationCanceledException();
    }

    private string GetCommonNamePart(IEnumerable<string> fileNames)
    {
        var strings = fileNames.Select(Path.GetFileNameWithoutExtension).ToList();

        // https://stackoverflow.com/a/30981377
        return new string(strings.Select(str => str.TakeWhile((c, index) => strings.All(s => s[index] == c)))
            .FirstOrDefault()?.ToArray());
    }
}
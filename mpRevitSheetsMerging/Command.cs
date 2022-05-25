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
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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
        ProgressWindow? progressWindow = null;
        try
        {
            var pluginName = Language.GetPluginLocalName(new ModPlusConnector());
            var dwgFiles = SelectMergingFiles();
            var sheetImportService = new SheetsImportService();
            var maxX = 0.0;
            
            // progress
            progressWindow = new ProgressWindow
            {
                ProgressBar = { Maximum = dwgFiles.Length }
            };
            AcApp.ShowModelessWindow(AcApp.MainWindow.Handle, progressWindow);

            var commonNamePart = GetCommonNamePart(dwgFiles);

            var index = 0;
            foreach (var dwgFile in dwgFiles.OrderBy(i => i, new OrdinalStringComparer()))
            {
                // Обработка файлов...
                progressWindow?.Dispatcher.Invoke(() => progressWindow.ProgressBar.Value = ++index);
                progressWindow?.Dispatcher.Invoke(() => progressWindow.TbProgress.Text = $"{Language.GetItem("h1")} {index}/{dwgFiles.Length}");

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
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (System.Exception exception)
        {
            exception.ShowInExceptionBox();
        }
        finally
        {
            progressWindow?.Dispatcher.Invoke(() => progressWindow?.Close());
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
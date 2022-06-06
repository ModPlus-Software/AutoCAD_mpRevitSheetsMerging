namespace mpRevitSheetsMerging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using ModPlus.Extensions;
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
    [CommandMethod("ModPlus", "mpRevitSheetsMerging", CommandFlags.Session)]
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

            using var docLock = AcApp.DocumentManager.MdiActiveDocument.LockDocument();

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
            
            // Объединение слоев...
            progressWindow?.Dispatcher.Invoke(() => progressWindow.TbProgress.Text = Language.GetItem("h4"));
            MergeLayers(progressWindow);
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

    private void MergeLayers(ProgressWindow? progressWindow)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var layers = new List<LayerInfo>();

        using var tr = db.TransactionManager.StartTransaction();
        using var bt = db.BlockTableId.GetObjectAs<BlockTable>();
        using var layerTable = db.LayerTableId.OpenAs<LayerTable>();
        foreach (var id in layerTable)
        {
            var layer = id.GetObjectAs<LayerTableRecord>(true);
            var layerName = layer.Name;
            if (layerName == "0" || layer.IsLocked || db.Clayer == layer.Id)
                continue;
            if (layerName.StartsWith("$") && layerName.Count(c => c == '$') >= 2)
                layers.Add(new LayerInfo(layer));
        }
        
        foreach (var layersGroup in layers.GroupBy(l => l.UniqLayerName))
        {
            // Объединение слоев...
            progressWindow?.Dispatcher.Invoke(() => progressWindow.TbProgress.Text = Language.GetItem("h4"));

            var layerInfos = layersGroup.OrderBy(l => l.FullName).ToList();
            var firstLayer = layerInfos[0];
            var layersToMerge = layerInfos.Skip(1).Select(l => l.FullName).ToList();
            
            foreach (var id in bt)
            {
                foreach (var ent in GetEntities(id.GetObjectAs<BlockTableRecord>()))
                {
                    if (layersToMerge.Contains(ent.Layer))
                        ent.LayerId = firstLayer.Layer.Id;
                }
            }

            foreach (var layerInfo in layerInfos.Skip(1))
            {
                layerInfo.Layer.Erase(true);
            }

            firstLayer.Layer.Name = firstLayer.UniqLayerName;
        }

        tr.Commit();
    }

    private IEnumerable<Entity> GetEntities(BlockTableRecord btr)
    {
        foreach (var id in btr)
        {
            var ent = id.TryGetObjectAs<Entity>(true);
            if (ent != null)
            {
                yield return ent;
            }
        }
    }

    private class LayerInfo
    {
        public LayerInfo(LayerTableRecord layer)
        {
            Layer = layer;
            FullName = layer.Name;
            UniqLayerName = GetUniqName(layer.Name);
        }

        public LayerTableRecord Layer { get; }

        public string FullName { get; }

        public string UniqLayerName { get; }

        private string GetUniqName(string fullName)
        {
            var result = string.Empty;
            var split = fullName.Split('$');
            for (var i = 2; i < split.Length; i++)
            {
                result += split[i];
            }

            return result;
        }
    }
}
namespace mpRevitSheetsMerging.Services;

using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using ModPlus.Extensions;

public class SheetsImportService
{
    private readonly CopyModelSpaceService _copyModelSpaceService;
    private readonly CopyLayoutService _copyLayoutService;

    public SheetsImportService()
    {
        _copyModelSpaceService = new CopyModelSpaceService();
        _copyLayoutService = new CopyLayoutService();
    }

    /// <summary>
    /// Импортирует листы из файла dwg в текущий чертёж в стартовую точку - левый нижний угол.
    /// </summary>
    /// <param name="dwgFile">Полный путь к файлу</param>
    /// <param name="commonNamePart">Общая часть имен файлов</param>
    /// <param name="maxX">Максимальное значение X</param>
    public void ImportSheets(string dwgFile, string commonNamePart, ref double maxX)
    {
        // Открытие чертежа с импортируемыми листами
        using var importDb = new Database(false, true);
        importDb.CloseInput(true);
        importDb.ReadDwgFile(dwgFile, FileShare.ReadWrite, true, string.Empty);

        // Копирование модели импортируемого чертежа в текущий со сдвигом в стартовую точку
        _copyModelSpaceService.Copy(importDb, ref maxX, out var move, out var isEmptyModelSpace);

        // Копирование импортируемых листов
        using var layoutDic = importDb.LayoutDictionaryId.OpenAs<DBDictionary>();
        foreach (var importLayoutItem in layoutDic)
        {
            using var importLayout = importLayoutItem.Value.OpenAs<Layout>();
            if (importLayout.ModelType)
                continue;

            var newLayoutName = Path.GetFileNameWithoutExtension(dwgFile);
            if (!string.IsNullOrEmpty(commonNamePart))
                newLayoutName = newLayoutName.Replace(commonNamePart, string.Empty);
            if (string.IsNullOrEmpty(newLayoutName))
                newLayoutName = Path.GetFileNameWithoutExtension(dwgFile);
            _copyLayoutService.Copy(importLayout, newLayoutName, move, isEmptyModelSpace);
            break;
        }
    }
}
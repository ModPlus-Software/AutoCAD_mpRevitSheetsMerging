namespace mpRevitSheetsMerging.Services;

using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ModPlus.Extensions;

/// <summary>
/// Сервис копирования объектов модели
/// </summary>
public class CopyModelSpaceService
{
    /// <summary>
    /// Копирует объекты Модели из импортируемого чертежа в текущий в указанную точку
    /// </summary>
    /// <param name="importDb">Импортируемый файл</param>
    /// <param name="maxX">Стартовая точка - левый нижний угол</param>
    /// <param name="move">Возвращает вектор перемещения объектов</param>
    public void Copy(Database importDb, ref double maxX, out Vector3d move)
    {
        var curDoc = Application.DocumentManager.MdiActiveDocument;
        var curDb = curDoc.Database;
        using var curT = curDoc.TransactionManager.StartTransaction();

        using var importMs = importDb.Ms();
        var importMsIds = importMs.OfType<ObjectId>().Where(id => id.IsFullyValid()).ToArray();
        if (!importMsIds.Any())
        {
            move = new Vector3d(0, 0, 0);
            return;
        }

        var importIds = new ObjectIdCollection(importMsIds);

        var imageFileNames = new Dictionary<string, string>();
        foreach (ObjectId id in importIds)
        {
            if (id.TryOpenAs<RasterImage>() is { } rasterImage &&
                rasterImage.ImageDefId.TryOpenAs<RasterImageDef>() is { } rasterImageDef &&
                !imageFileNames.ContainsKey(rasterImageDef.SourceFileName))
                imageFileNames.Add(rasterImageDef.SourceFileName, rasterImageDef.ActiveFileName);
        }

        var curMsId = curDb.MsId();
        var map = new IdMapping();

        curDb.WblockCloneObjects(importIds, curMsId, map, DuplicateRecordCloning.Ignore, false);

        var copyIds = importMsIds.Select(importId => map[importId].Value).ToArray();

        FixImages(copyIds, imageFileNames);
        Move(importDb, copyIds, ref maxX, out move);
        
        curT.Commit();
    }

    private void Move(Database importDb, ObjectId[] copyIds, ref double maxX, out Vector3d move)
    {
        if (!importDb.TileMode)
            importDb.TileMode = true;

        move = new Vector3d(maxX - importDb.Extmin.X, 0, 0);

        var importModelLength = importDb.Extmax.X - importDb.Extmin.X;
        maxX += importModelLength + (importModelLength * 0.3);

        var moveMatrix = Matrix3d.Displacement(move);

        foreach (var id in copyIds)
        {
            var ent = id.GetObjectAs<Entity>(true);
            ent.TransformBy(moveMatrix);
        }
    }

    private void FixImages(ObjectId[] copyIds, Dictionary<string, string> imageFileNames)
    {
        foreach (var objectId in copyIds)
        {
            if (objectId.TryGetObjectAs<RasterImage>() is { } rasterImage)
            {
                var imageDef = rasterImage.ImageDefId.GetObjectAs<RasterImageDef>(true);
                if (!string.IsNullOrEmpty(imageDef.ActiveFileName))
                    continue;
                if (imageFileNames.TryGetValue(imageDef.SourceFileName, out var activeFileName))
                    imageDef.ActiveFileName = activeFileName;
            }
        }
    }
}
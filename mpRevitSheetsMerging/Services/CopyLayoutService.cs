namespace mpRevitSheetsMerging.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ModPlus.Extensions;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

/// <summary>
/// Сервис копирования листов
/// </summary>
public class CopyLayoutService
{
    private readonly List<string> _toReplaceSymbols = new () { ";", ",", "=", "`" };
    private readonly List<string> _thatReplaceSymbols = new () { ".", ".", "-", "'" };

    /// <summary>
    /// Копирует импортируемый лист в текущий чертёж и смещает нацеливание видовых экранов
    /// </summary>
    /// <param name="importLayout">Лист из импортируемого файла</param>
    /// <param name="newLayoutName">Имя нового листа</param>
    /// <param name="move">Вектор перемещения объектов</param>
    /// <param name="isEmptyModelSpace">Пространство модели пусто</param>
    public void Copy(Layout importLayout, string newLayoutName, Vector3d move, bool isEmptyModelSpace)
    {
        var newLayoutId = CreateLayout(importLayout, newLayoutName);

        CopyContent(newLayoutId, importLayout, move, isEmptyModelSpace);
    }

    private void CopyContent(ObjectId newLayoutId, Layout importLayout, Vector3d move, bool isEmptyModelSpace)
    {
        /*
         * Если в исходном листе в Ревите нет вставленных видовых экранов, то при экспорте создается видовой экран
         * с размерами как у основной надписи. Поэтому при копировании содержимого пространства модели получаем
         * значение аргумента isEmptyModelSpace и если оно true (т.е. в пространстве модели ничего не было), то
         * удаляем ненужный видовой экран. Для этого ищем блок в пространстве листа с наибольшей площадью, а затем
         * проверяем площадь видовых экранов - если совпадает, то удаляем видовой экран
         */

        var curDoc = AcApp.DocumentManager.MdiActiveDocument;
        var curDb = curDoc.Database;

        using var curT = curDoc.TransactionManager.StartTransaction();

        var newLayout = newLayoutId.GetObjectAs<Layout>();

        using var importLayoutBtr = importLayout.BlockTableRecordId.OpenAs<BlockTableRecord>();
        var importLayoutIds = importLayoutBtr.OfType<ObjectId>().Where(id => id.IsFullyValid()).ToList();
        if (importLayoutIds.Any())
        {
            var largestBlockReferenceArea = double.NaN;
            if (isEmptyModelSpace)
            {
                foreach (var id in importLayoutIds)
                {
                    using var blk = id.TryOpenAs<BlockReference>();
                    
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (blk == null || blk.GeometricExtents == null)
                        continue;
                    var area = Math.Abs(blk.GeometricExtents.MaxPoint.X - blk.GeometricExtents.MinPoint.X) *
                               Math.Abs(blk.GeometricExtents.MaxPoint.Y - blk.GeometricExtents.MinPoint.Y);

                    if (double.IsNaN(largestBlockReferenceArea) || area > largestBlockReferenceArea)
                        largestBlockReferenceArea = area;
                }
            }

            for (var i = importLayoutIds.Count - 1; i >= 0; i--)
            {
                using var vp = importLayoutIds[i].TryOpenAs<Viewport>();
                if (vp == null)
                    continue;

                if (Math.Abs(vp.Width - 12) < 0.1 &&
                    Math.Abs(vp.Height - 9) < 0.1 &&
                    vp.GeometricExtents.MinPoint.IsEqualTo(Point3d.Origin))
                {
                    importLayoutIds.RemoveAt(i);
                }
                else if (isEmptyModelSpace)
                {
                    var area = Math.Abs(vp.GeometricExtents.MaxPoint.X - vp.GeometricExtents.MinPoint.X) *
                               Math.Abs(vp.GeometricExtents.MaxPoint.Y - vp.GeometricExtents.MinPoint.Y);
                    if (!double.IsNaN(largestBlockReferenceArea) && Math.Abs(largestBlockReferenceArea - area) < 0.1)
                        importLayoutIds.RemoveAt(i);
                }
            }

            var importIds = new ObjectIdCollection(importLayoutIds.ToArray());
            var map = new IdMapping();

            curDb.WblockCloneObjects(
                importIds,
                newLayout.BlockTableRecordId,
                map,
                DuplicateRecordCloning.MangleName,
                false);

            MoveViewports(newLayout, move);
        }

        curT.Commit();
    }

    private ObjectId CreateLayout(Layout importLayout, string newLayoutName)
    {
        var curDoc = AcApp.DocumentManager.MdiActiveDocument;
        var curDb = curDoc.Database;

        using var curT = curDb.TransactionManager.StartTransaction();

        var layoutName = ReplaceSymbols(newLayoutName);
        var layoutDic = curDb.LayoutDictionaryId.GetObjectAs<DBDictionary>();
        var index = 0;

        while (layoutDic.Contains(layoutName))
            layoutName = $"{newLayoutName} {++index}";

        var newLayoutId = LayoutManager.Current.CreateLayout(layoutName);
        var newLayout = newLayoutId.GetObjectAs<Layout>(true);
        newLayout.CopyFrom(importLayout);
        LayoutManager.Current.CurrentLayout = newLayout.LayoutName;
        foreach (ObjectId id in newLayout.GetViewports())
            id.GetObjectAs<Viewport>(true).Erase(true);

        curT.Commit();

        return newLayoutId;
    }

    private void MoveViewports(Layout newLayout, Vector3d move)
    {
        var move2d = new Vector2d(move.X, move.Y);
        var layoutBtr = newLayout.BlockTableRecordId.GetObjectAs<BlockTableRecord>();
        foreach (var vp in layoutBtr.GetObjects<Viewport>(true))
            vp.ViewCenter += move2d;
    }

    private string ReplaceSymbols(string newLayoutName)
    {
        for (var i = 0; i < _toReplaceSymbols.Count; i++)
        {
            newLayoutName = newLayoutName.Replace(_toReplaceSymbols[i], _thatReplaceSymbols[i]);
        }

        return newLayoutName;
    }
}
namespace mpRevitSheetsMerging.Extensions;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

/// <summary>
/// Расширения для <see cref="ObjectId"/>
/// </summary>
public static class ObjectIdExtensions
{
    /// <summary>
    /// Returns an object opened without using a transaction and cast to the given type
    /// </summary>
    /// <param name="id">Identifier</param>
    /// <param name="forWrite">Open for writing</param>
    /// <param name="forceOpenOnLockedLayer">Open even if the object is on a frozen layer</param>
    /// <typeparam name="T">Object type</typeparam>
    /// <exception cref="Exception">If the object does not match the specified type</exception>
    public static T OpenAs<T>(
        this ObjectId id,
        bool forWrite = false,
        bool forceOpenOnLockedLayer = true)
        where T : DBObject
    {
#pragma warning disable 618
        return (T)id.Open(
            forWrite ? OpenMode.ForWrite : OpenMode.ForRead,
            false,
            forceOpenOnLockedLayer);
#pragma warning restore 618
    }

    /// <summary>
    /// Returns an object opened without using a transaction and cast to the given type
    /// </summary>
    /// <param name="id">Identifier</param>
    /// <param name="forWrite">Open for writing</param>
    /// <param name="forceOpenOnLockedLayer">Open even if the object is on a frozen layer</param>
    /// <typeparam name="T">Object type</typeparam>
    /// <exception cref="Exception">If the object does not match the specified type</exception>
    public static T? TryOpenAs<T>(
        this ObjectId id,
        bool forWrite = false,
        bool forceOpenOnLockedLayer = true)
        where T : DBObject
    {
        if (!id.IsFullyValid())
            return null;

#pragma warning disable 618
        return id.Open(
            forWrite ? OpenMode.ForWrite : OpenMode.ForRead,
            false,
            forceOpenOnLockedLayer) as T;
#pragma warning restore 618
    }

    /// <summary>
    /// Открытие объекта через транзакцию, когда точно известен тип объекта.
    /// </summary>
    /// <param name="id">Id</param>
    /// <param name="forWrite">Открыть для записи</param>
    /// <param name="onLockedLayer">На заблокированном слое</param>
    /// <typeparam name="T">Тип</typeparam>
    public static T GetObjectAs<T>(
        this ObjectId id,
        bool forWrite = false,
        bool onLockedLayer = true)
        where T : DBObject
    {
        var mode = forWrite ? OpenMode.ForWrite : OpenMode.ForRead;
        return (T)id.GetObject(mode, false, onLockedLayer);
    }

    /// <summary>
    /// Открытие объекта через транзакцию, когда точно не известен тип объекта.
    /// </summary>
    /// <param name="id">Id</param>
    /// <param name="forWrite">Открыть для записи</param>
    /// <param name="forceOpenOnLockedLayer">Открыть, даже если объект находится на замороженном слое</param>
    /// <typeparam name="T">Тип объекта</typeparam>
    public static T? TryGetObjectAs<T>(
        this ObjectId id,
        bool forWrite = false,
        bool forceOpenOnLockedLayer = true)
        where T : DBObject
    {
        if (!id.IsFullyValid())
            return null;

        return id.GetObject(
            forWrite ? OpenMode.ForWrite : OpenMode.ForRead,
            false,
            forceOpenOnLockedLayer) as T;
    }

    /// <summary>
    /// Открытие объектов
    /// </summary>
    /// <param name="ids">Id объектов</param>
    /// <param name="forWrite">Открыть для записи</param>
    /// <param name="onLockedLayer">На заблокированном слое</param>
    /// <typeparam name="T">Тип</typeparam>
    public static IEnumerable<T> GetObjects<T>(
        this IEnumerable ids,
        bool forWrite = false,
        bool onLockedLayer = true)
        where T : DBObject
    {
        return ids
            .Cast<ObjectId>()
            .Select(id => id.TryGetObjectAs<T>(forWrite, onLockedLayer))
            .Where(res => res != null)!;
    }

    /// <summary>
    /// Возвращает истину, если идентификатор объекта полностью валидный: объект есть в базе и он не удалён
    /// </summary>
    /// <param name="id">Идентификатор</param>
    public static bool IsFullyValid(this ObjectId id) =>
        id.IsValid && !id.IsNull && !id.IsErased && !id.IsEffectivelyErased;
}
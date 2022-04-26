namespace mpRevitSheetsMerging.Extensions;

using Autodesk.AutoCAD.DatabaseServices;

/// <summary>
/// Расширения для <see cref="Database"/>
/// </summary>
public static class DatabaseExtensions
{
    public static ObjectId MsId(this Database db)
    {
        return SymbolUtilityServices.GetBlockModelSpaceId(db);
    }

    public static BlockTableRecord Ms(this Database db, bool write = false)
    {
        return MsId(db).OpenAs<BlockTableRecord>(write);
    }
}
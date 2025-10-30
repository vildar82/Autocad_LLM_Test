using Autodesk.AutoCAD.DatabaseServices;

namespace AutocadMcpPlugin;

public static class BlockTableRecordExtensions
{
    public static ObjectId AppendNewly(this BlockTableRecord block, Entity entity)
    {
        var id = block.AppendEntity(entity);
        if (block.Database.TransactionManager.TopTransaction is { } t)
            t.AddNewlyCreatedDBObject(entity, true);
        return id;
    }
}
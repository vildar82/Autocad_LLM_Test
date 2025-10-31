using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;

namespace AutocadLlmPlugin;

public static class DatabaseExtensions
{
    public static bool TryGetObjectId(this Database database, string id, out ObjectId objectId, out string error)
    {
        objectId = ObjectId.Null;
        error = string.Empty;

        if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            error = $"Некорректный идентификатор объекта: {id}.";
            return false;
        }

        try
        {
            var handle = new Handle(value);
            objectId = database.GetObjectId(false, handle, 0);
            if (objectId == ObjectId.Null)
            {
                error = $"Объект с Id {id} не найден.";
                return false;
            }

            return true;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            error = $"Объект с Id {id} не найден.";
            return false;
        }
    }
}
using System.Numerics;

namespace Impostor.Api.Innersloth.Maps;

public sealed class TaskData
{
    internal TaskData(int id, TaskTypes type, TaskCategories category, Vector2[] position, bool isVisual = false)
    {
        Id = id;
        Name = id.ToString();
        Type = type;
        Category = category;
        IsVisual = isVisual;
        Position = position;
    }

    public int Id { get; }

    public string Name { get; }

    public TaskTypes Type { get; }

    public TaskCategories Category { get; }

    public Vector2[] Position { get; }

    public bool IsVisual { get; }
}

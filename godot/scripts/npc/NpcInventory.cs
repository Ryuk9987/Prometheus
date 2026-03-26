#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// NPC personal inventory — what they're carrying.
/// Used for resource gathering, crafting, building.
/// </summary>
public partial class NpcInventory : Node
{
    private readonly Dictionary<ResourceType, float> _items = new();
    public IReadOnlyDictionary<ResourceType, float> Items => _items;

    public float Get(ResourceType r) => _items.TryGetValue(r, out float v) ? v : 0f;

    public void Add(ResourceType r, float amount)
    {
        if (!_items.ContainsKey(r)) _items[r] = 0f;
        _items[r] += amount;
    }

    public bool Remove(ResourceType r, float amount)
    {
        if (Get(r) < amount) return false;
        _items[r] -= amount;
        if (_items[r] <= 0f) _items.Remove(r);
        return true;
    }

    public bool Has(ResourceType r, float amount) => Get(r) >= amount;

    public float TotalWeight => _items.Values.Sum();

    public string Summary()
    {
        if (_items.Count == 0) return "Leer";
        return string.Join(", ", _items.Select(kv => $"{kv.Value:F1}x {kv.Key}"));
    }
}

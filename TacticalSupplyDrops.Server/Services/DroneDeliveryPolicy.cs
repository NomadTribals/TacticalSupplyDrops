namespace TacticalSupplyDrops.Server.Services;

public static class DroneDeliveryPolicy
{
    public const string ParcelTemplateId = "6a0000000000000000000030";
    public const int Capacity = 12;
    public const decimal MaximumWeightKg = 5m;

    public static string? Validate(IEnumerable<StashItemDetails> items, int footprint)
    {
        items = items.ToArray();
        if (items.Sum(item => (decimal)item.UnitWeight * item.Quantity) > MaximumWeightKg)
            return "Drone payload exceeds 5 kg. Reduce cargo or choose Standard Cargo.";
        if (footprint > Capacity) return "Drone parcel full: use at most 12 cargo cells or choose Standard Cargo.";
        foreach (var item in items)
        {
            if (item.Category is not ("AMMO" or "MEDICAL" or "FOOD"))
                return $"{item.Name} is not supported by Drone Delivery. Choose Standard Cargo.";
            if (!((item.Width <= 4 && item.Height <= 3) || (item.Width <= 3 && item.Height <= 4)))
                return $"{item.Name} is too large for the 4 x 3 drone parcel. Choose Standard Cargo.";
        }
        if (Pack(items.Select(item => (item.Width, item.Height)).ToArray()) == null)
            return "These items do not fit together in the 4 x 3 parcel. Reduce cargo or choose Standard Cargo.";
        return null;
    }

    public static (int X, int Y, bool Rotated)[]? Pack((int Width, int Height)[] sizes)
    {
        if (sizes.Any(size => size.Width <= 0 || size.Height <= 0) || sizes.Sum(size => (long)size.Width * size.Height) > Capacity) return null;
        var occupied = new bool[4, 3];
        var placements = new (int X, int Y, bool Rotated)[sizes.Length];
        bool Place(int index)
        {
            if (index == sizes.Length) return true;
            for (int rotation = 0; rotation < 2; rotation++)
            {
                int width = rotation == 0 ? sizes[index].Width : sizes[index].Height;
                int height = rotation == 0 ? sizes[index].Height : sizes[index].Width;
                if (width <= 0 || height <= 0) return false;
                for (int y = 0; y <= 3 - height; y++)
                for (int x = 0; x <= 4 - width; x++)
                {
                    bool free = true;
                    for (int yy = y; yy < y + height; yy++)
                    for (int xx = x; xx < x + width; xx++) if (occupied[xx, yy]) free = false;
                    if (!free) continue;
                    for (int yy = y; yy < y + height; yy++)
                    for (int xx = x; xx < x + width; xx++) occupied[xx, yy] = true;
                    placements[index] = (x, y, rotation == 1);
                    if (Place(index + 1)) return true;
                    for (int yy = y; yy < y + height; yy++)
                    for (int xx = x; xx < x + width; xx++) occupied[xx, yy] = false;
                }
            }
            return false;
        }
        return Place(0) ? placements : null;
    }
}

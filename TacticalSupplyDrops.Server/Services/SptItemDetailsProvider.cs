
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Locales;
using System.Linq;

namespace TacticalSupplyDrops.Server.Services;

[Injectable]
public class SptItemDetailsProvider(
    ItemHelper itemHelper,
    LocaleService localeService,
    ModItemIconResolver iconResolver) : IItemDetailsProvider
{
    public (string Name, int BaseValue, int Width, int Height, string IconUrl, string Category) GetDetails(Item item)
    {
        var template = itemHelper.GetItem(item.Template);
        var properties = template.Value?.Properties;

        string name = properties?.Name ?? "Unknown Item";

        var englishLocale = localeService.GetLocaleDb("en");

        string localizedNameKey = $"{item.Template} Name";

        if (englishLocale.TryGetValue(localizedNameKey, out string? localizedName) &&
            !string.IsNullOrWhiteSpace(localizedName))
        {
            name = localizedName;
        }

        int baseValue = (int)(itemHelper.GetItemPrice(item.Template) ?? 0);

        int width = properties?.Width ?? 1;
        int height = properties?.Height ?? 1;

        string iconTemplateId =
            iconResolver.GetIconTemplateId(item.Template);

        string iconUrl =
            $"https://assets.tarkov.dev/{iconTemplateId}-icon.webp";

        string category = GetCategory(item);

        return (name, baseValue, width, height, iconUrl, category);
    }

    public bool IsInstalledInEquipmentSlot(Item item, Item? parent)
    {
        if (parent == null || string.IsNullOrWhiteSpace(item.SlotId))
        {
            return false;
        }

        var parentTemplate = itemHelper.GetItem(parent.Template);

        var parentSlots = parentTemplate.Value?.Properties?.Slots;

        if (parentSlots == null)
        {
            return false;
        }

        return parentSlots.Any(slot =>
            !string.IsNullOrWhiteSpace(slot.Name) &&
            slot.Name == item.SlotId);
    }

    public double GetUnitWeight(Item item) => Math.Max(0, itemHelper.GetItem(item.Template).Value?.Properties?.Weight ?? 0);

    public bool IsLooseAmmo(Item item) => itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO) && !itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO_BOX);

    private string GetCategory(Item item)
    {
        if (
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO_BOX)
        )
        {
            return "AMMO";
        }

        if (
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MEDICAL) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MEDS) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MED_KIT) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MEDICAL_SUPPLIES) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.STIMULATOR) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.DRUGS)
        )
        {
            return "MEDICAL";
        }

        if (
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.FOOD) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.DRINK) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.FOOD_DRINK)
        )
        {
            return "FOOD";
        }

        if (itemHelper.IsOfBaseclass(item.Template, BaseClasses.WEAPON))
        {
            return "WEAPONS";
        }

        if (
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.ARMOR) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.ARMOR_PLATE) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.ARMORED_EQUIPMENT)
        )
        {
            return "ARMOR";
        }

        if (
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.LOOT_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MOB_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.SIMPLE_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.LOCKABLE_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.RANDOM_LOOT_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.STATIONARY_CONTAINER) ||
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.HIDEOUT_AREA_CONTAINER)
        )
        {
            return "CONTAINERS";
        }

        return "OTHER";
    }
}

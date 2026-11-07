
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace TacticalSupplyDrops.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } =
        "com.nomadtribals.tacticalsupplydrops";

    public string Name { get; init; } =
        "TacticalSupplyDrops";

    public string Author { get; init; } =
        "nomadtribals";

    public List<string>? Contributors { get; init; }

    public SemanticVersioning.Version Version { get; init; } =
        new("2.0.1");

    public SemanticVersioning.Range SptVersion { get; init; } =
        new("4.1.6");

    public List<string>? Incompatibilities { get; init; }

    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }

    public string? Url { get; init; }

    public string License { get; init; } = "MIT";

    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public class TacticalSupplyDropsMod(
    ISptLogger<TacticalSupplyDropsMod> logger,
    TemplateTable templateTable,
    CustomItemService customItemService,
    TradersTable tradersTable) : IOnLoad
{
    private const string VanillaRedFlareId =
        "62389ba9a63f32501b1b4451";

    private const string TsdOrangeFlareId =
        "6a0000000000000000000001";

    private const string TsdFieldTerminalId =
        "6a0000000000000000000002";

    private const string TsdFieldTerminalOfferId =
        "6a0000000000000000000003";

    private const string MechanicTraderId =
        "5a7c2eca46aef81a7ca2145d";

    private const string SpecialItemBaseId =
        "5447e0e74bdc2d3c308b4567";

    private const string RadioTransmitterBodyId =
        "6399f54b0a36db13c823ad21";

    private const string RoublesId =
        "5449016a4bdc2d6f028b456f";

    private const string TsdAirdropLootId =
        "TSD_ACTIVE_ORDER";

    private const string AmmoParentId =
        "5485a8684bdc2da71d8b4567";

    private const string Sp81TemplateId =
        "620109578d82e67e7911abf2";

    private const string Sp81ChamberName =
        "patron_in_weapon";

    private const string HandbookParentId =
        "5b47574386f77428ca22b33b";

    private const double FlareHandbookPrice =
        30000;

    private const double FlareFleaPrice =
        30000;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success(
            "Tactical Supply Drops has loaded successfully!");

        // Register the legacy orange flare and terminal independently.
        if (RegisterOrangeFlare())
        {
            AddOrangeFlareToSp81();
        }

        RegisterFieldTerminal();

        return Task.CompletedTask;
    }

    private bool RegisterFieldTerminal()
    {
        MongoId terminalId = new(TsdFieldTerminalId);

        if (!templateTable.Items.ContainsKey(terminalId))
        {
            if (!templateTable.Items.ContainsKey(
                new MongoId(RadioTransmitterBodyId)))
            {
                logger.Error(
                    "[TSD] Cannot register the field terminal. " +
                    "The vanilla radio transmitter template was not found.");

                return false;
            }

            var itemDetails = new NewItemFromCloneDetails
            {
                ItemTplToClone = new MongoId(RadioTransmitterBodyId),
                NewId = terminalId,
                // The terminal is carried for F10 access; it has no radio hand animation.
                ParentId = new MongoId(SpecialItemBaseId),
                NewItemName = "tsd_field_terminal",
                AddToHandbook = true,
                AddToFleaPriceDb = false,
                AddToWeaponShelf = false,
                HandbookParentId = HandbookParentId,
                HandbookPriceRoubles = 75000,
                OverrideProperties = new TemplateItemProperties
                {
                    Name = "TSD Field Terminal",
                    ShortName = "TSD Terminal",
                    Description =
                        "A hardened, sealed logistics terminal issued by " +
                        "Tactical Supply Drops. It authenticates secure " +
                        "cargo orders and coordinates field delivery. " +
                        "Carry it to establish a connection in raid.",
                    // The transmitter body is a quest item; the terminal must be carryable.
                    QuestItem = false,
                    IsSpecialSlotOnly = false,
                    UnlootableFromSlot = "SpecialSlot",
                    CanSellOnRagfair = false,
                    Prefab = new Prefab
                    {
                        Path = "assets/content/items/equipment/tsd_field_terminal/tsd_field_terminal.bundle",
                        Rcid = ""
                    }
                },
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name = "TSD Field Terminal",
                        ShortName = "TSD Terminal",
                        Description =
                            "A hardened field terminal for secure TSD " +
                            "logistics access and cargo dispatch."
                    }
                }
            };

            var result = customItemService.CreateItemFromClone(
                itemDetails,
                typeof(TacticalSupplyDropsMod).Assembly);

            if (!result.Success)
            {
                logger.Error(
                    "[TSD] Field terminal registration failed: " +
                    string.Join("; ", result.Errors));

                return false;
            }
        }

        AddFieldTerminalToSpecialSlots();
        AddFieldTerminalToMechanic();
        return true;
    }

    private void AddFieldTerminalToSpecialSlots()
    {
        MongoId terminalId = new(TsdFieldTerminalId);
        int patched = 0;
        foreach (var template in templateTable.Items.Values)
        {
            var slots = template.Properties?.Slots;
            if (slots == null) continue;
            foreach (var slot in slots)
            {
                if (slot.Name is not ("SpecialSlot1" or "SpecialSlot2" or "SpecialSlot3"))
                    continue;
                var filters = slot.Properties?.Filters;
                if (filters == null) continue;
                foreach (var filter in filters)
                {
                    // Keep the eligibility previously inherited from the radio category.
                    if (filter?.Filter?.Contains(new MongoId("62e9103049c018f425059f38")) == true
                        && filter.Filter.Add(terminalId)) patched++;
                }
            }
        }
        logger.Info($"[TSD] Field terminal enabled in {patched} special-slot filters.");
    }

    private void AddFieldTerminalToMechanic()
    {
        MongoId mechanicId = new(MechanicTraderId);
        MongoId offerId = new(TsdFieldTerminalOfferId);

        if (!tradersTable.TryGetValue(mechanicId, out var mechanic) ||
            mechanic?.Assort == null)
        {
            logger.Error(
                "[TSD] Could not add the field terminal to Mechanic: " +
                "his trader assortment was not available during mod loading.");

            return;
        }

        var assort = mechanic.Assort;
        if (assort.Items == null)
        {
            logger.Error(
                "[TSD] Mechanic's assortment has no item list.");

            return;
        }

        if (!assort.Items.Any(item => item.Id == offerId))
        {
            assort.Items.Add(new Item
            {
                Id = offerId,
                Template = new MongoId(TsdFieldTerminalId),
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 9999999
                }
            });
        }

        assort.BarterScheme[offerId] = new List<List<BarterScheme>>
        {
            new()
            {
                new BarterScheme
                {
                    Count = 75000,
                    Template = new MongoId(RoublesId)
                }
            }
        };
        assort.LoyalLevelItems[offerId] = 1;

        logger.Success(
            "[TSD] TSD Field Terminal added to Mechanic at Loyalty Level 1 " +
            "for 75,000 roubles. It fits the Special slot but does not " +
            "require it.");
    }

    private bool RegisterOrangeFlare()
    {
        MongoId newFlareId = new(TsdOrangeFlareId);

        // If the item already exists, it is registered.
        if (templateTable.Items.ContainsKey(newFlareId))
        {
            logger.Warning(
                "[TSD] Orange flare ID already exists. " +
                "Registration skipped: " + TsdOrangeFlareId);

            return true;
        }

        // Confirm that the vanilla source item exists.
        if (!templateTable.Items.ContainsKey(
            new MongoId(VanillaRedFlareId)))
        {
            logger.Error(
                "[TSD] Cannot register orange flare. " +
                "Vanilla red flare template was not found.");

            return false;
        }

        var itemDetails = new NewItemFromCloneDetails
        {
            ItemTplToClone = new MongoId(VanillaRedFlareId),

            NewId = newFlareId,

            ParentId = new MongoId(AmmoParentId),

            NewItemName = "tsd_patron_26x75_orange",

            // Include the flare in the handbook.
            AddToHandbook = true,

            // Register its Flea Market reference price.
            AddToFleaPriceDb = true,

            // Match the vanilla red flare's handbook classification.
            HandbookParentId = HandbookParentId,

            // Match the vanilla red flare's 30,000 rouble price.
            HandbookPriceRoubles = FlareHandbookPrice,

            FleaPriceRoubles = FlareFleaPrice,

            // This is ammunition, not a weapon.
            AddToWeaponShelf = false,

            // English localization.
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "Tactical Supply Drops Orange Flare",
                    ShortName = "TSD Orange Flare",
                    Description =
                        "A specialized orange signal flare used " +
                        "to initiate Tactical Supply Drops."
                }
            }
        };

        try
        {
            var result = customItemService.CreateItemFromClone(
                itemDetails,
                typeof(TacticalSupplyDropsMod).Assembly);

            if (!result.Success)
            {
                logger.Error(
                    "[TSD] Orange flare registration failed: " +
                    string.Join("; ", result.Errors));

                return false;
            }

            if (templateTable.Items.TryGetValue(
                new MongoId(VanillaRedFlareId),
                out var vanillaFlare))
            {
                logger.Info(
                    "[TSD] Vanilla red flare AirDropTemplateId: " +
                    (vanillaFlare.Properties?.AirDropTemplateId ?? "NULL"));
            }

            if (templateTable.Items.TryGetValue(
                newFlareId,
                out var registeredFlare))
            {
                registeredFlare.Properties.AirDropTemplateId =
                    TsdAirdropLootId;

                logger.Info(
                    "[TSD] Orange flare AirDropTemplateId: " +
                    (registeredFlare.Properties?.AirDropTemplateId ?? "NULL"));

                logger.Success(
                    "[TSD] Orange flare successfully registered! " +
                    "Name: " + registeredFlare.Name +
                    " | ID: " + TsdOrangeFlareId);

                logger.Success(
                    "[TSD] Orange flare pricing registered. " +
                    "Handbook: 30000 | Flea reference: 30000");

                return true;
            }

            logger.Error(
                "[TSD] Registration returned success, " +
                "but the item could not be found in TemplateTable.");

            return false;
        }
        catch (Exception ex)
        {
            logger.Error(
                "[TSD] Exception during orange flare registration: " +
                ex);

            return false;
        }
    }

    private void AddOrangeFlareToSp81()
    {
        try
        {
            MongoId weaponId = new(Sp81TemplateId);
            MongoId flareId = new(TsdOrangeFlareId);

            // Find the vanilla SP-81 weapon template.
            if (!templateTable.Items.TryGetValue(
                weaponId,
                out var sp81))
            {
                logger.Error(
                    "[TSD] Cannot patch SP-81 compatibility. " +
                    "Weapon template was not found.");

                return;
            }

            // Find the SP-81's ammunition chamber.
            var chamber = sp81.Properties?.Chambers?
                .FirstOrDefault(slot =>
                    string.Equals(
                        slot.Name,
                        Sp81ChamberName,
                        StringComparison.OrdinalIgnoreCase));

            if (chamber == null)
            {
                logger.Error(
                    "[TSD] Cannot patch SP-81 compatibility. " +
                    "The patron_in_weapon chamber was not found.");

                return;
            }

            // Find the chamber's existing ammunition filter.
            var filter = chamber.Properties?.Filters?
                .FirstOrDefault(existingFilter =>
                    existingFilter?.Filter != null);

            if (filter?.Filter == null)
            {
                logger.Error(
                    "[TSD] Cannot patch SP-81 compatibility. " +
                    "The chamber's ammunition filter was not found.");

                return;
            }

            // Add our custom flare without removing vanilla ammo.
            bool added = filter.Filter.Add(flareId);

            if (added)
            {
                logger.Success(
                    "[TSD] Orange flare added to SP-81 ammunition filter! " +
                    "Flare ID: " + TsdOrangeFlareId);
            }
            else
            {
                logger.Warning(
                    "[TSD] Orange flare was already present in the " +
                    "SP-81 ammunition filter.");
            }
        }
        catch (Exception ex)
        {
            logger.Error(
                "[TSD] Exception while patching SP-81 compatibility: " +
                ex);
        }
    }
}

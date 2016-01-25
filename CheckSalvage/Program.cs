using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using GW2Miner.Engine;
using GW2Miner.Domain;

namespace CheckSalvage
{
    public static class Program
    {
        struct resultRecord {
            public int Id;
            public string Name;
            public string upgradeName;
            public int MinSaleUnitPrice;
            public int UpgradeMinSaleUnitPrice;
            public double UpgradeAvgSaleUnitPrice;
            public RarityEnum RarityId;
            public int Level;
            public int OfferPrice;
            public double SalePrice;
            public double Profit;
            public double InstantProfit;
        }

        const int MAXBUY = 10;

        static TradeWorker trader = new TradeWorker();
        static double BLSalvageCost = 0;
        static bool auto = false;
        static bool freeBLSalvage = false;
        static int maxBuy = MAXBUY;

        static void ListViableRuneSigilItems(string suffix, Item upgrade, int ectoPrice, List<Item> itemList, List<resultRecord> result, TypeEnum type, bool nullUpgrade = false)
        {
            double chanceToGetEcto = 0.0;  // assume no ectos
            double chanceToGetUpgrade = 1.0;   // assume BLSK with 100% obtaining the upgrade per salvage
            double chanceToGetInsignia = 0.61; // assume using BLSK

            // Uncomment to check against average price of the upgrade, counter market manipulation
            //double averageUpgradeSellPrice = trader.monthlySellAverage(upgrade.Id);
            double averageUpgradeSellPrice = upgrade.MinSaleUnitPrice;

            foreach (Item item in itemList)
            {
                //if (item.Name.Split(' ').First() == "Soldier's")
                //{
                //}
                if (item.SellCount <= 0 || item.RarityId != upgrade.RarityId) continue;

                gw2apiItem apiItem = trader.GetGW2APIItem(item.Id);

                if (apiItem != null && (apiItem.Flags & (GW2APIFlagsEnum.No_Salvage)) != 0) continue;

                if (!nullUpgrade && apiItem != null && ((apiItem.TypeId == TypeEnum.Armor && apiItem.Armor.UpgradeId != upgrade.Id) ||
                                        (apiItem.TypeId == TypeEnum.Weapon && apiItem.Weapon.UpgradeId != upgrade.Id))) continue;

                if (nullUpgrade && apiItem != null && ((apiItem.TypeId == TypeEnum.Armor && apiItem.Armor.UpgradeId != null) ||
                                        (apiItem.TypeId == TypeEnum.Weapon && apiItem.Weapon.UpgradeId != null))) continue;

                if (!nullUpgrade && apiItem == null && !trader.Match(suffix, upgrade.Id, item.Name)) continue;

                int upgradeSalePrice = 1;

                if (!nullUpgrade)
                {
                    upgradeSalePrice = upgrade.MinSaleUnitPrice;
                }

                double salePrice = (upgradeSalePrice - 1) * 0.85 * chanceToGetUpgrade;  // 0.85 is to account for TP taxes

                salePrice = salePrice - BLSalvageCost;

                double insigniaPrice = 0.0;

                if (item.RarityId >= RarityEnum.Exotic)
                {
                    insigniaPrice = trader.InsigniaPrice(item.Name, type) * chanceToGetInsignia;
                }

                int offerPrice = item.MaxOfferUnitPrice + 1;

                if (offerPrice == 1)
                {
                    offerPrice = item.MinSaleUnitPrice;
                }

                double profit = salePrice - offerPrice + insigniaPrice + (chanceToGetEcto * ectoPrice);

                if (profit > 0.0 && item.RarityId >= RarityEnum.Rare && item.MinLevel >= 68) // we are only interested in these kinds of items for ectos that may result from the salvage
                {
                    resultRecord record = new resultRecord();
                    record.Id = item.Id;
                    record.Name = item.Name;
                    record.upgradeName = upgrade.Name;
                    record.UpgradeMinSaleUnitPrice = upgrade.MinSaleUnitPrice;
                    record.UpgradeAvgSaleUnitPrice = averageUpgradeSellPrice;
                    record.MinSaleUnitPrice = item.MinSaleUnitPrice;
                    record.RarityId = item.RarityId;
                    record.Level = item.MinLevel;
                    record.OfferPrice = offerPrice;
                    record.SalePrice = salePrice + insigniaPrice;
                    record.Profit = profit;
                    record.InstantProfit = salePrice - item.MinSaleUnitPrice + insigniaPrice;

                    result.Add(record);
                }
            }
        }

        static void IterateRunesSigils(List<Item> armorsCollection, List<Item> weaponsCollection, int ectoPrice, List<resultRecord> result, RarityEnum rarity)
        {
            List<Item> upgradesCollection = trader.search_items("", true, TypeEnum.Upgrade_Component, (int)UpgradeComponentSubTypeEnum.Rune, rarity).Result;
            List<Item> sigilsCollection = trader.search_items("", true, TypeEnum.Upgrade_Component, (int)UpgradeComponentSubTypeEnum.Sigil, rarity).Result;
            upgradesCollection.AddRange(sigilsCollection);

            foreach(Item upgrade in upgradesCollection)
            {
                //if (upgrade.BuyCount < 1.5 * upgrade.SellCount) continue;

                string name = upgrade.Name;

                if (upgrade.BuyCount <= 0 || (name.IndexOf("Rune") < 0 && name.IndexOf("Sigil") < 0)) continue;
               
                string[] words = name.Split(' ');
                string[] wordsTransformed = new string[words.Length - 2];
                Array.Copy(words, 2, wordsTransformed, 0, wordsTransformed.Length);
                string suffix = string.Join(" ", wordsTransformed);

                if (name.IndexOf("Rune") >= 0)
                {
                    ListViableRuneSigilItems(suffix, upgrade, ectoPrice, armorsCollection, result, TypeEnum.Armor);
                }
                else
                {
                    ListViableRuneSigilItems(suffix, upgrade, ectoPrice, weaponsCollection, result, TypeEnum.Weapon);
                }
            }

            // Note: Commented out because craftable exotics (without suffixes) do not yield inscriptions
            // Create null upgrades and call ListViableRuneSigilItems again for items without suffixes
            //Item upgrade1 = new Item();
            //upgrade1.RarityId = rarity;

            // TODO: Consider items with no suffixes?
            //ListViableRuneSigilItems(String.Empty, upgrade1, ectoPrice, armorsCollection, result, TypeEnum.Armor, true);
            //ListViableRuneSigilItems(String.Empty, upgrade1, ectoPrice, weaponsCollection, result, TypeEnum.Weapon, true);
        }

        static List<resultRecord> ViableSalvageItems(RarityEnum rarity, int ectoPrice)
        {
            List<Item> armorsCollection = trader.search_items("", true, TypeEnum.Armor, -1, rarity, 68).Result; // we are only interested in these kinds of items for ectos that may result from the salvage
            List<Item> weaponsCollection = trader.search_items("", true, TypeEnum.Weapon, -1, rarity, 68).Result; // we are only interested in these kinds of items for ectos that may result from the salvage

            List<resultRecord> result = new List<resultRecord>();

            IterateRunesSigils(armorsCollection, weaponsCollection, ectoPrice, result, rarity);

            return result;
        }

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                maxBuy = MAXBUY;
                if (int.TryParse(args[0], out maxBuy))
                {
                    Console.WriteLine("Automatic mode activated!...");
                    auto = true;
                }
                else
                {
                    freeBLSalvage = true;
                }
            }

            try
            {
                Task<List<Item>> itemList = trader.get_items(19721);
                int ectoPrice = ((Item)itemList.Result[0]).MinSaleUnitPrice;
                Console.WriteLine("Ecto price: {0}", ectoPrice);

                BLSalvageCost = freeBLSalvage ? 0.0 : trader.BlackLionKitSalvageCost;
                //BLSalvageCost = 0.0;
                Console.WriteLine("BL Salvage Cost: {0}", BLSalvageCost);

                List<resultRecord> result = ViableSalvageItems(RarityEnum.Exotic, ectoPrice);  // assume only exotics

                //if (result.Count == 0)
                //{
                //    result = ViableSalvageItems(RarityEnum.Rare, ectoPrice);
                //}

                result.Sort(delegate(resultRecord r1, resultRecord r2) { return r1.Profit.CompareTo(r2.Profit); });

                foreach (resultRecord rec in result)
                {
                    //if (rec.InstantProfit > 0.0)
                    if (rec.InstantProfit > BLSalvageCost)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;

                        Console.WriteLine("{0}: {1}({2})[{7}] Profit: {3} Instant: {4} Offer: {5} Sale: {6} {8}", rec.Id, rec.Name, rec.Level, rec.Profit, rec.InstantProfit,
                            rec.OfferPrice, rec.SalePrice, rec.upgradeName, ((rec.UpgradeMinSaleUnitPrice - rec.UpgradeAvgSaleUnitPrice) > 10000 ? "*" : ""));

                        Console.ResetColor();
                        continue;
                    }
                    //else if (rec.Profit > BLSalvageCost)
                    //{
                    //    Console.ForegroundColor = ConsoleColor.Yellow;
                    //}

                    Console.WriteLine("{0}: {1}({2})[{6}] Profit: {3} Offer: {4} Sale: {5} {7}", rec.Id, rec.Name, rec.Level, rec.Profit,
                        rec.OfferPrice, rec.SalePrice, rec.upgradeName, ((rec.UpgradeMinSaleUnitPrice - rec.UpgradeAvgSaleUnitPrice) > 10000 ? "*" : ""));

                    Console.ResetColor();
                }

                if (auto)
                {
                    Console.WriteLine("Automatic buying of the top {0} most profitable of the items...", maxBuy);
                    result.Reverse();
                    int i = 0;
                    foreach (resultRecord rec in result)
                    {
                        i++;
                        trader.Buy(rec.Id, 1, rec.OfferPrice).Wait();
                        if (i >= maxBuy) break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(ExceptionHelper.FlattenException(e));
            }

            Console.WriteLine("Hit ENTER to exit...");
            Console.ReadLine();
        }
    }
}

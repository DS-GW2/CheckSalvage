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
            public int MinSaleUnitPrice;
            public RarityEnum RarityId;
            public int Level;
            public int OfferPrice;
            public double SalePrice;
            public double Profit;
            public double InstantProfit;
        }

        static TradeWorker trader = new TradeWorker();
        static double BLSalvageCost = 0;

        static void ListViableRuneSigilItems(string suffix, Item upgrade, int ectoPrice, List<Item> itemList, List<resultRecord> result)
        {
            foreach (Item item in itemList)
            {
                if (item.SellCount <= 0 || item.RarityId != upgrade.RarityId) continue;

                gw2apiItem apiItem = trader.GetGW2APIItem(item.Id);

                if (apiItem != null && ((apiItem.TypeId == TypeEnum.Armor && apiItem.Armor.UpgradeId != upgrade.Id) || 
                                        (apiItem.TypeId == TypeEnum.Weapon && apiItem.Weapon.UpgradeId != upgrade.Id))) continue;

                if (apiItem == null && !trader.Match(suffix, upgrade.Id, item.Name)) continue;

                double salePrice = (upgrade.MinSaleUnitPrice - 1) * 0.85;

                salePrice = salePrice - BLSalvageCost;

                int offerPrice = item.MaxOfferUnitPrice + 1;

                double profit = salePrice - offerPrice;

                if (profit > 0.0 && item.RarityId >= RarityEnum.Rare && item.MinLevel >= 68)
                {
                    resultRecord record = new resultRecord();
                    record.Id = item.Id;
                    record.Name = item.Name;
                    record.MinSaleUnitPrice = item.MinSaleUnitPrice;
                    record.RarityId = item.RarityId;
                    record.Level = item.MinLevel;
                    record.OfferPrice = offerPrice;
                    record.SalePrice = salePrice;
                    record.Profit = profit;
                    record.InstantProfit = salePrice - item.MinSaleUnitPrice;

                    result.Add(record);
                }
            }
        }

        static void IterateRunesSigils(List<Item> armorsCollection, List<Item> weaponsCollection, int ectoPrice, List<resultRecord> result)
        {
            List<Item> upgradesCollection = trader.search_items("", true, TypeEnum.Upgrade_Component, (int)UpgradeComponentSubTypeEnum.Rune, RarityEnum.Exotic).Result;
            List<Item> sigilsCollection = trader.search_items("", true, TypeEnum.Upgrade_Component, (int)UpgradeComponentSubTypeEnum.Sigil, RarityEnum.Exotic).Result;
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
                    ListViableRuneSigilItems(suffix, upgrade, ectoPrice, armorsCollection, result);
                }
                else
                {
                    ListViableRuneSigilItems(suffix, upgrade, ectoPrice, weaponsCollection, result);
                }

            }
        }

        static void Main(string[] args)
        {
            try
            {
                Task<List<Item>> itemList = trader.get_items(19721);
                int ectoPrice = ((Item)itemList.Result[0]).MinSaleUnitPrice;
                Console.WriteLine("Ecto price: {0}", ectoPrice);

                BLSalvageCost = trader.BlackLionKitSalvageCost;
                Console.WriteLine("BL Salvage Cost: {0}", BLSalvageCost);

                List<Item> armorsCollection = trader.search_items("", true, TypeEnum.Armor, -1, RarityEnum.Exotic, 68).Result;
                List<Item> weaponsCollection = trader.search_items("", true, TypeEnum.Weapon, -1, RarityEnum.Exotic, 68).Result;

                List<resultRecord> result = new List<resultRecord>();

                IterateRunesSigils(armorsCollection, weaponsCollection, ectoPrice, result);

                result.Sort(delegate(resultRecord r1, resultRecord r2) { return r1.Profit.CompareTo(r2.Profit); });

                foreach (resultRecord rec in result)
                {
                    if (rec.InstantProfit > 0.0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;

                        Console.WriteLine("{0}: {1}({2}) Profit: {3} Instant: {4} Offer: {5} Sale: {6}", rec.Id, rec.Name, rec.Level, rec.Profit, rec.InstantProfit,
                            rec.OfferPrice, rec.SalePrice);

                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine("{0}: {1}({2}) Profit: {3} Offer: {4} Sale: {5}", rec.Id, rec.Name, rec.Level, rec.Profit,
                                                rec.OfferPrice, rec.SalePrice);
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

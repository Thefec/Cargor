// UpgradeAssets.cs
namespace NewCss
{
    public static class UpgradeAssets
    {
        public static int GetCost(ItemType t)
        {
            switch (t)
            {
                case ItemType.MoreCapacityNone: return 0;
                case ItemType.MoreCapacity_1:   return 100;
                case ItemType.MoreCapacity_2:   return 200;
                case ItemType.MoreCapacity_3:   return 300;
                case ItemType.TableSlotsIncrease_None: return 0;
                case ItemType.TableSlotsIncrease_1:   return 100;
                case ItemType.TableSlotsIncrease_2:   return 200;
                case ItemType.QueueCapacity_None: return 0;
                case ItemType.QueueCapacity_1: return 150;
                case ItemType.QueueCapacity_2: return 300;
                case ItemType.QueueCapacity_3: return 450;
                
                default:                        return 0;
            }
        }

        public static string GetName(ItemType t)
        {
            switch (t)
            {
                case ItemType.MoreCapacityNone: return "Capacity 0";
                case ItemType.MoreCapacity_1:   return "Capacity +1";
                case ItemType.MoreCapacity_2:   return "Capacity +2";
                case ItemType.MoreCapacity_3:   return "Capacity +3";
                case ItemType.TableSlotsIncrease_None: return "Table 0";
                case ItemType.TableSlotsIncrease_1:   return "Table +1";
                case ItemType.TableSlotsIncrease_2:   return "Table +2";
                case ItemType.QueueCapacity_None: return "Queue Capacity 0";
                case ItemType.QueueCapacity_1: return "Queue Capacity +1";
                case ItemType.QueueCapacity_2: return "Queue Capacity +2";
                case ItemType.QueueCapacity_3: return "Queue Capacity +3";
                default:                        return t.ToString();
            }
        }
    }
}
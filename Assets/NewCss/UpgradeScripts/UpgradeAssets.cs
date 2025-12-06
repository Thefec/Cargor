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
            string key = $"Upgrade_{t}";
            string localized = LocalizationHelper.GetLocalizedString(key);
            
            // If localization key doesn't exist, fall back to enum name
            if (localized == key)
            {
                return t.ToString();
            }
            
            return localized;
        }
    }
}
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
                case ItemType.MoreCapacity_4:   return 240;
                case ItemType.MoreCapacity_5:   return 260;
                case ItemType.MoreCapacity_6:   return 260;
                case ItemType.MoreCapacity_7:   return 280;
                case ItemType.MoreCapacity_8:   return 280;
                case ItemType.MoreCapacity_9:   return 300;
                case ItemType.MoreCapacity_10:  return 300;
                case ItemType.MoreCapacity_11:  return 300;
                case ItemType.MoreCapacity_12:  return 320;
                case ItemType.MoreCapacity_13:  return 340;
                case ItemType.MoreCapacity_14:  return 360;
                case ItemType.MoreCapacity_15:  return 380;
                case ItemType.TableSlotsIncrease_None: return 0;
                case ItemType.TableSlotsIncrease_1:   return 100;
                case ItemType.TableSlotsIncrease_2:   return 200;
                case ItemType.QueueCapacity_None: return 0;
                case ItemType.QueueCapacity_1: return 150;
                case ItemType.QueueCapacity_2: return 300;
                case ItemType.QueueCapacity_3: return 450;
                
                default:                        return 380; // güvenli fallback: asla bedava upgrade verilmesin (bkz. UPGRADE_PRICING_REPORT.md §3)
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
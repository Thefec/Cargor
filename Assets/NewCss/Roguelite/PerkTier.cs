namespace NewCss
{
    /// <summary>Perk güç tier'ı — havuz kilidi buna göre (rapor §5).</summary>
    public enum PerkTier { T1 = 0, T2 = 1, T3 = 2 }

    /// <summary>Omurga (fiziksel, tier'sız, hep havuzda) mi yoksa perk mi.</summary>
    public enum PerkKind { LeveledBackbone = 0, Perk = 1 }
}

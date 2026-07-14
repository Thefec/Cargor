namespace NewCss
{
    /// <summary>Reroll fiyat eğrisi — rapor §7 (50/90/160/290/525, ×1.8, günlük sıfırlanır).</summary>
    public static class RerollCurve
    {
        // Onaylı sabit tablo (rapor v3.2 §7). Ondalık ×1.8 yuvarlamasını yeniden hesaplamak yerine
        // tabloyu birebir sabitliyoruz — kontrol bu değerleri onayladı.
        private static readonly int[] Costs = { 50, 90, 160, 290, 525 };

        public static int CostForReroll(int rerollIndexThisDay)
        {
            if (rerollIndexThisDay < 0) rerollIndexThisDay = 0;
            if (rerollIndexThisDay >= Costs.Length) return Costs[Costs.Length - 1];
            return Costs[rerollIndexThisDay];
        }
    }
}

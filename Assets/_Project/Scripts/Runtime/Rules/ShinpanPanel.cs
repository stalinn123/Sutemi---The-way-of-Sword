using System;

namespace Kendo.Rules
{
    public struct IpponDecision
    {
        public bool awarded;
        public int flags;
        public int abstentions;
        public Target target;
        public InvalidReason reason;
    }

    /// <summary>
    /// Three virtual shinpan (one shushin, two fukushin). Each raises a flag when the strike is yuko-datotsu and its
    /// smallest official margin clears that judge's own strictness. A judge without a clear view abstains.
    /// Decision rule: FIK Regulations Art. 26 (two flags; optionally one flag with two abstentions – see config).
    /// </summary>
    public sealed class ShinpanPanel
    {
        /// <summary>Design values: small differences between judges make marginal strikes split the panel.</summary>
        public float[] strictness = { 0.00f, 0.06f, 0.12f };
        public float minViewQuality = 0.3f;

        public IpponDecision Decide(StrikeEvaluation ev, KendoRuleConfig rules, float[] viewQuality = null)
        {
            var d = new IpponDecision { target = ev.target, reason = ev.invalidReason };
            for (int i = 0; i < strictness.Length; i++)
            {
                float view = viewQuality != null && i < viewQuality.Length ? viewQuality[i] : 1f;
                if (view < minViewQuality) { d.abstentions++; continue; }
                float viewPenalty = (1f - view) * 0.2f;
                if (ev.yukoDatotsu && ev.officialMargin >= strictness[i] + viewPenalty) d.flags++;
            }
            d.awarded = d.flags >= 2 || (rules.oneFlagWithAbstentionsCounts && d.flags == 1 && d.abstentions == 2);
            return d;
        }
    }
}

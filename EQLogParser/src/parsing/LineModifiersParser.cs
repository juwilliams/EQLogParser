﻿using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EQLogParser
{
  static class LineModifiersParser
  {
    private static readonly Dictionary<string, byte> AllModifiers = new()
    {
      { "Assassinate", 1 }, { "Crippling Blow", 1 }, { "Critical", 1 }, { "Deadly Strike", 1 }, { "Double Bow Shot", 1 }, { "Finishing Blow", 1 },
      { "Flurry", 1 }, { "Headshot", 1 }, { "Lucky", 1 }, { "Rampage", 1 }, { "Riposte", 1 }, { "Slay Undead", 1 }, { "Strikethrough", 1 },
      { "Twincast", 1 }, { "Wild Rampage", 1 },
    };

    private static readonly Dictionary<string, byte> CritModifiers = new()
    {
      { "Crippling Blow", 1 }, { "Critical", 1 }, { "Deadly Strike", 1 }, { "Finishing Blow", 1}
    };

    public const short NONE = -1;
    public const short CRIT = 2;
    private const short TWINCAST = 1;
    private const short LUCKY = 4;
    private const short RAMPAGE = 8;
    private const short STRIKETHROUGH = 16;
    private const short RIPOSTE = 32;
    private const short ASSASSINATE = 64;
    private const short HEADSHOT = 128;
    private const short SLAY = 256;
    private const short DOUBLEBOW = 512;
    private const short FLURRY = 1024;
    private const short FINISHING = 2048;

    private static readonly ConcurrentDictionary<string, short> MaskCache = new();

    internal static bool IsAssassinate(int mask) => mask > -1 && (mask & ASSASSINATE) != 0;
    internal static bool IsCrit(int mask) => mask > -1 && (mask & CRIT) != 0;
    internal static bool IsDoubleBowShot(int mask) => mask > -1 && (mask & DOUBLEBOW) != 0;
    internal static bool IsFinishingBlow(int mask) => mask > -1 && (mask & FINISHING) != 0;
    internal static bool IsFlurry(int mask) => mask > -1 && (mask & FLURRY) != 0;
    internal static bool IsHeadshot(int mask) => mask > -1 && (mask & HEADSHOT) != 0;
    internal static bool IsLucky(int mask) => mask > -1 && (mask & LUCKY) != 0;
    internal static bool IsTwincast(int mask) => mask > -1 && (mask & TWINCAST) != 0;
    internal static bool IsSlayUndead(int mask) => mask > -1 && (mask & SLAY) != 0;
    internal static bool IsRampage(int mask) => mask > -1 && (mask & RAMPAGE) != 0;
    internal static bool IsRiposte(int mask) => mask > -1 && (mask & RIPOSTE) != 0 && (mask & STRIKETHROUGH) == 0;
    internal static bool IsStrikethrough(int mask) => mask > -1 && (mask & STRIKETHROUGH) != 0;

    internal static void UpdateStats(HitRecord record, Attempt playerStats, Attempt theHit = null)
    {
      if (record.ModifiersMask > -1 && record.Type != Labels.MISS)
      {
        if ((record.ModifiersMask & ASSASSINATE) != 0)
        {
          playerStats.AssHits++;
          playerStats.TotalAss += record.Total;

          if (theHit != null)
          {
            theHit.AssHits++;
          }
        }

        if ((record.ModifiersMask & DOUBLEBOW) != 0)
        {
          playerStats.DoubleBowHits++;

          if (theHit != null)
          {
            theHit.DoubleBowHits++;
          }
        }

        if ((record.ModifiersMask & FLURRY) != 0)
        {
          playerStats.FlurryHits++;

          if (theHit != null)
          {
            theHit.FlurryHits++;
          }
        }

        if ((record.ModifiersMask & HEADSHOT) != 0)
        {
          playerStats.HeadHits++;
          playerStats.TotalHead += record.Total;

          if (theHit != null)
          {
            theHit.HeadHits++;
          }
        }

        if ((record.ModifiersMask & FINISHING) != 0)
        {
          playerStats.FinishingHits++;
          playerStats.TotalFinishing += record.Total;

          if (theHit != null)
          {
            theHit.FinishingHits++;
          }
        }

        if ((record.ModifiersMask & TWINCAST) != 0)
        {
          playerStats.TwincastHits++;

          if (theHit != null)
          {
            theHit.TwincastHits++;
          }
        }
        else
        {
          playerStats.TotalNonTwincast += record.Total;
        }

        if ((record.ModifiersMask & RAMPAGE) != 0)
        {
          playerStats.RampageHits++;

          if (theHit != null)
          {
            theHit.RampageHits++;
          }
        }

        // A Strikethrough Riposte is the attacker attacking through a riposte from the defender
        if (IsRiposte(record.ModifiersMask))
        {
          playerStats.RiposteHits++;
          playerStats.TotalRiposte += record.Total;

          if (theHit != null)
          {
            theHit.RiposteHits++;
          }
        }

        if (IsStrikethrough(record.ModifiersMask))
        {
          playerStats.StrikethroughHits++;

          if (theHit != null)
          {
            theHit.StrikethroughHits++;
          }
        }

        if ((record.ModifiersMask & SLAY) != 0)
        {
          playerStats.SlayHits++;
          playerStats.TotalSlay += record.Total;

          if (theHit != null)
          {
            theHit.SlayHits++;
          }
        }

        if ((record.ModifiersMask & CRIT) != 0)
        {
          playerStats.CritHits++;

          if (theHit != null)
          {
            theHit.CritHits++;
          }

          if ((record.ModifiersMask & LUCKY) == 0)
          {
            playerStats.TotalCrit += record.Total;

            if (theHit != null)
            {
              theHit.TotalCrit += record.Total;
            }

            if ((record.ModifiersMask & TWINCAST) == 0)
            {
              playerStats.NonTwincastCritHits++;
              playerStats.TotalNonTwincastCrit += record.Total;

              if (theHit != null)
              {
                theHit.NonTwincastCritHits++;
                theHit.TotalNonTwincastCrit += record.Total;
              }
            }
          }
        }

        if ((record.ModifiersMask & LUCKY) != 0)
        {
          playerStats.LuckyHits++;
          playerStats.TotalLucky += record.Total;

          if (theHit != null)
          {
            theHit.LuckyHits++;
            theHit.TotalLucky += record.Total;
          }

          if ((record.ModifiersMask & TWINCAST) == 0)
          {
            playerStats.NonTwincastLuckyHits++;
            playerStats.TotalNonTwincastLucky += record.Total;

            if (theHit != null)
            {
              theHit.NonTwincastLuckyHits++;
              theHit.TotalNonTwincastLucky += record.Total;
            }
          }
        }
      }
    }

    internal static short Parse(string player, string modifiers, double currentTime)
    {
      short result = -1;

      if (!string.IsNullOrEmpty(modifiers))
      {
        if (!MaskCache.TryGetValue(modifiers, out result))
        {
          result = BuildVector(player, modifiers, currentTime);
          MaskCache[modifiers] = result;
        }

        if (IsAssassinate(result))
        {
          PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
          PlayerManager.Instance.SetPlayerClass(player, SpellClass.ROG, "Class chosen from use of Assassinate.");
        }
        else if (IsDoubleBowShot(result))
        {
          PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
          PlayerManager.Instance.SetPlayerClass(player, SpellClass.RNG, "Class chosen from use of Double Bow Shot.");
        }
        else if (IsHeadshot(result))
        {
          PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
          PlayerManager.Instance.SetPlayerClass(player, SpellClass.RNG, "Class chosen from use of Headshot.");
        }
        else if (IsSlayUndead(result))
        {
          PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
          PlayerManager.Instance.SetPlayerClass(player, SpellClass.PAL, "Class closen from use of Slay Undead.");
        }
      }

      return result;
    }

    private static short BuildVector(string player, string modifiers, double currentTime)
    {
      short result = 0;

      var temp = "";
      foreach (var modifier in modifiers.Split(' '))
      {
        temp += modifier;
        if (AllModifiers.ContainsKey(temp))
        {
          if (CritModifiers.ContainsKey(temp))
          {
            result |= CRIT;
          }

          switch (temp)
          {
            case "Lucky":
              result |= LUCKY;
              break;
            case "Assassinate":
              result |= ASSASSINATE;
              PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
              PlayerManager.Instance.SetPlayerClass(player, SpellClass.ROG, "Class chosen from use of Assassinate.");
              break;
            case "Double Bow Shot":
              result |= DOUBLEBOW;
              PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
              PlayerManager.Instance.SetPlayerClass(player, SpellClass.RNG, "Class chosen from use of Double Bow Shot.");
              break;
            case "Finishing Blow":
              result |= FINISHING;
              break;
            case "Flurry":
              result |= FLURRY;
              break;
            case "Headshot":
              result |= HEADSHOT;
              PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
              PlayerManager.Instance.SetPlayerClass(player, SpellClass.RNG, "Class chosen from use of Headshot.");
              break;
            case "Twincast":
              result |= TWINCAST;
              break;
            case "Rampage":
            case "Wild Rampage":
              result |= RAMPAGE;
              break;
            case "Riposte":
              result |= RIPOSTE;
              break;
            case "Strikethrough":
              result |= STRIKETHROUGH;
              break;
            case "Slay Undead":
              result |= SLAY;
              PlayerManager.Instance.AddVerifiedPlayer(player, currentTime);
              PlayerManager.Instance.SetPlayerClass(player, SpellClass.PAL, "Class closen from use of Slay Undead.");
              break;
            case "Locked":
              // do nothing for now
              break;
          }

          temp = ""; // reset
        }
        else
        {
          temp += " ";
        }
      }

      return result;
    }
  }
}

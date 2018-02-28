// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2018 Andrey Antukh <niwi@niwi.nz>

using System;
using System.Linq;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using Microsoft.Win32;

namespace cAlgo {
  static class Extensions {
    public static bool In<T>(this T item, params T[] items) {
      if (items == null)
        throw new ArgumentNullException("items");

      return items.Contains(item);
    }

    public static string TimeAgo(this DateTime dateTime) {
      string result = string.Empty;
      var timeSpan = DateTime.Now.Subtract(dateTime);

      if (timeSpan <= TimeSpan.FromSeconds(60)) {
        result = string.Format("{0} seconds ago", timeSpan.Seconds);
      } else if (timeSpan <= TimeSpan.FromMinutes(60)) {
        result = timeSpan.Minutes > 1 ?
            String.Format("about {0} minutes ago", timeSpan.Minutes) :
            "about a minute ago";
      } else if (timeSpan <= TimeSpan.FromHours(24)) {
        result = timeSpan.Hours > 1 ?
            String.Format("about {0} hours ago", timeSpan.Hours) :
            "about an hour ago";
      } else if (timeSpan <= TimeSpan.FromDays(30)) {
        result = timeSpan.Days > 1 ?
            String.Format("about {0} days ago", timeSpan.Days) :
            "yesterday";
      } else if (timeSpan <= TimeSpan.FromDays(365)) {
        result = timeSpan.Days > 30 ?
            String.Format("about {0} months ago", timeSpan.Days / 30) :
            "about a month ago";
      } else {
        result = timeSpan.Days > 365 ?
            String.Format("about {0} years ago", timeSpan.Days / 365) :
            "about a year ago";
      }

      return result;
    }
  }

  public class Timing : Tuple<Int32, Int32> {
    public Int32 Reference { get { return Item1; } }
    public Int32 Local { get { return Item2; } }

    public Timing(int reference, int local) : base(reference, local) {
    }
  }

  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.Registry)]
  public class Screener : Robot {

    [Parameter("Source")]
    public DataSeries Source { get; set; }

    private TimeFrame[] timeFrames = new TimeFrame[] {
      TimeFrame.Minute10,
      TimeFrame.Hour,
      TimeFrame.Daily
    };

    private String[] symbols = new String[] {
      "EURUSD",
      "GBPUSD",
      "EURJPY",
      "USDJPY",
      "AUDUSD",
      "USDCHF",
      "GBPJPY",
      "USDCAD",
      "EURGBP",
      "EURCHF",
      "AUDJPY",
      "NZDUSD",
      "CHFJPY",
      "EURAUD",
      "CADJPY",
      "GBPAUD",
      "EURCAD",
      "AUDCAD",
      "GBPCHF",
      "AUDCHF",
      "GBPCAD",
      "GBPNZD",
      "NZDCAD",
      "NZDCHF",
      "NZDJPY",
      "AUDNZD",
      "CADCHF",
      "EURNZD",
      "XAUUSD",
      "#Japan225",
      "#USNDAQ100",
      "#USSPX500",
      "#Spain35",
      "BRENT",
      "NAT.GAS"
    };

    private Dictionary<String, Dictionary<TimeFrame, MarketSeries>> lseries;
    private Dictionary<String, Dictionary<TimeFrame, MarketSeries>> rseries;

    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> lmm150;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> lmm300;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> rmm150;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> rmm300;

    private Dictionary<String, Dictionary<TimeFrame, MacdCrossOver>> lmacd;
    private Dictionary<String, Dictionary<TimeFrame, MacdCrossOver>> rmacd;

    private State state;
    private long barCounter;
    private long ticks = 0;
    private bool fistRun = true;

    protected override void OnStart() {
      barCounter = Source.Count;

      state = new State();

      lseries = new Dictionary<string, Dictionary<TimeFrame, MarketSeries>>();
      rseries = new Dictionary<string, Dictionary<TimeFrame, MarketSeries>>();

      lmm150 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      lmm300 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      rmm150 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      rmm300 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();

      lmacd = new Dictionary<string, Dictionary<TimeFrame, MacdCrossOver>>();
      rmacd = new Dictionary<string, Dictionary<TimeFrame, MacdCrossOver>>();

      Print("Initializing screener local state.");

      foreach (var sym in symbols) {
        lseries[sym] = new Dictionary<TimeFrame, MarketSeries>();
        rseries[sym] = new Dictionary<TimeFrame, MarketSeries>();

        lmm150[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        lmm300[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        rmm150[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        rmm300[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();

        lmacd[sym] = new Dictionary<TimeFrame, MacdCrossOver>();
        rmacd[sym] = new Dictionary<TimeFrame, MacdCrossOver>();

        foreach (var tf in timeFrames) {
          Print("Initializing data for: {0}/{1}.", sym, tf);

          var reftf = GetReferenceTimeframe(tf);
          var lmks = MarketData.GetSeries(sym, tf);
          var rmks = MarketData.GetSeries(sym, reftf);

          lmm150[sym][tf] = Indicators.WeightedMovingAverage(lmks.Close, 150);
          lmm300[sym][tf] = Indicators.WeightedMovingAverage(lmks.Close, 300);
          rmm150[sym][tf] = Indicators.WeightedMovingAverage(rmks.Close, 150);
          rmm300[sym][tf] = Indicators.WeightedMovingAverage(rmks.Close, 300);

          lmacd[sym][tf] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
          rmacd[sym][tf] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

          lseries[sym][tf] = lmks;
          rseries[sym][tf] = rmks;
        }
      }

      Print("Initialization finished.");
      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    protected override void OnStop() {
      Print("Bot Stopped with total ticks: {0}", ticks);
    }

    private bool IsNewBar() {
      if (barCounter < Source.Count) {
        barCounter = Source.Count;
        return true;
      } else {
        return false;
      }
    }

    protected override void OnTick() {
      ticks++;

      if (IsNewBar() || fistRun) {
        HandleUpdate();
      }

      if (fistRun) fistRun = false;
    }

    // -------------------------------------------
    // ----  Market Timing
    // -------------------------------------------

    private Timing CalculateMarketTiming(String sym, TimeFrame tf) {
      var local = CalculateLocalTiming(sym, tf);
      var reference = CalculateReferenceTiming(sym, tf);

      return new Timing(reference, local);
    }

    private int CalculateLocalTiming(String sym, TimeFrame tf) {
      if (IsTrendUp(lseries[sym][tf], lmm300[sym][tf])) {
        if (lmacd[sym][tf].Histogram.LastValue > 0 && lmacd[sym][tf].Signal.LastValue > 0) {
          return 1;
        } else if (lmacd[sym][tf].Histogram.LastValue > 0 && lmacd[sym][tf].Signal.LastValue < 0) {
          return 4;
        } else if (lmacd[sym][tf].Histogram.LastValue <= 0 && lmacd[sym][tf].Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (lmacd[sym][tf].Histogram.LastValue < 0 && lmacd[sym][tf].Signal.LastValue < 0) {
          return -1;
        } else if (lmacd[sym][tf].Histogram.LastValue < 0 && lmacd[sym][tf].Signal.LastValue > 0) {
          return -4;
        } else if (lmacd[sym][tf].Histogram.LastValue >= 0 && lmacd[sym][tf].Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    private int CalculateReferenceTiming(String sym, TimeFrame tf) {
      if (IsTrendUp(rseries[sym][tf], rmm300[sym][tf])) {
        if (rmacd[sym][tf].Histogram.LastValue > 0 && rmacd[sym][tf].Signal.LastValue > 0) {
          return 1;
        } else if (rmacd[sym][tf].Histogram.LastValue > 0 && rmacd[sym][tf].Signal.LastValue < 0) {
          return 4;
        } else if (rmacd[sym][tf].Histogram.LastValue <= 0 && rmacd[sym][tf].Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (rmacd[sym][tf].Histogram.LastValue < 0 && rmacd[sym][tf].Signal.LastValue < 0) {
          return -1;
        } else if (rmacd[sym][tf].Histogram.LastValue < 0 && rmacd[sym][tf].Signal.LastValue > 0) {
          return -4;
        } else if (rmacd[sym][tf].Histogram.LastValue >= 0 && rmacd[sym][tf].Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    private TimeFrame GetReferenceTimeframe(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return TimeFrame.Daily;
      } else if (tf == TimeFrame.Minute10) {
        return TimeFrame.Hour;
      } else if (tf == TimeFrame.Daily) {
        return TimeFrame.Weekly;
      } else if (tf == TimeFrame.Weekly) {
        return TimeFrame.Weekly;
      } else {
        return TimeFrame.Hour;
      }
    }

    private bool IsTrendUp(MarketSeries series, WeightedMovingAverage wma) {
      var close = series.Close.LastValue;
      var value = wma.Result.LastValue;

      if (value < close) {
        return true;
      } else {
        return false;
      }
    }

    // -------------------------------------------
    // ----  MACD
    // -------------------------------------------

    public int GetSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[sym][tf];
      var wma150 = lmm150[sym][tf];
      var wma300 = lmm300[sym][tf];
      var macd = lmacd[sym][tf];

      // Only operate on impulsive reference timing.

      if (timing.Reference.In(1, 4)) {
        int punctuation = 0;

        if (series.Low.LastValue <= wma150.Result.LastValue && (macd.Signal.LastValue <= 0 || macd.MACD.LastValue <= 0)) {
          punctuation++;

          if (macd.Histogram.LastValue >= 0) {
            punctuation += 2;
          }

          if (series.Low.LastValue <= wma300.Result.LastValue) {
            punctuation++;
          }
        }
        return punctuation;
      } else if (timing.Reference.In(-1, -4)) {
        int punctuation = 0;

        if (series.High.LastValue >= wma150.Result.LastValue && (macd.Signal.LastValue >= 0 || macd.MACD.LastValue >= 0)) {
          punctuation++;

          if (macd.Histogram.LastValue <= 0) {
            punctuation += 2;
          }

          if (series.High.LastValue >= wma300.Result.LastValue) {
            punctuation++;
          }
        }

        return punctuation;
      } else {
        return 0;
      }
    }

    // -------------------------------------------
    // ----  Rendering
    // -------------------------------------------

    private void HandleUpdate() {
      foreach (var tf in timeFrames) {
        foreach (var sym in symbols) {
          var timing = CalculateMarketTiming(sym, tf);
          var punctuation = GetSignal(sym, tf, timing);

          var value = new State.Value(sym, tf, timing, punctuation);
          state.Update(sym, tf, value);
          HandleNotifications(value);
        }
      }

      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    // -------------------------------------------
    // ----  Notifications
    // -------------------------------------------

    public void HandleNotifications(State.Value value) {
      var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
      var subkey = String.Format("{0}-{1}", value.Symbol, value.TimeFrame.ToString());

      if (value.Punctuation == 0) {
        Registry.SetValue(keyName, subkey, value.Punctuation);
      } else {
        var candidate = (int)Registry.GetValue(keyName, subkey, 0);

        var from = "andsux@gmail.com";
        var to = "niwi@niwi.nz";

        if ((value.Punctuation > candidate && candidate >= 0) || (value.Punctuation < candidate && candidate <= 0)) {
          Registry.SetValue(keyName, subkey, value.Punctuation);

          var timeFrame = state.TimeFrameToString(value.TimeFrame);
          var tradeType = value.Punctuation > 0 ? "Buy" : "Sell";
          var subject = String.Format("Trade oportunity: {0} {1}/{2} - Ranking: {3} ", tradeType, value.Symbol, timeFrame, value.Punctuation);

          Notifications.SendEmail(from, to, subject, "");
        }
      }
    }
  }

  public class State {
    public class Key : Tuple<String, TimeFrame> {
      public String Symbol { get { return Item1; } }
      public TimeFrame TimeFrame { get { return Item2; } }

      public Key(string symbol, TimeFrame timeFrame) : base(symbol, timeFrame) { }
    }

    public class Value {
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }
      public Timing Timing { get; set; }
      public TimeFrame TimeFrame { get; set; }
      public String Symbol { get; set; }
      public int Punctuation { get; set; }

      public Value(String symbol, TimeFrame timeFrame, Timing timing, int punctuation) {
        this.Symbol = symbol;
        this.TimeFrame = timeFrame;
        this.Punctuation = punctuation;
        this.Timing = timing;
        this.CreatedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
      }

      public bool IsZero() {
        return Punctuation == 0;
      }

      public void UpdateWith(Value other) {
        if (other.TimeFrame != this.TimeFrame ||
            other.Symbol != this.Symbol) {
          throw new Exception("Cant update value with not matching TimeFrame & Symbol.");
        }
        this.Punctuation = other.Punctuation;
        this.Timing = other.Timing;
        this.UpdatedAt = other.CreatedAt;
      }
    }

    private Dictionary<Key, Value> local;

    public State() {
      local = new Dictionary<Key, Value>();
    }

    public void Update(String sym, TimeFrame tf, Value value) {
      var key = new Key(sym, tf);

      if (value.IsZero()) {
        local.Remove(key);
      } else {
        if (local.ContainsKey(key)) {
          local[key].UpdateWith(value);
        } else {
          local.Add(key, value);
        }
      }
    }

    public string TimeFrameToString(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return "H1";
      } else if (tf == TimeFrame.Daily) {
        return "D1";
      } else {
        return "M10";
      }
    }

    public string Render() {
      var output = "";

      output += string.Format("\t\t\t\tUpdated at: {0}\r\n\r\n", DateTime.Now.ToString("yyyy - MM - dd HH: mm:ss"));
      output += string.Format("\t{0,-10}\t{1,-5}\t{2,-8}\t{3,-8}\t{4,-20}\r\n", "Symbol", "TF", "TM",  "VAL", "Created At");
      output += "\t----------------------------------------------------------------------------------------------------------\r\n";

      foreach (var item in local.ToList().OrderBy(o => o.Value.CreatedAt).Reverse()) {
        var key = item.Key;
        var value = item.Value;
        var timing = value.Timing;

        var tfStr = TimeFrameToString(key.TimeFrame);
        var timingStr = string.Format("{0,2},{1,2}", timing.Reference, timing.Local);

        var pointsStr = string.Format("{0,2}", value.Punctuation);
        output += string.Format("\t{0,-10}\t{1,-5}\t{2,-8}\t{3,-8}\t{4,-20}\r\n", key.Symbol, tfStr, timingStr, pointsStr, value.CreatedAt.TimeAgo());
      }

      return output;
    }
  }
}

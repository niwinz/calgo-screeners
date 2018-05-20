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
  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.Registry)]
  public class ScreenerMtf : Robot {
    [Parameter("Source")]
    public DataSeries Source { get; set; }

    [Parameter("PullBack Signals?", DefaultValue = true)]
    public bool EnablePB { get; set; }

    [Parameter("PullBack Time Frames" , DefaultValue = "H1,H4,D1")]
    public string PBTimeFrames { get; set; }

    [Parameter("MMX Signals?", DefaultValue = false)]
    public bool EnableMMX { get; set; }

    [Parameter("MMX Time Frames?", DefaultValue = "M5,H1")]
    public string MMXTimeFrames { get; set; }

    [Parameter("MMX Periods?", DefaultValue = 24)]
    public int MMXPeriods { get; set; }

    [Parameter("Enable email alerts?", DefaultValue = false)]
    public Boolean EnableEmailAlerts { get; set; }

    [Parameter("Enable sound Alerts?", DefaultValue = false)]
    public Boolean EnableSoundAlerts { get; set; }

    private String[] symbols = new String[] {
      // Forex
      "AUDNZD",
      "CADJPY",
      "AUDJPY",
      "GBPUSD",
      "EURUSD",
      "EURCAD",
      "EURJPY",
      "GBPJPY",
      "GBPCAD",
      "GBPAUD",
      "EURNZD",
      "NZDJPY",
      "USDCHF",
      "EURGBP",
      "USDCAD",
      "NZDUSD",
      "AUDUSD",
      "USDJPY",
      "AUDCAD",
      "EURAUD",
      "GBPCHF",
      "XAUUSD",
      "NZDCHF",
      "GBPNZD",
      "NZDCAD",

      // Indexes
      "US500",
      "USTEC",
      "DE30",
      "ES35",
    };

    private Dictionary<String, String> spaths;

    private Dictionary<TimeFrame, Dictionary<String, MarketSeries>> lseries;
    private Dictionary<TimeFrame, Dictionary<String, MarketSeries>> rseries;

    private Dictionary<TimeFrame, Dictionary<String, ExponentialMovingAverage>> lmm50;
    private Dictionary<TimeFrame, Dictionary<String, WeightedMovingAverage>> lmm200;
    private Dictionary<TimeFrame, Dictionary<String, WeightedMovingAverage>> rmm200;

    private Dictionary<TimeFrame, Dictionary<String, MacdCrossOver>> lmacd;
    private Dictionary<TimeFrame, Dictionary<String, MacdCrossOver>> rmacd;

    private State state;

    private TimeFrame[] GetAllRequiredTimeFrames() {
      var bundle = new HashSet<TimeFrame>();


      if (EnablePB) {
        foreach (var item in ParseTimeFrames(PBTimeFrames)) {
          bundle.Add(item);
        }
      }

      if (EnableMMX) {
        foreach (var item in ParseTimeFrames(MMXTimeFrames)) {
          bundle.Add(item);
        }
      }

      return bundle.ToArray();
    }

    protected override void OnStart() {
      state = new State(TimeFrame.Daily);

      lseries = new Dictionary<TimeFrame, Dictionary<string, MarketSeries>>();
      rseries = new Dictionary<TimeFrame, Dictionary<string, MarketSeries>>();

      lmm50 = new Dictionary<TimeFrame, Dictionary<string, ExponentialMovingAverage>>();
      lmm200 = new Dictionary<TimeFrame, Dictionary<string, WeightedMovingAverage>>();
      rmm200 = new Dictionary<TimeFrame, Dictionary<string, WeightedMovingAverage>>();

      lmacd = new Dictionary<TimeFrame, Dictionary<string, MacdCrossOver>>();
      rmacd = new Dictionary<TimeFrame, Dictionary<string, MacdCrossOver>>();

      spaths = new Dictionary<string, string>(3) {
        { "PB", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-1.wav" },
        { "MMX", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-4.wav" },
        { "VCN", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-3.wav" }
      };

      Print("Initializing screener local state.");

      foreach(var tf in GetAllRequiredTimeFrames()) {
        InitializeIndicators(tf);
      }

      Print("Initialization finished.");
      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    private void InitializeIndicators(TimeFrame tf) {
      lmm50[tf] = new Dictionary<string, ExponentialMovingAverage>();
      lmm200[tf] = new Dictionary<string, WeightedMovingAverage>();
      rmm200[tf] = new Dictionary<string, WeightedMovingAverage>();

      lmacd[tf] = new Dictionary<string, MacdCrossOver>();
      rmacd[tf] = new Dictionary<string, MacdCrossOver>();

      rseries[tf] = new Dictionary<string, MarketSeries>();
      lseries[tf] = new Dictionary<string, MarketSeries>();

      foreach (var sym in symbols) {
        Print("Initializing data for: {0}/{1}.", sym, tf);

        var reftf = GetReferenceTimeFrame(tf);
        var lmks = MarketData.GetSeries(sym, tf);
        var rmks = MarketData.GetSeries(sym, reftf);

        lmm50[tf][sym] = Indicators.ExponentialMovingAverage(lmks.Close, 50);

        lmm200[tf][sym] = Indicators.WeightedMovingAverage(lmks.Close, 200);
        rmm200[tf][sym] = Indicators.WeightedMovingAverage(rmks.Close, 200);

        lmacd[tf][sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
        rmacd[tf][sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

        lseries[tf][sym] = lmks;
        rseries[tf][sym] = rmks;
      }
    }

    protected override void OnBar() {
      // PB Signals
      if (EnablePB) {
        foreach (var tf in ParseTimeFrames(PBTimeFrames)) {
          foreach (var sym in symbols) {
            var timing = CalculateMarketTiming(sym, tf);
            var val = GetPBSignal(sym, tf, timing);
            var value = new State.Value("PB", sym, tf, timing, val);

            state.Update(value);

            if (EnableEmailAlerts) {
              HandleEmailAlerts(value);
            }

            if (EnableSoundAlerts) {
              HandleSoundAlerts(value);
            }
          }
        }
      }

      // MMX Signals
      if (EnableMMX) {
        foreach (var tf in ParseTimeFrames(MMXTimeFrames)) {
          foreach (var sym in symbols) {
            var timing = CalculateMarketTiming(sym, tf);
            var val = GetMMXSignal(sym, tf, timing);
            var value = new State.Value("MMX", sym, tf, timing, val);

            state.Update(value);

            if (EnableEmailAlerts) {
              HandleEmailAlerts(value);
            }

            if (EnableSoundAlerts) {
              HandleSoundAlerts(value);
            }
          }
        }
      }

      var output = state.Render();
      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    // -------------------------------------------
    // ----  Pull Back Signal
    // -------------------------------------------

    public int GetPBSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[tf][sym];
      var mm50 = lmm50[tf][sym];
      var mm200 = lmm200[tf][sym];
      var macd = lmacd[tf][sym];

      if (mm50.Result.LastValue > mm200.Result.LastValue
          && In(timing.Reference, 1, 4, -2, -3)
          && macd.MACD.LastValue < 0
          && macd.Signal.LastValue < 0
          && macd.Histogram.LastValue >= 0) {
        return 1;
      }

      if (mm50.Result.LastValue < mm200.Result.LastValue
          && In(timing.Reference, -1, -4, 2, 3)
          && macd.MACD.LastValue > 0
          && macd.Signal.LastValue > 0
          && macd.Histogram.LastValue <= 0) {
        return -1;
      }

      return 0;
    }

    // -------------------------------------------
    // ----  MMX Signal
    // -------------------------------------------

    public int GetMMXSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[tf][sym];
      var mm50 = lmm50[tf][sym];
      var mm200 = lmm200[tf][sym];

      var hasCrossedAbove = false;
      var hasCrossedBelow = false;
      var periods = MMXPeriods;

      for (int i = 1; i < periods; i++) {
        hasCrossedAbove = mm50.Result.HasCrossedAbove(mm200.Result, i);
        if (hasCrossedAbove == true) break;
      }

      for (int i = 1; i < periods; i++) {
        hasCrossedBelow = mm50.Result.HasCrossedBelow(mm200.Result, i);
        if (hasCrossedBelow == true) break;
      }

      if (hasCrossedAbove) {
        return 1;
      }

      if (hasCrossedBelow) {
        return -1;
      }

      return 0;
    }

    // -------------------------------------------
    // ----  Market Timing
    // -------------------------------------------

    public class Timing : Tuple<Int32, Int32> {
      public Int32 Reference { get { return Item1; } }
      public Int32 Local { get { return Item2; } }

      public Timing(int reference, int local) : base(reference, local) {
      }
    }

    private Timing CalculateMarketTiming(String sym, TimeFrame tf) {
      var local = CalculateTiming(lmacd[tf][sym], lmm200[tf][sym], lseries[tf][sym]);
      var reference = CalculateTiming(rmacd[tf][sym], rmm200[tf][sym], rseries[tf][sym]);
      return new Timing(reference, local);
    }

    private int CalculateTiming(MacdCrossOver macd, WeightedMovingAverage wma, MarketSeries series) {
      if (IsTrendUp(series, wma)) {
        if (macd.Histogram.LastValue > 0 && macd.Signal.LastValue > 0) {
          return 1;
        } else if (macd.Histogram.LastValue > 0 && macd.Signal.LastValue < 0) {
          return 4;
        } else if (macd.Histogram.LastValue <= 0 && macd.Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (macd.Histogram.LastValue < 0 && macd.Signal.LastValue < 0) {
          return -1;
        } else if (macd.Histogram.LastValue < 0 && macd.Signal.LastValue > 0) {
          return -4;
        } else if (macd.Histogram.LastValue >= 0 && macd.Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    public TimeFrame GetReferenceTimeFrame(TimeFrame tf) {
      if (tf == TimeFrame.Minute5 || tf == TimeFrame.Minute) {
        return TimeFrame.Hour;
      } else if (tf == TimeFrame.Minute15) {
        return TimeFrame.Hour4;
      } else if (tf == TimeFrame.Hour) {
        return TimeFrame.Daily;
      } else if (tf == TimeFrame.Hour4) {
        return TimeFrame.Day2;
      } else if (tf == TimeFrame.Daily) {
        return TimeFrame.Weekly;
      } else if (tf == TimeFrame.Weekly) {
        return TimeFrame.Monthly;
      } else {
        throw new Exception(string.Format("GetReferenceTimeFrame: timeframe {0} not supported", tf));
      }
    }

    private bool IsTrendUp(MarketSeries series, WeightedMovingAverage ma) {
      var close = series.Close.LastValue;
      var value = ma.Result.LastValue;

      if (value < close) {
        return true;
      } else {
        return false;
      }
    }

    // -------------------------------------------
    // ----  Notifications
    // -------------------------------------------

    public void HandleEmailAlerts(State.Value value) {
      var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
      var subkey = String.Format("{0}-EMAIL", value.GetKey());

      if (value.IsZero()) {
        Registry.SetValue(keyName, subkey, 0);
      } else {
        var obj = Registry.GetValue(keyName, subkey, null);
        Int32 candidate;

        if (obj == null) {
          candidate = 0;
        } else {
          candidate = (int)obj;
        }

        var from = "andsux@gmail.com";
        var to = "niwi@niwi.nz";

        if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
          Registry.SetValue(keyName, subkey, value.Val);
         
          var tradeType = value.GetTradeType().ToString();
          var tfStr = TimeFrameAsString(value.TimeFrame);

          var subject = String.Format("{0} trade oportunity on {1} {2} - Strategy: {3}, Points: {4} ", tradeType, tfStr, value.Symbol, value.Name, value.Val);
          Notifications.SendEmail(from, to, subject, "");
        }
      }
    }

    public void HandleSoundAlerts(State.Value value) {
      var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
      var subkey = String.Format("{0}-SND", value.GetKey());

      if (value.IsZero()) {
        Registry.SetValue(keyName, subkey, 0);
      } else {
        var obj = Registry.GetValue(keyName, subkey, null);
        Int32 candidate;

        if (obj == null) {
          candidate = 0;
        } else {
          candidate = (int)obj;
        }

        if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
          Registry.SetValue(keyName, subkey, value.Val);
          Notifications.PlaySound(spaths[value.Name]);
        }
      }
    }

    // -------------------------------------------
    // ----  Internal State
    // -------------------------------------------

    public class State {
      public class Value {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public String Name { get; set; }
        public Timing Timing { get; set; }
        public String Symbol { get; set; }
        public TimeFrame TimeFrame { get; set; }
        public Int32 Val { get; set; }

        public Value(String name, String symbol, TimeFrame tf, Timing timing, Int32 value) {
          this.CreatedAt = DateTime.Now;
          this.UpdatedAt = DateTime.Now;

          this.Name = name;
          this.Symbol = symbol;
          this.TimeFrame = tf;
          this.Timing = timing;
          this.Val = value;
        }

        public String GetKey() {
          return String.Format("{0}-{1}-{2}", this.Symbol, this.TimeFrame, this.Name);
        }

        public bool IsZero() {
          return (this.Val == 0);
        }

        public TradeType GetTradeType() {
          return this.Val > 0 ? TradeType.Buy : TradeType.Sell;
        }

        public void UpdateWith(Value other) {
          if (other.Val != this.Val) {
            this.UpdatedAt = other.CreatedAt;
          }

          this.Val = other.Val;
          this.Timing = other.Timing;
        }
      }

      private Dictionary<String, Value> local;
      private TimeFrame timeFrame;

      public State(TimeFrame timeFrame) {
        this.local = new Dictionary<String, Value>();
        this.timeFrame = timeFrame;
      }

      public void Update(Value value) {
        var key = value.GetKey();

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

      public string Render() {
        var output = "";

        output += string.Format("\tUpdated at: {0}\r\n\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        output += string.Format("\t{0,-10}\t{1,-8}\t{2,-8}\t{3,-8}\t{4,-8}\t{5,-20}\r\n", "Symbol", "TF", "TM", "ST", "PT", "Created At");
        output += "\t----------------------------------------------------------------------------------------------------------\r\n";

        foreach (var item in local.ToList().OrderBy(o => o.Value.CreatedAt).Reverse()) {
          var key = item.Key;
          var value = item.Value;
          var timing = value.Timing;

          var timingStr = string.Format("{0,2},{1,2}", timing.Reference, timing.Local);
          var stStr = string.Format("{0,4}", value.Name);
          var tfStr = string.Format("{0,4}", TimeFrameAsString(value.TimeFrame));
          var ptstr = string.Format("{0,2}", value.Val);
          output += string.Format("\t{0,-10}\t{1,-8}\t{2,-8}\t{3,-8}\t{4,-8}\t{5,-20}\r\n", value.Symbol, tfStr, timingStr, stStr, ptstr, TimeAgo(value.CreatedAt));
        }

        return output;
      }
    }

    // -------------------------------------------
    // ----  Helpers
    // -------------------------------------------


    public static TimeFrame[] ParseTimeFrames(string tfs) {
      string[] result = tfs.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
      TimeFrame[] output = new TimeFrame[result.Count()];

      for (int i = 0; i < result.Count(); i++) {
        var tfname = result[i];
        if (tfname == "H1") {
          output[i] = TimeFrame.Hour;
        } else if (tfname == "D1") {
          output[i] = TimeFrame.Daily;
        } else if (tfname == "H4") {
          output[i] = TimeFrame.Hour4;
        } else if (tfname == "M15") {
          output[i] = TimeFrame.Minute15;
        } else if (tfname == "M5") {
          output[i] = TimeFrame.Minute5;
        } else if (tfname == "M1") {
          output[i] = TimeFrame.Minute;
        } else {
          throw new Exception(string.Format("ParseTimeFrames: timeframe {0} not supported", tfname));

        }
      }

      return output;
    }

    public static bool In<T>(T item, params T[] items) {
      if (items == null)
        throw new ArgumentNullException("items");

      return items.Contains(item);
    }

    public static string TimeAgo(DateTime dateTime) {
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

    public static string TimeFrameAsString(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return "H1";
      } else if (tf == TimeFrame.Daily) {
        return "D1";
      } else if (tf == TimeFrame.Hour4) {
        return "H4";
      } else if (tf == TimeFrame.Minute15) {
        return "M15";
      } else if (tf == TimeFrame.Minute5) {
        return "M5";
      } else {
        throw new Exception(string.Format("TimeFrameAsString: timeframe {0} not supported", tf));
      }

    }
  }
}

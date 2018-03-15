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

    public static string AsString(this TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return "H1";
      } else if (tf == TimeFrame.Daily) {
        return "D1";
      } else {
        return "M5";
      }
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

    [Parameter("Time Frame")]
    public TimeFrame TFrame { get; set; }

    [Parameter("Enable MACD?", DefaultValue = true)]
    public Boolean EnableMacd { get; set; }

    [Parameter("Enable VCN?", DefaultValue = false)]
    public Boolean EnableVcn { get; set; }

    [Parameter("Enable email alerts?", DefaultValue = false)]
    public Boolean EnableEmailAlerts { get; set; }

    [Parameter("Enable sound Alerts?", DefaultValue = false)]
    public Boolean EnableSoundAlerts { get; set; }

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
      "BTCUSD",
      "USTEC",
      "US30",
      "US500",
      "UK100",
      "ES35",
      "JP225",
      "DE30",
      "STOXX50",
      "AUS200",
      // "XTIUSD" 
    };

    private String AlertSoundPath = "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-1.wav";

    private Dictionary<String, MarketSeries> lseries;
    private Dictionary<String, MarketSeries> rseries;

    private Dictionary<String, ExponentialMovingAverage> lmm8;
    private Dictionary<String, WeightedMovingAverage> lmm50;
    private Dictionary<String, WeightedMovingAverage> lmm150;
    private Dictionary<String, WeightedMovingAverage> lmm300;
    private Dictionary<String, WeightedMovingAverage> rmm150;
    private Dictionary<String, WeightedMovingAverage> rmm300;

    private Dictionary<String, MacdCrossOver> lmacd;
    private Dictionary<String, MacdCrossOver> rmacd;

    private Dictionary<String, AverageTrueRange> latr;


    private State state;

    protected override void OnStart() {
      state = new State(TFrame);

      lseries = new Dictionary<string, MarketSeries>();
      rseries = new Dictionary<string, MarketSeries>();

      lmm8 = new Dictionary<string, ExponentialMovingAverage>();
      lmm50 = new Dictionary<string, WeightedMovingAverage>();
      lmm150 = new Dictionary<string, WeightedMovingAverage>();
      lmm300 = new Dictionary<string, WeightedMovingAverage>();
      rmm150 = new Dictionary<string, WeightedMovingAverage>();
      rmm300 = new Dictionary<string, WeightedMovingAverage>();

      lmacd = new Dictionary<string, MacdCrossOver>();
      rmacd = new Dictionary<string, MacdCrossOver>();

      latr = new Dictionary<string, AverageTrueRange>();

      Print("Initializing screener local state.");

      foreach (var sym in symbols) {
        Print("Initializing data for: {0}/{1}.", sym, TFrame);

        var reftf = GetReferenceTimeframe(TFrame);
        var lmks = MarketData.GetSeries(sym, TFrame);
        var rmks = MarketData.GetSeries(sym, reftf);

        lmm8[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 8);
        lmm50[sym] = Indicators.WeightedMovingAverage(lmks.Close, 50);
        lmm150[sym] = Indicators.WeightedMovingAverage(lmks.Close, 150);
        lmm300[sym] = Indicators.WeightedMovingAverage(lmks.Close, 300);
        rmm150[sym] = Indicators.WeightedMovingAverage(rmks.Close, 150);
        rmm300[sym] = Indicators.WeightedMovingAverage(rmks.Close, 300);

        lmacd[sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
        rmacd[sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

        latr[sym] = Indicators.AverageTrueRange(lmks, 14, MovingAverageType.Exponential);

        lseries[sym] = lmks;
        rseries[sym] = rmks;
      }

      Print("Initialization finished.");
      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    protected override void OnBar() {
      foreach (var sym in symbols) {
        var timing = CalculateMarketTiming(sym);

        if (EnableMacd) {
          var val = GetMacdSignal(sym, timing);
          var value = new State.Value("MACD", sym, timing, val);
          state.Update(value);

          if (EnableEmailAlerts) {
            HandleEmailAlerts(value);

          }

          if (EnableSoundAlerts) {
            HandleSoundAlerts(value);
          }
        }

        if (EnableVcn) {
          var val = GetVcnSignal(sym, timing);
          var value = new State.Value("VCN", sym, timing, val);
          state.Update(value);

          if (EnableEmailAlerts) {
            HandleEmailAlerts(value);
          }

          if (EnableSoundAlerts) {
            HandleSoundAlerts(value);
          }
        }
      }
      
      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }

    // -------------------------------------------
    // ----  Market Timing
    // -------------------------------------------

    private Timing CalculateMarketTiming(String sym) {
      var local = CalculateLocalTiming(sym);
      var reference = CalculateReferenceTiming(sym);

      return new Timing(reference, local);
    }

    private int CalculateLocalTiming(String sym) {
      if (IsTrendUp(lseries[sym], lmm300[sym])) {
        if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue > 0) {
          return 1;
        } else if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue < 0) {
          return 4;
        } else if (lmacd[sym].Histogram.LastValue <= 0 && lmacd[sym].Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue < 0) {
          return -1;
        } else if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue > 0) {
          return -4;
        } else if (lmacd[sym].Histogram.LastValue >= 0 && lmacd[sym].Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    private int CalculateReferenceTiming(String sym) {
      if (IsTrendUp(rseries[sym], rmm300[sym])) {
        if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue > 0) {
          return 1;
        } else if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue < 0) {
          return 4;
        } else if (rmacd[sym].Histogram.LastValue <= 0 && rmacd[sym].Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue < 0) {
          return -1;
        } else if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue > 0) {
          return -4;
        } else if (rmacd[sym].Histogram.LastValue >= 0 && rmacd[sym].Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    private TimeFrame GetReferenceTimeframe(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return TimeFrame.Daily;
      } else if (tf == TimeFrame.Minute5) {
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

    public int GetMacdSignal(string sym, Timing timing) {
      var series = lseries[sym];
      var wma50 = lmm50[sym];
      var wma150 = lmm150[sym];
      var wma300 = lmm300[sym];
      var macd = lmacd[sym];

      
      if (wma150.Result.LastValue > wma300.Result.LastValue
          && wma150.Result.Last(1) > wma300.Result.Last(1)
          && timing.Reference.In(1, 4, -2, -3)
          && series.Low.LastValue <= wma150.Result.LastValue
          && macd.Signal.LastValue < 0
          && macd.MACD.LastValue < 0) {
        if(macd.Histogram.LastValue <= 0) {
          return 1;
        } else {
          return 2;
        }
      }

      if (wma150.Result.LastValue < wma300.Result.LastValue
          && wma150.Result.Last(1) < wma300.Result.Last(1)
          && timing.Reference.In(-1, -4, 2, 3)
          && series.High.LastValue >= wma150.Result.LastValue
          && macd.Signal.LastValue > 0
          && macd.MACD.LastValue > 0) {
        if (macd.Histogram.LastValue >= 0) {
          return 1;
        } else {
          return 2;
        }
      }

      return 0;
    }

    public int GetVcnSignal(String sym, Timing timing) {
      var series = lseries[sym];
      var wma150 = lmm150[sym];
      var ema8 = lmm8[sym];

      if (timing.Reference.In(1, 4)
          && timing.Local.In(1, 4)
          && series.Low.Last(1) > ema8.Result.Last(1)
          && series.Low.Last(2) > ema8.Result.Last(2)
          && series.Low.LastValue <= ema8.Result.LastValue) {
        return 2;
      }

      if (timing.Reference.In(-1, -4)
          && timing.Local.In(-1, -4)
          && series.High.Last(1) < ema8.Result.Last(1)
          && series.High.Last(2) < ema8.Result.Last(2)
          && series.High.LastValue >= ema8.Result.LastValue) {
        return -2;
      }

      return 0;
    }

    // -------------------------------------------
    // ----  Notifications
    // -------------------------------------------

    public void HandleEmailAlerts(State.Value value) {
      var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
      var subkey = String.Format("{0}-{1}", value.GetKey(), this.TFrame);

      if (value.IsZero()) {
        Registry.SetValue(keyName, subkey, 0);
      } else {
        var candidate = (int)Registry.GetValue(keyName, subkey, 0);

        var from = "andsux@gmail.com";
        var to = "niwi@niwi.nz";

        if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
          Registry.SetValue(keyName, subkey, value.Val);
         
          var tradeType = value.GetTradeType().ToString();
          var subject = String.Format("Trade oportunity: {0} {1} {2}/{3} - Punctuation: {4} ", value.Name, tradeType, value.Symbol, TFrame.AsString(), value.Val);

          Notifications.SendEmail(from, to, subject, "");
        }
      }
    }

    public void HandleSoundAlerts(State.Value value) {
      var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
      var subkey = String.Format("{0}-{1}-SND", value.GetKey(), this.TFrame);

      if (value.IsZero()) {
        Registry.SetValue(keyName, subkey, 0);
      } else {
        var candidate = (int)Registry.GetValue(keyName, subkey, 0);
        if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
          Registry.SetValue(keyName, subkey, value.Val);
          Notifications.PlaySound(AlertSoundPath);
        }
      }
    }
  }

  public class State {
    public class Value {
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }
      public String Name { get; set; }
      public Timing Timing { get; set; }
      public String Symbol { get; set; }
      public Int32 Val { get; set; }

      public Value(String name, String symbol, Timing timing, Int32 value) {
        this.CreatedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;

        this.Name = name;
        this.Symbol = symbol;
        this.Timing = timing;
        this.Val = value;
      }

      public String GetKey() {
        return String.Format("{0}-{1}", this.Symbol, this.Name);
      }

      public bool IsZero() {
        return (this.Val == 0);
      }

      public TradeType GetTradeType() {
        return this.Val > 0 ? TradeType.Buy : TradeType.Sell;
      }

      public void UpdateWith(Value other) {
        if (other.Symbol != this.Symbol) {
          throw new Exception("Cant update value with not matching Symbol.");
        }

        if (other.Name != this.Name) {
          throw new Exception("Cant update value with not matching Name.");
        }

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
      output += string.Format("\tTime Frame: {0}\r\n\r\n", timeFrame.AsString());
      output += string.Format("\t{0,-10}\t{1,-8}\t{2,-8}\t{3,-8}\t{4,-20}\r\n", "Symbol", "TM",  "ST", "PT", "Created At");
      output += "\t----------------------------------------------------------------------------------------------------------\r\n";

      foreach (var item in local.ToList().OrderBy(o => o.Value.CreatedAt).Reverse()) {
        var key = item.Key;
        var value = item.Value;
        var timing = value.Timing;

        var timingStr = string.Format("{0,2},{1,2}", timing.Reference, timing.Local);
        var stStr = string.Format("{0,4}", value.Name);
        var ptstr = string.Format("{0,2}", value.Val);
        output += string.Format("\t{0,-10}\t{1,-8}\t{2,-8}\t{3,-8}\t{4,-20}\r\n", value.Symbol, timingStr, stStr, ptstr, value.CreatedAt.TimeAgo());
      }

      return output;
    }
  }
}

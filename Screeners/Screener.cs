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

    [Parameter("Enable Pull Back Signals?", DefaultValue = true)]
    public Boolean EnablePB { get; set; }

    //[Parameter("Enable ACC?", DefaultValue = false)]
    //public Boolean EnableAcc { get; set; }

    //[Parameter("Enable VCN?", DefaultValue = false)]
    //public Boolean EnableVcn { get; set; }

    [Parameter("Enable email alerts?", DefaultValue = false)]
    public Boolean EnableEmailAlerts { get; set; }

    [Parameter("Enable sound Alerts?", DefaultValue = false)]
    public Boolean EnableSoundAlerts { get; set; }

    private String[] symbols = new String[] {
      // Main Currency Pairs
      "EURUSD",
      "GBPUSD",
      "NZDUSD",
      "AUDUSD",
      "USDJPY",
      "USDCHF",
      "USDCAD",

      // Personal Preference
      "NZDJPY",
      "NZDCAD",
      "AUDCAD",
      "AUDNZD",
      "AUDJPY",
      "EURJPY",
      "EURAUD",
     
      // Indexes
      "USTEC",
      "US500",
      "DE30",
      "STOXX50",
    };

    private Dictionary<String, String> spaths;

    private Dictionary<String, MarketSeries> lseries;
    private Dictionary<String, MarketSeries> rseries;

    private Dictionary<String, ExponentialMovingAverage> lmm6;
    private Dictionary<String, ExponentialMovingAverage> lmm18;
    private Dictionary<String, ExponentialMovingAverage> lmm50;
    private Dictionary<String, ExponentialMovingAverage> lmm100;
    private Dictionary<String, ExponentialMovingAverage> rmm100;
    private Dictionary<String, SimpleMovingAverage> lmm200;
    private Dictionary<String, SimpleMovingAverage> rmm200;

    private Dictionary<String, MacdCrossOver> lmacd;
    private Dictionary<String, MacdCrossOver> rmacd;

    private State state;

    protected override void OnStart() {
      state = new State(TFrame);

      lseries = new Dictionary<string, MarketSeries>();
      rseries = new Dictionary<string, MarketSeries>();

      lmm6 = new Dictionary<string, ExponentialMovingAverage>();
      lmm18 = new Dictionary<string, ExponentialMovingAverage>();
      lmm50 = new Dictionary<string, ExponentialMovingAverage>();
      lmm100 = new Dictionary<string, ExponentialMovingAverage>();
      rmm100 = new Dictionary<string, ExponentialMovingAverage>();

      lmm200 = new Dictionary<string, SimpleMovingAverage>();
      rmm200 = new Dictionary<string, SimpleMovingAverage>();

      lmacd = new Dictionary<string, MacdCrossOver>();
      rmacd = new Dictionary<string, MacdCrossOver>();

      spaths = new Dictionary<string, string>(3);
      spaths.Add("MACD", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-1.wav");
      spaths.Add("ACC", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-4.wav");
      spaths.Add("VCN", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-3.wav");

      Print("Initializing screener local state.");

      foreach (var sym in symbols) {
        Print("Initializing data for: {0}/{1}.", sym, TFrame);

        var reftf = GetReferenceTimeframe(TFrame);
        var lmks = MarketData.GetSeries(sym, TFrame);
        var rmks = MarketData.GetSeries(sym, reftf);

        lmm6[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 6);
        lmm18[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 18);

        lmm50[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 50);
        lmm100[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 100);
        rmm100[sym] = Indicators.ExponentialMovingAverage(rmks.Close, 100);

        lmm200[sym] = Indicators.SimpleMovingAverage(lmks.Close, 200);
        rmm200[sym] = Indicators.SimpleMovingAverage(rmks.Close, 200);

        lmacd[sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
        rmacd[sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

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

        if (EnablePB) {
          var val = GetPBSignal(sym, timing);
          var value = new State.Value("PB", sym, timing, val);
          state.Update(value);

          if (EnableEmailAlerts) {
            HandleEmailAlerts(value);

          }

          if (EnableSoundAlerts) {
            HandleSoundAlerts(value);
          }
        }

        //if (EnableAcc) {
        //  var val = GetAccSignal(sym, timing);
        //  var value = new State.Value("ACC", sym, timing, val);
        //  state.Update(value);

        //  if (EnableEmailAlerts) {
        //    HandleEmailAlerts(value);

        //  }

        //  if (EnableSoundAlerts) {
        //    HandleSoundAlerts(value);
        //  }
        //}

        //if (EnableVcn) {
        //  var val = GetVcnSignal(sym, timing);
        //  var value = new State.Value("VCN", sym, timing, val);
        //  state.Update(value);

        //  if (EnableEmailAlerts) {
        //    HandleEmailAlerts(value);
        //  }

        //  if (EnableSoundAlerts) {
        //    HandleSoundAlerts(value);
        //  }
        //}
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
      if (IsTrendUp(lseries[sym], lmm200[sym])) {
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
      if (IsTrendUp(rseries[sym], rmm200[sym])) {
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

    public TimeFrame GetReferenceTimeframe(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return TimeFrame.Daily;
      } else if (tf == TimeFrame.Minute5 || tf == TimeFrame.Minute) {
        return TimeFrame.Hour;
      } else if (tf == TimeFrame.Minute15) {
        return TimeFrame.Hour4;
      } else if (tf == TimeFrame.Daily) {
        return TimeFrame.Weekly;
      } else if (tf == TimeFrame.Weekly) {
        return TimeFrame.Weekly;
      } else {
        return TimeFrame.Daily;
      }
    }

    private bool IsTrendUp(MarketSeries series, SimpleMovingAverage ma) {
      var close = series.Close.LastValue;
      var value = ma.Result.LastValue;

      if (value < close) {
        return true;
      } else {
        return false;
      }
    }

    // -------------------------------------------
    // ----  MACD
    // -------------------------------------------

    public int GetPBSignal(string sym, Timing timing) {
      var series = lseries[sym];
      var mm50 = lmm50[sym];
      var mm100 = lmm100[sym];
      var mm200 = lmm200[sym];
      var macd = lmacd[sym];

      if (mm100.Result.LastValue > mm200.Result.LastValue
          && mm100.Result.Last(1) > mm200.Result.Last(1)
          && mm50.Result.LastValue > mm200.Result.LastValue
          && timing.Reference.In(1, 4, -2, -3)
          && series.High.LastValue > mm200.Result.LastValue
          && macd.MACD.LastValue < 0) {
        if(macd.Histogram.LastValue <= 0) {
          return 1;
        } else {
          return 2;
        }
      }

      if (mm100.Result.LastValue < mm200.Result.LastValue
          && mm100.Result.Last(1) < mm200.Result.Last(1)
          && mm50.Result.LastValue < mm200.Result.LastValue
          && timing.Reference.In(-1, -4, 2, 3)
          && series.Low.LastValue < mm200.Result.LastValue
          && macd.MACD.LastValue > 0) {
        if (macd.Histogram.LastValue >= 0) {
          return 1;
        } else {
          return 2;
        }
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
          Notifications.PlaySound(spaths[value.Name]);
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

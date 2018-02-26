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

  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.None)]
  public class Screener : Robot {

    [Parameter("Source")]
    public DataSeries Source { get; set; }

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
      "#USSPX500"
    };

    private Dictionary<String, Dictionary<TimeFrame, MarketSeries>> lseries;
    private Dictionary<String, Dictionary<TimeFrame, MarketSeries>> rseries;

    private Dictionary<String, Dictionary<TimeFrame, ExponentialMovingAverage>> lmm8;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> lmm50;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> lmm150;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> lmm300;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> rmm150;
    private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> rmm300;

    private Dictionary<String, Dictionary<TimeFrame, MacdCrossOver>> lmacd;
    private Dictionary<String, Dictionary<TimeFrame, MacdCrossOver>> rmacd;

    private Dictionary<String, Dictionary<TimeFrame, StochasticOscillator>> lstoc;

    private List<TimeFrame> timeFrames;


    private State state;

    private long barCounter;
    private long ticks = 0;
    private bool fistRun = true;

    protected override void OnStart() {
      barCounter = Source.Count;

      state = new State();
      timeFrames = new List<TimeFrame>(new[] { TimeFrame.Minute5, TimeFrame.Hour, TimeFrame.Daily });

      lseries = new Dictionary<string, Dictionary<TimeFrame, MarketSeries>>();
      rseries = new Dictionary<string, Dictionary<TimeFrame, MarketSeries>>();

      lmm8 = new Dictionary<string, Dictionary<TimeFrame, ExponentialMovingAverage>>();
      lmm50 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      lmm150 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      lmm300 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      rmm150 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
      rmm300 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();

      lstoc = new Dictionary<string, Dictionary<TimeFrame, StochasticOscillator>>();
      lmacd = new Dictionary<string, Dictionary<TimeFrame, MacdCrossOver>>();
      rmacd = new Dictionary<string, Dictionary<TimeFrame, MacdCrossOver>>();

      Print("Initializing screener local state.");


      foreach (var sym in symbols) {
        //state.Update(sym, TimeFrame.Hour, new State.Data(new Timing(1, 1), 1, 1, 1));
        lseries[sym] = new Dictionary<TimeFrame, MarketSeries>();
        rseries[sym] = new Dictionary<TimeFrame, MarketSeries>();

        lmm8[sym] = new Dictionary<TimeFrame, ExponentialMovingAverage>();
        lmm50[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        lmm150[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        lmm300[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        rmm150[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
        rmm300[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();

        lstoc[sym] = new Dictionary<TimeFrame, StochasticOscillator>();
        lmacd[sym] = new Dictionary<TimeFrame, MacdCrossOver>();
        rmacd[sym] = new Dictionary<TimeFrame, MacdCrossOver>();

        foreach (var tf in timeFrames) {
          Print("Initializing data for: {0}/{1}.", sym, tf);

          var reftf = GetReferenceTimeframe(tf);
          var lmks = MarketData.GetSeries(sym, tf);
          var rmks = MarketData.GetSeries(sym, reftf);

          lmm8[sym][tf] = Indicators.ExponentialMovingAverage(lmks.Close, 8);
          lmm50[sym][tf] = Indicators.WeightedMovingAverage(lmks.Close, 50);
          lmm150[sym][tf] = Indicators.WeightedMovingAverage(lmks.Close, 150);
          lmm300[sym][tf] = Indicators.WeightedMovingAverage(lmks.Close, 300);
          rmm150[sym][tf] = Indicators.WeightedMovingAverage(rmks.Close, 150);
          rmm300[sym][tf] = Indicators.WeightedMovingAverage(rmks.Close, 300);

          lmacd[sym][tf] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
          rmacd[sym][tf] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

          lstoc[sym][tf] = Indicators.StochasticOscillator(lmks, 13, 3, 3, MovingAverageType.Weighted);

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
    // ----  ACC
    // -------------------------------------------

    public int GetAccSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[sym][tf];
      var wma50 = lmm50[sym][tf];
      var stoc = lstoc[sym][tf];

      // Only operate on impulsive reference timing.
      if (timing.Item1.In(1, 4)) {
        if (series.Low.LastValue <= wma50.Result.LastValue
            && stoc.PercentK.LastValue < 30
            && stoc.PercentK.LastValue < stoc.PercentD.LastValue) {
          return 1;
        } else if ((series.Low.LastValue <= wma50.Result.LastValue
                    || series.Low.Last(1) <= wma50.Result.Last(1))
                   && (stoc.PercentK.Last(1) < 30 || stoc.PercentK.LastValue <= 30)
                   && stoc.PercentK.HasCrossedAbove(stoc.PercentD, 2)) { 
          return 2;
        } else {
          return 0;
        }
      } else if (timing.Item1.In(-1, -4)) {
        if (series.High.LastValue >= wma50.Result.LastValue
            && stoc.PercentK.LastValue > 70
            && stoc.PercentK.LastValue > stoc.PercentD.LastValue) {
          return -1;
        } else if ((series.High.LastValue >= wma50.Result.LastValue
                    || series.High.Last(1) >= wma50.Result.Last(1))
                   && (stoc.PercentK.Last(1) > 70 || stoc.PercentK.LastValue >= 70)
                   && stoc.PercentK.HasCrossedBelow(stoc.PercentD, 2)) {
          return -2;
        } else {
          return 0;
        }
      } else {
        return 0;
      }
    }
    
    // -------------------------------------------
    // ----  MACD
    // -------------------------------------------

    public int GetMacdSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[sym][tf];
      var wma150 = lmm150[sym][tf];
      var macd = lmacd[sym][tf];

      // Only operate on impulsive reference timing.
      if (timing.Item1.In(1, 4)) {
        if (series.Low.LastValue <= wma150.Result.LastValue
            && (macd.Signal.LastValue < 0 || macd.MACD.LastValue < 0)
            && macd.Histogram.LastValue < 0) {
          return 1;
        } else if (series.Low.LastValue <= wma150.Result.LastValue
                   && (macd.Signal.LastValue < 0 || macd.MACD.LastValue < 0)
                   && macd.Histogram.LastValue >= 0) {
          return 2;
        } else {
          return 0;
        }
      } else if (timing.Item1.In(-1, -4)) {
        if (series.High.LastValue >= wma150.Result.LastValue
            && (macd.Signal.LastValue > 0 || macd.MACD.LastValue > 0)
            && macd.Histogram.LastValue > 0) {
          return -1;
        } else if (series.Low.LastValue >= wma150.Result.LastValue
                   && (macd.Signal.LastValue > 0 || macd.MACD.LastValue > 0)
                   && macd.Histogram.LastValue <= 0) {
          return -2;
        } else {
          return 0;
        }
      } else {
        return 0;
      }
    }

    // -------------------------------------------
    // ----  VCN
    // -------------------------------------------

    public int GetVcnSignal(string sym, TimeFrame tf, Timing timing) {
      var series = lseries[sym][tf];
      var ema8 = lmm8[sym][tf];
      var wma50 = lmm50[sym][tf];

      if (timing.Reference.In(1, 4) && timing.Local.In(1, 4) && ema8.Result.LastValue > wma50.Result.LastValue) {
        if (series.Low.Last(1) > ema8.Result.Last(1)
            && series.Low.Last(2) > ema8.Result.Last(2)
            && series.Low.Last(3) > ema8.Result.Last(3)
            && series.Low.LastValue <= ema8.Result.LastValue) {
          return 2;
        } else if (series.Low.LastValue > ema8.Result.LastValue) {
          return 1;
        } else {
          return 0;
        }
      } else if (timing.Reference.In(-1, -4) && timing.Local.In(-1, -4) && ema8.Result.LastValue < wma50.Result.LastValue) {
        if (series.High.Last(1) < ema8.Result.Last(1)
            && series.High.Last(2) < ema8.Result.Last(2)
            && series.High.Last(3) < ema8.Result.Last(3)
            && series.High.LastValue >= ema8.Result.LastValue) {
          return -2;
        } else if (MarketSeries.High.LastValue < ema8.Result.LastValue) {
          return -1;
        } else {
          return 0;
        }
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
          var vcn = GetVcnSignal(sym, tf, timing);
          var macd = GetMacdSignal(sym, tf, timing);
          var acc = GetAccSignal(sym, tf, timing);

          var data = new State.Data(timing, vcn, macd, acc);
          state.Update(sym, tf, data);
        }
      }

      var output = state.Render();

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", output, StaticPosition.TopLeft, Colors.Black);
    }
  }

  public class State {
    public class Data : Tuple<Timing, Int32, Int32, Int32> {
      public Timing Timing { get { return Item1; }  }
      public Int32 Vcn { get { return Item2; } }
      public Int32 Macd { get { return Item3; } }
      public Int32 Acc { get { return Item4; } }

      public Data(Timing timing, Int32 vcn, Int32 macd, Int32 acc) : base(timing, vcn, macd, acc) { }

      public bool IsZero() {
        return Vcn == 0 && Macd == 0 && Acc == 0;
      }
    }

    public class Key : Tuple<String, TimeFrame> {
      public String Symbol { get { return Item1; } }
      public TimeFrame TimeFrame { get { return Item2; } }

      public Key(string symbol, TimeFrame timeFrame) : base(symbol, timeFrame) { }
    }

    public class Value {
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }
      public Data Data { get; set; }

      public Value() { }
      public Value(Data data) {
        this.Data = data;
        this.CreatedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
      }
    }

    private Dictionary<Key, Value> local;

    public State() {
      local = new Dictionary<Key, Value>();
    }

    public void Update(String sym, TimeFrame tf, Data data) {
      var key = new Key(sym, tf);

      if (data.IsZero()) {
        local.Remove(key);
      } else {
        if (local.ContainsKey(key)) {
          var value = local[key];
          value.Data = data;
          value.UpdatedAt = DateTime.Now;
        } else {
          var value = new Value(data);
          local.Add(key, value);
        }
      }
    }

    private string TimeFrameToString(TimeFrame tf) {
      if (tf == TimeFrame.Hour) {
        return "H1";
      } else if (tf == TimeFrame.Daily) {
        return "D1";
      } else {
        return "M5";
      }
    }

    public string Render() {
      var output = "";

      output += string.Format("\t\t\t\tUpdated at: {0}\r\n\r\n", DateTime.Now.ToString("yyyy - MM - dd HH: mm:ss"));
      output += string.Format("\t{0,-10}\t{1,-5}\t{2,-8}\t{3,-8}\t{4,-8}\t{5,-8}\t{6,-20}\r\n", "Symbol", "TF", "TM", "VCN", "MACD", "ACC", "Created At");
      output += "\t----------------------------------------------------------------------------------------------------------\r\n";

      foreach (var item in local.ToList().OrderBy(o => o.Value.CreatedAt).Reverse()) {
        var key = item.Key;
        var value = item.Value;

        var timing = value.Data.Timing;

        var tfStr = TimeFrameToString(key.TimeFrame);
        var timingStr = string.Format("{0,2},{1,2}", timing.Reference, timing.Local);

        var vcnStr = string.Format("{0,2}", value.Data.Vcn);
        var macdStr = string.Format("{0,2}", value.Data.Macd);
        var accStr = string.Format("{0,2}", value.Data.Acc);
        output += string.Format("\t{0,-10}\t{1,-5}\t{2,-8}\t{3,-8}\t{4,-8}\t{5,-8}\t{6,-20}\r\n", key.Symbol, tfStr, timingStr, vcnStr, macdStr, accStr, value.CreatedAt.TimeAgo());
      }

      return output;
    }
  }
}

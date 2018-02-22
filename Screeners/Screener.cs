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
  using TimingResult = Tuple<int, int>;

  static class Extensions {
    public static bool In<T>(this T item, params T[] items) {
      if (items == null)
        throw new ArgumentNullException("items");

      return items.Contains(item);
    }
  }

  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.None)]
  public class Screener : Robot {

    [Parameter("Source")]
    public DataSeries Source { get; set; }

    [Parameter("Time Frame")]
    public TimeFrame loctf { get; set; }

    private Dictionary<String, MarketSeries> lseries;
    private Dictionary<String, MarketSeries> rseries;

    private String[] symbols = new String[] {
            "EURUSD",
            "GBPUSD",
            //"EURJPY",
            //"USDJPY",
            //"AUDUSD",
            //"USDCHF",
            //"GBPJPY",
            //"USDCAD",
            //"EURGBP",
            //"EURCHF",
            //"AUDJPY",
            //"NZDUSD",
            //"CHFJPY",
            //"EURAUD",
            //"CADJPY",
            //"GBPAUD",
            //"EURCAD",
            //"AUDCAD",
            "GBPCHF",
            //"AUDCHF",
            //"GBPCAD",
            //"GBPNZD",
            //"NZDCAD",
            //"NZDCHF",
            //"NZDJPY",
            //"AUDNZD",
            //"CADCHF",
            //"EURNZD"
        };
    //private String[] pairs = new String[] { "EURUSD", "USDJPY" };

    private Dictionary<String, ExponentialMovingAverage> lmm8;
    private Dictionary<String, WeightedMovingAverage> lmm50;
    private Dictionary<String, WeightedMovingAverage> lmm150;
    private Dictionary<String, WeightedMovingAverage> lmm300;
    private Dictionary<String, WeightedMovingAverage> rmm150;
    private Dictionary<String, WeightedMovingAverage> rmm300;

    private Dictionary<String, MacdCrossOver> lmacd;
    private Dictionary<String, MacdCrossOver> rmacd;

    private Dictionary<String, StochasticOscillator> lstoc;

    private long barCounter;
    private long ticks = 0;
    private TimeFrame reftf;
    private bool fistRun = true;

    protected override void OnStart() {
      barCounter = Source.Count;

      lseries = new Dictionary<string, MarketSeries>();
      rseries = new Dictionary<string, MarketSeries>();

      lmm8 = new Dictionary<string, ExponentialMovingAverage>();
      lmm50 = new Dictionary<string, WeightedMovingAverage>();
      lmm150 = new Dictionary<string, WeightedMovingAverage>();
      lmm300 = new Dictionary<string, WeightedMovingAverage>();
      rmm150 = new Dictionary<string, WeightedMovingAverage>();
      rmm300 = new Dictionary<string, WeightedMovingAverage>();

      lstoc = new Dictionary<string, StochasticOscillator>();

      lmacd = new Dictionary<string, MacdCrossOver>();
      rmacd = new Dictionary<string, MacdCrossOver>();

      Print("Initializing screener local state.");

      reftf = GetReferenceTimeframe(loctf);

      foreach (var sym in symbols) {
        var lmks = MarketData.GetSeries(sym, loctf);
        var rmks = MarketData.GetSeries(sym, reftf);

        Print("Initializing data for: {0}/{1}.", sym, loctf);

        lmm8[sym] = Indicators.ExponentialMovingAverage(lmks.Close, 8);
        lmm50[sym] = Indicators.WeightedMovingAverage(lmks.Close, 50);
        lmm150[sym] = Indicators.WeightedMovingAverage(lmks.Close, 150);
        lmm300[sym] = Indicators.WeightedMovingAverage(lmks.Close, 300);
        rmm150[sym] = Indicators.WeightedMovingAverage(rmks.Close, 150);
        rmm300[sym] = Indicators.WeightedMovingAverage(rmks.Close, 300);

        lmacd[sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
        rmacd[sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

        lstoc[sym] = Indicators.StochasticOscillator(lmks, 13, 3, 3, MovingAverageType.Weighted);

        lseries[sym] = lmks;
        rseries[sym] = rmks;
      }

      Print("Initialization finished.");
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

    private Tuple<int, int> CalculateMarketTiming(String sym) {
      var local = CalculateLocalTiming(sym);
      var reference = CalculateReferenceTiming(sym);

      return new Tuple<int, int>(reference, local);
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

    // -------------------------------------------
    // ----  ACC
    // -------------------------------------------

    public int GetAccSignal(string sym, TimingResult timing) {
      var series = lseries[sym];
      var wma50 = lmm50[sym];
      var stoc = lstoc[sym];

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

    public int GetMacdSignal(string sym, TimingResult timing) {
      var series = lseries[sym];
      var wma150 = lmm150[sym];
      var macd = lmacd[sym];

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

    public int GetVcnSignal(string sym, TimingResult timing) {
      var series = lseries[sym];
      var ema8 = lmm8[sym];

      if (timing.Item1.In(1, 4) && timing.Item2.In(1, 4)) {
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
      } else if (timing.Item1.In(-1, -4) && timing.Item2.In(-1, -4)) {
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
      var buffer = new List<String>(symbols.Length);
      var time = Server.Time.ToString("yyyy-MM-dd HH:mm:ss");

      buffer.Add(string.Format("Timeframe: {0}\r\nUpdated at: {1}\r\n\r\n\r\n", loctf.ToString(), time));
      buffer.Add(string.Format("{0,12}\t{1,8}\t{2,8}\t{3,8}\t{4,8}\r\n", "Symbol", "Timing", "VCN", "MACD", "ACC"));
      buffer.Add("-------------------------------------------------------------------------------------\n\r");

      foreach (var sym in symbols) {
        buffer.Add(HandleUpdateSymbol(sym));
      }

      var result = "";
      foreach (var item in buffer) {
        result += item;
      }

      ChartObjects.RemoveObject("screener");
      ChartObjects.DrawText("screener", result, StaticPosition.TopLeft, Colors.Black);
    }

    private string HandleUpdateSymbol(String sym) {
      var timing = CalculateMarketTiming(sym);
      var vcn = GetVcnSignal(sym, timing);
      var macd = GetMacdSignal(sym, timing);
      var acc = GetAccSignal(sym, timing);

      var timingStr = string.Format("{0,2},{1,2}", timing.Item1, timing.Item2);
      var vcnStr = string.Format("{0,2}", vcn);
      var macdStr = string.Format("{0,2}", macd);
      var accStr = string.Format("{0,2}", acc);
      return string.Format("{0, 12}\t{1,8}\t{2,8}\t{3,8}\t{4,8}\r\n", sym, timingStr, vcnStr, macdStr, accStr);
    }
  }
}

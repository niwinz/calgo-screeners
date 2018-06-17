// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2018 Andrey Antukh <niwi@niwi.nz>

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
//using System.Web.Script.Serialization;


namespace cAlgo {
  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.FullAccess)]
  public class ScreenerITD : Robot {

    [Parameter("PullBack M5?", DefaultValue = false)]
    public bool EnablePBM5 { get; set; }

    [Parameter("PullBack H1?", DefaultValue = true)]
    public bool EnablePBH1 { get; set; }

    [Parameter("PullBack H4?", DefaultValue = true)]
    public bool EnablePBH4 { get; set; }

    [Parameter("VCN M5?", DefaultValue = true)]
    public bool EnableVCNM5 { get; set; }

    [Parameter("VCN H1?", DefaultValue = true)]
    public bool EnableVCNH1 { get; set; }

    [Parameter("MX M5?", DefaultValue = true)]
    public bool EnableMXM5 { get; set; }

    [Parameter("MX H1?", DefaultValue = true)]
    public bool EnableMXH1 { get; set; }

    [Parameter("MMX Periods?", DefaultValue = 12)]
    public int MXPeriods { get; set; }

    [Parameter("Events Bus?", DefaultValue = true)]
    public bool EnableEventsBus { get; set; }


    //[Parameter("Enable email alerts?", DefaultValue = false)]
    //public Boolean EnableEmailAlerts { get; set; }

    //[Parameter("Enable sound Alerts?", DefaultValue = false)]
    //public Boolean EnableSoundAlerts { get; set; }

    private String[] symbols = new String[] {
      //"AUDUSD",
      //"AUDCAD",
      //"AUDCHF",
      //"AUDJPY",
      //"AUDNZD",
      //"CADJPY",
      //"CADCHF",
      //"CHFJPY",
      //"GBPUSD",
      //"GBPAUD",
      //"GBPCHF",
      //"GBPJPY",
      //"GBPNZD",
      //"EURCHF",
      //"EURJPY",
      //"EURNZD",
      //"EURGBP",
      //"EURAUD",
      //"EURUSD",
      //"NZDCHF",
      //"NZDCAD",
      //"NZDJPY",
      //"NZDUSD",
      //"XAUUSD",
      //"XTIUSD",
      //"US500",
      //"USTEC",
      //"DE30",
      //"ES35",
      //"STOXX50",

      // Forex

      "AUDUSD",

      "GBPAUD",
      "GBPUSD",
      "GBPCHF",

      "CHFJPY",

      "EURAUD",
      "EURGBP",
      "EURJPY",
      "EURNZD",
      "EURUSD",
      
      "USDCAD",
      "USDJPY",

      //"NZDUSD", // baja volatilidad

      "XAUUSD",
      "XTIUSD",

      // Indexes
      "US500",
      "USTEC",
      "DE30",
    };

    private Dictionary<String, String> soundPaths;

    private Dictionary<String, AverageTrueRange> atr_D1;
    private Dictionary<String, AverageTrueRange> atr_H4;
    private Dictionary<String, AverageTrueRange> atr_H1;
    private Dictionary<String, AverageTrueRange> atr_M5;

    private Dictionary<String, MarketSeries> seriesD1;
    private Dictionary<String, MarketSeries> seriesH4;
    private Dictionary<String, MarketSeries> seriesH1;
    private Dictionary<String, MarketSeries> seriesM5;
    
    private Dictionary<String, ExponentialMovingAverage> mm8_D1;
    private Dictionary<String, ExponentialMovingAverage> mm8_H4;
    private Dictionary<String, ExponentialMovingAverage> mm8_H1;
    private Dictionary<String, ExponentialMovingAverage> mm8_M5;

    private Dictionary<String, ExponentialMovingAverage> mm55_D1;
    private Dictionary<String, ExponentialMovingAverage> mm55_H4;
    private Dictionary<String, ExponentialMovingAverage> mm55_H1;
    private Dictionary<String, ExponentialMovingAverage> mm55_M5;

    private Dictionary<String, ExponentialMovingAverage> mm200_D1;
    private Dictionary<String, ExponentialMovingAverage> mm200_H4;
    private Dictionary<String, ExponentialMovingAverage> mm200_H1;
    private Dictionary<String, ExponentialMovingAverage> mm200_M5;

    private Dictionary<String, MacdCrossOver> macd_D1;
    private Dictionary<String, MacdCrossOver> macd_H4;
    private Dictionary<String, MacdCrossOver> macd_H1;
    private Dictionary<String, MacdCrossOver> macd_M5;

    private State state;
    private EventsBus Bus;

    protected override void OnStart() {
      Print("Initializing screener local state.");

      seriesD1 = new Dictionary<string, MarketSeries>();
      seriesH4 = new Dictionary<string, MarketSeries>();
      seriesH1 = new Dictionary<string, MarketSeries>();
      seriesM5 = new Dictionary<string, MarketSeries>();

      atr_D1 = new Dictionary<string, AverageTrueRange>();
      atr_H4 = new Dictionary<string, AverageTrueRange>();
      atr_H1 = new Dictionary<string, AverageTrueRange>();
      atr_M5 = new Dictionary<string, AverageTrueRange>();

      mm200_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_M5 = new Dictionary<string, ExponentialMovingAverage>();

      mm55_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_M5 = new Dictionary<string, ExponentialMovingAverage>();

      mm8_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_M5 = new Dictionary<string, ExponentialMovingAverage>();

      macd_D1 = new Dictionary<string, MacdCrossOver>();
      macd_H4 = new Dictionary<string, MacdCrossOver>();
      macd_H1 = new Dictionary<string, MacdCrossOver>();
      macd_M5 = new Dictionary<string, MacdCrossOver>();

      state = new State();
      Bus = new EventsBus("tcp://*:8888");

      foreach (var sym in symbols) {
        Print("Initializing data for {0}", sym);

        // Initialize Indicators
        var lseriesD1 = MarketData.GetSeries(sym, TimeFrame.Daily);
        var lseriesH4 = MarketData.GetSeries(sym, TimeFrame.Hour4);
        var lseriesH1 = MarketData.GetSeries(sym, TimeFrame.Hour);
        var lseriesM5 = MarketData.GetSeries(sym, TimeFrame.Minute5);

        atr_D1[sym] = Indicators.AverageTrueRange(lseriesD1, 14, MovingAverageType.Exponential);
        atr_H4[sym] = Indicators.AverageTrueRange(lseriesH4, 14, MovingAverageType.Exponential);
        atr_H1[sym] = Indicators.AverageTrueRange(lseriesH1, 14, MovingAverageType.Exponential);
        atr_M5[sym] = Indicators.AverageTrueRange(lseriesM5, 14, MovingAverageType.Exponential);

        mm200_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 200);
        mm200_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 200);
        mm200_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 200);
        mm200_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 200);

        mm55_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 55);
        mm55_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 55);
        mm55_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 55);
        mm55_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 55);

        mm8_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 8);
        mm8_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 8);
        mm8_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 8);
        mm8_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 8);

        macd_D1[sym] = Indicators.MacdCrossOver(lseriesD1.Close, 26, 12, 9);
        macd_H4[sym] = Indicators.MacdCrossOver(lseriesH4.Close, 26, 12, 9);
        macd_H1[sym] = Indicators.MacdCrossOver(lseriesH1.Close, 26, 12, 9);
        macd_M5[sym] = Indicators.MacdCrossOver(lseriesM5.Close, 26, 12, 9);

        seriesD1[sym] = lseriesD1;
        seriesH4[sym] = lseriesH4;
        seriesH1[sym] = lseriesH1;
        seriesM5[sym] = lseriesM5;

        state.AddAsset(sym);
      }

      UpdateTiming();

      soundPaths = new Dictionary<string, string>(3) {
        { "PB", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-1.wav" },
        { "MMX", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-4.wav" },
        { "VCN", "C:\\Users\\Andrey\\Music\\Sounds\\sms-alert-3.wav" }
      };

      Print("Initialization finished.");

      if (EnableEventsBus) {
        Print("Starting event bus.");
        Bus.Start();
      }

      Bus.Emit(state.ToJson());

      using (StreamWriter sw = new StreamWriter(@"C:\Users\Andrey\Desktop\json.txt")) {
        sw.Write(state.ToJson());
      }

      Render();
    }

    protected override void OnStop() {
      Bus.Stop();
      Print("Bot stopped.");
    }

    private void UpdateTiming() {
      foreach (var sym in symbols) {
        var timing = CalculateMarketTiming(sym);
        state.UpdateTiming(sym, timing);
      }
    }

    protected override void OnTick() {
      // Update timing data on all symbols
      UpdateTiming();

      // Check for pullback signals

      if (EnablePBM5) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetPBSignal(sym, TimeFrame.Minute5, asset.Timing);
          asset.AddSignal(new Signal("PB", TimeFrame.Minute5, value));
        }
      }

      if (EnablePBH1) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetPBSignal(sym, TimeFrame.Hour, asset.Timing);
          asset.AddSignal(new Signal("PB", TimeFrame.Hour, value));
        }
      }

      if (EnablePBH4) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetPBSignal(sym, TimeFrame.Hour4, asset.Timing);
          asset.AddSignal(new Signal("PB", TimeFrame.Hour4, value));
        }
      }

      if (EnableVCNM5) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetVCNSignal(sym, TimeFrame.Minute5, asset.Timing);
          asset.AddSignal(new Signal("VCN", TimeFrame.Minute5, value));
        }
      }

      if (EnableVCNH1) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetVCNSignal(sym, TimeFrame.Hour, asset.Timing);
          asset.AddSignal(new Signal("VCN", TimeFrame.Hour, value));
        }
      }

      if (EnableMXM5) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetMXSignal(sym, TimeFrame.Minute5, asset.Timing);
          asset.AddSignal(new Signal("MX", TimeFrame.Minute5, value));
        }
      }

      if (EnableMXH1) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetMXSignal(sym, TimeFrame.Hour, asset.Timing);
          asset.AddSignal(new Signal("MX", TimeFrame.Hour, value));
        }
      }

      Render();
      Bus.Emit(state.ToJson());

      using (StreamWriter sw = new StreamWriter(@"C:\Users\Andrey\Desktop\json.txt")) {
        sw.Write(state.ToJson());
      }
    }

    // -------------------------------------------
    // ----  Pull Back Signal
    // -------------------------------------------

    public int GetPBSignal(string sym, TimeFrame ltf, Timing timing) {
      var rtf = ReferenceTimeFrame(ltf);

      MarketSeries series;
      ExponentialMovingAverage mm55;
      ExponentialMovingAverage mm200;
      MacdCrossOver macd;

      if (ltf == TimeFrame.Minute5) {
        series = seriesM5[sym];
        mm55 = mm55_M5[sym];
        mm200 = mm200_M5[sym];
        macd = macd_M5[sym];
      } else if (ltf == TimeFrame.Hour) {
        series = seriesH1[sym];
        mm55 = mm55_H1[sym];
        mm200 = mm200_H1[sym];
        macd = macd_H1[sym];
      } else if (ltf == TimeFrame.Hour4) {
        series = seriesH4[sym];
        mm55 = mm55_H4[sym];
        mm200 = mm200_H4[sym];
        macd = macd_H4[sym];
      } else {
        throw new Exception("TimeFrame not supported.");
      }

      if (mm55.Result.LastValue > mm200.Result.LastValue
          && series.Close.LastValue > mm200.Result.LastValue
          && In(timing.Get(rtf), 1, -2)
          && macd.MACD.LastValue < 0
          && macd.Histogram.LastValue <= 0) {
        return 1;
      }

      if (mm55.Result.LastValue < mm200.Result.LastValue
          && series.Close.LastValue < mm200.Result.LastValue
          && In(timing.Get(rtf), -1, 2)
          && macd.MACD.LastValue > 0
          && macd.Histogram.LastValue >= 0) {
        return -1;
      }

      return 0;
    }

    // -------------------------------------------
    // ----  VCN Signal
    // -------------------------------------------

    public int GetVCNSignal(string sym, TimeFrame ltf, Timing timing) {
      var rtf = ReferenceTimeFrame(ltf);

      MarketSeries series;
      ExponentialMovingAverage mm8;
      ExponentialMovingAverage mm55;
      ExponentialMovingAverage mm200;
      MacdCrossOver macd;

      if (ltf == TimeFrame.Minute5) {
        series = seriesM5[sym];
        mm8 = mm8_M5[sym];
        mm55 = mm55_M5[sym];
        mm200 = mm200_M5[sym];
        macd = macd_M5[sym];
      } else if (ltf == TimeFrame.Hour) {
        series = seriesH1[sym];
        mm8 = mm8_H1[sym];
        mm55 = mm55_H1[sym];
        mm200 = mm200_H1[sym];
        macd = macd_H1[sym];
      } else {
        throw new Exception("TimeFrame not supported.");
      }

      if (macd.Histogram.Last(1) > 0
          && macd.Histogram.Last(2) > 0
          && macd.Histogram.Last(3) > 0
          && series.Low.Last(3) > mm8.Result.Last(3)
          && series.Low.Last(2) > mm8.Result.Last(2)
          && series.Low.Last(1) > mm8.Result.Last(1)
          && series.Low.Last(0) <= mm8.Result.Last(0)) {
        if (In(timing.Get(rtf), 1, -2)) {
          return 2;
        } else {
          return 1;
        }
      }
      if (macd.Histogram.Last(1) < 0
          && macd.Histogram.Last(2) < 0
          && macd.Histogram.Last(3) < 0
          && series.High.Last(3) < mm8.Result.Last(3)
          && series.High.Last(2) < mm8.Result.Last(2)
          && series.High.Last(1) < mm8.Result.Last(1)
          && series.High.Last(0) >= mm8.Result.Last(0)) {
        if (In(timing.Get(rtf), -1, 2)) {
          return -2;
        } else {
          return -1;
        }
      }

      return 0;
    }

    // -------------------------------------------
    // ----  MX Signal
    // -------------------------------------------

    public int GetMXSignal(string sym, TimeFrame ltf, Timing timing) {
      ExponentialMovingAverage mm55;
      ExponentialMovingAverage mm200;

      if (ltf == TimeFrame.Minute5) {
        mm55 = mm55_M5[sym];
        mm200 = mm200_M5[sym];
      } else if (ltf == TimeFrame.Hour) {
        mm55 = mm55_H1[sym];
        mm200 = mm200_H1[sym];
      } else {
        throw new Exception("TimeFrame not supported.");
      }

      var hasCrossedAbove = false;
      var hasCrossedBelow = false;
      var periods = MXPeriods;

      for (int i = 1; i < periods; i++) {
        hasCrossedAbove = mm55.Result.HasCrossedAbove(mm200.Result, i);
        if (hasCrossedAbove == true) break;
      }

      for (int i = 1; i < periods; i++) {
        hasCrossedBelow = mm55.Result.HasCrossedBelow(mm200.Result, i);
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

    public class Timing {
      private Dictionary<TimeFrame, Int32> Data;

      public Int32 D1 { get { return Data[TimeFrame.Daily]; } }
      public Int32 H4 { get { return Data[TimeFrame.Hour4]; } }
      public Int32 H1 { get { return Data[TimeFrame.Hour]; } }
      public Int32 M5 { get { return Data[TimeFrame.Minute5]; } }

      public Timing(int d1, int h4, int h1, int m5) {
        Data = new Dictionary<TimeFrame, int>(4);
        Data[TimeFrame.Daily] = d1;
        Data[TimeFrame.Hour4] = h4;
        Data[TimeFrame.Hour] = h1;
        Data[TimeFrame.Minute5] = m5;
      }

      public Int32 Get(TimeFrame tf) {
        return Data[tf];
      }
    }

    private Timing CalculateMarketTiming(string sym) {
      var d1 = CalculateTiming(macd_D1[sym], mm200_D1[sym], seriesD1[sym]);
      var h4 = CalculateTiming(macd_H4[sym], mm200_H4[sym], seriesH4[sym]);
      var h1 = CalculateTiming(macd_H1[sym], mm200_H1[sym], seriesH1[sym]);
      var m5 = CalculateTiming(macd_M5[sym], mm200_M5[sym], seriesM5[sym]);
      return new Timing(d1, h4, h1, m5);
    }

    private int CalculateTiming(MacdCrossOver macd, ExponentialMovingAverage ma, MarketSeries series) {
      if (IsTrendUp(series, ma)) {
        if (macd.Histogram.LastValue > 0) {
          return 1;
        } else {
          return 2;
        }
      } else {
        if (macd.Histogram.LastValue <= 0) {
          return -1;
        } else {
          return -2;
        }
      }
    }

    private bool IsTrendUp(MarketSeries series, ExponentialMovingAverage ma) {
      var close = series.Close.LastValue;
      var value = ma.Result.LastValue;

      if (value < close) {
        return true;
      } else {
        return false;
      }
    }

    public TimeFrame ReferenceTimeFrame(TimeFrame tf) {
      if (tf == TimeFrame.Minute5) {
        return TimeFrame.Hour;
      } else if (tf == TimeFrame.Hour) {
        return TimeFrame.Daily;
      } else if (tf == TimeFrame.Hour4) {
        return TimeFrame.Daily;
      } else {
        return TimeFrame.Hour;
      }
    }

    // -------------------------------------------
    // ----  Notifications
    // -------------------------------------------

    //public void HandleEmailAlerts(State.Value value) {
    //  var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
    //  var subkey = String.Format("{0}-EMAIL", value.GetKey());

    //  if (value.IsZero()) {
    //    Registry.SetValue(keyName, subkey, 0);
    //  } else {
    //    var obj = Registry.GetValue(keyName, subkey, null);
    //    Int32 candidate;

    //    if (obj == null) {
    //      candidate = 0;
    //    } else {
    //      candidate = (int)obj;
    //    }

    //    var from = "andsux@gmail.com";
    //    var to = "niwi@niwi.nz";

    //    if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
    //      Registry.SetValue(keyName, subkey, value.Val);

    //      var tradeType = value.GetTradeType().ToString();
    //      var tfStr = TimeFrameAsString(value.TimeFrame);

    //      var subject = String.Format("{0} trade oportunity on {1} {2} - Strategy: {3}, Points: {4} ", tradeType, tfStr, value.Symbol, value.Name, value.Val);
    //      Notifications.SendEmail(from, to, subject, "");
    //    }
    //  }
    //}

    //public void HandleSoundAlerts(State.Value value) {
    //  var keyName = "HKEY_CURRENT_USER\\ScreenerEmailNotificationsState";
    //  var subkey = String.Format("{0}-SND", value.GetKey());

    //  if (value.IsZero()) {
    //    Registry.SetValue(keyName, subkey, 0);
    //  } else {
    //    var obj = Registry.GetValue(keyName, subkey, null);
    //    Int32 candidate;

    //    if (obj == null) {
    //      candidate = 0;
    //    } else {
    //      candidate = (int)obj;
    //    }

    //    if ((value.Val > candidate && candidate >= 0) || (value.Val < candidate && candidate <= 0)) {
    //      Registry.SetValue(keyName, subkey, value.Val);
    //      Notifications.PlaySound(spaths[value.Name]);
    //    }
    //  }
    //}

    // -------------------------------------------
    // ----  Internal State
    // -------------------------------------------

    public class Signal {
      public String Name { get; set; }
      public Int32 Value { get; set; }
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }

      private TimeFrame timeFrame;

      public String TimeFrame {
        get { return TimeFrameAsString(timeFrame); }
      }

      public Signal(String name, TimeFrame tf, Int32 value) {
        this.Name = name;
        this.timeFrame = tf;
        this.Value = value;
        this.CreatedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
      }

      public bool IsUpTrend() {
        return this.Value > 0;
      }

      public String Key() {
        return string.Format("{0}-{1}", this.Name, this.TimeFrame);
      }

      public bool IsValid() {
        return this.Value != 0;
      }
      
      public void UpdateWith(Signal other) {
        if (this.Value != other.Value) {
          this.UpdatedAt = DateTime.Now;
        }
        this.Value = other.Value;
      }

      public override bool Equals(object obj) {
        return this.Key().Equals(((Signal)obj).Key());
      }

      public override int GetHashCode() {
        return this.Key().GetHashCode();
      }
    }

    public class Asset {
      public string Name { get; set; }
      public Timing Timing { get; set; }

      private Dictionary<String, Signal> Data;

      public HashSet<Signal> Signals {
        get { return new HashSet<Signal>(Data.Values); }
      }

      public Asset(string name) {
        this.Data = new Dictionary<string, Signal>();
        this.Name = name;
      }

      public void AddSignal(Signal signal) {
        if (signal.IsValid()) {
          try {
            Data[signal.Key()].UpdateWith(signal);
          } catch (KeyNotFoundException) {
            Data.Add(signal.Key(), signal);
          }
        } else {
          if (Data.ContainsKey(signal.Key())) {
            Data.Remove(signal.Key());
          }
        }
      }
    }

    public class State {
      public OrderedDictionary Data { get; set; }

      public State() {
        Data = new OrderedDictionary();
      }

      public Asset GetAsset(String name) {
        return Data[name] as Asset;
      }

      public void AddAsset(string name) {
        Data.Add(name, new Asset(name));
      }

      public void UpdateTiming(string key, Timing value) {
        var asset = GetAsset(key);
        asset.Timing = value;
      }

      public String ToJson() {
        JsonSerializer serializer = new JsonSerializer();
        //serializer.Converters.Add(new JavaScriptDateTimeConverter());
        serializer.NullValueHandling = NullValueHandling.Ignore;
        serializer.Formatting = Formatting.Indented;

        using (var buffer = new StringWriter()) {
          var values = Data.Values as ICollection<Asset>;
          serializer.Serialize(buffer, Data.Values);
          return buffer.ToString();
        }
      }
    }
    // -------------------------------------------
    // ----  Rendering
    // -------------------------------------------

    private void Render() {
      var output = new List<String>(symbols.Length+2);

      output.Add(string.Format("\t{0,-15}\t{1,-20}\t{2}", "Asset", "Timing", "Signals"));
      output.Add("\t----------------------------------------------------------------------------------------------------------");

      foreach (var sym in symbols) {
        var asset = state.GetAsset(sym);
        var timing = asset.Timing;

        var timingStr = string.Format("{0},{1},{2},{3}",
                                      timing.D1,
                                      timing.H4,
                                      timing.H1,
                                      timing.M5);

        var signalsOutput = new HashSet<String>();
        foreach (var signal in asset.Signals) {
          if (signal.IsUpTrend()) {
            signalsOutput.Add(string.Format("{0}(▲,{1},{2})", signal.Name, signal.TimeFrame, signal.Value));
          } else {
            signalsOutput.Add(string.Format("{0}(▼,{1},{2})", signal.Name, signal.TimeFrame, signal.Value));
          }
        }

        output.Add(string.Format("\t{0,-15}\t{1,-20}\t{2}", asset.Name, timingStr, String.Join(", ", signalsOutput)));

        var buffer = String.Join("\r\n", output);
        ChartObjects.RemoveObject("screener");
        ChartObjects.DrawText("screener", buffer, StaticPosition.TopLeft, Colors.Black);
      }
    }

    // -------------------------------------------
    // ----  Event Bus
    // -------------------------------------------

    public class EventsBus {
      private PublisherSocket Server;
      private String BindAddress;
      private Boolean Started = false;

      public EventsBus(String bind) {
        Server = new PublisherSocket();
        Server.Options.SendHighWatermark = 5000;

        BindAddress = bind;
      }

      public void Start() {
        Server.Bind(BindAddress);
        Started = true;
      }

      public void Stop() {
        if (Started) {
          Started = false;
          Server.Close();
        }
      }

      public void Emit(String msg) {
        if (Started) {
          Server.SendMoreFrame("update").SendFrame(msg);
        }
      }
    }

    // -------------------------------------------
    // ----  Helpers
    // -------------------------------------------

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

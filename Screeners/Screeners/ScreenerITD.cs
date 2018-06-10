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

namespace cAlgo {
  [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.FullAccess)]
  public class ScreenerITD : Robot {
    //[Parameter("Source")]
    //public DataSeries Source { get; set; }

    [Parameter("PullBack H4?", DefaultValue = true)]
    public bool EnablePBH4 { get; set; }

    [Parameter("PullBack H1?", DefaultValue = true)]
    public bool EnablePBgH1 { get; set; }

    [Parameter("Events Bus?", DefaultValue = true)]
    public bool EnableEventsBus { get; set; }


    //[Parameter("Enable email alerts?", DefaultValue = false)]
    //public Boolean EnableEmailAlerts { get; set; }

    //[Parameter("Enable sound Alerts?", DefaultValue = false)]
    //public Boolean EnableSoundAlerts { get; set; }

    private String[] symbols = new String[] {
      // Forex
      "GBPUSD",
      "EURUSD",
      //"GBPAUD",
      //"EURNZD",
      //"NZDJPY",
      //"USDCAD",
      //"NZDUSD",
      //"AUDUSD",
     
      // Indexes
      //"US500",
      //"USTEC",
      //"DE30",
      //"ES35",
    };

    private Dictionary<String, String> soundPaths;

    private Dictionary<String, AverageTrueRange> atr_D1;
    private Dictionary<String, AverageTrueRange> atr_H4;
    private Dictionary<String, AverageTrueRange> atr_H1;
    private Dictionary<String, AverageTrueRange> atr_M5;
    private Dictionary<String, AverageTrueRange> atr_CR;

    private Dictionary<String, MarketSeries> seriesD1;
    private Dictionary<String, MarketSeries> seriesH4;
    private Dictionary<String, MarketSeries> seriesH1;
    private Dictionary<String, MarketSeries> seriesM5;
    private Dictionary<String, MarketSeries> seriesCR;
    
    private Dictionary<String, ExponentialMovingAverage> mm8_D1;
    private Dictionary<String, ExponentialMovingAverage> mm8_H4;
    private Dictionary<String, ExponentialMovingAverage> mm8_H1;
    private Dictionary<String, ExponentialMovingAverage> mm8_M5;
    private Dictionary<String, ExponentialMovingAverage> mm8_CR;

    private Dictionary<String, ExponentialMovingAverage> mm55_D1;
    private Dictionary<String, ExponentialMovingAverage> mm55_H4;
    private Dictionary<String, ExponentialMovingAverage> mm55_H1;
    private Dictionary<String, ExponentialMovingAverage> mm55_M5;
    private Dictionary<String, ExponentialMovingAverage> mm55_CR;

    private Dictionary<String, ExponentialMovingAverage> mm200_D1;
    private Dictionary<String, ExponentialMovingAverage> mm200_H4;
    private Dictionary<String, ExponentialMovingAverage> mm200_H1;
    private Dictionary<String, ExponentialMovingAverage> mm200_M5;
    private Dictionary<String, ExponentialMovingAverage> mm200_CR;

    private Dictionary<String, MacdCrossOver> macd_D1;
    private Dictionary<String, MacdCrossOver> macd_H4;
    private Dictionary<String, MacdCrossOver> macd_H1;
    private Dictionary<String, MacdCrossOver> macd_M5;
    private Dictionary<String, MacdCrossOver> macd_CR;

    private State state;
    private EventsBus Bus;

    protected override void OnStart() {
      Print("Initializing screener local state.");

      seriesD1 = new Dictionary<string, MarketSeries>();
      seriesH4 = new Dictionary<string, MarketSeries>();
      seriesH1 = new Dictionary<string, MarketSeries>();
      seriesM5 = new Dictionary<string, MarketSeries>();
      seriesCR = new Dictionary<string, MarketSeries>();

      atr_D1 = new Dictionary<string, AverageTrueRange>();
      atr_H4 = new Dictionary<string, AverageTrueRange>();
      atr_H1 = new Dictionary<string, AverageTrueRange>();
      atr_M5 = new Dictionary<string, AverageTrueRange>();
      atr_CR = new Dictionary<string, AverageTrueRange>();

      mm200_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_M5 = new Dictionary<string, ExponentialMovingAverage>();
      mm200_CR = new Dictionary<string, ExponentialMovingAverage>();

      mm55_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_M5 = new Dictionary<string, ExponentialMovingAverage>();
      mm55_CR = new Dictionary<string, ExponentialMovingAverage>();

      mm8_D1 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_H4 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_H1 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_M5 = new Dictionary<string, ExponentialMovingAverage>();
      mm8_CR = new Dictionary<string, ExponentialMovingAverage>();

      macd_D1 = new Dictionary<string, MacdCrossOver>();
      macd_H4 = new Dictionary<string, MacdCrossOver>();
      macd_H1 = new Dictionary<string, MacdCrossOver>();
      macd_M5 = new Dictionary<string, MacdCrossOver>();
      macd_CR = new Dictionary<string, MacdCrossOver>();

      state = new State();
      Bus = new EventsBus("tcp://*:8888");

      foreach (var sym in symbols) {
        Print("Initializing data for {0}", sym);

        // Initialize Indicators
        var lseriesD1 = MarketData.GetSeries(sym, TimeFrame.Daily);
        var lseriesH4 = MarketData.GetSeries(sym, TimeFrame.Hour4);
        var lseriesH1 = MarketData.GetSeries(sym, TimeFrame.Hour);
        var lseriesM5 = MarketData.GetSeries(sym, TimeFrame.Minute5);
        var lseriesCR = MarketData.GetSeries(sym, TimeFrame);

        atr_D1[sym] = Indicators.AverageTrueRange(lseriesD1, 14, MovingAverageType.Exponential);
        atr_H4[sym] = Indicators.AverageTrueRange(lseriesH4, 14, MovingAverageType.Exponential);
        atr_H1[sym] = Indicators.AverageTrueRange(lseriesH1, 14, MovingAverageType.Exponential);
        atr_M5[sym] = Indicators.AverageTrueRange(lseriesM5, 14, MovingAverageType.Exponential);
        atr_CR[sym] = Indicators.AverageTrueRange(lseriesCR, 14, MovingAverageType.Exponential);

        mm200_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 200);
        mm200_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 200);
        mm200_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 200);
        mm200_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 200);
        mm200_CR[sym] = Indicators.ExponentialMovingAverage(lseriesCR.Close, 200);

        mm55_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 55);
        mm55_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 55);
        mm55_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 55);
        mm55_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 55);
        mm55_CR[sym] = Indicators.ExponentialMovingAverage(lseriesCR.Close, 55);

        mm8_D1[sym] = Indicators.ExponentialMovingAverage(lseriesD1.Close, 8);
        mm8_H4[sym] = Indicators.ExponentialMovingAverage(lseriesH4.Close, 8);
        mm8_H1[sym] = Indicators.ExponentialMovingAverage(lseriesH1.Close, 8);
        mm8_M5[sym] = Indicators.ExponentialMovingAverage(lseriesM5.Close, 8);
        mm8_CR[sym] = Indicators.ExponentialMovingAverage(lseriesCR.Close, 8);

        macd_D1[sym] = Indicators.MacdCrossOver(lseriesD1.Close, 26, 12, 9);
        macd_H4[sym] = Indicators.MacdCrossOver(lseriesH4.Close, 26, 12, 9);
        macd_H1[sym] = Indicators.MacdCrossOver(lseriesH1.Close, 26, 12, 9);
        macd_M5[sym] = Indicators.MacdCrossOver(lseriesM5.Close, 26, 12, 9);
        macd_CR[sym] = Indicators.MacdCrossOver(lseriesCR.Close, 26, 12, 9);

        seriesD1[sym] = lseriesD1;
        seriesH4[sym] = lseriesH4;
        seriesH1[sym] = lseriesH1;
        seriesM5[sym] = lseriesM5;
        seriesCR[sym] = lseriesCR;

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

      //using (StreamWriter sw = new StreamWriter(@"C:\Users\Andrey\Desktop\json.txt")) {
      //  sw.Write(state.ToJson());
      //}

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

      // Check for pullback signals on H1
      if (EnablePBgH1) {
        foreach (string sym in symbols) {
          var asset = state.GetAsset(sym);
          var value = GetPBSignal(sym, TimeFrame.Hour, asset.Timing);
          asset.AddSignal(new Signal("PB", TimeFrame.Hour, value));
        }
      }

      Render();
      Bus.Emit(state.ToJson());
    }

    // -------------------------------------------
    // ----  Pull Back Signal
    // -------------------------------------------

    public int GetPBSignal(string sym, TimeFrame ltf, Timing timing) {
      var rtf = ReferenceTimeFrame(ltf);

      //var series = lseries[tf][sym];
      //var mm50 = lmm50[tf][sym];
      //var mm200 = lmm200[tf][sym];
      //var macd = lmacd[tf][sym];

      //if (mm50.Result.LastValue > mm200.Result.LastValue
      //    && In(timing.Reference, 1, 4, -2, -3)
      //    && macd.MACD.LastValue < 0
      //    && macd.Signal.LastValue < 0
      //    && macd.Histogram.LastValue >= 0) {
      //  return 1;
      //}

      //if (mm50.Result.LastValue < mm200.Result.LastValue
      //    && In(timing.Reference, -1, -4, 2, 3)
      //    && macd.MACD.LastValue > 0
      //    && macd.Signal.LastValue > 0
      //    && macd.Histogram.LastValue <= 0) {
      //  return -1;
      //}

      return 0;
    }

    // -------------------------------------------
    // ----  Market Timing
    // -------------------------------------------

    public class Timing {
      public Dictionary<TimeFrame, Int32> Data;

      public Timing(TimeFrame ctf, int d1, int h4, int h1, int m5, int cr) {
        Data = new Dictionary<TimeFrame, int>(5);
        Data[TimeFrame.Daily] = d1;
        Data[TimeFrame.Hour4] = h4;
        Data[TimeFrame.Hour] = h1;
        Data[TimeFrame.Minute5] = m5;
        Data[ctf] = cr;
      }
    }

    private Timing CalculateMarketTiming(string sym) {
      var d1 = CalculateTiming(macd_D1[sym], mm200_D1[sym], seriesD1[sym]);
      var h4 = CalculateTiming(macd_H4[sym], mm200_H4[sym], seriesH4[sym]);
      var h1 = CalculateTiming(macd_H1[sym], mm200_H1[sym], seriesH1[sym]);
      var m5 = CalculateTiming(macd_M5[sym], mm200_M5[sym], seriesM5[sym]);
      var cr = CalculateTiming(macd_CR[sym], mm200_CR[sym], seriesCR[sym]);
      return new Timing(TimeFrame, d1, h4, h1, m5, cr);
    }

    private int CalculateTiming(MacdCrossOver macd, ExponentialMovingAverage ma, MarketSeries series) {
      if (IsTrendUp(series, ma)) {
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
    // ----  Internal State
    // -------------------------------------------

    public class Signal {
      public String Name { get; set; }
      public Int32 Value { get; set; }
      public TimeFrame TimeFrame { get; set; }
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }

      public Signal(String name, TimeFrame tf, Int32 value) {
        this.Name = name;
        this.TimeFrame = tf;
        this.Value = value;
      }

      public String Key() {
        return string.Format("{0}-{1}", this.Name, TimeFrameAsString(this.TimeFrame));
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

      public Asset(string name) {
        this.Data = new Dictionary<string, Signal>();
        this.Name = name;
      }

      public Signal[] GetSignals() {
        return Data.Values.ToArray<Signal>();
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
        serializer.Converters.Add(new JavaScriptDateTimeConverter());
        serializer.NullValueHandling = NullValueHandling.Ignore;
        serializer.Formatting = Formatting.Indented;

        var buffer = new StringWriter();

        using (buffer = new StringWriter()) {
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
                                      timing.Data[TimeFrame.Daily],
                                      timing.Data[TimeFrame.Hour4],
                                      timing.Data[TimeFrame.Hour],
                                      timing.Data[TimeFrame.Minute5]);

        var signalsOutput = new HashSet<String>();
        foreach (var signal in asset.GetSignals()) {
          signalsOutput.Add(string.Format("{0}(1)", signal.Name, TimeFrameAsString(signal.TimeFrame)));
        }

        output.Add(string.Format("\t{0,-15}\t{1,-20}\t{2}", asset.Name, timingStr, String.Join(",", signalsOutput)));

        var buffer = String.Join("\r\n", output);
        ChartObjects.RemoveObject("screener");
        ChartObjects.DrawText("screener", buffer, StaticPosition.TopLeft, Colors.Black);
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

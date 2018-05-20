// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2018 Andrey Antukh <niwi@niwi.nz>
using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
  [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
  public class SimplifiedTimingIndicator : Indicator {
    public class Timing : Tuple<Int32, Int32> {
      public Int32 Reference { get { return Item1; } }
      public Int32 Local { get { return Item2; } }

      public Timing(int reference, int local) : base(reference, local) {
      }
    }

    private MacdCrossOver lmacd;
    private MacdCrossOver rmacd;

    private WeightedMovingAverage lma;
    private WeightedMovingAverage rma;

    private MarketSeries rseries;
    private TimeFrame reftf;

    public IndicatorDataSeries Reference { get; set; }
    public IndicatorDataSeries Local { get; set; }

    protected override void Initialize() {
      reftf = GetReferenceTimeFrame(MarketSeries.TimeFrame);
      rseries = MarketData.GetSeries(reftf);

      lmacd = Indicators.MacdCrossOver(MarketSeries.Close, 26, 12, 9);
      rmacd = Indicators.MacdCrossOver(rseries.Close, 26, 12, 9);

      lma = Indicators.WeightedMovingAverage(MarketSeries.Close, 200);
      rma = Indicators.WeightedMovingAverage(rseries.Close, 200);

      Print("Load reference time frame: {0}", reftf);

      Reference = CreateDataSeries();
      Local = CreateDataSeries();

      var result = CalculateMarketTiming();
      Reference[MarketSeries.Close.Count - 1] = result.Reference;
      Local[MarketSeries.Close.Count - 1] = result.Local;
    }

    public override void Calculate(int index) {
      if (IsRealTime) {
        var result = CalculateMarketTiming();
        Reference[index] = result.Reference;
        Local[index] = result.Local;
      }
    }

    private Timing CalculateMarketTiming() {
      var local = CalculateTiming(lmacd, lma, MarketSeries);
      var reference = CalculateTiming(rmacd, rma, rseries);

      var msg = string.Format("Timing: {0} {1}", reference, local);
      ChartObjects.RemoveObject("test");
      ChartObjects.DrawText("test", msg, StaticPosition.TopCenter, Colors.Black);

      return new Timing(reference, local);
    }

    private int CalculateTiming(MacdCrossOver macd, WeightedMovingAverage wma, MarketSeries series) {
      if (IsTrendUp(series, wma)) {
        if (macd.Histogram.LastValue > 0) {
          return 1;
        } else {
          return 2;
        }
      } else {
        if (macd.Histogram.LastValue <= 0) {
          return -1;
        }  else {
          return -2;
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
  }
}

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
  public class Timing : Indicator {
    private MacdCrossOver lmacd;
    private MacdCrossOver rmacd;

    private WeightedMovingAverage lwma;
    private WeightedMovingAverage rwma;

    private MarketSeries rseries;
    private TimeFrame reftf;

    public IndicatorDataSeries Reference { get; set; }
    public IndicatorDataSeries Local { get; set; }

    protected override void Initialize() {
      reftf = GetReferenceTimeframe(MarketSeries.TimeFrame);
      rseries = MarketData.GetSeries(reftf);

      lmacd = Indicators.MacdCrossOver(MarketSeries.Close, 26, 12, 9);
      rmacd = Indicators.MacdCrossOver(rseries.Close, 26, 12, 9);

      lwma = Indicators.WeightedMovingAverage(MarketSeries.Close, 300);
      rwma = Indicators.WeightedMovingAverage(rseries.Close, 300);

      Print("Load reference time frame: {0}", reftf);

      Reference = CreateDataSeries();
      Local = CreateDataSeries();

      var result = CalculateMarketTiming();
      Reference[MarketSeries.Close.Count - 1] = result.Item1;
      Local[MarketSeries.Close.Count - 1] = result.Item2;
    }

    public override void Calculate(int index) {
      if (IsRealTime) {
        var result = CalculateMarketTiming();
        Reference[index] = result.Item1;
        Local[index] = result.Item2;
      }
    }

    private Tuple<int, int> CalculateMarketTiming() {
      var local = CalculateLocalTiming();
      var reference = CalculateReferenceTiming();

      var msg = string.Format("Market timing: {0} {1}", reference, local);
      ChartObjects.RemoveObject("test");
      ChartObjects.DrawText("test", msg, StaticPosition.TopCenter, Colors.Black);

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

    public int CalculateLocalTiming() {
      if (IsTrendUp(MarketSeries, lwma)) {
        if (lmacd.Histogram.LastValue > 0 && lmacd.Signal.LastValue > 0) {
          return 1;
        } else if (lmacd.Histogram.LastValue > 0 && lmacd.Signal.LastValue < 0) {
          return 4;
        } else if (lmacd.Histogram.LastValue <= 0 && lmacd.Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (lmacd.Histogram.LastValue < 0 && lmacd.Signal.LastValue < 0) {
          return -1;
        } else if (lmacd.Histogram.LastValue < 0 && lmacd.Signal.LastValue > 0) {
          return -4;
        } else if (lmacd.Histogram.LastValue >= 0 && lmacd.Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    public int CalculateReferenceTiming() {
      if (IsTrendUp(rseries, rwma)) {
        if (rmacd.Histogram.LastValue > 0 && rmacd.Signal.LastValue > 0) {
          return 1;
        } else if (rmacd.Histogram.LastValue > 0 && rmacd.Signal.LastValue < 0) {
          return 4;
        } else if (rmacd.Histogram.LastValue <= 0 && rmacd.Signal.LastValue >= 0) {
          return 2;
        } else {
          return 3;
        }
      } else {
        if (rmacd.Histogram.LastValue < 0 && rmacd.Signal.LastValue < 0) {
          return -1;
        } else if (rmacd.Histogram.LastValue < 0 && rmacd.Signal.LastValue > 0) {
          return -4;
        } else if (rmacd.Histogram.LastValue >= 0 && rmacd.Signal.LastValue <= 0) {
          return -2;
        } else {
          return -3;
        }
      }
    }

    public TimeFrame GetReferenceTimeframe(TimeFrame tf) {
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
  }
}

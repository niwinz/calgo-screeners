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

    [Parameter("Enable ATR INFO?", DefaultValue = true)]
    public bool EnableATRInfo { get; set; }

    [Parameter("Enable D1?", DefaultValue = true)]
    public bool EnableD1 { get; set; }

    [Parameter("Enable H4?", DefaultValue = false)]
    public bool EnableH4 { get; set; }

    [Parameter("Enable H1?", DefaultValue = true)]
    public bool EnableH1 { get; set; }

    [Parameter("Enable M5", DefaultValue = true)]
    public bool EnableM5 { get; set; }

    private MacdCrossOver macdD1;
    private MacdCrossOver macdH4;
    private MacdCrossOver macdH1;
    private MacdCrossOver macdM5;

    private WeightedMovingAverage mmD1;
    private WeightedMovingAverage mmH4;
    private WeightedMovingAverage mmH1;
    private WeightedMovingAverage mmM5;

    private MarketSeries seriesD1;
    private MarketSeries seriesH4;
    private MarketSeries seriesH1;
    private MarketSeries seriesM5;

    private AverageTrueRange atr;

    //public IndicatorDataSeries Reference { get; set; }
    //public IndicatorDataSeries Local { get; set; }

    protected override void Initialize() {
      if (EnableATRInfo) {
        atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
      }

      if (EnableD1) {
        seriesD1 = MarketData.GetSeries(TimeFrame.Daily);
        macdD1 = Indicators.MacdCrossOver(seriesD1.Close, 26, 12, 9);
        mmD1 = Indicators.WeightedMovingAverage(seriesD1.Close, 300);
      }

      if (EnableH4) {
        seriesH4 = MarketData.GetSeries(TimeFrame.Hour4);
        macdH4 = Indicators.MacdCrossOver(seriesH4.Close, 26, 12, 9);
        mmH4 = Indicators.WeightedMovingAverage(seriesH4.Close, 300);
      }

      if (EnableH1) {
        seriesH1 = MarketData.GetSeries(TimeFrame.Hour);
        macdH1 = Indicators.MacdCrossOver(seriesH1.Close, 26, 12, 9);
        mmH1 = Indicators.WeightedMovingAverage(seriesH1.Close, 300);
      }

      if (EnableM5) {
        seriesM5 = MarketData.GetSeries(TimeFrame.Minute5);
        macdM5 = Indicators.MacdCrossOver(seriesM5.Close, 26, 12, 9);
        mmM5 = Indicators.WeightedMovingAverage(seriesM5.Close, 300);
      }
    }

    public override void Calculate(int index) {
      if (!IsRealTime) return;

      String outputHeader = "\t";
      String outputValue = "\t";

      if (EnableATRInfo) {
        var rval = atr.Result.LastValue;
        double val = 100;

        if (Symbol.PipSize == 0.0001) {
          val = Math.Round(rval * 10000, 1);
        } else if (Symbol.PipSize == 0.01) {
          val = Math.Round(rval * 100, 1);
        }

        outputValue += string.Format("{0}\t|\t", val);
        outputHeader += string.Format("ATR\t|\t");
      }

      if (EnableD1) {
        var value = CalculateTiming(macdD1, mmD1, seriesD1);
        outputValue += string.Format("{0}\t", value);
        outputHeader += string.Format("D1\t");

      }
      if (EnableH4) {
        var value = CalculateTiming(macdH4, mmH4, seriesH4);
        outputValue += string.Format("{0}\t", value);
        outputHeader += string.Format("H4\t");
      }

      if (EnableH1) {
        var value = CalculateTiming(macdH1, mmH1, seriesH1);
        outputValue += string.Format("{0}\t", value);
        outputHeader += string.Format("H1\t");
      }

      if (EnableM5) {
        var value = CalculateTiming(macdM5, mmM5, seriesM5);
        outputValue += string.Format("{0}\t", value);
        outputHeader += string.Format("M5\t");

      }

      String output = string.Format("{0}\n{1}", outputHeader, outputValue);

      ChartObjects.RemoveObject("timing");
      ChartObjects.DrawText("timing", output , StaticPosition.TopCenter, Colors.Black);
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

﻿// This Source Code Form is subject to the terms of the Mozilla Public
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

    private ExponentialMovingAverage mmD1;
    private ExponentialMovingAverage mmH4;
    private ExponentialMovingAverage mmH1;
    private ExponentialMovingAverage mmM5;

    private MarketSeries seriesD1;
    private MarketSeries seriesH4;
    private MarketSeries seriesH1;
    private MarketSeries seriesM5;

    private AverageTrueRange atr;

    protected override void Initialize() {
      if (EnableATRInfo) {
        atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
      }

      if (EnableD1) {
        seriesD1 = MarketData.GetSeries(TimeFrame.Daily);
        macdD1 = Indicators.MacdCrossOver(seriesD1.Close, 26, 12, 9);
        mmD1 = Indicators.ExponentialMovingAverage(seriesD1.Close, 200);
      }

      if (EnableH4) {
        seriesH4 = MarketData.GetSeries(TimeFrame.Hour4);
        macdH4 = Indicators.MacdCrossOver(seriesH4.Close, 26, 12, 9);
        mmH4 = Indicators.ExponentialMovingAverage(seriesH4.Close, 200);
      }

      if (EnableH1) {
        seriesH1 = MarketData.GetSeries(TimeFrame.Hour);
        macdH1 = Indicators.MacdCrossOver(seriesH1.Close, 26, 12, 9);
        mmH1 = Indicators.ExponentialMovingAverage(seriesH1.Close, 200);
      }

      if (EnableM5) {
        seriesM5 = MarketData.GetSeries(TimeFrame.Minute5);
        macdM5 = Indicators.MacdCrossOver(seriesM5.Close, 26, 12, 9);
        mmM5 = Indicators.ExponentialMovingAverage(seriesM5.Close, 200);
      }
    }

    public override void Calculate(int index) {
      String outputHeader = "\t";
      String outputValue = "\t";

      if (EnableATRInfo) {
        var rval = atr.Result.LastValue;
        double val = 100;

        if (Symbol.PipSize == 0.0001) {
          val = Math.Round(rval * 10000, 1);
        } else if (Symbol.PipSize == 0.01) {
          val = Math.Round(rval * 100, 1);
        } else if (Symbol.PipSize == 0.1) {
          val = Math.Round(rval * 10, 1);
        } else {
          throw new Exception("Unexpected pip size.");
        }

        outputValue += string.Format("{0}\t|\t", val);
        outputHeader += string.Format("ATR\t|\t");
      }

      if (EnableD1) {
        var value = CalculateTiming(macdD1, mmD1, seriesD1);
        outputHeader += string.Format("D1\t");
        outputValue += string.Format("{0}({1})\t", TimingToArrow(value), value);
      }

      if (EnableH4) {
        var value = CalculateTiming(macdH4, mmH4, seriesH4);
        outputHeader += string.Format("H4\t");
        outputValue += string.Format("{0}({1})\t", TimingToArrow(value), value);
      }

      if (EnableH1) {
        var value = CalculateTiming(macdH1, mmH1, seriesH1);
        outputValue += string.Format("{0}({1})\t", TimingToArrow(value), value);
        outputHeader += string.Format("H1\t");
      }

      if (EnableM5) {
        var value = CalculateTiming(macdM5, mmM5, seriesM5);
        outputValue += string.Format("{0}({1})\t", TimingToArrow(value), value);
        outputHeader += string.Format("M5\t");

      }

      String output = string.Format("{0}\n{1}", outputHeader, outputValue);

      ChartObjects.RemoveObject("timing");
      ChartObjects.DrawText("timing", output , StaticPosition.TopCenter, Colors.Black);
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
        }  else {
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

    private string TimingToArrow(int timing) {
      if (timing == 1 || timing == -2) {
        return "▲";
      } else {
        return "▼";
      }
    }
  }
}

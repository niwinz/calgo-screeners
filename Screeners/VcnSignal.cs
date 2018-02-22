// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2018 Andrey Antukh <niwi@niwi.nz>

// VCN Strategy indicator.
//  
// This indicator is not suitable for create automated robots,
// it works just as visual indicator for quick look on the
// possible signal (this is because it does not uses the
// reference market timing).

using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
  [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
  public class VcnSignal : Indicator {
    private string upArrow = "↑";
    private string downArrow = "↓";
    private double arrowOffset;

    private MacdCrossOver macd;
    private WeightedMovingAverage wma300;
    private ExponentialMovingAverage ema8;

    protected override void Initialize() {
      macd = Indicators.MacdCrossOver(MarketSeries.Close, 26, 12, 9);
      wma300 = Indicators.WeightedMovingAverage(MarketSeries.Close, 300);
      ema8 = Indicators.ExponentialMovingAverage(MarketSeries.Close, 8);
      arrowOffset = Symbol.PipSize * 20;
    }

    public override void Calculate(int index) {
      // Do nothing at start
      if (index < 4) return;

      var isTrendUp = MarketSeries.Close[index] > wma300.Result[index];
      var position = wma300.Result[index];

      if (isTrendUp) {
        if ((macd.Histogram[index] > 0 && macd.Signal[index] > 0) ||
            (macd.Histogram[index] > 0 && macd.Signal[index] < 0)) {
          if (MarketSeries.Low[index - 1] > ema8.Result[index - 1]
              && MarketSeries.Low[index - 2] > ema8.Result[index - 2]
              && MarketSeries.Low[index - 3] > ema8.Result[index - 3]
              && MarketSeries.Low[index] <= ema8.Result[index]) {
            ChartObjects.DrawText(string.Format("vcn-buy-{0}", index), upArrow, index, position - arrowOffset,
                                  VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Red);
          } else if (MarketSeries.Low[index] > ema8.Result[index]) {
            ChartObjects.DrawText(string.Format("vcn-buy-{0}", index), upArrow, index, position - arrowOffset,
                                  VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Blue);
          }
        }
      } else {
        if ((macd.Histogram[index] < 0 && macd.Signal[index] < 0) ||
            (macd.Histogram[index] < 0 && macd.Signal[index] > 0)) {
          if (MarketSeries.High[index - 1] < ema8.Result[index - 1]
              && MarketSeries.High[index - 2] < ema8.Result[index - 2]
              && MarketSeries.High[index - 3] < ema8.Result[index - 3]
              && MarketSeries.High[index] >= ema8.Result[index]) {
            ChartObjects.DrawText(string.Format("vcn-sell-{0}", index), downArrow, index, position - arrowOffset,
                                  VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Red);
          } else if (MarketSeries.High[index] < ema8.Result[index]) {
            ChartObjects.DrawText(string.Format("vcn-sell-{0}", index), downArrow, index, position - arrowOffset,
                                  VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Blue);

          }
        }
      }
    }
  }
}

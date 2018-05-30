// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2018 Andrey Antukh <niwi@niwi.nz>

using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
  [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
  public class CWMA : Indicator {
    private WeightedMovingAverage wma;

    [Parameter("Period", DefaultValue = 150)]
    public int Period { get; set; }

    [Output("Up Trend", PlotType = PlotType.DiscontinuousLine, Color = Colors.Green)]
    public IndicatorDataSeries UpTrend { get; set; }

    [Output("Down Trend", PlotType = PlotType.DiscontinuousLine, Color = Colors.Red)]
    public IndicatorDataSeries DownTrend { get; set; }

    protected override void Initialize() {
      wma = Indicators.WeightedMovingAverage(MarketSeries.Close, Period);
    }

    public override void Calculate(int index) {
      var close = MarketSeries.Close[index];
      var value = wma.Result[index];

      if (value < close) {
        UpTrend[index] = value;
      } else {
        DownTrend[index] = value;
      }
    }
  }
}

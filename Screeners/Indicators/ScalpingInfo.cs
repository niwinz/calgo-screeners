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
  public class ScalpingInfo : Indicator {

    [Parameter("Risk %?", DefaultValue = 1)]
    public double Risk { get; set; }

    private AverageTrueRange atr;

    protected override void Initialize() { 
      atr = Indicators.AverageTrueRange(14, MovingAverageType.Exponential);
    }

    public override void Calculate(int index) {
      if (!IsRealTime) return;

      String output = "";

      var rval = atr.Result.LastValue;
      double ratr;

      if (Symbol.PipSize == 0.0001) {
        ratr = Math.Round(rval * 10000, 1);
      } else if (Symbol.PipSize == 0.01) {
        ratr = Math.Round(rval * 100, 1);
      } else {
        ratr = 0;
      }

      output += string.Format("TP:\t{0}\n", ratr * 2);
      output += string.Format("SL:\t{0}\n", ratr * 4);
      output += string.Format("QTY:\t{0}", GetQuantity(ratr*4));

      ChartObjects.RemoveObject("scalpinginfo");
      ChartObjects.DrawText("scalpinginfo", output, StaticPosition.TopLeft, Colors.DarkGreen);
    }

    private double GetQuantity(double stopLoss) {
      var riskedAmount = Account.Balance * (Risk / 100);
      var rvp = riskedAmount / stopLoss;
      var vol = (rvp * Symbol.Ask) / Symbol.PipSize;
      return Symbol.VolumeInUnitsToQuantity(Symbol.NormalizeVolumeInUnits(vol, RoundingMode.ToNearest));
    }
  }
}

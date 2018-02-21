using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GeneralTendence : Indicator {
        private WeightedMovingAverage wma150;
        private string dotchar = "▪";

        protected override void Initialize() {
            wma150 = Indicators.WeightedMovingAverage(MarketSeries.Close, 150);
        }

        public override void Calculate(int index) {
            var close = MarketSeries.Close[index];
            var value = wma150.Result[index];

            if (value < close) {
                ChartObjects.DrawText(string.Format("up-{0}", index), dotchar, index, value, VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Green);
            } else {
                ChartObjects.DrawText(string.Format("down-{0}", index), dotchar, index, value, VerticalAlignment.Center, HorizontalAlignment.Center, Colors.Red);
            }
        }
    }
}

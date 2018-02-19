using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo {
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class Screener : Robot {

        [Parameter("Source")]
        public DataSeries Source { get; set; }

        private Dictionary<String, Dictionary<TimeFrame, MarketSeries>> series;
        private Dictionary<String, Symbol> symbols;

        private String[] pairs = new String[] {
            "EURUSD",
            "GBPUSD",
            "EURJPY",
            "USDJPY",
            "AUDUSD",
            "USDCHF",
            "GBPJPY",
            "USDCAD",
            "EURGBP",
            "EURCHF",
            "AUDJPY",
            "NZDUSD",
            "CHFJPY",
            "EURAUD",
            "CADJPY",
            "GBPAUD",
            "EURCAD",
            "AUDCAD",
            "GBPCHF",
            "AUDCHF",
            "GBPCAD",
            "GBPNZD",
            "NZDCAD",
            "NZDCHF",
            "NZDJPY",
            "AUDNZD",
            "CADCHF",
            "EURNZD"
        };
        //private String[] pairs = new String[] { "EURUSD" };
        private TimeFrame[] timeFrames = new TimeFrame[]
        {
            TimeFrame.Daily,
            TimeFrame.Hour,
            //TimeFrame.Minute5
        };

        private Dictionary<String, Dictionary<TimeFrame, ExponentialMovingAverage>> mm8;
        private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> mm50;
        private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> mm150;
        private Dictionary<String, Dictionary<TimeFrame, WeightedMovingAverage>> mm300;

        private Dictionary<String, Dictionary<TimeFrame, StochasticOscillator>> sto;
        private Dictionary<String, Dictionary<TimeFrame, MacdCrossOver>> macd;

        private Dictionary<String, Dictionary<TimeFrame, long?>> barCounters;
        private long ticks = 0;

        private ScreenerState state;

        protected override void OnStart() {
            state = new ScreenerState();
            barCounters = new Dictionary<string, Dictionary<TimeFrame, long?>>();
            series = new Dictionary<string, Dictionary<TimeFrame, MarketSeries>>();
            symbols = new Dictionary<string, Symbol>();

            mm8 = new Dictionary<string, Dictionary<TimeFrame, ExponentialMovingAverage>>();
            mm50 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
            mm150 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();
            mm300 = new Dictionary<string, Dictionary<TimeFrame, WeightedMovingAverage>>();

            sto = new Dictionary<string, Dictionary<TimeFrame, StochasticOscillator>>();
            macd = new Dictionary<string, Dictionary<TimeFrame, MacdCrossOver>>();

            Print("Initializing screener local state.");

            foreach (var sym in pairs) {
                symbols[sym] = MarketData.GetSymbol(sym);
            }

            foreach (var sym in pairs) {
                series[sym] = new Dictionary<TimeFrame, MarketSeries>();
                mm8[sym] = new Dictionary<TimeFrame, ExponentialMovingAverage>();
                mm50[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
                mm150[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
                mm300[sym] = new Dictionary<TimeFrame, WeightedMovingAverage>();
                sto[sym] = new Dictionary<TimeFrame, StochasticOscillator>();
                macd[sym] = new Dictionary<TimeFrame, MacdCrossOver>();
                barCounters[sym] = new Dictionary<TimeFrame, long?>();

                foreach (var tf in timeFrames) {
                    var mks = MarketData.GetSeries(sym, tf);
                    Print("Initializing data for: {0}/{1}.", sym, tf);

                    barCounters[sym][tf] = mks.Close.Count;

                    mm8[sym][tf] = Indicators.ExponentialMovingAverage(mks.Close, 50);
                    mm50[sym][tf] = Indicators.WeightedMovingAverage(mks.Close, 50);
                    mm150[sym][tf] = Indicators.WeightedMovingAverage(mks.Close, 150);
                    mm300[sym][tf] = Indicators.WeightedMovingAverage(mks.Close, 300);

                    sto[sym][tf] = Indicators.StochasticOscillator(mks, 14, 3, 3, MovingAverageType.Simple);
                    macd[sym][tf] = Indicators.MacdCrossOver(mks.Close, 26, 12, 9);

                    series[sym][tf] = mks;
                }
            }

            Print("Initialization finished.");
        }

        protected override void OnStop() {
            Print("Bot Stopped with total ticks: {0}", ticks);
            Print("Local state generated: {0}", state.AsJSON());
            File.WriteAllText("C:\\Users\\Andrey\\Desktop\\trading.json", state.AsJSON());
        }

        protected override void OnTick() {
            ticks++;

            foreach (var sym in pairs) {
                foreach (var tf in timeFrames) {
                    if (barCounters[sym][tf] < series[sym][tf].Close.Count) {
                        //Print("New bar is created on {0} timeframe {1}", sym, tf);
                        barCounters[sym][tf] = series[sym][tf].Close.Count;
                        HandleOnBar(sym, tf);
                    }
                }
            }
        }

        private void HandleOnBar(String sym, TimeFrame tf) {
            HandleMacNW(sym, tf);
            File.WriteAllText("C:\\Users\\Andrey\\Desktop\\trading.json", state.AsJSON());
        }

        private void HandleMacNW(String sym, TimeFrame tf) {
            var _mm50 = mm50[sym][tf];
            var _mm150 = mm150[sym][tf];
            var _mm300 = mm300[sym][tf];
            var _ms = series[sym][tf];
            var _macd = macd[sym][tf];

            if (_mm150.Result.Last(1) > _mm300.Result.Last(1)
                && IsRising(_mm150.Result)
                && IsRising(_mm300.Result)
                && _macd.MACD.Last(1) < 0 && _macd.Signal.Last(1) < 0) {

                int relevance = 1;

                if (_ms.Close.Last(1) <= _mm150.Result.Last(1)) {
                    relevance++;
                }

                if (_macd.MACD.Last(1) > _macd.Signal.Last(1)) {
                    relevance++;
                }

                Print("Signal MacNW: type=Buy timeframe={0} relevance={1}", tf, relevance);
                state.update(sym, tf, "buy", relevance);
            } else if (_mm150.Result.Last(1) < _mm300.Result.Last(1)
                       && IsFalling(_mm150.Result)
                       && IsFalling(_mm300.Result)
                       && _macd.MACD.Last(1) > 0 && _macd.Signal.Last(1) > 0) {

                int relevance = 1;

                if (_ms.Close.Last(1) >= _mm150.Result.Last(1)) {
                    relevance++;
                }

                if (_macd.MACD.Last(1) < _macd.Signal.Last(1)) {
                    relevance++;
                }

                state.update(sym, tf, "sell", relevance);
                Print("Signal MacNW: type=Sell timeframe={0} relevance={1}", tf, relevance);
            } else {
                state.update(sym, tf, "none");
            }
        }

        private bool IsRising(DataSeries data) {
            int periods = 5;
            double sum = data.Last(1);

            for (int i = periods; i > 1; i--) {
                sum += data.Last(i);
            }

            return (sum / periods) > data.Last(periods);
        }

        private bool IsFalling(DataSeries data) {
            int periods = 5;
            double sum = data.Last(1);
            for (int i = periods; i > 1; i--) {
                sum += data.Last(i);
            }

            return (sum / periods) < data.Last(periods);
        }
    }

    public class ScreenerState {
        private class StateItem {
            public String status { get; set; }
            public int relevance { get; set; }
            public String updatedAt { get; set; }
        }

        private Dictionary<String, Dictionary<String, StateItem>> data;

        public ScreenerState() {
            data = new Dictionary<string, Dictionary<string, StateItem>>();
        }

        public void update(String sym, TimeFrame tf, String status) {
            update(sym, tf, status, 0);
        }

        public void update(String sym, TimeFrame tf, String status, int relevance) {
            if (!data.ContainsKey(sym)) {
                data[sym] = new Dictionary<string, StateItem>();
            }

            string tfs = translateTimeFrame(tf);

            var si = new StateItem();
            si.status = status;
            si.relevance = relevance;
            si.updatedAt = DateTime.Now.ToString();

            data[sym][tfs] = si;
        }

        public string AsJSON() {
            var json = new JavaScriptSerializer();
            return json.Serialize(data);
        }

        private string translateTimeFrame(TimeFrame tf) {
            if (tf == TimeFrame.Daily) {
                return "D1";
            } else if (tf == TimeFrame.Hour) {
                return "H1";
            } else {
                throw new Exception("Unexpected time frame.");
            }

        }
    }
}

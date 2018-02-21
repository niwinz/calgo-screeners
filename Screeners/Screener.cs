using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace cAlgo {

    public class Form1 : Form {
        public Button button1;
        public TextBox textBox1;
        public Form1() {
            button1 = new Button();
            button1.Size = new Size(40, 40);
            button1.Location = new Point(30, 30);
            button1.Text = "Click me";
            this.Controls.Add(button1);
            button1.Click += new EventHandler(button1_Click);


            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.AcceptsReturn = true;
            this.textBox1.AcceptsTab = true;
            this.textBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox1.Multiline = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(284, 264);
            this.Controls.Add(this.textBox1);


        }
        private void button1_Click(object sender, EventArgs e) {
            MessageBox.Show("Hello World");
        }

        //[STAThread]
        //static void Main() {
        //    Application.EnableVisualStyles();
        //    Application.Run(new Form1());
        //}
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class Screener : Robot {

        [Parameter("Source")]
        public DataSeries Source { get; set; }

        private Dictionary<String, MarketSeries> lseries;
        private Dictionary<String, MarketSeries> rseries;

        private Dictionary<String, Symbol> symbols;

        private String[] pairs = new String[] {
            "EURUSD",
            "GBPUSD",
            //"EURJPY",
            //"USDJPY",
            //"AUDUSD",
            //"USDCHF",
            //"GBPJPY",
            //"USDCAD",
            //"EURGBP",
            //"EURCHF",
            //"AUDJPY",
            //"NZDUSD",
            //"CHFJPY",
            //"EURAUD",
            //"CADJPY",
            //"GBPAUD",
            //"EURCAD",
            //"AUDCAD",
            //"GBPCHF",
            //"AUDCHF",
            //"GBPCAD",
            //"GBPNZD",
            //"NZDCAD",
            //"NZDCHF",
            //"NZDJPY",
            //"AUDNZD",
            //"CADCHF",
            //"EURNZD"
        };
        //private String[] pairs = new String[] { "EURUSD", "USDJPY" };

        //private Dictionary<String, ExponentialMovingAverage> mm8;
        //private Dictionary<String, WeightedMovingAverage> mm50;
        private Dictionary<String, WeightedMovingAverage> lmm150;
        private Dictionary<String, WeightedMovingAverage> lmm300;
        private Dictionary<String, WeightedMovingAverage> rmm150;
        private Dictionary<String, WeightedMovingAverage> rmm300;

        //private Dictionary<String, StochasticOscillator> sto;
        private Dictionary<String, MacdCrossOver> lmacd;
        private Dictionary<String, MacdCrossOver> rmacd;

        private Dictionary<String, long?> barCounters;
        private long barCounter;
        private long ticks = 0;
        private TimeFrame reftf;
        private bool fistRun = true;


        protected override void OnStart() {
            barCounters = new Dictionary<string, long?>();
            barCounter = Source.Count;

            lseries = new Dictionary<string, MarketSeries>();
            rseries = new Dictionary<string, MarketSeries>();

            symbols = new Dictionary<string, Symbol>();

            //mm8 = new Dictionary<string, ExponentialMovingAverage>();
            //mm50 = new Dictionary<string, WeightedMovingAverage>();
            lmm150 = new Dictionary<string, WeightedMovingAverage>();
            lmm300 = new Dictionary<string, WeightedMovingAverage>();
            rmm150 = new Dictionary<string, WeightedMovingAverage>();
            rmm300 = new Dictionary<string, WeightedMovingAverage>();

            //sto = new Dictionary<string, StochasticOscillator>();
            lmacd = new Dictionary<string, MacdCrossOver>();
            rmacd = new Dictionary<string, MacdCrossOver>();

            Print("Initializing screener local state.");

            foreach (var sym in pairs) {
                symbols[sym] = MarketData.GetSymbol(sym);
            }

            reftf = GetReferenceTimeframe(MarketSeries.TimeFrame);

            foreach (var sym in pairs) {
                var lmks = MarketData.GetSeries(sym, MarketSeries.TimeFrame);
                var rmks = MarketData.GetSeries(sym, reftf);

                Print("Initializing data for: {0}/{1}.", sym, MarketSeries.TimeFrame);

                barCounters[sym] = lmks.Close.Count;

                //mm8[sym] = Indicators.ExponentialMovingAverage(mks.Close, 50);
                //mm50[sym] = Indicators.WeightedMovingAverage(mks.Close, 50);
                lmm150[sym] = Indicators.WeightedMovingAverage(lmks.Close, 150);
                lmm300[sym] = Indicators.WeightedMovingAverage(lmks.Close, 300);
                rmm150[sym] = Indicators.WeightedMovingAverage(rmks.Close, 150);
                rmm300[sym] = Indicators.WeightedMovingAverage(rmks.Close, 300);

                //sto[sym] = Indicators.StochasticOscillator(mks, 14, 3, 3, MovingAverageType.Simple);
                lmacd[sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
                rmacd[sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

                lseries[sym] = lmks;
                rseries[sym] = rmks;
            }

            //var thread = new Thread(() => {
            //    Application.EnableVisualStyles();
            //    Application.Run(new Form1());
            //});

            //thread.IsBackground = true;
            //thread.Start();

            Print("Initialization finished.");
        }

        protected override void OnStop() {
            Print("Bot Stopped with total ticks: {0}", ticks);

        }

        private bool IsNewBar() {
            if (barCounter < Source.Count) {
                barCounter = Source.Count;
                return true;
            } else {
                return false;
            }
        }

        // -------------------------------------------
        // ----  Market Timing
        // -------------------------------------------

        private Tuple<int, int> CalculateMarketTiming(String sym) {
            var local = CalculateLocalTiming(sym);
            var reference = CalculateReferenceTiming(sym);

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

        private int CalculateLocalTiming(String sym) {
            if (IsTrendUp(lseries[sym], lmm150[sym])) {
                if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue > 0) {
                    return 1;
                } else if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue < 0) {
                    return 4;
                } else if (lmacd[sym].Histogram.LastValue <= 0 && lmacd[sym].Signal.LastValue >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue < 0) {
                    return -1;
                } else if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue > 0) {
                    return -4;
                } else if (lmacd[sym].Histogram.LastValue >= 0 && lmacd[sym].Signal.LastValue <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        private int CalculateReferenceTiming(String sym) {
            if (IsTrendUp(rseries[sym], rmm150[sym])) {
                if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue > 0) {
                    return 1;
                } else if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue < 0) {
                    return 4;
                } else if (rmacd[sym].Histogram.LastValue <= 0 && rmacd[sym].Signal.LastValue >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue < 0) {
                    return -1;
                } else if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue > 0) {
                    return -4;
                } else if (rmacd[sym].Histogram.LastValue >= 0 && rmacd[sym].Signal.LastValue <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        private TimeFrame GetReferenceTimeframe(TimeFrame tf) {
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

        protected override void OnTick() {
            ticks++;

            if (IsNewBar() || fistRun) {

                var output = new List<String>(symbols.Count);
                output.Add(string.Format("{0,12}\t{1,8}\r\n", "Symbol", "Timing"));
                output.Add("---------------------------------------------------------------------------------------------\n\r");

                foreach (var sym in pairs) {
                    HandleOnBar(sym, output);
                }

                var result = "";
                foreach (var item in output) {
                    result += item;
                }

                ChartObjects.RemoveObject("screener");
                ChartObjects.DrawText("screener", result, StaticPosition.TopLeft, Colors.Black);
            }

            if (fistRun) fistRun = false;
        }

        private void HandleOnBar(String sym, List<String> output) {
            var timing = CalculateMarketTiming(sym);

            var timingStr = string.Format("{0,2},{1,2}", timing.Item1, timing.Item2);
            var msg = string.Format("{0, 12}\t{1,8}\r\n", sym, timingStr);

            output.Add(msg);
        }
    }
}

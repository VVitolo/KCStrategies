#region Using declarations
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core;
using BlueZ = NinjaTrader.NinjaScript.Indicators.BlueZ; // Alias for better readability
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
using WilliamR = NinjaTrader.NinjaScript.Indicators.TradeSaber_SignalMod.TOWilliamsTraderOracleSignalMOD;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class AtmTraderMultiTimeSeries : Strategy, ICustomTypeDescriptor //
    {
		#region Variables		
		
		// ATM Strategy Variables
		private string atmStrategyId = string.Empty;
		private string orderId = string.Empty;
		private bool isAtmStrategyCreated = false;
		private DateTime lastEntryTime;

		// Indicator Variables
		private BlueZ.BlueZHMAHooks hullMAHooks;
		private bool hmaHooksUp;
		private bool hmaHooksDown;

       	private RegressionChannel RegressionChannel1, RegressionChannel2;
		private RegressionChannelHighLow RegressionChannelHighLow1;	
		private bool regChanUp;
		private bool regChanDown;
		
		private VMA vmaIndicator;
		private bool volMaUp;
		private bool volMaDown;
		
		private Momentum momentumIndicator;	
		private bool momoUp;
		private bool momoDown;
		
		private  WilliamR WilliamsR1;
		private bool WillyUp;
		private bool WillyDown;
		
		private CMO CMO1;
		private bool cmoUp;
		private bool cmoDown;
		
		private LinReg2 LinReg1;
		private bool linRegUp;
		private bool linRegDown;
		
		private TrendMagic TrendMagic1;
		private int cciPeriod;
		private int atrPeriod;
		private bool trendMagicUp;
		private bool trendMagicDown;
		
        private T3TrendFilter T3TrendFilter1;
        private double TrendyUp;
        private double TrendyDown;
		private bool trendyUp;
		private bool trendyDown;
		
		private ADX adxIndicator;	
		private bool adxUp;
		
		// Trend Variables
		public bool uptrend;
		public bool downtrend;

		// Signal Variables
		public bool longSignal;
		public bool shortSignal;
		
		// Position Variables
		public bool isLong;
		public bool isShort;
		public bool isFlat;

		// Progress Tracking
		private double actualPnL;
		private bool trailingDrawdownReached = false;

		private double entryPrice;
		private double currentPrice;	
		
		// Trade Direction Management
		private bool tradesPerDirection;
		private int counterLong;
		private int counterShort;
		
		// Quick Order Buttons
		private bool QuickLong;
		private bool QuickShort;
		private bool quickLongBtnActive;
		private bool quickShortBtnActive;

		// Time Management
		private bool isEnableTime2;
		private bool isEnableTime3;
		private bool isEnableTime4;
		private bool isEnableTime5;
		private bool isEnableTime6;

		// Strategy Enablement
		private bool isManualTradeEnabled = true; // Default to enabled
		private bool isAutoTradeEnabled = false;
		private bool isLongEnabled = true; // Default to enabled
		private bool isShortEnabled = true; // Default to enabled

		// WPF Control Variables
		private RowDefinition addedRow;
		private ChartTab chartTab;
		private Chart chartWindow;
		private Grid chartTraderGrid, chartTraderButtonsGrid, lowerButtonsGrid;

		private Button manualBtn, autoBtn, longBtn, shortBtn, quickLongBtn, quickShortBtn;
		private Button moveTSBtn, moveToBEBtn;
		private Button moveTS50PctBtn, closeBtn, panicBtn;
		private bool panelActive;
		private TabItem tabItem;
		private Grid myGrid;

		// P&L Variables
		private double totalPnL;
		private double cumPnL;
		private double dailyPnL;
		private bool canTradeOK = true;
		private bool canTradeToday;

		private bool syncPnl;
		private double historicalTimeTrades; // Sync P&L
		private double dif; // To Calculate PNL sync
		private double cumProfit; // For real time pnl and pnl synchronization

		private bool restartPnL;
		
		// Error Handling
		private readonly object orderLock = new object(); // Critical for thread safety
		private Dictionary<string, Order> activeOrders = new Dictionary<string, Order>(); // Track active orders with labels.
		private DateTime lastOrderActionTime = DateTime.MinValue;
		private readonly TimeSpan minOrderActionInterval = TimeSpan.FromSeconds(1); // Prevent rapid order submissions.
		private bool orderErrorOccurred = false; // Flag to halt trading after an order error.

		// Rogue Order Detection
		private DateTime lastAccountReconciliationTime = DateTime.MinValue;
		private readonly TimeSpan accountReconciliationInterval = TimeSpan.FromMinutes(5); // Check for rogue orders every 5 minutes

		// Trailing Drawdown variables
		private double maxProfit;  // Stores the highest profit achieved
		
		#region Multi Time Series Variables and Properties
		// ─────────────────────────────
	    // NEW MULTI TIME SERIES VARIABLES
	    // ─────────────────────────────

	    // Dictionaries to map our additional data series timeframes (by their BarsArray index)
	    private Dictionary<int, int> timeframeToBarsIndex = new Dictionary<int, int>();
	    private List<int> enabledTimeframes = new List<int>();
	
	    // To store computed signal (true/false) from each added data series.
	    // The key is the BarsArray index (which is also stored in enabledTimeframes).
	    private Dictionary<int, bool> multiSeriesSignals = new Dictionary<int, bool>();
		
		// ─────────────────────────────
	    // NEW PROPERTIES FOR MULTI DATA SERIES
	    // ─────────────────────────────
	
		#region Bools and default settings for Heiken Ashi MINUTE based conditions
		// Bools for Heiken Ashi based conditions
        [NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 1 minute", Order = 1, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi1min { get; set; } = false;

		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 2 minute", Order = 2, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi2min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 3 minute", Order = 3, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi3min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 5 minute", Order = 4, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi5min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi 15 minute", Order = 5, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshi15min { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Custom minutes", Order = 6, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public bool UseHeikenAshiCustom { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Custom minutes value", Order = 7, GroupName = "03. Timeframes: Heiken Ashi - Minute Input")]
        public int HeikenAshiCustomMinutes { get; set; } = 30;
		
		// Endregion Bools for Heiken Ashi based conditions
		#endregion
		
		#region Bools and default settings for Heiken Ashi TICK based conditions
		// Bools for Heiken Ashi based conditions
		
		// Heiken Ashi Tick Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 1", Order = 1, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 1 value", Order = 2, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries1Value { get; set; } = 150;

		// Heiken Ashi Tick Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 2", Order = 3, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 2 value", Order = 4, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries2Value { get; set; } = 300;
		
		// Heiken Ashi Tick Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 3", Order = 5, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 3 value", Order = 6, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries3Value { get; set; } = 500;
		
		// Heiken Ashi Tick Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use Heiken Ashi Tick Series 4", Order = 7, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public bool UseHeikenTICKSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Heiken Ashi TICK Series 4 value", Order = 8, GroupName = "03. Timeframes: Heiken Ashi - TICK Input")]
        public int UseHeikenTICKSeries4Value { get; set; } = 1000;
		
		// Endregion Bools for Heiken Ashi based conditions
		#endregion
		
		
		
		#region Bools and default settings for NinzaRenko based conditions
		// Bools for Renko based conditions
		
		// NinzaRenko Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 1", Order = 1, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 1 Brick Size", Order = 2, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries1BrickSize { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 1 Trend Threshold", Order = 3, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries1TrendThreshold { get; set; } = 1;
		
		// NinzaRenko Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 2", Order = 4, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 2 Brick Size", Order = 5, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries2BrickSize { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 2 Trend Threshold", Order = 6, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries2TrendThreshold { get; set; } = 5;
		
		// NinzaRenko Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 3", Order = 7, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 3 Brick Size", Order = 8, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries3BrickSize { get; set; } = 8;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 3 Trend Threshold", Order = 9, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries3TrendThreshold { get; set; } = 4;
		
		// NinzaRenko Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 4", Order = 10, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 4 Brick Size", Order = 11, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries4BrickSize { get; set; } = 18;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 4 Trend Threshold", Order = 12, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries4TrendThreshold { get; set; } = 3;
		
		// NinzaRenko Series 5
		[NinjaScriptProperty]
        [Display(Name = "Use NinzaRenko Series 5", Order = 13, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public bool UseNinzaRenkoSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 5 Brick Size", Order = 14, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries5BrickSize { get; set; } = 32;
		
		[NinjaScriptProperty]
        [Display(Name = "NinzaRenko Series 5 Trend Threshold", Order = 15, GroupName = "03. Timeframes: Renko - NinzaRenko")]
        public int NinzaRenkoSeries5TrendThreshold { get; set; } = 8;
		
		// End Region Bools and default settings for Renko based conditions
		#endregion
		
		
		#region Bools and default settings for RV Bars based conditions
		// Bools for Renko based conditions
		
		// RV Series 1
        [NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 1", Order = 1, GroupName = "03. Timeframes: RV Bars")]
        public bool UseRVBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 1 Directional Bar Ticks", Order = 2, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries1DirectionBarTicks { get; set; } = 8;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 1 Reversal Bars", Order = 3, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries1ReversalBars { get; set; } = 4;
		
		// RV Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 2", Order = 4, GroupName = "03. Timeframes: RV Bars")]
        public bool UseRVBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 2 Directional Bar Ticks", Order = 5, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries2DirectionBarTicks { get; set; } = 5;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 2 Reversal Bars", Order = 6, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries2ReversalBars { get; set; } = 12;
		
		// RV Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 3", Order = 7, GroupName = "03. Timeframes: RV Bars")]
        public bool UseRVBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 3 Directional Bar Ticks", Order = 8, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries3DirectionBarTicks { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 3 Reversal Bars", Order = 9, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries3ReversalBars { get; set; } = 8;
		
		// RV Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 4", Order = 10, GroupName = "03. Timeframes: RV Bars")]
        public bool UseRVBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 4 Directional Bar Ticks", Order = 11, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries4DirectionBarTicks { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 4 Reversal Bars", Order = 12, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries4ReversalBars { get; set; } = 8;
		
		// RV Series 5
		[NinjaScriptProperty]
        [Display(Name = "Use RVBars Series 5", Order = 13, GroupName = "03. Timeframes: RV Bars")]
        public bool UseRVBarsSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 5 Directional Bar Ticks", Order = 14, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries5DirectionBarTicks { get; set; } = 15;
		
		[NinjaScriptProperty]
        [Display(Name = "RVBars Series 5 Reversal Bars", Order = 15, GroupName = "03. Timeframes: RV Bars")]
        public int RVBarsSeries5ReversalBars { get; set; } = 6;
		
		// End Region Bools and default settings for RV Bars based conditions
		#endregion
		
		
		#region Bools and default settings for ORenko based conditions
		// Bools for Orenko based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use ORenko Series 1", Order = 1, GroupName = "03. Timeframes: Renko - Orenko")]
        public bool UseORenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Trend Threshold Value", Order = 2, GroupName = "03. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1TrendThresholdValue { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Open Offset Value", Order = 3, GroupName = "03. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1OpenOffsetValue { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "ORenko Series 1 Reversal Threshold Value", Order = 4, GroupName = "03. Timeframes: Renko - Orenko")]
        public int ORenkoSeries1ReversalThresholdValue { get; set; } = 28;
		
		#endregion
		
		
		#region Bools and default settings for Unirenko based conditions
		// Bools for Unirenko based conditions
		
		// Unirenko Series 1
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 1", Order = 1, GroupName = "03. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Tick Trend Value", Order = 2, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Open Offset Value", Order = 3, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1OpenOffsetValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 1 Tick Reversal Value", Order = 4, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries1TickReversalValue { get; set; } = 10;
		
		// Unirenko Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 2", Order = 5, GroupName = "03. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Tick Trend Value", Order = 6, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Open Offset Value", Order = 7, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 2 Tick Reversal Value", Order = 8, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries2TickReversalValue { get; set; } = 20;
		
		// Unirenko Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 3", Order = 9, GroupName = "03. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Tick Trend Value", Order = 10, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Open Offset Value", Order = 11, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3OpenOffsetValue { get; set; } = 44;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 3 Tick Reversal Value", Order = 12, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries3TickReversalValue { get; set; } = 44;
		
		// Unirenko Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use Unirenko Series 4", Order = 13, GroupName = "03. Timeframes: Renko - Unirenko")]
        public bool UseUnirenkoSeries4 { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Tick Trend Value", Order = 14, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Open Offset Value", Order = 15, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4OpenOffsetValue { get; set; } = 50;
		
		[NinjaScriptProperty]
        [Display(Name = "Unirenko Series 4 Tick Reversal Value", Order = 16, GroupName = "03. Timeframes: Renko - Unirenko")]
        public int UnirenkoSeries4TickReversalValue { get; set; } = 200;
		
		// Endregion Bools and default settings for Unirenko based conditions
		#endregion
		
		
		#region Bools and default settings for UnirenkoHA based conditions
		// Bools for UnirenkoHA based conditions
		
		// UnirenkoHA Series 1
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 1", Order = 1, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Tick Trend Value", Order = 2, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Open Offset Value", Order = 3, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1OpenOffsetValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 1 Tick Reversal Value", Order = 4, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries1TickReversalValue { get; set; } = 10;
		
		// UnirenkoHA Series 2
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 2", Order = 10, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Tick Trend Value", Order = 11, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2TickTrendValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Open Offset Value", Order = 12, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 2 Tick Reversal Value", Order = 13, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries2TickReversalValue { get; set; } = 30;
		
		// UnirenkoHA Series 3
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 3", Order = 20, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Tick Trend Value", Order = 21, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3TickTrendValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Open Offset Value", Order = 22, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 3 Tick Reversal Value", Order = 23, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries3TickReversalValue { get; set; } = 50;
		
		// UnirenkoHA Series 4
		[NinjaScriptProperty]
        [Display(Name = "Use UnirenkoHA (Unirenko Heiken Ashi) Series 4", Order = 30, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public bool UseUnirenkoHASeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Tick Trend Value", Order = 31, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4TickTrendValue { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Open Offset Value", Order = 32, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4OpenOffsetValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "UnirenkoHA (Unirenko Heiken Ashi) Series 4 Tick Reversal Value", Order = 33, GroupName = "03. Timeframes: Renko - UnirenkoHA")]
        public int UnirenkoHASeries4TickReversalValue { get; set; } = 100;
		// Endregion Bools and default settings for UnirenkoHA based conditions
		#endregion
		
		
		
		#region Bools and default settings for volume based conditions
		// Bools for volume based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 1", Order = 1, GroupName = "03. Timeframes: Volume")]
        public bool UseVolumeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 1 Value", Order = 2, GroupName = "03. Timeframes: Volume")]
        public int VolumeSeries1Value { get; set; } = 250;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 2", Order = 3, GroupName = "03. Timeframes: Volume")]
        public bool UseVolumeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 2 Value", Order = 4, GroupName = "03. Timeframes: Volume")]
        public int VolumeSeries2Value { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 3", Order = 5, GroupName = "03. Timeframes: Volume")]
        public bool UseVolumeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 3 Value", Order = 6, GroupName = "03. Timeframes: Volume")]
        public int VolumeSeries3Value { get; set; } = 1000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 4", Order = 7, GroupName = "03. Timeframes: Volume")]
        public bool UseVolumeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 4 Value", Order = 8, GroupName = "03. Timeframes: Volume")]
        public int VolumeSeries4Value { get; set; } = 2000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Volume Series 5", Order = 9, GroupName = "03. Timeframes: Volume")]
        public bool UseVolumeSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Volume Series 5 Value", Order = 10, GroupName = "03. Timeframes: Volume")]
        public int VolumeSeries5Value { get; set; } = 5000;
		
		// Endregion Bools for volume based conditions
		#endregion

		
		#region Bools and default settings for range based conditions
		// Bools for range based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 1", Order = 1, GroupName = "03. Timeframes: Range")]
        public bool UseRangeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 1 Value", Order = 2, GroupName = "03. Timeframes: Range")]
        public int RangeSeries1Value { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 2", Order = 3, GroupName = "03. Timeframes: Range")]
        public bool UseRangeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 2 Value", Order = 4, GroupName = "03. Timeframes: Range")]
        public int RangeSeries2Value { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 3", Order = 5, GroupName = "03. Timeframes: Range")]
        public bool UseRangeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 3 Value", Order = 6, GroupName = "03. Timeframes: Range")]
        public int RangeSeries3Value { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 4", Order = 7, GroupName = "03. Timeframes: Range")]
        public bool UseRangeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 4 Value", Order = 8, GroupName = "03. Timeframes: Range")]
        public int RangeSeries4Value { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 5", Order = 9, GroupName = "03. Timeframes: Range")]
        public bool UseRangeSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Range Series 5 Value", Order = 10, GroupName = "03. Timeframes: Range")]
        public int RangeSeries5Value { get; set; } = 50;
		
		// Endregion Bools and default settings for range based conditions
		#endregion
		
		
		#region Bools and default settings for MEAN range based conditions
		// Bools for MEAN range based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 1", Order = 1, GroupName = "03. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 1 Value", Order = 2, GroupName = "03. Timeframes: Range - MEAN")]
        public int MEANRangeSeries1Value { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 2", Order = 3, GroupName = "03. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 2 Value", Order = 4, GroupName = "03. Timeframes: Range - MEAN")]
        public int MEANRangeSeries2Value { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 3", Order = 5, GroupName = "03. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 3 Value", Order = 6, GroupName = "03. Timeframes: Range - MEAN")]
        public int MEANRangeSeries3Value { get; set; } = 10;
		
		[NinjaScriptProperty]
        [Display(Name = "Use MEAN Range Series 4", Order = 7, GroupName = "03. Timeframes: Range - MEAN")]
        public bool UseMEANRangeSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "MEAN Range Series 4 Value", Order = 8, GroupName = "03. Timeframes: Range - MEAN")]
        public int MEANRangeSeries4Value { get; set; } = 30;

		// Endregion Bools and default settings for MEAN range based conditions
		#endregion
		
		
		#region Bools and default settings for tick based conditions
		// Bools for tick based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 1", Order = 1, GroupName = "03. Timeframes: Tick")]
        public bool UseTickSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 1 Value", Order = 2, GroupName = "03. Timeframes: Tick")]
        public int UseTickSeries1Value { get; set; } = 200;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 2", Order = 3, GroupName = "03. Timeframes: Tick")]
        public bool UseTickSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 2 Value", Order = 4, GroupName = "03. Timeframes: Tick")]
        public int UseTickSeries2Value { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 3", Order = 5, GroupName = "03. Timeframes: Tick")]
        public bool UseTickSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 3 Value", Order = 6, GroupName = "03. Timeframes: Tick")]
        public int UseTickSeries3Value { get; set; } = 1000;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 4", Order = 7, GroupName = "03. Timeframes: Tick")]
        public bool UseTickSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 4 Value", Order = 8, GroupName = "03. Timeframes: Tick")]
        public int UseTickSeries4Value { get; set; } = 1500;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Tick Series 5", Order = 9, GroupName = "03. Timeframes: Tick")]
        public bool UseTickSeries5 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Tick Series 5 Value", Order = 10, GroupName = "03. Timeframes: Tick")]
        public int UseTickSeries5Value { get; set; } = 2000;
		
		// Endregion Bools and default settings for tick based conditions
		#endregion
		
		
		#region Bools and default settings for TBars based conditions
		// Bools and settings for TBars based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 1", Order = 1, GroupName = "03. Timeframes: TBars")]
        public bool UseTBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 1 Bar Speed", Order = 2, GroupName = "03. Timeframes: TBars")]
        public int UseTBarsSeries1BarSpeedValue { get; set; } = 4;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 2", Order = 3, GroupName = "03. Timeframes: TBars")]
        public bool UseTBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 2 Bar Speed", Order = 4, GroupName = "03. Timeframes: TBars")]
        public int UseTBarsSeries2BarSpeedValue { get; set; } = 12;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 3", Order = 5, GroupName = "03. Timeframes: TBars")]
        public bool UseTBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 3 Bar Speed", Order = 6, GroupName = "03. Timeframes: TBars")]
        public int UseTBarsSeries3BarSpeedValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use TBars Series 4", Order = 7, GroupName = "03. Timeframes: TBars")]
        public bool UseTBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "TBars Series 4 Bar Speed", Order = 8, GroupName = "03. Timeframes: TBars")]
        public int UseTBarsSeries4BarSpeedValue { get; set; } = 34;
		
		
		
		// Endregion Bools and default settings for TBars based conditions
		#endregion
		
		
		#region Bools and default settings for Delta Bars based conditions
		// NOTE: The Delta isn't working correctly as of 2025/02/08 JET
		// Bools for Delta Bars based conditions
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 1", Order = 1, GroupName = "03. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 1 Trend Delta ", Order = 2, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries1TrendDeltaValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 1 Trend Reversal ", Order = 3, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries1TrendReversalValue { get; set; } = 20;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 2", Order = 4, GroupName = "03. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 2 Trend Delta ", Order = 5, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries2TrendDeltaValue { get; set; } = 30;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 2 Trend Reversal ", Order = 6, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries2TrendReversalValue { get; set; } = 30;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 3", Order = 7, GroupName = "03. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 3 Trend Delta ", Order = 8, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries3TrendDeltaValue { get; set; } = 40;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 3 Trend Reversal ", Order = 9, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries3TrendReversalValue { get; set; } = 40;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Delta Bars Series 4", Order = 10, GroupName = "03. Timeframes: Delta Bars")]
        public bool UseDeltaBarsSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 4 Trend Delta ", Order = 11, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries4TrendDeltaValue { get; set; } = 50;
		
		[NinjaScriptProperty]
        [Display(Name = "Delta Bars Series 4 Trend Reversal ", Order = 12, GroupName = "03. Timeframes: Delta Bars")]
        public int DeltaBarsSeries4TrendReversalValue { get; set; } = 50;
		
		// Endregion Bools and default settings for Delta Bars based conditions
		#endregion
		
		
		#region Bools and default settings for Line Break for MINUTE based charts
		// This section is meant to specify line break data series. Users will specify the MINUTE value, and the number of line breaks.
		// A separate section will need to be created for other data series, such as the Line Break of Tick, or the Line Break of Volume
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 1", Order = 1, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 1, Minute value ", Order = 2, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries1MinuteValue { get; set; } = 1;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 1, # Breaks Value ", Order = 3, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries1BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 2", Order = 4, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 2, Minute value ", Order = 5, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries2MinuteValue { get; set; } = 2;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 2, # Breaks Value ", Order = 6, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries2BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 3", Order = 7, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 3, Minute value ", Order = 8, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries3MinuteValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 3, # Breaks Value ", Order = 9, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries3BreaksValue { get; set; } = 3;
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break MINUTES Series 4", Order = 10, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public bool UseLineBreakMINUTEBasedSeries4 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 4, Minute value ", Order = 11, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries4MinuteValue { get; set; } = 5;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break MINUTES Series 4, # Breaks Value ", Order = 12, GroupName = "03. Timeframes: Line Break - Minute Based")]
        public int LineBreakMinutesBasedSeries4BreaksValue { get; set; } = 3;
		
		// Endregion Bools and default settings for Line Break for MINUTE based charts
		#endregion
		
		
		#region Bools and default settings for Line Break for TICK based charts
		// This section is meant to specify line break data series. Users will specify the TICK value, and the number of line breaks.
		// A separate section will need to be created for other data series, such as the Line Break of Tick, or the Line Break of Volume
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 1", Order = 1, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries1 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 1, TICK value ", Order = 2, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries1TickValue { get; set; } = 100;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 1, # Breaks Value ", Order = 3, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries1BreaksValue { get; set; } = 3;
		
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 2", Order = 4, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries2 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 2, TICK value ", Order = 5, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries2TickValue { get; set; } = 250;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 2, # Breaks Value ", Order = 6, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries2BreaksValue { get; set; } = 3;
		
		
		[NinjaScriptProperty]
        [Display(Name = "Use Line Break TICK Series 3", Order = 7, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public bool UseLineBreakTickBasedSeries3 { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 3, TICK value ", Order = 8, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries3TickValue { get; set; } = 500;
		
		[NinjaScriptProperty]
        [Display(Name = "Line Break TICK Series 3, # Breaks Value ", Order = 9, GroupName = "03. Timeframes: Line Break - TICK Based")]
        public int LineBreakTickBasedSeries3BreaksValue { get; set; } = 3;
		
		// Endregion Bools and default settings for Line Break for TICK based charts
		#endregion
		
		
		#region Bools and default settings for Second and Minute based conditions
		// Bools for Second and Minute based conditions
        [NinjaScriptProperty]
        [Display(Name = "Use 30 Second", Order = 1, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use30Second { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Use 1 Minute", Order = 2, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use1Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 2 Minute", Order = 3, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use2Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 3 Minute", Order = 4, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use3Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 5 Minute", Order = 5, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use5Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 10 Minute", Order = 6, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use10Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 15 Minute", Order = 7, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use15Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 30 Minute", Order = 8, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use30Minute { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 1 Hour", Order = 9, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use1Hour { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use 4 Hour", Order = 10, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool Use4Hour { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Custom (enter in Minutes)", Order = 11, GroupName = "03. Timeframes: Timeframes Minute")]
        public bool UseMinuteBasedCustom { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Use Custom: # Minutes", Order = 12, GroupName = "03. Timeframes: Timeframes Minute")]
        public int UseMinuteBasedCustomIntValue { get; set; } = 135;
		
		// Endregion for Bools and default settings for Second and Minute based conditions
		#endregion
		
		
		
		// ─────────────────────────────
	    // Minimum count of additional series signals required for a valid entry.
		// ─────────────────────────────
	    [NinjaScriptProperty]
	    [Display(Name = "Min Required Time Series Signals", Order = 10, GroupName = "03. Multi Time Series Options")]
	    public int MinRequiredTimeSeriesSignals { get; set; } = 1;

		//endregion Multi Time Series Variables and Properties
		#endregion

		#region Order Label Constants (Highly Recommended)

		// Define your order labels as constants.  This prevents typos and ensures consistency.
		private const string LongEntryLabel = "LE";
		private const string ShortEntryLabel = "SE";
		private const string QuickLongEntryLabel = "QLE";
		private const string QuickShortEntryLabel = "QSE";
		private const string Add1LongEntryLabel = "Add1LE";
		private const string Add1ShortEntryLabel = "Add1SE";
		// Add constants for other order labels as needed (e.g., "LE2", "SE2", "TrailingStop")

		#endregion

//		// KillAll 
		private Account chartTraderAccount;

		#region TradeToDiscord

		private ClientWebSocket clientWebSocket;
		private List<dynamic> signalHistory = new List<dynamic>();
		private DateTime lastDiscordMessageTime = DateTime.MinValue;
		private readonly TimeSpan discordRateLimitInterval = TimeSpan.FromSeconds(30); // Adjust the interval as needed

		private string lastSignalType = "N/A";
		private double lastEntryPrice = 0.0;
		private double lastStopLoss = 0.0;
		private double lastProfitTarget = 0.0;
		private DateTime lastSignalTime = DateTime.MinValue;

		#endregion	
		
		#endregion
		
		public override string DisplayName { get { return Name; } }
		
		#region OnStateChange
		protected override void OnStateChange()
        {
			switch (State)
			{
				case State.SetDefaults:
					ConfigureStrategyDefaults();
					break;
				case State.Configure:
					ConfigureStrategy();
					ConfigureMultiTimeSeries(); // << New method to add extra data series based on bool properties
					break;
				case State.DataLoaded:
					InitializeIndicators();
					LoadChartTraderButtons();
					maxProfit = totalPnL;
					break;
				case State.Historical:
					break;
				case State.Terminated:
					CleanUpStrategy();
					break;
			}
		}
			
        private void ConfigureStrategyDefaults()
		{
			Description = @"Base Strategy with OEB v.5.0.2 TradeSaber(Dre). and ArchReactor for KC (Khanh Nguyen)";
			Name = "ATM Trader";
			BaseAlgoVersion = "ATM Trader v4.7";
			Author = "indiVGA, Khanh Nguyen, Oshi, based on ArchReactor";
			Version = "Version 4.7 Mar. 2025";
			Credits = "";
			StrategyName = "";
			ChartType = "Orenko 34-40-40"; // TODO: Document Magic Numbers

			EntriesPerDirection = 10;
			Calculate = Calculate.OnPriceChange;
			EntryHandling = EntryHandling.AllEntries;
			IsExitOnSessionCloseStrategy = true;
			ExitOnSessionCloseSeconds = 30;
			IsFillLimitOnTouch = false;
			MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
			OrderFillResolution = OrderFillResolution.High;
			Slippage = 0;
			StartBehavior = StartBehavior.WaitUntilFlat;
			TimeInForce = TimeInForce.Gtc;
			TraceOrders = false;
			RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
			StopTargetHandling = StopTargetHandling.PerEntryExecution;
			BarsRequiredToTrade = 20;
			IsInstantiatedOnEachOptimizationIteration = false;

			// Default Parameters
			isManualTradeEnabled = true;
			isLongEnabled = true;
			isShortEnabled = true;
			canTradeOK = true;

			OrderType = OrderType.Limit;
			ATMStrategyTemplate = "ATM";

			HmaPeriod = 50;
			enableHmaHooks = true;
			showHmaHooks = true;

			RegChanPeriod = 40;
			RegChanWidth = 4;
			RegChanWidth2 = 3;
			enableRegChan1 = true;
			enableRegChan2 = true;
			showRegChan1 = true;
			showRegChan2 = true;
			showRegChanHiLo = true;

			enableVMA = true;
			showVMA = true;

			enableMomo = true;
			showMomo = true;
			MomoUp = 1;
			MomoDown = -1;

			enableWilly = true;
			showWilly = false;
			wrUp = -20;
			wrDown = -80;
			wrPeriod = 14;
				
			enableSuperRex = true;
			showCMO = false;
			CmoUp = 10;
			CmoDown = -10;
				
			enableLinReg = true;
			showLinReg = false;
			LinRegPeriod = 14;
				
			enableTrendMagic = true;
			showTrendMagic = false;
			cciPeriod = 20;
			atrPeriod = 14;
			atrMult	= 0.1;
				
			enableADX = false;
			showAdx = false;
			adxPeriod = 7;
			adxThreshold = 25;
			
            // T3 Trend Filter settings
            Factor 				= 0.5;
            Period1 			= 1;
            Period2 			= 1;
            Period3 			= 1;
            Period4 			= 1;
            Period5 			= 9;
			enableTrendy		= false;
			showTrendy			= false;
				
			TickMove = 4;
			BreakevenOffset = 4;
			
			tradesPerDirection = false;
			longPerDirection = 5;
			shortPerDirection = 5;

			QuickLong = false;
			QuickShort = false;

			counterLong = 0;
			counterShort = 0;

			Start = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
			End = DateTime.Parse("07:30", System.Globalization.CultureInfo.InvariantCulture);
			Start2 = DateTime.Parse("07:31", System.Globalization.CultureInfo.InvariantCulture);
			End2 = DateTime.Parse("08:00", System.Globalization.CultureInfo.InvariantCulture);
			Start3 = DateTime.Parse("08:01", System.Globalization.CultureInfo.InvariantCulture);
			End3 = DateTime.Parse("12:00", System.Globalization.CultureInfo.InvariantCulture);
			Start4 = DateTime.Parse("12:01", System.Globalization.CultureInfo.InvariantCulture);
			End4 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
			Start5 = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
			End5 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
			Start6 = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
			End6 = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);

			// Panel Status
			showDailyPnl = true;
			PositionDailyPNL = TextPosition.TopLeft;
			colorDailyProfitLoss = Brushes.Cyan;

			showPnl = false;
			PositionPnl = TextPosition.BottomLeft;
			colorPnl = Brushes.Yellow;

			// PnL Daily Limits
			dailyLossProfit = true;
			DailyProfitLimit = 100000;
			DailyLossLimit = 1000;
			TrailingDrawdown = 1000;
			StartTrailingDD = 3000;
			maxProfit = double.MinValue;
			enableTrailingDD = true;
		}
			
        private void ConfigureStrategy()
		{
			RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
		}
		
		
		// ─────────────────────────────
    	// NEW MULTI TIME SERIES SECTION: ADD/MODIFY DATA SERIES HERE
    	// ─────────────────────────────
		private void ConfigureMultiTimeSeries()
		{
        	int index = 1;  // BarsArray[0] is the primary series, additional series indices start at 1

				#region Add All Data Series
				
				#region UseHeikenAshi MINUTE Add Data Series
				if (UseHeikenAshi1min)
				{
				    AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 1, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				if (UseHeikenAshi2min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 2, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi3min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 3, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi5min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 5, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshi15min) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, 1, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenAshiCustom) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Minute, HeikenAshiCustomMinutes, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UseHeikenAshi MINUTE Add Data Series
				#endregion
				
				#region UseHeikenAshi TICK Add Data Series
				if (UseHeikenTICKSeries1)
				{
				    AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries1Value, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				if (UseHeikenTICKSeries2) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries2Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenTICKSeries3) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries3Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseHeikenTICKSeries4) 
				{
		            AddHeikenAshi(Instrument.FullName, BarsPeriodType.Tick, UseHeikenTICKSeries4Value, MarketDataType.Last);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UseHeikenAshi MINUTE Add Data Series
				#endregion
				
				#region NinzaRenko Add Data Series
				if (UseNinzaRenkoSeries1)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					// AddRenko(Bars, 10);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries1BrickSize, Value2 = NinzaRenkoSeries1TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries2)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries2BrickSize, Value2 = NinzaRenkoSeries2TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries3)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries3BrickSize, Value2 = NinzaRenkoSeries3TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries4)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries4BrickSize, Value2 = NinzaRenkoSeries4TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseNinzaRenkoSeries5)
				{
					//AddDataSeries(BarsPeriodType.Renko, 10, 1);
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)12345, Value = NinzaRenkoSeries5BrickSize, Value2 = NinzaRenkoSeries5TrendThreshold });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion NinzaRenko Add Data Series
				#endregion

				#region RV Bars Add Data Series
				if (UseRVBarsSeries1)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries1DirectionBarTicks, Value2 = RVBarsSeries1ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries2DirectionBarTicks, Value2 = RVBarsSeries2ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries3DirectionBarTicks, Value2 = RVBarsSeries3ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries4DirectionBarTicks, Value2 = RVBarsSeries4ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRVBarsSeries5)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)991, Value = RVBarsSeries5DirectionBarTicks, Value2 = RVBarsSeries5ReversalBars });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion RV Bars Add Data Series
				#endregion
				
				#region Unirenko Add Data Series
				if (UseUnirenkoSeries1)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries1OpenOffsetValue, Value = UnirenkoSeries1TickTrendValue, Value2 = UnirenkoSeries1TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries2OpenOffsetValue, Value = UnirenkoSeries2TickTrendValue, Value2 = UnirenkoSeries2TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries3)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries3OpenOffsetValue, Value = UnirenkoSeries3TickTrendValue, Value2 = UnirenkoSeries3TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoSeries4)
				{
					// (BarsPeriodType)2018 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2018, BarsPeriodTypeName = "Unirenko", BaseBarsPeriodValue = UnirenkoSeries4OpenOffsetValue, Value = UnirenkoSeries4TickTrendValue, Value2 = UnirenkoSeries4TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Unirenko Add Data Series		
				#endregion
				
				#region UnirenkoHA Add Data Series
				if (UseUnirenkoHASeries1)
				{
					// (BarsPeriodType)2021 - the 2018 needs to be found based on the Unirenko ID on a machine.
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries1OpenOffsetValue, Value = UnirenkoHASeries1TickTrendValue, Value2 = UnirenkoHASeries1TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries2OpenOffsetValue, Value = UnirenkoHASeries2TickTrendValue, Value2 = UnirenkoHASeries2TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries3OpenOffsetValue, Value = UnirenkoHASeries3TickTrendValue, Value2 = UnirenkoHASeries3TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseUnirenkoHASeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2021, BarsPeriodTypeName = "UnirenkoHA", BaseBarsPeriodValue = UnirenkoHASeries4OpenOffsetValue, Value = UnirenkoHASeries4TickTrendValue, Value2 = UnirenkoHASeries4TickReversalValue});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion UnirenkoHA Add Data Series		
				#endregion
				
				#region Volume Add Data Series
				if (UseVolumeSeries1)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries2)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries3)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries4)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseVolumeSeries5)
				{	
					AddDataSeries(BarsPeriodType.Volume, VolumeSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Volume Add Data Series
				#endregion
				
				#region Range Bar Add Data Series
				if (UseRangeSeries1)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries2)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries3)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries4)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseRangeSeries5)
				{	
					AddDataSeries(BarsPeriodType.Range, RangeSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Range Bar Add Data Series
				#endregion
				
				#region MEANRange Bar Add Data Series
				if (UseMEANRangeSeries1)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries1Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries2)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries2Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries3)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries3Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMEANRangeSeries4)
				{
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)322, Value = MEANRangeSeries4Value});
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion MEANRange Bar Add Data Series
				#endregion
				
				#region Tick Add Data Series
				if (UseTickSeries1)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries1Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries2)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries2Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries3)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries3Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTickSeries4)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries4Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseTickSeries5)
				{	
					AddDataSeries(BarsPeriodType.Tick, UseTickSeries5Value);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Tick Add Data Series
				#endregion
				
				#region TBars Add Data Series
				if (UseTBarsSeries1)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries1BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTBarsSeries2)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries2BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseTBarsSeries3)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries3BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				if (UseTBarsSeries4)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)98765, BarsPeriodTypeName = "TBars", BaseBarsPeriodValue = UseTBarsSeries4BarSpeedValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				
				
				// Endregion Tbars Add Data Series
				#endregion
				
				#region ORenko Add Data Series
				if (UseORenkoSeries1)
				{	
					AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)2023, BarsPeriodTypeName = "ORenko", BaseBarsPeriodValue = ORenkoSeries1TrendThresholdValue, Value = ORenkoSeries1OpenOffsetValue, Value2 = ORenkoSeries1ReversalThresholdValue});
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}	
				
				// Endregion ORenko Add Data Series
				#endregion
				
				#region Delta Bars Add Data Series
				// Delta bars are based on Tick data, so the tick data has to be added
				if (UseDeltaBarsSeries1)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries1TrendDeltaValue, Value2 = DeltaBarsSeries1TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}

				if (UseDeltaBarsSeries2)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries2TrendDeltaValue, Value2 = DeltaBarsSeries2TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				if (UseDeltaBarsSeries3)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries3TrendDeltaValue, Value2 = DeltaBarsSeries3TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				if (UseDeltaBarsSeries4)
				{
				  	AddDataSeries(new BarsPeriod() { BarsPeriodType = (BarsPeriodType)15, Value = DeltaBarsSeries4TrendDeltaValue, Value2 = DeltaBarsSeries4TrendReversalValue });
					timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;

				}
				
				#endregion
				
				#region Line Break MINUTES Add Data Series
				// Add the line break data series. This uses a built-in method similar to Heiken Ashi, as referenced here: https://ninjatrader.com/support/helpguides/nt8/NT%20HelpGuide%20English.html?adddataseries.htm
				
				
				if (UseLineBreakMINUTEBasedSeries1)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries1MinuteValue, LineBreakMinutesBasedSeries1BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}


				if (UseLineBreakMINUTEBasedSeries2)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries2MinuteValue, LineBreakMinutesBasedSeries2BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakMINUTEBasedSeries3)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries3MinuteValue, LineBreakMinutesBasedSeries3BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakMINUTEBasedSeries4)
				{
					AddLineBreak(Instrument.FullName, BarsPeriodType.Minute, LineBreakMinutesBasedSeries4MinuteValue, LineBreakMinutesBasedSeries4BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				
				
				#endregion
				
				#region Line Break TICK Add Data Series
				// Add the line break data series. This uses a built-in method similar to Heiken Ashi, as referenced here: https://ninjatrader.com/support/helpguides/nt8/NT%20HelpGuide%20English.html?adddataseries.htm
				if (UseLineBreakTickBasedSeries1)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries1TickValue, LineBreakTickBasedSeries1BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}


				if (UseLineBreakTickBasedSeries2)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries2TickValue, LineBreakTickBasedSeries2BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				
				if (UseLineBreakTickBasedSeries3)
				{
				    AddLineBreak(Instrument.FullName, BarsPeriodType.Tick, LineBreakTickBasedSeries3TickValue, LineBreakTickBasedSeries3BreaksValue, MarketDataType.Last);
				    timeframeToBarsIndex[index] = BarsArray.Length - 1;
				    enabledTimeframes.Add(BarsArray.Length - 1);
				    index++;
				}
				// endregion Line Break TICK Add Data Series
				#endregion
				
				#region Second and Minute Add Data Series
				// Add Second and Minute data series.
				if (Use30Second) 
				{
		            AddDataSeries(BarsPeriodType.Second, 30);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use1Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 1);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use2Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 2);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use3Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 3);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use5Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 5);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
				}
				if (Use10Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 10);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use15Minute) 
				{
		            AddDataSeries(BarsPeriodType.Minute, 15);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use30Minute)
				{
		            AddDataSeries(BarsPeriodType.Minute, 30);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use1Hour)
				{
		            AddDataSeries(BarsPeriodType.Minute, 60);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (Use4Hour)
				{
		            AddDataSeries(BarsPeriodType.Minute, 240);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				if (UseMinuteBasedCustom)
				{
		            AddDataSeries(BarsPeriodType.Minute, UseMinuteBasedCustomIntValue);
		            timeframeToBarsIndex[index] = BarsArray.Length - 1;
		            enabledTimeframes.Add(BarsArray.Length - 1);
		            index++;
				}
				// endregion Second and Minute Add Data Series
				#endregion
				
				#endregion Add All Data Series
		}
		
		private void InitializeIndicators()
		{
			hullMAHooks = BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
			hullMAHooks.Plots[0].Brush = Brushes.White;
			hullMAHooks.Plots[0].Width = 2;
			if (showHmaHooks) AddChartIndicator(hullMAHooks);

			RegressionChannel1 = RegressionChannel(Close, RegChanPeriod, RegChanWidth);
			if (showRegChan1) AddChartIndicator(RegressionChannel1);

			RegressionChannel2 = RegressionChannel(Close, RegChanPeriod, RegChanWidth2);
			if (showRegChan2) AddChartIndicator(RegressionChannel2);

			RegressionChannelHighLow1 = RegressionChannelHighLow(Close, RegChanPeriod, RegChanWidth);
			if (showRegChanHiLo) AddChartIndicator(RegressionChannelHighLow1);

            LinReg1 	= LinReg2(Close, LinRegPeriod);
			LinReg1.Plots[0].Width = 2;
			if (showLinReg) AddChartIndicator(LinReg1);
			
			vmaIndicator = VMA(Close, 9, 9);
			vmaIndicator.Plots[0].Brush = Brushes.SkyBlue;
			vmaIndicator.Plots[0].Width = 3;
			if (showVMA) AddChartIndicator(vmaIndicator);

			momentumIndicator = Momentum(Close, 14);
			momentumIndicator.Plots[0].Brush = Brushes.Yellow;
			momentumIndicator.Plots[0].Width = 2;
			if (showMomo) AddChartIndicator(momentumIndicator);

			WilliamsR1    = TOWilliamsTraderOracleSignalMOD(Close, 14, @"LongEntry", @"ShortEntry");
			WilliamsR1.Plots[0].Brush = Brushes.Yellow;
			WilliamsR1.Plots[0].Width = 1;
			if (showWilly) AddChartIndicator(WilliamsR1);	
				
            CMO1				= CMO(Close, 14);
			CMO1.Plots[0].Brush = Brushes.Yellow;
			CMO1.Plots[0].Width = 2;
			if (showCMO) AddChartIndicator(CMO1);
				 		
			TrendMagic1		 	= TrendMagic(cciPeriod, atrPeriod, atrMult, false);
            if (showTrendMagic) AddChartIndicator(TrendMagic1);
			
			T3TrendFilter1 = T3TrendFilter(Close, Factor, Period1, Period2, Period3, Period4, Period5, false);
			if (showTrendy) AddChartIndicator(T3TrendFilter1);
			
			adxIndicator = ADX(Close, adxPeriod);
			adxIndicator.Plots[0].Brush = Brushes.Cyan;
			adxIndicator.Plots[0].Width = 2;
			if (showAdx) AddChartIndicator(adxIndicator);

			maxProfit = totalPnL;
		}
			
		private void LoadChartTraderButtons()
		{
			Dispatcher.InvokeAsync(() => { CreateWPFControls(); });
		}

		private void CleanUpStrategy()
		{
			ChartControl?.Dispatcher.InvokeAsync(() => { DisposeWPFControls(); });

			clientWebSocket?.Dispose();

			lock (orderLock)
			{
				if (activeOrders.Count > 0)
				{
					Print($"{Time[0]}: Strategy terminated with active orders. Investigate:");
					foreach (var kvp in activeOrders)
					{
						Print($"{Time[0]}: Order Label: {kvp.Key}, Order ID: {kvp.Value.OrderId}");
						CancelOrder(kvp.Value);
					}
				}
			}
		}
        
		#endregion	
		
        #region OnBarUpdate
		protected override void OnBarUpdate()
        {
			if (BarsInProgress != 0 || CurrentBars[0] < 5 || orderErrorOccurred)
                return;				
	
			if (Bars.IsFirstBarOfSession)
			{
			    canTradeToday = true;
				cumPnL 			= totalPnL; ///Double that copies the full session PnL (If trading multiple days). Is only calculated once per day.
				dailyPnL		= totalPnL - cumPnL; ///Subtract the copy of the full session by the full session PnL. This resets your daily PnL back to 0.
				Print ($"{Time[0]} //On Bar Update//// IsFirst Bar of SessioncumPnL: {cumPnL}, dailyPnL: {dailyPnL}, totalPnL: {totalPnL}, CumProfit is {SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit}");

			}
			
			if (!canTradeToday || State == State.Historical) return;

			//Track the Highest Profit Achieved
			if (totalPnL > maxProfit)
			{
				maxProfit = totalPnL;
                Print ($"{Time[0]} ///On Bar Update//// Updated maxProfit: {maxProfit}");

			}

			dailyPnL = (totalPnL) - (cumPnL); ///Your daily limit is the difference between these

			PositionDailyPNL = TextPosition.TopLeft;
			PositionPnl = TextPosition.BottomLeft;
			
			// Account Reconciliation
			if (DateTime.Now - lastAccountReconciliationTime > accountReconciliationInterval)
			{
				ReconcileAccountOrders();
				lastAccountReconciliationTime = DateTime.Now;
			}

            bool channelSlopeUp = (RegressionChannel1.Middle[1] > RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] <= RegressionChannel1.Middle[3]) 
				|| (RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1] && Low[0] > Low[2] && Low[2] <= RegressionChannel1.Lower[2]);
    		bool priceNearLowerChannel = (Low[0] > RegressionChannelHighLow1.Lower[2]);

			bool channelSlopeDown = (RegressionChannel1.Middle[1] < RegressionChannel1.Middle[2]) && (RegressionChannel1.Middle[2] >= RegressionChannel1.Middle[3])
				|| (RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1] && High[0] < High[2] && High[2] >= RegressionChannel1.Upper[2]);
    		bool priceNearUpperChannel = (High[0] < RegressionChannelHighLow1.Upper[2]);

			bool RegChanUp = RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1];
			bool RegChanDown = RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1];

            regChanUp = !enableRegChan1 || (channelSlopeUp || priceNearLowerChannel);
            regChanDown = !enableRegChan1 || (channelSlopeDown || priceNearUpperChannel);
			
			linRegUp = enableLinReg ? LinReg1[1] > LinReg1[2] && LinReg1[2] > LinReg1[3] : true;
			linRegDown = enableLinReg ? LinReg1[1] < LinReg1[2] && LinReg1[2] < LinReg1[3] : true;
			
			hmaHooksUp = !enableHmaHooks || ((Close[0] > hullMAHooks[0] && hullMAHooks.trend[0] == 1 && hullMAHooks.trend[1] == -1) 
				|| (Close[0] > hullMAHooks[0]));
			
			hmaHooksDown = !enableHmaHooks || ((Close[0] < hullMAHooks[0] && hullMAHooks.trend[0] == -1 && hullMAHooks.trend[1] == 1)  
				|| (Close[0] < hullMAHooks[0]));
			
			bool hmaUp = (Close[0] > hullMAHooks[0]);
			bool hmaDown = (Close[0] < hullMAHooks[0]);
			
			momoUp = !enableMomo || (momentumIndicator[0] > MomoUp && momentumIndicator[0] > momentumIndicator[1]);
			momoDown = !enableMomo || (momentumIndicator[0] < MomoDown && momentumIndicator[0] < momentumIndicator[1]);

			WillyUp = !enableWilly || (WilliamsR1[1] >= wrUp);
            WillyDown = !enableWilly || (WilliamsR1[1] <= wrDown);
			
			hmaHooksUp = !enableHmaHooks || Close[0] > hullMAHooks[0];
			hmaHooksDown = !enableHmaHooks || Close[0] < hullMAHooks[0];

			volMaUp = !enableVMA || Close[0] > vmaIndicator[0];
			volMaDown = !enableVMA || Close[0] < vmaIndicator[0];

			cmoUp = !enableSuperRex || CMO1[0] >= CmoUp;
            cmoDown = !enableSuperRex || CMO1[0] <= CmoDown;
			
			trendMagicUp = TrendMagic1.Trend[1] > TrendMagic1.Trend[2];
            trendMagicDown = TrendMagic1.Trend[1] < TrendMagic1.Trend[2];	
			
            TrendyUp = T3TrendFilter1.Values[0][0];
            TrendyDown = T3TrendFilter1.Values[1][0];

			trendyUp = enableTrendy ? TrendyUp >= 5 && TrendyDown == 0 : true;
            trendyUp = enableTrendy ? TrendyDown <= -5 && TrendyUp == 0 : true;	
			
			uptrend = volMaUp && hmaUp && RegChanUp;
			downtrend = volMaDown && hmaDown && RegChanDown;

			longSignal = hmaHooksUp || regChanUp || WillyUp || momoUp || cmoUp || linRegUp || trendMagicUp || trendyUp;
            shortSignal = hmaHooksDown || regChanDown || WillyDown || momoDown || cmoDown || linRegDown || trendMagicDown || trendyDown; 
			
			// ─────────────────────────────
		    // Evaluate Signals from all additional (multi) data series
		    // ─────────────────────────────
			EvaluateMultiTimeSeriesSignals();
			
			// Now combine the multi-series signals into the entry condition.
	        // For example, you might require that at least MinRequiredTimeSeriesSignals count are true:
	        int countExtraSignals = multiSeriesSignals.Values.Count(s => s);
        
			// Determine if additional series conditions are valid.
			bool extraSeriesValid = enabledTimeframes.Count == 0 
			    ? true 
			    : multiSeriesSignals.Values.Count(s => s) >= MinRequiredTimeSeriesSignals;
			
			entryPrice = Position.AveragePrice;
			currentPrice = Close[0];

			UpdatePositionState();

			if (Bars.IsFirstBarOfSession)
			{
				cumPnL = totalPnL;
				dailyPnL = totalPnL - cumPnL;
			}

			if (showPnl) ShowPNLStatus();

			if (isAutoTradeEnabled)
			{
				ProcessLongEntry(extraSeriesValid);
				ProcessShortEntry(extraSeriesValid);
			}

			if (!isAtmStrategyCreated)
				return;

			UpdateAtmStrategyStatus();

			if (atmStrategyId.Length > 0)
			{
				UpdateStopPrice();
				PrintAtmStrategyInfo();
			}

			ResetTradesPerDirection();
			ResetStopLoss();
			KillSwitch();
        }
		
		#endregion
		
		private void UpdatePositionState()
		{
			isLong = Position.MarketPosition == MarketPosition.Long;
			isShort = Position.MarketPosition == MarketPosition.Short;
			isFlat = Position.MarketPosition == MarketPosition.Flat;
		}

		private bool AtmStrategyNotActive()
        {
            return orderId.Length == 0 && atmStrategyId.Length == 0;
        }
		
		// Updated ProcessLongEntry method
		private void ProcessLongEntry(bool extraSeriesValid)
		{
		    if (IsLongEntryConditionMet(extraSeriesValid))
		    {
		        if (!tradesPerDirection || (tradesPerDirection && counterLong < longPerDirection))
		        {
		            counterLong++;
		            counterShort = 0;
		            CreateAtmStrategy(OrderAction.Buy, LongEntryLabel, Brushes.Cyan);
		        }
		        else
		        {
		            Print("Limit long trades in a row");
		        }
		    }
		}
		
		// Updated ProcessShortEntry method
		private void ProcessShortEntry(bool extraSeriesValid)
		{
		    if (IsShortEntryConditionMet(extraSeriesValid))
		    {
		        if (!tradesPerDirection || (tradesPerDirection && counterShort < shortPerDirection))
		        {
		            counterLong = 0;
		            counterShort++;
		            CreateAtmStrategy(OrderAction.SellShort, ShortEntryLabel, Brushes.Yellow);
		        }
		        else
		        {
		            Print("Limit short trades in a row");
		        }
		    }
		}

		private bool IsLongEntryConditionMet(bool extraSeriesValid)
		{
			return longSignal
				   && extraSeriesValid
				   && AtmStrategyNotActive()
//				   && (isManualTradeEnabled)
				   && (isLongEnabled)
				   && (checkTimers())
				   && ((dailyLossProfit ? dailyPnL > -DailyLossLimit : true))
				   && ((dailyLossProfit ? dailyPnL < DailyProfitLimit : true))
				   && (isFlat)
				   && (uptrend)
				   && (!trailingDrawdownReached)
				   && (canTradeOK)
				   && (canTradeToday);
		}

		private bool IsShortEntryConditionMet(bool extraSeriesValid)
		{
			return shortSignal
				   && extraSeriesValid
				   && AtmStrategyNotActive()
//				   && (isManualTradeEnabled)
				   && (isShortEnabled)
				   && (checkTimers())
				   && ((dailyLossProfit ? dailyPnL > -DailyLossLimit : true))
				   && ((dailyLossProfit ? dailyPnL < DailyProfitLimit : true))
				   && (isFlat)
				   && (downtrend)
				   && (!trailingDrawdownReached)
				   && (canTradeOK)
				   && (canTradeToday);
		}

		private void ResetTradesPerDirection()
		{
			if (tradesPerDirection)
			{
				if (counterLong != 0 && Close[1] < Open[1])
					counterLong = 0;
				if (counterShort != 0 && Close[1] > Open[1])
					counterShort = 0;
			}
		}

		private void ResetStopLoss()
		{
			if (isFlat)
			{
				quickLongBtnActive = false;
				quickShortBtnActive = false;

				lock (orderLock)
				{
					activeOrders.Clear();
				}
			}
		}
		
		#region ATM Strategy Methods

		private void CreateAtmStrategy(OrderAction orderAction, string signalName, Brush signalBrush)
		{
			isAtmStrategyCreated = false;
		    atmStrategyId = GetAtmStrategyUniqueId();
		    orderId = GetAtmStrategyUniqueId();

			Print($"Your atmStrategyId is: {atmStrategyId} OrderId is: {orderId}");

			OrderType orderType = (OrderType == OrderType.Market) ? OrderType.Market : OrderType.Limit;
			double orderPrice = (orderType == OrderType.Limit) ? (orderAction == OrderAction.Buy ? GetCurrentBid() : GetCurrentAsk()) : 0;

			AtmStrategyCreate(orderAction, orderType, orderPrice, 0, TimeInForce.Gtc, orderId, ATMStrategyTemplate, atmStrategyId, (atmCallbackErrorCode, atmCallBackId) =>
			{
				if (atmCallbackErrorCode == ErrorCode.NoError && atmCallBackId == atmStrategyId)
					isAtmStrategyCreated = true;
			});

			DrawArrow(signalName, orderPrice, signalBrush);
		}

		private void DrawArrow(string signalName, double signalPrice, Brush signalBrush)
		{
			if (signalName == LongEntryLabel)
				Draw.ArrowUp(this, signalName + CurrentBars[0], false, 0, signalPrice, signalBrush);
			else if (signalName == ShortEntryLabel)
				Draw.ArrowDown(this, signalName + CurrentBars[0], false, 0, signalPrice, signalBrush);
		}

		private void UpdateAtmStrategyStatus()
		{
			if (orderId.Length > 0)
			{
				string[] status = GetAtmStrategyEntryOrderStatus(orderId);

				if (status.Length > 0)
				{
					PrintOrderStatus(status);
					if (status[2] == "Filled" || status[2] == "Cancelled" || status[2] == "Rejected")
						orderId = string.Empty;
				}
			}
			else if (atmStrategyId.Length > 0 && GetAtmStrategyMarketPosition(atmStrategyId) == MarketPosition.Flat)
			{
				atmStrategyId = string.Empty;
			}
		}

		private void PrintOrderStatus(string[] status)
		{
			Print($"The entry order average fill price is: {status[0]}");
			Print($"The entry order filled amount is: {status[1]}");
			Print($"The entry order order state is: {status[2]}");
		}

		private void PrintAtmStrategyInfo()
		{
			Print($"The current ATM Strategy market position is: {GetAtmStrategyMarketPosition(atmStrategyId)}");
			Print($"The current ATM Strategy position quantity is: {GetAtmStrategyPositionQuantity(atmStrategyId)}");
			Print($"The current ATM Strategy average price is: {GetAtmStrategyPositionAveragePrice(atmStrategyId)}");
			Print($"The current ATM Strategy Unrealized PnL is: {GetAtmStrategyUnrealizedProfitLoss(atmStrategyId)}");
		}
		#endregion

        private void UpdateStopPrice()
        {
            MarketPosition marketPosition = GetAtmStrategyMarketPosition(atmStrategyId);
            double newStopPrice = 0;

            if (marketPosition == MarketPosition.Long)
            {
                newStopPrice = Low[0] - 3 * TickSize;
                if (newStopPrice < GetCurrentBid())
                {
                    AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
                }
            }
            else if (marketPosition == MarketPosition.Short)
            {
                newStopPrice = High[0] + 3 * TickSize;
                if (newStopPrice > GetCurrentAsk())
                {
                    AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
                }
            }
        }

		#region EvaluateMultiTimeSeriesSignals
	    // This method loops through each enabled additional data series and evaluates a sample condition.
	    // In this example, we simply check whether the close of the additional series is greater than its open.
	    // You can replace this with any custom condition per your strategy.
	    private void EvaluateMultiTimeSeriesSignals()
	    {
	        // Clear previous signals
	        multiSeriesSignals.Clear();
	
	        foreach (int idx in enabledTimeframes)
	        {
	            // Make sure there are enough bars in the additional series.
	            if (BarsArray[idx].Count > 1)
	            {
	                // Access the additional data series using its BarsArray index.
	                double seriesClose = BarsArray[idx].GetClose(BarsArray[idx].Count - 1);
	                double seriesOpen = BarsArray[idx].GetOpen(BarsArray[idx].Count - 1);
	
	                // Example condition: signal is true if close > open.
	                bool seriesSignal = seriesClose > seriesOpen;
	                multiSeriesSignals[idx] = seriesSignal;
	            }
	            else
	            {
	                // Not enough data – assume no signal.
	                multiSeriesSignals[idx] = false;
	            }
	        }
	    }
	    #endregion
		
		#region Order Submission Helpers

		// This method encapsulates all order submissions and error handling.
		private Order SubmitEntryOrder(string orderLabel, OrderType orderType, int contracts)
		{
			Order submittedOrder = null;

			lock (orderLock)
			{
				if (!CanSubmitOrder())
				{
					Print($"{Time[0]}: Cannot submit {orderLabel} order: Minimum order interval not met.");
					return null;
				}

				try
				{
					submittedOrder = SubmitOrder(orderLabel, orderType, contracts);

					if (submittedOrder != null)
					{
						activeOrders[orderLabel] = submittedOrder;
						lastOrderActionTime = DateTime.Now;
						Print($"{Time[0]}: Submitted {orderLabel} order with OrderId: {submittedOrder.OrderId}");
					}
					else
					{
						Print($"{Time[0]}: Error: {orderLabel} Entry order was null after submission.");
						orderErrorOccurred = true;
					}
				}
				catch (Exception ex)
				{
					Print($"{Time[0]}: Error submitting {orderLabel} entry order: {ex.Message}");
					orderErrorOccurred = true;
				}
			}

			return submittedOrder;
		}
		
		private Order SubmitOrder(string orderLabel, OrderType orderType, int contracts)
		{
			switch (orderType)
			{
				case OrderType.Market:
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel)
						return EnterLong(contracts, orderLabel);
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel)
						return EnterShort(contracts, orderLabel);
					break;
				case OrderType.Limit:
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel)
						return EnterLongLimit(contracts, GetCurrentBid(), orderLabel);
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel)
						return EnterShortLimit(contracts, GetCurrentAsk(), orderLabel);
					break;
				case OrderType.MIT:
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel)
						return EnterLongMIT(contracts, GetCurrentBid(), orderLabel);
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel)
						return EnterShortMIT(contracts, GetCurrentAsk(), orderLabel);
					break;
				case OrderType.StopLimit:
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel)
						return EnterLongLimit(contracts, GetCurrentBid(), orderLabel);
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel)
						return EnterShortLimit(contracts, GetCurrentAsk(), orderLabel);
					break;
				case OrderType.StopMarket:
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel)
						return EnterLong(contracts, orderLabel);
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel)
						return EnterShort(contracts, orderLabel);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(orderType), orderType, "Unsupported order type");
			}
			return null;
		}

		private void SubmitExitOrder(string orderLabel)
		{
			lock (orderLock)
			{
				try
				{
					if (orderLabel == LongEntryLabel || orderLabel == QuickLongEntryLabel || orderLabel == Add1LongEntryLabel)
					{
						ExitLong(orderLabel);
					}
					else if (orderLabel == ShortEntryLabel || orderLabel == QuickShortEntryLabel || orderLabel == Add1ShortEntryLabel)
					{
						ExitShort(orderLabel);
					}
					else
					{
						Print($"Error: invalid order label {orderLabel}");
					}

					if (!activeOrders.ContainsKey(orderLabel))
						Print($"Cannot cancel order that does not exist");

					if (activeOrders.TryGetValue(orderLabel, out Order orderToCancel))
					{
						CancelOrder(orderToCancel);
						activeOrders.Remove(orderLabel);
					}
				}
				catch (Exception ex)
				{
					Print($"Error submitting Exit order: {ex.Message}");
					orderErrorOccurred = true;
				}
			}
		}

		#endregion

		#region Rogue Order Detection

		private void ReconcileAccountOrders()
		{
			lock (orderLock)
			{
				try
				{
					var accounts = Account.All;

					if (accounts == null || accounts.Count == 0)
					{
						Print($"{Time[0]}: No accounts found.");
						return;
					}

					foreach (Account account in accounts)
					{
						List<Order> accountOrders = new List<Order>();

						try
						{
							foreach (Position position in account.Positions)
							{
								Instrument instrument = position.Instrument;
								foreach (Order order in Orders)
								{
									if (order.Instrument == instrument && order.Account == account)
									{
										accountOrders.Add(order);
									}
								}
							}
						}
						catch (Exception ex)
						{
							Print($"{Time[0]}: Error getting orders for account {account.Name}: {ex.Message}");
							continue;
						}

						if (accountOrders == null || accountOrders.Count == 0)
						{
							Print($"{Time[0]}: No orders found in account {account.Name}.");
							continue;
						}

						HashSet<string> strategyOrderIds = new HashSet<string>(activeOrders.Values.Select(o => o.OrderId));

						foreach (Order accountOrder in accountOrders)
						{
							if (!strategyOrderIds.Contains(accountOrder.OrderId))
							{
								Print($"{Time[0]}: Rogue order detected! Account: {accountOrder.Account.Name} OrderId: {accountOrder.OrderId}, OrderType: {accountOrder.OrderType}, OrderStatus: {accountOrder.OrderState}, Quantity: {accountOrder.Quantity}, AveragePrice: {accountOrder.AverageFillPrice}");

								try
								{
									CancelOrder(accountOrder);
									Print($"{Time[0]}: Attempted to cancel rogue order: {accountOrder.OrderId}");
								}
								catch (Exception ex)
								{
									Print($"{Time[0]}: Failed to Cancel rogue order. Account: {accountOrder.Account.Name} OrderId: {accountOrder.OrderId}, OrderType: {accountOrder.OrderType}, OrderStatus: {accountOrder.OrderState}, Quantity: {accountOrder.Quantity}, AveragePrice: {accountOrder.AverageFillPrice}, Reason: {ex.Message}");
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					Print($"{Time[0]}: Error during account reconciliation: {ex.Message}");
					orderErrorOccurred = true;
				}
			}
		}

		#endregion

		#region Helper Methods

		// Method to check the minimum interval between order submissions
		private bool CanSubmitOrder()
		{
			return (DateTime.Now - lastOrderActionTime) >= minOrderActionInterval;
		}

		#endregion

		#region OnExecutionUpdate

		protected virtual void OnExecutionUpdate(Execution execution, string executionId, double price,
									   int quantity, MarketPosition marketPosition, string orderId,
									   DateTime time)
		{
			// *** CRITICAL: Track order fills, modifications, and cancellations ***
			lock (orderLock)
			{
				// Find the order in our activeOrders dictionary
				string orderLabel = activeOrders.FirstOrDefault(x => x.Value.OrderId == orderId).Key;

				if (!string.IsNullOrEmpty(orderLabel))
				{
					switch (execution.Order.OrderState)
					{
						case OrderState.Filled:
							Print($"{Time[0]}: Order {orderId} with label {orderLabel} filled.");
							activeOrders.Remove(orderLabel); // Remove the order when it's filled.

							if (execution.Order.OrderState == OrderState.Filled && isFlat)
							{
								if (execution.Order.Name.StartsWith("LE") || execution.Order.Name.StartsWith("QLE") || execution.Order.Name.StartsWith("Add1LE"))
								{
									counterLong = 0;
								}
								else if (execution.Order.Name.StartsWith("SE") || execution.Order.Name.StartsWith("QSE") || execution.Order.Name.StartsWith("Add1SE"))
								{
									counterShort = 0;
								}
							}

							break;

						case OrderState.Cancelled:
							Print($"{Time[0]}: Order {orderId} with label {orderLabel} cancelled.");
							activeOrders.Remove(orderLabel); // Remove cancelled orders
							break;

						case OrderState.Rejected:
							Print($"{Time[0]}: Order {orderId} with label {orderLabel} rejected.");
							activeOrders.Remove(orderLabel); // Remove rejected orders
							break;

						default:
							Print($"{Time[0]}: Order {orderId} with label {orderLabel} updated to state: {execution.Order.OrderState}");
							break;
					}
				}
				else
				{
					// This could indicate a rogue order or an order not tracked by the strategy.
					Print($"{Time[0]}: Execution update for order {orderId}, but order is not tracked by the strategy.");

					// Attempt to Cancel the Rogue Order
					try
					{
						CancelOrder(execution.Order);
						Print($"{Time[0]}: Successfully Canceled the Rogue Order: {orderId}.");
					}
					catch (Exception ex)
					{
						Print($"{Time[0]}: Could not Cancel the Rogue Order: {orderId}. {ex.Message}");
						orderErrorOccurred = true;  // Consider whether to halt trading
					}
				}
			}
		}

		#endregion

		#region Daily PNL
		
		protected override void OnPositionUpdate(Cbi.Position position, double averagePrice, 
			int quantity, Cbi.MarketPosition marketPosition)
		{			
			if (isFlat && SystemPerformance.AllTrades.Count > 0)
			{
//				PositionPnl = TextPosition.BottomLeft;
//				totalPnL = 0; //backtest
			
				totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit; ///Double that sets the total PnL 
				dailyPnL = (totalPnL) - (cumPnL); ///Your daily limit is the difference between these
				
				// Re-enable the strategy if it was disabled by the DD and totalPnL increases
				if (enableTrailingDD && trailingDrawdownReached && totalPnL > maxProfit - TrailingDrawdown)
	            {
	                trailingDrawdownReached = false;
					isManualTradeEnabled = true;
					Print("Trailing Drawdown Lifted. Strategy Re-Enabled!");
				}
	
				if (dailyPnL <= -DailyLossLimit) //Print this when daily Pnl is under Loss Limit
				{
					
					Print("Daily Loss of " + DailyLossLimit +  " has been hit. No More Entries! Daily PnL >> " + dailyPnL + " <<" +  Time[0]);
					
					Text myTextLoss = Draw.TextFixed(this, "loss_text", "Daily Loss of " + DailyLossLimit +  " has been hit. No More Entries! Daily PnL >> " + "$" + totalPnL + " <<", PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 100);
					myTextLoss.Font = new SimpleFont("Arial", 18) {Bold = true };
	
				}				
				
				if (dailyPnL >= DailyProfitLimit) //Print this when daily Pnl is above Profit limit
				{
					
					Print("Daily Profit of " + DailyProfitLimit +  " has been hit. No more Entries! Daily PnL >>" +  dailyPnL + " <<" + Time[0]);
					
					Text myTextProfit = Draw.TextFixed(this, "profit_text", "Daily Profit of " + DailyProfitLimit +  " has been hit. No more Entries! Daily PnL >>" + "$" +  totalPnL + " <<", PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 100);
					myTextProfit.Font = new SimpleFont("Arial", 18) {Bold = true };	
				}
			}	
			
			if (isFlat)	checkPositions(); // Detect unwanted Positions opened (possible rogue Order?)						
		}
		
		protected void checkPositions()
		{
		//	Detect unwanted Positions opened (possible rogue Order?)
	        double currentPosition = Position.Quantity; // Get current position quantity
		
			if (isFlat)
			{
		        foreach (var order in Orders)
		        {
		            if (order != null) CancelOrder(order);
		        }				
			}
		}	
		
		#endregion	
		
		#region Chart Trader Button Handling
		protected void DecorateButton(Button button, string content, Brush background, Brush borderBrush, Brush foreground)
		{
			button.Content = content;
			button.Background = background;
			button.BorderBrush = borderBrush;
			button.Foreground = foreground;
		}

		protected void DecorateDisabledButtons(Button myButton, string stringButton)
		{
			DecorateButton(myButton, stringButton, Brushes.DarkRed, Brushes.Black, Brushes.White);
		}

		protected void DecorateEnabledButtons(Button myButton, string stringButton)
		{
			DecorateButton(myButton, stringButton, Brushes.DarkGreen, Brushes.Black, Brushes.White);
		}

		protected void DecorateNeutralButtons(Button myButton, string stringButton)
		{
			DecorateButton(myButton, stringButton, Brushes.LightGray, Brushes.Black, Brushes.Black);
		}

		protected void DecorateGrayButtons(Button myButton, string stringButton)
		{
			DecorateButton(myButton, stringButton, Brushes.DarkGray, Brushes.Black, Brushes.Black);
		}

		protected void CreateWPFControls()
		{
			chartWindow = System.Windows.Window.GetWindow(ChartControl.Parent) as Chart;

			if (chartWindow == null)
				return;

			chartTraderGrid = (chartWindow.FindFirst("ChartWindowChartTraderControl") as Gui.Chart.ChartTrader).Content as Grid;
			chartTraderButtonsGrid = chartTraderGrid.Children[0] as Grid;

		    InitializeButtonGrid(); // Call InitializeButtonGrid FIRST
		    CreateButtons(); // Call CreateButtons BEFORE SetButtonLocations and AddButtonsToGrid

			addedRow = new RowDefinition() { Height = new GridLength(250) };

			SetButtonLocations();
			AddButtonsToGrid();
	
			if (TabSelected())
				InsertWPFControls();
	
			chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;
		}

		protected void CreateButtons()
		{
			Style basicButtonStyle = System.Windows.Application.Current.FindResource("BasicEntryButton") as Style;
	
			manualBtn = CreateButton("\uD83D\uDD12 Manual On", basicButtonStyle, "Enable (Green) / Disbled (Red) Manual Button", OnButtonClick);
			if (isManualTradeEnabled) DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
			else DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
	
			autoBtn = CreateButton("\uD83D\uDD12 Auto On", basicButtonStyle, "Enable (Green) / Disbled (Red) Auto Button", OnButtonClick);
			if (isAutoTradeEnabled) DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
			else DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
	
			longBtn = CreateButton("LONG", basicButtonStyle, "Enable (Green) / Disbled (Red) Auto Long Entry", OnButtonClick);
			if (isLongEnabled) DecorateEnabledButtons(longBtn, "LONG");
			else DecorateDisabledButtons(longBtn, "LONG Off");
	
			shortBtn = CreateButton("SHORT", basicButtonStyle, "Enable (Green) / Disbled (Red) Auto Short Entry", OnButtonClick);
			if (isShortEnabled) DecorateEnabledButtons(shortBtn, "SHORT");
			else DecorateDisabledButtons(shortBtn, "SHORT Off");
	
			quickLongBtn = CreateButton("Buy", basicButtonStyle, "Buy Market Entry", OnButtonClick);
			DecorateEnabledButtons(quickLongBtn, "Buy");
	
			quickShortBtn = CreateButton("Sell", basicButtonStyle, "Sell Market Entry", OnButtonClick);
			DecorateDisabledButtons(quickShortBtn, "Sell");
	
			moveTSBtn = CreateButton("Move TS", basicButtonStyle, "Increase trailing stop", OnButtonClick, Brushes.DarkBlue, Brushes.Yellow);
			moveTS50PctBtn = CreateButton("Move TS 50%", basicButtonStyle, "Move trailing stop 50% closer to the current price", OnButtonClick, Brushes.DarkBlue, Brushes.Yellow);
			moveToBEBtn = CreateButton("Breakeven", basicButtonStyle, "Move stop to breakeven if in profit", OnButtonClick, Brushes.DarkBlue, Brushes.White);
	
			closeBtn = CreateButton("Close All Positions", basicButtonStyle, "Manual Close: CloseAllPosiions manually", OnButtonClick, Brushes.DarkRed, Brushes.White);
			panicBtn = CreateButton("\u2620 Panic Shutdown", basicButtonStyle, "PanicBtn: CloseAllPosiions", OnButtonClick, Brushes.DarkRed, Brushes.Yellow);
		}

		private Button CreateButton(string content, Style style, string toolTip, RoutedEventHandler clickHandler, Brush background = null, Brush foreground = null)
		{
			Button button = new Button
			{
				Content = content,
				Height = 25,
				Margin = new Thickness(1, 0, 1, 0),
				Padding = new Thickness(0, 0, 0, 0),
				Style = style,
				BorderThickness = new Thickness(1.5),
				IsEnabled = true,
				ToolTip = toolTip
			};
	
			if (background != null) button.Background = background;
			if (foreground != null) button.Foreground = foreground;
	
			button.Click += clickHandler;
	
			return button;
		}

		protected void InitializeButtonGrid()
		{
			lowerButtonsGrid = new Grid();
	
			for (int i = 0; i < 2; i++)
			{
				lowerButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition());
			}
	
			for (int i = 0; i <= 9; i++)
			{
				lowerButtonsGrid.RowDefinitions.Add(new RowDefinition());
			}
		}

		protected void SetButtonLocations()
		{
			SetButtonLocation(manualBtn, 0, 1);
			SetButtonLocation(autoBtn, 1, 1);
			SetButtonLocation(longBtn, 0, 2);
			SetButtonLocation(shortBtn, 1, 2);
			SetButtonLocation(quickLongBtn, 0, 3);
			SetButtonLocation(quickShortBtn, 1, 3);
			SetButtonLocation(moveTSBtn, 0, 4);
			SetButtonLocation(moveTS50PctBtn, 1, 4);
			SetButtonLocation(moveToBEBtn, 0, 5, 2);
			SetButtonLocation(closeBtn, 0, 6, 2);
			SetButtonLocation(panicBtn, 0, 7, 2);
		}

		protected void SetButtonLocation(Button button, int column, int row, int columnSpan = 1)
		{
			Grid.SetColumn(button, column);
			Grid.SetRow(button, row);
	
			if (columnSpan > 1)
				Grid.SetColumnSpan(button, columnSpan);
		}

		protected void AddButtonsToGrid()
		{
			lowerButtonsGrid.Children.Add(manualBtn);
			lowerButtonsGrid.Children.Add(autoBtn);
			lowerButtonsGrid.Children.Add(longBtn);
			lowerButtonsGrid.Children.Add(shortBtn);
			lowerButtonsGrid.Children.Add(quickLongBtn);
			lowerButtonsGrid.Children.Add(quickShortBtn);
			lowerButtonsGrid.Children.Add(moveTSBtn);
			lowerButtonsGrid.Children.Add(moveTS50PctBtn);
			lowerButtonsGrid.Children.Add(moveToBEBtn);
			lowerButtonsGrid.Children.Add(closeBtn);
			lowerButtonsGrid.Children.Add(panicBtn);
		}

		protected void OnButtonClick(object sender, RoutedEventArgs rea)
		{
			Button button = sender as Button;
	
			if (button == manualBtn)
			{
				isManualTradeEnabled = !isManualTradeEnabled;
				if (isManualTradeEnabled)
				{
					DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
					DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
				}
				else
				{
					DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
					DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
				}
				Print($"Strategy: {isManualTradeEnabled}");
				return;
			}

			if (button == autoBtn)
			{
				isAutoTradeEnabled = !isAutoTradeEnabled;
				if (isAutoTradeEnabled)
				{
					DecorateEnabledButtons(autoBtn, "\uD83D\uDD12 Auto On");
					DecorateDisabledButtons(manualBtn, "\uD83D\uDD13 Manual Off");
				}
				else
				{
					DecorateDisabledButtons(autoBtn, "\uD83D\uDD13 Auto Off");
					DecorateEnabledButtons(manualBtn, "\uD83D\uDD12 Manual On");
				}
				Print($"Strategy: {isAutoTradeEnabled}");
				return;
			}

			if (button == longBtn)
			{
				isLongEnabled = !isLongEnabled;
				if (isLongEnabled)
					DecorateEnabledButtons(longBtn, "LONG");
				else
					DecorateDisabledButtons(longBtn, "LONG Off");
				Print($"Long Enabled: {isLongEnabled}");
				return;
			}
	
			if (button == shortBtn)
			{
				isShortEnabled = !isShortEnabled;
				if (isShortEnabled)
					DecorateEnabledButtons(shortBtn, "SHORT");
				else
					DecorateDisabledButtons(shortBtn, "SHORT Off");
				Print($"Short Enabled: {isShortEnabled}");
				return;
			}

			if (button == quickLongBtn && isManualTradeEnabled && isLongEnabled)
			{
				QuickLong = !QuickLong;
				Print($"Buy Market On: {QuickLong}");
				quickLongBtnActive = true;
				
				ProcessLongEntry(true);
	
				QuickLong = false;
				return;
			}

			if (button == quickShortBtn && isManualTradeEnabled && isShortEnabled)
			{
				QuickShort = !QuickShort;
				Print($"Sell Market On: {QuickShort}");
				quickShortBtnActive = true;
				
				ProcessShortEntry(true);
	
				QuickShort = false;
				return;
			}
	
			#region Move Trailing Stop Button
			if (button == moveTSBtn)
			{
				if (!string.IsNullOrEmpty(atmStrategyId))
				{
					MoveTrailingStop(TickMove);
					ForceRefresh();
				}
				else
					Print("Not moving target, invalid state of atmStrategyId");
				return;
			}
			#endregion

			#region Move Trailing Stop 50% Button
			if (button == moveTS50PctBtn)
			{
				if (!string.IsNullOrEmpty(atmStrategyId))
				{
					MoveTrailingStopPercent(0.5); // 50%
					ForceRefresh();
				}
				else
					Print("Not moving target, invalid state of atmStrategyId");
				return;
			}
			#endregion

			#region Move To Breakeven Button

			if (button == moveToBEBtn)
			{
				if (!string.IsNullOrEmpty(atmStrategyId))
				{
					MoveToBreakeven();
					ForceRefresh();
				}
				else
					Print("Not moving target, invalid state of atmStrategyId");
				return;
			}
			#endregion

			if (button == closeBtn) { CloseAllPositions(); ForceRefresh(); return; }
			if (button == panicBtn) { FlattenAllPositions(); ForceRefresh(); return; }
		}
		
		#region Move Trailing Stop Methods
		private void MoveTrailingStop(int tickMove)
		{
			if (string.IsNullOrEmpty(atmStrategyId))
			{
				Print("No ATM strategy active to move the stop.");
				return;
			}
	
			MarketPosition marketPosition = GetAtmStrategyMarketPosition(atmStrategyId);
	
			string[,] stopTargetInfo = GetAtmStrategyStopTargetOrderStatus("", atmStrategyId);
	
			if (stopTargetInfo == null || stopTargetInfo.GetLength(0) == 0)
			{
				Print("Could not retrieve stop target order status. Check ATM strategy configuration.");
				return;
			}
	
			if (marketPosition == MarketPosition.Long)
			{
				if (double.TryParse(stopTargetInfo[0, 0], out double currentStopPrice))
				{
					double newStopPrice = currentStopPrice + tickMove * TickSize;
					AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
					Print($"Moving Long Stop to {newStopPrice}");
				}
				else
				{
					Print("Could not parse long stop price from ATM strategy. Check ATM Strategy configuration.");
				}
			}
			else if (marketPosition == MarketPosition.Short)
			{
				if (double.TryParse(stopTargetInfo[0, 0], out double currentStopPrice))
				{
					double newStopPrice = currentStopPrice - tickMove * TickSize;
					AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
					Print($"Moving Short Stop to {newStopPrice}");
				}
				else
				{
					Print("Could not parse short stop price from ATM strategy. Check ATM Strategy configuration.");
				}
			}
			else
			{
				Print("No open position to move the stop.");
			}
		}

		private void MoveTrailingStopPercent(double percent)
		{
			if (string.IsNullOrEmpty(atmStrategyId))
			{
				Print("No ATM strategy active to move the stop.");
				return;
			}
	
			MarketPosition marketPosition = GetAtmStrategyMarketPosition(atmStrategyId);
	
			string[,] stopTargetInfo = GetAtmStrategyStopTargetOrderStatus("", atmStrategyId);
	
			if (stopTargetInfo == null || stopTargetInfo.GetLength(0) < 2) // Check if both stop and target order info is available.
			{
				Print("Could not retrieve stop target order status or target is missing. Check ATM strategy configuration or ensure multiple targets");
				return;
			}
			double currentStopPrice = 0;
			double profitTarget = 0;
			if (marketPosition == MarketPosition.Long)
			{
				if (double.TryParse(stopTargetInfo[0, 0], out currentStopPrice) && double.TryParse(stopTargetInfo[1, 0], out profitTarget))
				{
					double distanceToTarget = profitTarget;
					double moveAmount = percent * (distanceToTarget - currentStopPrice);
					double newStopPrice = currentStopPrice + moveAmount;
					AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
					Print($"Moving Long Stop by {percent * 100}% to {newStopPrice}");
				}
				else
				{
					Print("Could not parse long stop or target price from ATM strategy. Check ATM Strategy configuration.");
				}
			}
			else if (marketPosition == MarketPosition.Short)
			{
	
				if (double.TryParse(stopTargetInfo[0, 0], out currentStopPrice) && double.TryParse(stopTargetInfo[1, 0], out profitTarget))
				{
					double distanceToTarget = profitTarget;
					double moveAmount = percent * (currentStopPrice - distanceToTarget);
					double newStopPrice = currentStopPrice - moveAmount;
					AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
					Print($"Moving Short Stop by {percent * 100}% to {newStopPrice}");
				}
				else
				{
					Print("Could not parse short stop or target price from ATM strategy. Check ATM Strategy configuration.");
				}
			}
			else
			{
				Print("No open position to move the stop.");
			}
		}


		private void MoveToBreakeven()
		{
			if (string.IsNullOrEmpty(atmStrategyId))
			{
				Print("No ATM strategy active to move to breakeven.");
				return;
			}
	
			MarketPosition marketPosition = GetAtmStrategyMarketPosition(atmStrategyId);
			double entryPrice = GetAtmStrategyPositionAveragePrice(atmStrategyId);
	
			string[,] stopTargetInfo = GetAtmStrategyStopTargetOrderStatus("", atmStrategyId);
	
			if (stopTargetInfo == null || stopTargetInfo.GetLength(0) == 0)
			{
				Print("Could not retrieve stop target order status. Check ATM strategy configuration.");
				return;
			}

			if (double.TryParse(stopTargetInfo[0, 0], out double currentStopPrice))
			{
				if (marketPosition == MarketPosition.Long)
				{
					if (Close[0] > entryPrice + BreakevenOffset * TickSize)
					{
						double newStopPrice = entryPrice + BreakevenOffset * TickSize;
						AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
						Print($"Moving Long Stop to Breakeven + {BreakevenOffset} ticks: {newStopPrice}");
					}
					else
					{
						Print("Long position not profitable enough to move to breakeven.");
					}
				}
				else if (marketPosition == MarketPosition.Short)
				{
					if (Close[0] < entryPrice - BreakevenOffset * TickSize)
					{
						double newStopPrice = entryPrice - BreakevenOffset * TickSize;
						AtmStrategyChangeStopTarget(0, newStopPrice, GetAtmStrategyUniqueId(), atmStrategyId);
						Print($"Moving Short Stop to Breakeven - {BreakevenOffset} ticks: {newStopPrice}");
					}
					else
					{
						Print("Short position not profitable enough to move to breakeven.");
					}
				}
				else
				{
					Print("No open position to move to breakeven.");
				}
			}
			else
			{
				Print("Could not parse current stop price. Check ATM Strategy configuration.");
			}
		}

		//Helper method to retrieve stop price
		private double GetAtmStrategyStopPrice(string strategyId)
		{
			double stopPrice = 0;
			string[,] stopTargetInfo = GetAtmStrategyStopTargetOrderStatus("", atmStrategyId);
			if (stopTargetInfo != null && stopTargetInfo.GetLength(0) > 0)
			{
				if (double.TryParse(stopTargetInfo[0, 0], out stopPrice))
					return stopPrice;
			}

			return 0;
		}

		//Helper method to retrieve profit target
		private double GetAtmStrategyProfitTarget(string strategyId)
		{
			double profitTarget = 0;
			string[,] stopTargetInfo = GetAtmStrategyStopTargetOrderStatus("", atmStrategyId);
			if (stopTargetInfo != null && stopTargetInfo.GetLength(0) > 1)
			{
				if (double.TryParse(stopTargetInfo[1, 0], out profitTarget))
					return profitTarget;
			}
			return 0;
		}

		#endregion
		
		#region Dispose
		protected void DisposeWPFControls()
		{
			if (chartWindow != null)
				chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

			manualBtn.Click -= OnButtonClick;
			autoBtn.Click -= OnButtonClick;
			longBtn.Click -= OnButtonClick;
			shortBtn.Click -= OnButtonClick;
			quickLongBtn.Click -= OnButtonClick;
			quickShortBtn.Click -= OnButtonClick;
			moveTSBtn.Click -= OnButtonClick;
			moveTS50PctBtn.Click -= OnButtonClick;
			moveToBEBtn.Click -= OnButtonClick;
			closeBtn.Click -= OnButtonClick;
			panicBtn.Click -= OnButtonClick;

			RemoveWPFControls();
		}
		#endregion

		#region Insert WPF
		public void InsertWPFControls()
		{
			if (panelActive)
				return;

			chartTraderGrid.RowDefinitions.Add(addedRow);
			Grid.SetRow(lowerButtonsGrid, (chartTraderGrid.RowDefinitions.Count - 1));
			chartTraderGrid.Children.Add(lowerButtonsGrid);

			panelActive = true;
		}
		#endregion

		#region Remove WPF
		protected void RemoveWPFControls()
		{
			if (!panelActive)
				return;

			if (chartTraderButtonsGrid != null || lowerButtonsGrid != null)
			{
				chartTraderGrid.Children.Remove(lowerButtonsGrid);
				chartTraderGrid.RowDefinitions.Remove(addedRow);
			}

			panelActive = false;
		}
		#endregion

		#region Tab Selected
		protected bool TabSelected()
		{
			foreach (TabItem tab in chartWindow.MainTabControl.Items)
				if ((tab.Content as ChartTab).ChartControl == ChartControl && tab == chartWindow.MainTabControl.SelectedItem)
					return true;

			return false;
		}

		protected void TabChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count <= 0)
				return;

			tabItem = e.AddedItems[0] as TabItem;
			if (tabItem == null)
				return;

			chartTab = tabItem.Content as ChartTab;
			if (chartTab == null)
				return;

			if (TabSelected())
				InsertWPFControls();
			else
				RemoveWPFControls();
		}
		#endregion

		#region Close All Positions
		protected void CloseAllPositions()
		{
			if (!string.IsNullOrEmpty(atmStrategyId))
			{
				Print("Closing open position for ATM strategy.");
				AtmStrategyClose(atmStrategyId);
			}
			else
			{
				Print("No active ATM strategy to close.");
			}
		}

		protected void FlattenAllPositions()
		{
			Position openPosition = Position;
			Account myAccount = Account.All.FirstOrDefault(a => a.Name == chartTraderAccount.DisplayName);

			if (myAccount == null)
				throw new Exception("Account not found.");

			if (openPosition != null && openPosition.MarketPosition != MarketPosition.Flat)
			{
				List<Instrument> instrumentNames = new List<Instrument>();
				foreach (Position position in chartTraderAccount.Positions)
				{
					Instrument instrument = position.Instrument;
					if (!instrumentNames.Contains(instrument))
						instrumentNames.Add(instrument);
				}
				chartTraderAccount.Flatten((ICollection<Instrument>)instrumentNames);
			}
		}
		#endregion

		protected bool checkTimers()
		{
			if ((Times[0][0].TimeOfDay >= Start.TimeOfDay) && (Times[0][0].TimeOfDay < End.TimeOfDay)
					|| (isEnableTime2 && Times[0][0].TimeOfDay >= Start2.TimeOfDay && Times[0][0].TimeOfDay <= End2.TimeOfDay)
					|| (isEnableTime3 && Times[0][0].TimeOfDay >= Start3.TimeOfDay && Times[0][0].TimeOfDay <= End3.TimeOfDay)
					|| (isEnableTime4 && Times[0][0].TimeOfDay >= Start4.TimeOfDay && Times[0][0].TimeOfDay <= End4.TimeOfDay)
					|| (isEnableTime5 && Times[0][0].TimeOfDay >= Start5.TimeOfDay && Times[0][0].TimeOfDay <= End5.TimeOfDay)
					|| (isEnableTime6 && Times[0][0].TimeOfDay >= Start6.TimeOfDay && Times[0][0].TimeOfDay <= End6.TimeOfDay)
			)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		
		private string GetActiveTimer()
		{
		//	check active timer	
		    TimeSpan currentTime = Times[0][0].TimeOfDay;
		
		    if ((Times[0][0].TimeOfDay >= Start.TimeOfDay) && (Times[0][0].TimeOfDay < End.TimeOfDay))
		    {
		        return $"{Start:HH\\:mm} - {End:HH\\:mm}";
		    }
		    else if (Time2 && Times[0][0].TimeOfDay >= Start2.TimeOfDay && Times[0][0].TimeOfDay <= End2.TimeOfDay)
		    {
		        return $"{Start2:HH\\:mm} - {End2:HH\\:mm}";
		    }
		    else if (Time3 && Times[0][0].TimeOfDay >= Start3.TimeOfDay && Times[0][0].TimeOfDay <= End3.TimeOfDay)
		    {
		        return $"{Start3:HH\\:mm} - {End3:HH\\:mm}";
		    }
		    else if (Time4 && Times[0][0].TimeOfDay >= Start4.TimeOfDay && Times[0][0].TimeOfDay <= End4.TimeOfDay)
		    {
		        return $"{Start4:HH\\:mm} - {End4:HH\\:mm}";
		    }
		    else if (Time5 && Times[0][0].TimeOfDay >= Start5.TimeOfDay && Times[0][0].TimeOfDay <= End5.TimeOfDay)
		    {
		        return $"{Start5:HH\\:mm} - {End5:HH\\:mm}";
		    }
		    else if (Time6 && Times[0][0].TimeOfDay >= Start6.TimeOfDay && Times[0][0].TimeOfDay <= End6.TimeOfDay)
		    {
		        return $"{Start6:HH\\:mm} - {End6:HH\\:mm}";
		    }
		
		    return "No active timer";
		}

		protected void ShowPNLStatus()
		{
			string textLine1 = GetActiveTimer();
			string textLine3 = $"{counterLong} / {longPerDirection} | " + (tradesPerDirection ? "On" : "Off");
			string textLine5 = $"{counterShort} / {shortPerDirection} | " + (tradesPerDirection ? "On" : "Off");

			string statusPnlText = $"Active Timer:\t{textLine1}\nLong Per Direction:\t{textLine3}\nShort Per Direction:\t{textLine5}";
			SimpleFont font = new SimpleFont("Arial", 16);

			Draw.TextFixed(this, "statusPnl", statusPnlText, PositionPnl, colorPnl, font, Brushes.Transparent, Brushes.Transparent, 0);
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			if (showDailyPnl) DrawStrategyPnl(chartControl);
		}

		protected void DrawStrategyPnl(ChartControl chartControl)
		{	
			double realizedPnL = Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
			double unrealizedProfitLoss = Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
			cumProfit = syncPnl ? historicalTimeTrades + realizedPnL : realizedPnL + dif;
			double totalPnL = cumProfit + unrealizedProfitLoss;
//			string total = totalPnL.ToString("N0");

			// Track the Highest Profit Achieved
			if (totalPnL > maxProfit)
			{
				maxProfit = totalPnL;
			}
			
			string direction = uptrend? "Up" : downtrend ? "Down" : "Neutral";
			string entry = longSignal && uptrend ? "Long" : shortSignal && downtrend? "Short" : "No Trade";
			string realTimeTradeText = $"{Account.Name} | {Account.Connection.Options.Name}\nRealized PnL:\t${realizedPnL:F2}\nUnrealized PnL:\t${unrealizedProfitLoss:F2}\nTotal PnL:\t${totalPnL:F2}\nMax Profit:\t${maxProfit:F2}\nTrend Direction:\t{direction}\nEntry:\t{entry}";
			SimpleFont font = new SimpleFont("Arial", 18);

			colorDailyProfitLoss = totalPnL == 0 ? Brushes.Cyan: totalPnL > 0 ? Brushes.Lime : Brushes.Red;
			
			Draw.TextFixed(this, "realTimeTradeText", realTimeTradeText, PositionDailyPNL, colorDailyProfitLoss, font, Brushes.Transparent, Brushes.Transparent, 0);
		}

		#region KillSwitch
		protected void KillSwitch()
		{
			totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
			dailyPnL = totalPnL + Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);

			// Determine all relevant order labels
			List<string> longOrderLabels = new List<string> { LongEntryLabel }; // Base Labels for Longs
			List<string> shortOrderLabels = new List<string> { ShortEntryLabel }; // Base Labels for Shorts

		    // Common Action: Close all Positions and Disable the Strategy
		    Action closeAllPositionsAndDisableStrategy = () =>
		    {
		        foreach (string label in longOrderLabels)
		        {
		            ExitLong(Convert.ToInt32(Position.Quantity), @"LongExitKillSwitch", label);
		        }
		
		        foreach (string label in shortOrderLabels)
		        {
		            ExitShort(Convert.ToInt32(Position.Quantity), @"ShortExitKillSwitch", label);
		        }
		
		        isManualTradeEnabled = false;
		        Print("Kill Switch Activated: Strategy Disabled!");
		    };

		    if (dailyLossProfit && enableTrailingDD) //Check both the enableDailyLossLimit and enableTrailingDD
		    {
		        if (totalPnL >= StartTrailingDD && (maxProfit - totalPnL) >= TrailingDrawdown && Position.Quantity > 0)
		        {
		            closeAllPositionsAndDisableStrategy();
		            trailingDrawdownReached = true;
					Print("Max drawdown has been reached!  No more trading for the day.");
		        }
		    }

			if (dailyLossProfit && enableTrailingDD) //Check both the enableDailyLossLimit and enableTrailingDD
			{
				if (totalPnL >= StartTrailingDD && (maxProfit - totalPnL) >= TrailingDrawdown)
				{
					closeAllPositionsAndDisableStrategy();
					trailingDrawdownReached = true;
				}
			}

			if (dailyPnL <= -DailyLossLimit)
			{
				closeAllPositionsAndDisableStrategy();
			}

			if (dailyPnL >= DailyProfitLimit)
			{
				closeAllPositionsAndDisableStrategy();
			}

			if (!isManualTradeEnabled)
				Print("Kill Switch Activated!");
		}
		#endregion

		#region Custom Property Manipulation

		public void ModifyProperties(PropertyDescriptorCollection col)
		{
			if (!TradesPerDirection)
			{
				col.Remove(col.Find(nameof(longPerDirection), true));
				col.Remove(col.Find(nameof(shortPerDirection), true));
			}
			if (!isEnableTime2)
			{
				col.Remove(col.Find(nameof(Start2), true));
				col.Remove(col.Find(nameof(End2), true));
			}
			if (!isEnableTime3)
			{
				col.Remove(col.Find(nameof(Start3), true));
				col.Remove(col.Find(nameof(End3), true));
			}
			if (!isEnableTime4)
			{
				col.Remove(col.Find(nameof(Start4), true));
				col.Remove(col.Find(nameof(End4), true));
			}
			if (!isEnableTime5)
			{
				col.Remove(col.Find(nameof(Start5), true));
				col.Remove(col.Find(nameof(End5), true));
			}
			if (!isEnableTime6)
			{
				col.Remove(col.Find(nameof(Start6), true));
				col.Remove(col.Find(nameof(End6), true));
			}
		}
		#endregion

		#region ICustomTypeDescriptor Members

		public AttributeCollection GetAttributes() { return TypeDescriptor.GetAttributes(GetType()); }
		public string GetClassName() { return TypeDescriptor.GetClassName(GetType()); }
		public string GetComponentName() { return TypeDescriptor.GetComponentName(GetType()); }
		public TypeConverter GetConverter() { return TypeDescriptor.GetConverter(GetType()); }
		public EventDescriptor GetDefaultEvent() { return TypeDescriptor.GetDefaultEvent(GetType()); }
		public PropertyDescriptor GetDefaultProperty() { return TypeDescriptor.GetDefaultProperty(GetType()); }
		public object GetEditor(Type editorBaseType) { return TypeDescriptor.GetEditor(GetType(), editorBaseType); }
		public EventDescriptorCollection GetEvents(Attribute[] attributes) { return TypeDescriptor.GetEvents(GetType(), attributes); }
		public EventDescriptorCollection GetEvents() { return TypeDescriptor.GetEvents(GetType()); }
		public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptorCollection orig = TypeDescriptor.GetProperties(GetType(), attributes);
			PropertyDescriptor[] arr = new PropertyDescriptor[orig.Count];
			orig.CopyTo(arr, 0);
			PropertyDescriptorCollection col = new PropertyDescriptorCollection(arr);

			ModifyProperties(col);

			return col;
		}
		public PropertyDescriptorCollection GetProperties() { return TypeDescriptor.GetProperties(GetType()); }
		public object GetPropertyOwner(PropertyDescriptor pd) { return this; }

		#endregion
		
		#region Properties - Release Notes
	
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name = "Base Algo Version", Order = 1, GroupName = "01. Release Notes")]
		public string BaseAlgoVersion { get; set; }
	
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name = "Author", Order = 2, GroupName = "01. Release Notes")]
		public string Author { get; set; }
	
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name = "Strategy Name", Order = 3, GroupName = "01. Release Notes")]
		public string StrategyName { get; set; }
	
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name = "Version", Order = 4, GroupName = "01. Release Notes")]
		public string Version { get; set; }
	
		[ReadOnly(true)]
		[NinjaScriptProperty]
		[Display(Name = "Credits", Order = 5, GroupName = "01. Release Notes")]
		public string Credits { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Chart Type", Order = 6, GroupName = "01. Release Notes")]
		public string ChartType { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "ATM Strategy Template", Order = 7, GroupName = "01. Release Notes")]
		public string ATMStrategyTemplate { get; set; }
	
		#endregion

		#region Properties - Order Settings
	
		[NinjaScriptProperty]
		[Display(Name = "Order Type", Order = 1, GroupName = "02. Order Settings")]
		public OrderType OrderType { get; set; }
	
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Breakeven Offset", Order = 2, GroupName = "02. Order Settings")]
		public int BreakevenOffset { get; set; }
	
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Tick Move", Order = 3, GroupName = "02. Order Settings")]
		public int TickMove { get; set; }
	
		#endregion
	
		#region Properties - Profit/Loss Limit
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Daily Loss / Profit ", Description = "Enable / Disable Daily Loss & Profit control", Order = 1, GroupName = "05. Profit/Loss Limit	")]
		[RefreshProperties(RefreshProperties.All)]
		public bool dailyLossProfit
		{ get; set; }
	
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Daily Profit Limit ($)", Description = "No positive or negative sign, just integer", Order = 2, GroupName = "05. Profit/Loss Limit	")]
		public double DailyProfitLimit { get; set; }
	
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Daily Loss Limit ($)", Description = "No positive or negative sign, just integer", Order = 3, GroupName = "05. Profit/Loss Limit	")]
		public double DailyLossLimit { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Trailing Drawdown", Description = "Enable / Disable trailing drawdown", Order = 4, GroupName = "05. Profit/Loss Limit	")]
		[RefreshProperties(RefreshProperties.All)]
		public bool enableTrailingDD { get; set; }
	
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Trailing Drawdown ($)", Description = "No positive or negative sign, just integer", Order = 5, GroupName = "05. Profit/Loss Limit	")]
		public double TrailingDrawdown { get; set; }
	
		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Start Trailing Drawdown ($)", Description = "No positive or negative sign, just integer", Order = 6, GroupName = "05. Profit/Loss Limit	")]
		public double StartTrailingDD { get; set; }
	
		#endregion
	
		#region Properties - Trades Per Direction
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Trades Per Direction", Description = "Switch off Historical Trades to use this option.", Order = 1, GroupName = "06. Trades Per Direction")]
		[RefreshProperties(RefreshProperties.All)]
		public bool TradesPerDirection
		{
			get { return tradesPerDirection; }
			set { tradesPerDirection = (value); }
		}
	
		[NinjaScriptProperty]
		[Display(Name = "Long Per Direction", Description = "Number of long in a row", Order = 2, GroupName = "06. Trades Per Direction")]
		public int longPerDirection { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Short Per Direction", Description = "Number of short in a row", Order = 3, GroupName = "06. Trades Per Direction")]
		public int shortPerDirection { get; set; }
	
		#endregion
	
		#region Properties - Strategy Settings
	
		[NinjaScriptProperty]
		[Display(Name = "Enable VMA", Order = 1, GroupName = "08. Strategy Settings")]
		public bool enableVMA { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show VMA", Order = 2, GroupName = "08. Strategy Settings")]
		public bool showVMA { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable HMA Hooks", Order = 3, GroupName = "08. Strategy Settings")]
		public bool enableHmaHooks { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show HMA Hooks", Order = 4, GroupName = "08. Strategy Settings")]
		public bool showHmaHooks { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "HMA Period", Order = 5, GroupName = "08. Strategy Settings")]
		public int HmaPeriod { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Regression Channel", Order = 6, GroupName = "08. Strategy Settings")]
		public bool enableRegChan1 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Inner Regression Channel", Order = 7, GroupName = "08. Strategy Settings")]
		public bool enableRegChan2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Outer Regression Channel", Order = 8, GroupName = "08. Strategy Settings")]
		public bool showRegChan1 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Inner Regression Channel", Order = 9, GroupName = "08. Strategy Settings")]
		public bool showRegChan2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show High and Low Lines", Order = 10, GroupName = "08. Strategy Settings")]
		public bool showRegChanHiLo { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Regression Channel Period", Order = 11, GroupName="08. Strategy Settings")]
		public int RegChanPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Outer Regression Channel Width", Order = 12, GroupName="08. Strategy Settings")]
		public double RegChanWidth
		{ get; set; }
			
		[NinjaScriptProperty]
		[Display(Name = "Inner Regression Channel Width", Order = 13, GroupName = "08. Strategy Settings")]
		public double RegChanWidth2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Momentum", Order = 14, GroupName = "08. Strategy Settings")]
		public bool enableMomo { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Show Momentum", Order = 15, GroupName = "08. Strategy Settings")]
		public bool showMomo { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Momentum Up", Order = 16, GroupName = "08. Strategy Settings")]
		public int MomoUp { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Momentum Down", Order = 17, GroupName = "08. Strategy Settings")]
		public int MomoDown { get; set; }
	
		[NinjaScriptProperty]
        [Display(Name = "Enable Willy", Order = 18, GroupName = "08. Strategy Settings")]
        public bool enableWilly { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Show Willy", Order = 19, GroupName = "08. Strategy Settings")]
        public bool showWilly { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Willy Period", Order = 20, GroupName="08. Strategy Settings")]
		public int wrPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Willy Up", Order = 21, GroupName="08. Strategy Settings")]
		public int wrUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Willy Down", Order = 22, GroupName="08. Strategy Settings")]
		public int wrDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable SuperRex", Order = 23, GroupName = "08. Strategy Settings")]
        public bool enableSuperRex { get; set; }
        
		[NinjaScriptProperty]
		[Display(Name="CMO Up", Order = 24, GroupName="08. Strategy Settings")]
		public int CmoUp
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="CMO Down", Order = 25, GroupName="08. Strategy Settings")]
		public int CmoDown
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show CMO", Order = 26, GroupName = "08. Strategy Settings")]
        public bool showCMO { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable Chaser", Order = 27, GroupName = "08. Strategy Settings")]
        public bool enableLinReg { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Show Linear Regression Curve", Order = 28, GroupName = "08. Strategy Settings")]
        public bool showLinReg { get; set; }
        
		[NinjaScriptProperty]
		[Display(Name="Linear Regression Period", Order = 29, GroupName="08. Strategy Settings")]
		public int LinRegPeriod
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable MagicTrendy", Order = 30, GroupName = "08. Strategy Settings")]
        public bool enableTrendMagic { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show MagicTrendy", Order = 31, GroupName = "08. Strategy Settings")]
        public bool showTrendMagic { get; set; }
		
        [NinjaScriptProperty]
		[Display(Name="TrendMagic ATR Multiplier", Order = 32, GroupName="08. Strategy Settings")]
		public double atrMult
		{ get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Enable Trendy", Order = 33, GroupName = "08. Strategy Settings")]
        public bool enableTrendy { get; set; }
        
		[NinjaScriptProperty]
        [Display(Name = "Show T3 Trend Filter", Order = 34, GroupName = "08. Strategy Settings")]
        public bool showTrendy { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Factor", Order = 35, GroupName = "08. Strategy Settings")]
        public double Factor { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Period 1", Order = 36, GroupName = "08. Strategy Settings")]
        public int Period1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Period 2", Order = 37, GroupName = "08. Strategy Settings")]
        public int Period2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Period 3", Order = 38, GroupName = "08. Strategy Settings")]
        public int Period3 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Period 4", Order = 39, GroupName = "08. Strategy Settings")]
        public int Period4 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Period 5", Order = 40, GroupName = "08. Strategy Settings")]
        public int Period5 { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "Enable ADX", Order = 41, GroupName = "08. Strategy Settings")]
        public bool enableADX { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Show ADX", Order = 42, GroupName = "08. Strategy Settings")]
		public bool showAdx { get; set; }
	
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "ADX Period", Order = 43, GroupName = "08. Strategy Settings")]
		public int adxPeriod { get; set; }
	
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Threshold", Order = 44, GroupName = "08. Strategy Settings")]
        public int adxThreshold { get; set; }
		
		#endregion

		#region Properties - Timeframes
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Trades", Order = 1, GroupName = "10. Timeframes")]
		public DateTime Start { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Trades", Order = 2, GroupName = "10. Timeframes")]
		public DateTime End { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 2", Description = "Enable 2 times.", Order = 3, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time2
		{
			get { return isEnableTime2; }
			set { isEnableTime2 = (value); }
		}
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Time 2", Order = 4, GroupName = "10. Timeframes")]
		public DateTime Start2 { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Time 2", Order = 5, GroupName = "10. Timeframes")]
		public DateTime End2 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 3", Description = "Enable 3 times.", Order = 6, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time3
		{
			get { return isEnableTime3; }
			set { isEnableTime3 = (value); }
		}
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Time 3", Order = 7, GroupName = "10. Timeframes")]
		public DateTime Start3 { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Time 3", Order = 8, GroupName = "10. Timeframes")]
		public DateTime End3 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 4", Description = "Enable 4 times.", Order = 9, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time4
		{
			get { return isEnableTime4; }
			set { isEnableTime4 = (value); }
		}
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Time 4", Order = 10, GroupName = "10. Timeframes")]
		public DateTime Start4 { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Time 4", Order = 11, GroupName = "10. Timeframes")]
		public DateTime End4 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 5", Description = "Enable 5 times.", Order = 12, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time5
		{
			get { return isEnableTime5; }
			set { isEnableTime5 = (value); }
		}
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Time 5", Order = 13, GroupName = "10. Timeframes")]
		public DateTime Start5 { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Time 5", Order = 14, GroupName = "10. Timeframes")]
		public DateTime End5 { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Enable Time 6", Description = "Enable 6 times.", Order = 15, GroupName = "10. Timeframes")]
		[RefreshProperties(RefreshProperties.All)]
		public bool Time6
		{
			get { return isEnableTime6; }
			set { isEnableTime6 = (value); }
		}
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "Start Time 6", Order = 16, GroupName = "10. Timeframes")]
		public DateTime Start6 { get; set; }
	
		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
		[Display(Name = "End Time 6", Order = 17, GroupName = "10. Timeframes")]
		public DateTime End6 { get; set; }
	
		#endregion
	
		#region Properties - Status Panel
	
		[NinjaScriptProperty]
		[Display(Name = "Show Daily PnL", Order = 1, GroupName = "11. Status Panel")]
		public bool showDailyPnl { get; set; }
	
		[XmlIgnore()]
		[Display(Name = "Daily PnL Color", Order = 2, GroupName = "11. Status Panel")]
		public Brush colorDailyProfitLoss { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "Daily PnL Position", Description = "Daily PNL Alert Position", Order = 3, GroupName = "11. Status Panel")]
		public TextPosition PositionDailyPNL { get; set; }
	
		// Serialize our Color object
		[Browsable(false)]
		public string colorDailyProfitLossSerialize
		{
			get { return Serialize.BrushToString(colorDailyProfitLoss); }
			set { colorDailyProfitLoss = Serialize.StringToBrush(value); }
		}
	
		[NinjaScriptProperty]
		[Display(Name = "Show STATUS PANEL", Order = 4, GroupName = "11. Status Panel")]
		public bool showPnl { get; set; }
	
		[XmlIgnore()]
		[Display(Name = "STATUS PANEL Color", Order = 5, GroupName = "11. Status Panel")]
		public Brush colorPnl { get; set; }
	
		[NinjaScriptProperty]
		[Display(Name = "STATUS PANEL Position", Description = "Status PNL Position", Order = 6, GroupName = "11. Status Panel")]
		public TextPosition PositionPnl { get; set; }
	
		// Serialize our Color object
		[Browsable(false)]
		public string colorPnlSerialize
		{
			get { return Serialize.BrushToString(colorPnl); }
   			set { colorPnl = Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[Display(Name="Show Historical Trades", Description = "Show Historical Teorical Trades", Order= 7, GroupName="11. Status Panel")]
		public bool ShowHistorical
		{ get; set; }
		
        #endregion
		
		#region 12. WebHook

		[NinjaScriptProperty]
		[Display(Name="Activate Discord webhooks", Description="Activate One or more Discord webhooks", GroupName="11. Webhook", Order = 0)]
		public bool useWebHook { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Discord webhooks", Description="One or more Discord webhooks, separated by comma.", GroupName="11. Webhook", Order = 2)]
		public string DiscordWebhooks
		{ get; set; }
		
		#endregion	
		
		#endregion
    }
}
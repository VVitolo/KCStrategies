#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
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
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
  abstract public class KCAlgoBase : Strategy, ICustomTypeDescriptor
  {
    #region Variables

    private DateTime lastEntryTime = DateTime.MinValue;  // Initialize to MinValue
    private int lastStopUpdateBar = -1; // Track the last bar where we updated stops
    private double lastAdjustedStopPrice = 0;  // Track the last adjusted stop price
    [XmlIgnore]
    protected bool profitTargetsSet = false;  // Track if profit targets are already set

    // File logging
    private string LogFilePath;  // Changed to instance variable to allow dynamic initialization
    private static readonly object LogLock = new object();
    private bool loggerInitialized = false;

    protected bool marketIsChoppy;
    protected bool autoDisabledByChop; // Tracks if Auto was turned off by the system due to chop

    // Indicator Variables
    protected BlueZ.BlueZHMAHooks hullMAHooks;
    protected bool hmaUp;
    protected bool hmaDown;

    protected BuySellPressure BuySellPressure1;
    protected bool buyPressureUp;
    protected bool sellPressureUp;
    protected Series<double> buyPressure;
    protected Series<double> sellPressure;

    protected RegressionChannel RegressionChannel1, RegressionChannel2;
    protected RegressionChannelHighLow RegressionChannelHighLow1;
    protected bool regChanUp;
    protected bool regChanDown;

    protected VMA VMA1;
    protected bool volMaUp;
    protected bool volMaDown;

    protected Momentum Momentum1;
    protected double currentMomentum;
    protected bool momoUp;
    protected bool momoDown;

    protected ADX ADX1;
    protected double currentAdx;
    protected bool adxUp;

    protected ATR ATR1;
    protected double currentAtr;
    protected bool atrUp;

    protected bool aboveEMAHigh;
    protected bool belowEMALow;

    protected bool uptrend;
    protected bool downtrend;

    protected bool priceUp;
    protected bool priceDown;

    [XmlIgnore]
    public bool isLong;
    [XmlIgnore]
    public bool isShort;
    [XmlIgnore]
    public bool isFlat;
    [XmlIgnore]
    public bool exitLong;
    [XmlIgnore]
    public bool exitShort;
    [XmlIgnore]
    public bool longSignal;
    [XmlIgnore]
    public bool shortSignal;

    protected double lastStopLevel = 0;  // Tracks the last stop level


    // Progress tracking

    [XmlIgnore]
    protected int trailStop;
    [XmlIgnore]
    protected bool _beRealized;
    [XmlIgnore]
    protected bool enableFixedStopLoss;
    [XmlIgnore]
    protected bool trailingDrawdownReached;


    [XmlIgnore]
    protected double entryPrice;

    [XmlIgnore]
    protected bool additionalContractExists;


    protected bool tradesPerDirection;
    [Browsable(false)]
    public int counterLong;
    [Browsable(false)]
    public int counterShort;

    protected bool isEnableTime2;
    protected bool isEnableTime3;
    protected bool isEnableTime4;
    protected bool isEnableTime5;
    protected bool isEnableTime6;

    [Browsable(false)]
    public bool isManualEnabled { get; set; }
    [Browsable(false)]
    public bool isAutoEnabled { get; set; }
    [Browsable(false)]
    public bool isLongEnabled { get; set; }
    [Browsable(false)]
    public bool isShortEnabled { get; set; }

    //		Chart Trader Buttons
    private System.Windows.Controls.RowDefinition addedRow;
    private Gui.Chart.ChartTab chartTab;
    private Gui.Chart.Chart chartWindow;
    private System.Windows.Controls.Grid chartTraderGrid, chartTraderButtonsGrid, lowerButtonsGrid;

    //		New Toggle Buttons
    private System.Windows.Controls.Button manualBtn, autoBtn, longBtn, shortBtn;
    private System.Windows.Controls.Button BEBtn, TSBtn, moveTSBtn, moveToBEBtn;
    private System.Windows.Controls.Button moveTS50PctBtn, closeBtn, panicBtn;
    private bool panelActive;
    private System.Windows.Controls.TabItem tabItem;



    //		Status Panel
    private string textLine0;
    private string textLine1;
    private string textLine2;
    private string textLine3;
    private string textLine4;
    private string textLine5;
    private string textLine6;
    private string textLine7;

    //		PnL
    [XmlIgnore]
    protected double totalPnL;
    [XmlIgnore]
    protected double cumPnL;
    [XmlIgnore]
    protected double dailyPnL;


    protected bool beSetAuto;
    protected bool showctrlBESetAuto;
    protected bool atrTrailSetAuto;
    protected bool showAtrTrailSetAuto;
    protected bool enableTrail;
    protected bool showTrailOptions;
    public bool tickTrail;

    protected TrailStopTypeKC trailStopType;
    protected bool showAtrTrailOptions;

    protected bool enableFixedProfitTarget;

    // Error Handling
    private readonly object orderLock = new object(); // Critical for thread safety
    private Dictionary<string, Order> activeOrders = new Dictionary<string, Order>(); // Track active orders with labels.
    private DateTime lastOrderActionTime = DateTime.MinValue;
    private readonly TimeSpan minOrderActionInterval = TimeSpan.FromSeconds(1); // Prevent rapid order submissions.
    protected bool orderErrorOccurred = false; // Flag to halt trading after an order error.

    // Rogue Order Detection
    private DateTime lastAccountReconciliationTime = DateTime.MinValue;
    private readonly TimeSpan accountReconciliationInterval = TimeSpan.FromMinutes(5); // Check for rogue orders every 5 minutes

    // Trailing Drawdown variables
    [XmlIgnore]
    protected double maxProfit;  // Stores the highest profit achieved
    private double maxDrawdown = 0;  // Stores the worst drawdown seen
    private double highestNetProfit = 0;  // Track highest net profit for drawdown calc


    // Daily tracking variables
    private double dailyHighestProfit = 0;
    private double dailyWorstDrawdown = 0;
    private double dailyStartEquity = 0;
    private double previousDayPnL = 0;
    #endregion

    #region Order Label Constants (Highly Recommended)

    // Define your order labels as constants.  This prevents typos and ensures consistency.
    private const string LE = "LE";
    private const string LE2 = "LE2";
    private const string LE3 = "LE3";
    private const string LE4 = "LE4";
    private const string SE = "SE";
    private const string SE2 = "SE2";
    private const string SE3 = "SE3";
    private const string SE4 = "SE4";
    private const string ManualClose1 = "Manual Close 1"; // Label for the manual close action
                                                          // Add constants for other order labels as needed (e.g., LE2, SE2, "TrailingStop")

    #endregion

    #region Constants

    private const string ManualButton = "ManualBtn";
    private const string AutoButton = "AutoBtn";
    private const string LongButton = "LongBtn";
    private const string ShortButton = "ShortBtn";

    private const string BEButton = "BEBtn";
    private const string TSButton = "TSBtn";
    private const string MoveTSButton = "MoveTSBtn";
    private const string MoveTS50PctButton = "MoveTS50PctBtn";
    private const string MoveToBeButton = "MoveToBeBtn";
    private const string CloseButton = "CloseBtn";
    private const string PanicButton = "PanicBtn";


    #endregion



    public override string DisplayName { get { return Name; } }

    #region OnStateChange
    protected override void OnStateChange()
    {
      if (State == State.SetDefaults)
      {
        LogMessage($"OnStateChange(): Entering SetDefaults state for {Name}", "STATE");
        Description = @"Base Strategy with OEB v.5.0.2 TradeSaber(Dre). and ArchReactor for KC (Khanh Nguyen), Johny";
        Name = "KCAlgoBase";
        BaseAlgoVersion = "KCAlgoBase v6.0";
        Author = "Johny,indiVGA, Khanh Nguyen, Oshi, based on ArchReactor";
        Version = "Version 6.0 Apr. 2025";
        Credits = "";
        StrategyName = "";
        ChartType = "Orenko 34-40-40";

        // Initialize PnL tracking variables
        maxProfit = 0;
        highestNetProfit = 0;
        totalPnL = 0;
        dailyPnL = 0;
        cumPnL = 0;

        // Set default value for ShowStatusPanels
        ShowStatusPanels = true;

        // Set default value for EnableLogging
        EnableLogging = true;

        InitializeLogger();
        LogMessage($"OnStateChange(): Strategy defaults set: {Name} {Version}", "STATE");

        EntriesPerDirection = 10;         // This value should limit the number of contracts that the strategy can open per direction.
                                          // It has nothing to do with the parameter defining the entries per direction that we define in the strategy and are controlled by code.
        Calculate = Calculate.OnEachTick;
        EntryHandling = EntryHandling.AllEntries;
        IsExitOnSessionCloseStrategy = true;
        ExitOnSessionCloseSeconds = 30;
        IsFillLimitOnTouch = false;
        MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
        OrderFillResolution = OrderFillResolution.Standard;
        Slippage = 0;
        StartBehavior = StartBehavior.WaitUntilFlat;
        TimeInForce = TimeInForce.Gtc;
        TraceOrders = false;
        RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
        StopTargetHandling = StopTargetHandling.PerEntryExecution;
        BarsRequiredToTrade = 120;
        RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose; // important to manage errors on rogue orders
        IsInstantiatedOnEachOptimizationIteration = false;

        // Default Parameters
        isAutoEnabled = true;
        isManualEnabled = false;
        isLongEnabled = true;
        isShortEnabled = true;


        OrderType = OrderType.Limit;

        // Choppiness Defaults
        SlopeLookBack = 4;
        FlatSlopeFactor = 0.125;
        ChopAdxThreshold = 25;
        EnableChoppinessDetection = true;
        marketIsChoppy = false;
        autoDisabledByChop = false;
        enableBackgroundSignal = true;

        enableBuySellPressure = true;
        showBuySellPressure = false;

        HmaPeriod = 16;
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

        MomoUp = 1;
        MomoDown = -1;
        enableMomo = true;
        showMomo = false;

        adxPeriod = 7;
        AdxThreshold = 25;
        adxThreshold2 = 50;
        adxExitThreshold = 45;
        enableADX = true;
        showAdx = false;

        emaLength = 110;
        enableEMAFilter = false;
        showEMA = false;

        AtrPeriod = 14;
        atrThreshold = 1.5;
        enableVolatility = true;


        enableExit = false;

        LimitOffset = 4;
        TickMove = 4;


        EnableFixedProfitTarget = true; // Default



        Contracts = 1;
        Contracts2 = 1;
        Contracts3 = 1;
        Contracts4 = 1;

        InitialStop = 97;

        ProfitTarget = 40;
        ProfitTarget2 = 44;
        ProfitTarget3 = 48;
        ProfitTarget4 = 52;

        EnableProfitTarget2 = true;
        EnableProfitTarget3 = true;
        EnableProfitTarget4 = true;

        //	Set BE Stop
        BESetAuto = true;
        beSetAuto = true;
        showctrlBESetAuto = true;
        BE_Trigger = 32;
        BE_Offset = 4;
        _beRealized = false;

        //	Trailing Stops
        enableTrail = true;
        tickTrail = true;
        showTrailOptions = true;
        trailStopType = TrailStopTypeKC.Tick_Trail;

        //	ATR Trail
        atrTrailSetAuto = false;
        showAtrTrailSetAuto = false;
        showAtrTrailOptions = false;
        enableAtrProfitTarget = false;
        atrMultiplier = 1.5;
        RiskRewardRatio = 0.75;




        tradesPerDirection = false;
        longPerDirection = 5;
        shortPerDirection = 5;
        iBarsSinceExit = 0;


        counterLong = 0;
        counterShort = 0;

        Start = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
        End = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
        Start2 = DateTime.Parse("11:30", System.Globalization.CultureInfo.InvariantCulture);
        End2 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
        Start3 = DateTime.Parse("15:00", System.Globalization.CultureInfo.InvariantCulture);
        End3 = DateTime.Parse("18:00", System.Globalization.CultureInfo.InvariantCulture);
        Start4 = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
        End4 = DateTime.Parse("03:30", System.Globalization.CultureInfo.InvariantCulture);
        Start5 = DateTime.Parse("06:30", System.Globalization.CultureInfo.InvariantCulture);
        End5 = DateTime.Parse("13:00", System.Globalization.CultureInfo.InvariantCulture);
        Start6 = DateTime.Parse("00:00", System.Globalization.CultureInfo.InvariantCulture);
        End6 = DateTime.Parse("23:59", System.Globalization.CultureInfo.InvariantCulture);

        // Panel Status
        showDailyPnl = true;
        PositionDailyPNL = TextPosition.TopLeft;
        colorDailyProfitLoss = Brushes.Cyan; // Default value

        showPnl = false;
        PositionPnl = TextPosition.BottomLeft;
        colorPnl = Brushes.Yellow; // Default value



        // PnL Daily Limits
        dailyLossProfit = true;
        DailyProfitLimit = 100000;
        DailyLossLimit = 2000;
        TrailingDrawdown = 1000;
        StartTrailingDD = 3000;
        maxProfit = double.MinValue;  // double.MinValue guarantees that any totalPnL will trigger it to set the variable
        enableTrailingDrawdown = true;

        ShowHistorical = true;

        TradeDelaySeconds = 10;  // Default to 10 seconds delay

        // Add default values for playback speeds
        NormalPlaybackSpeed = 20;  // Default normal speed
        SlowPlaybackSpeed = 5;    // Default slow speed for orders

      }
      else if (State == State.Configure)
      {
        LogMessage("OnStateChange(): Entering Configure state - Setting up RealtimeErrorHandling and Series", "STATE");
        RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
        buyPressure = new Series<double>(this);
        sellPressure = new Series<double>(this);
        LogMessage("OnStateChange(): Configure state completed", "STATE");
      }
      else if (State == State.DataLoaded)
      {
        LogMessage("OnStateChange(): Entering DataLoaded state - Initializing indicators", "STATE");
        try
        {
          hullMAHooks = BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
          hullMAHooks.Plots[0].Brush = Brushes.White;
          hullMAHooks.Plots[0].Width = 2;
          if (showHmaHooks)
          {
            AddChartIndicator(hullMAHooks);
            LogMessage("OnStateChange(): Added HMA Hooks indicator to chart", "INDICATOR");
          }

          RegressionChannel1 = RegressionChannel(Close, RegChanPeriod, RegChanWidth);
          if (showRegChan1)
          {
            AddChartIndicator(RegressionChannel1);
            LogMessage("OnStateChange(): Added Regression Channel 1 to chart", "INDICATOR");
          }

          RegressionChannel2 = RegressionChannel(Close, RegChanPeriod, RegChanWidth2);
          if (showRegChan2)
          {
            AddChartIndicator(RegressionChannel2);
            LogMessage("OnStateChange(): Added Regression Channel 2 to chart", "INDICATOR");
          }

          RegressionChannelHighLow1 = RegressionChannelHighLow(Close, RegChanPeriod, RegChanWidth);
          if (showRegChanHiLo)
          {
            AddChartIndicator(RegressionChannelHighLow1);
            LogMessage("OnStateChange(): Added Regression Channel High Low to chart", "INDICATOR");
          }

          BuySellPressure1 = BuySellPressure(Close);
          BuySellPressure1.Plots[0].Width = 2;
          BuySellPressure1.Plots[0].Brush = Brushes.Lime;
          BuySellPressure1.Plots[1].Width = 2;
          BuySellPressure1.Plots[1].Brush = Brushes.Red;
          if (showBuySellPressure)
          {
            AddChartIndicator(BuySellPressure1);
            LogMessage("OnStateChange(): Added Buy/Sell Pressure indicator to chart", "INDICATOR");
          }

          VMA1 = VMA(Close, 9, 9);
          VMA1.Plots[0].Brush = Brushes.SkyBlue;
          VMA1.Plots[0].Width = 3;
          if (showVMA)
          {
            AddChartIndicator(VMA1);
            LogMessage("OnStateChange(): Added VMA indicator to chart", "INDICATOR");
          }

          ATR1 = ATR(AtrPeriod);
          LogMessage("OnStateChange(): ATR indicator initialized", "INDICATOR");

          Momentum1 = Momentum(Close, 14);
          Momentum1.Plots[0].Brush = Brushes.Yellow;
          Momentum1.Plots[0].Width = 2;
          if (showMomo)
          {
            AddChartIndicator(Momentum1);
            LogMessage("OnStateChange(): Added Momentum indicator to chart", "INDICATOR");
          }

          ADX1 = ADX(Close, adxPeriod);
          ADX1.Plots[0].Brush = Brushes.Yellow;
          ADX1.Plots[0].Width = 2;
          if (showAdx)
          {
            AddChartIndicator(ADX1);
            LogMessage("OnStateChange(): Added ADX indicator to chart", "INDICATOR");
          }

          if (showEMA)
          {
            AddChartIndicator(EMA(High, emaLength));
            AddChartIndicator(EMA(Low, emaLength));
            LogMessage("OnStateChange(): Added EMA indicators to chart", "INDICATOR");
          }

          // Initialize maxProfit with the totalPnL (if it's the first time, it's 0)
          maxProfit = totalPnL;  // Ensure maxProfit starts at the current PnL
          LogMessage("OnStateChange(): DataLoaded state completed - All indicators initialized", "STATE");
        }
        catch (Exception ex)
        {
          LogError("OnStateChange(): Error in DataLoaded state while initializing indicators", ex);
        }
      }
      else if (State == State.Historical)
      {
        LogMessage("OnStateChange(): Entering Historical state - Creating WPF controls", "STATE");
        Dispatcher.InvokeAsync((() =>
        {
          try
          {
            CreateWPFControls();
            LogMessage("OnStateChange(): WPF controls created successfully", "UI");
          }
          catch (Exception ex)
          {
            LogError("OnStateChange(): Error creating WPF controls", ex);
          }
        }));
      }
      else if (State == State.Terminated)
      {
        LogMessage("OnStateChange(): Entering Terminated state - Cleaning up resources", "STATE");
        ChartControl?.Dispatcher.InvokeAsync(() =>
        {
          try
          {
            DisposeWPFControls();
            LogMessage("OnStateChange(): WPF controls disposed successfully", "UI");
          }
          catch (Exception ex)
          {
            LogError("OnStateChange(): Error disposing WPF controls", ex);
          }
        });

        // Log any remaining active orders
        lock (orderLock)
        {
          if (activeOrders.Count > 0)
          {
            LogWarning($"OnStateChange(): Strategy terminated with {activeOrders.Count} active orders:");
            foreach (var kvp in activeOrders)
            {
              LogWarning($"OnStateChange(): Active order at termination - Label: {kvp.Key}, Order ID: {kvp.Value.OrderId}");
              try
              {
                CancelOrder(kvp.Value);
                LogMessage($"OnStateChange(): Cancelled order {kvp.Value.OrderId} during termination", "ORDER");
              }
              catch (Exception ex)
              {
                LogError($"OnStateChange(): Failed to cancel order {kvp.Value.OrderId} during termination", ex);
              }
            }
          }
          else
          {
            LogMessage("OnStateChange(): No active orders at termination", "STATE");
          }
        }
        LogMessage($"OnStateChange(): Strategy {Name} terminated", "STATE");
      }
    }
    #endregion

    #region OnBarUpdate
    protected override void OnBarUpdate()
    {
      // Move delay check to top and make it more strict
      TimeSpan timeSinceLastEntry = DateTime.Now - lastEntryTime;
      if (timeSinceLastEntry < TradeDelay)
      {
        LogMessage($"OnBarUpdate(): Trade delay active - No entries allowed for {(TradeDelay - timeSinceLastEntry).TotalSeconds:F1} more seconds", "ENTRY_DELAY");
        return;
      }

      if (BarsInProgress != 0 || CurrentBars[0] < BarsRequiredToTrade || orderErrorOccurred)
      {
        if (orderErrorOccurred)
          LogWarning("OnBarUpdate(): Skipped due to previous order error");
        else
          LogDebug($"OnBarUpdate(): Skipped: BarsInProgress={BarsInProgress}, CurrentBars={CurrentBars[0]}, Required={BarsRequiredToTrade}");
        return;
      }

      // Session tracking logic
      if (Bars.IsFirstBarOfSession)
      {
        // Log previous session's statistics
        if (dailyStartEquity > 0)  // Only log if we have previous session data
        {
          LogMessage($"Session End Summary - Daily P&L: {dailyPnL:C}, Highest Profit: {dailyHighestProfit:C}, Worst DD: {dailyWorstDrawdown:C}", "SESSION");
          previousDayPnL = dailyPnL;  // Store previous day's P&L
        }

        // Reset daily tracking variables
        dailyStartEquity = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
        dailyHighestProfit = 0;
        dailyWorstDrawdown = 0;
        dailyPnL = 0;
        cumPnL = totalPnL;  // Update cumulative P&L for new session

        LogMessage($"New Session Started - Starting Equity: {dailyStartEquity:C}, Previous Day P&L: {previousDayPnL:C}", "SESSION");
      }

      // Update daily statistics
      double currentPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
      double unrealizedPnL = Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
      double netProfit = currentPnL + unrealizedPnL;

      // Calculate daily P&L
      dailyPnL = netProfit - dailyStartEquity;

      // Update daily highest profit
      if (dailyPnL > dailyHighestProfit)
      {
        dailyHighestProfit = dailyPnL;
        LogMessage($"New daily highest profit: {dailyHighestProfit:C}", "PROFIT");
      }

      // Calculate and update daily drawdown
      double currentDailyDD = dailyHighestProfit - dailyPnL;
      if (currentDailyDD > dailyWorstDrawdown)
      {
        dailyWorstDrawdown = currentDailyDD;
        LogMessage($"New daily worst drawdown: {dailyWorstDrawdown:C}", "DRAWDOWN");
      }

      // Always update core indicators used for visualization/logging - MOVED HERE
      currentAtr = ATR1[0];
      currentAdx = ADX1[0];
      currentMomentum = Momentum1[0];

      //Track the Highest Profit Achieved
      if (totalPnL > maxProfit)
      {
        maxProfit = totalPnL;
        LogMessage($"OnBarUpdate(): New max profit achieved: {maxProfit:C}", "PROFIT");
      }

      if (!ShowHistorical && State != State.Realtime)
      {
        LogDebug("OnBarUpdate(): Skipping historical processing as ShowHistorical=false");
        return;
      }

      trailStop = InitialStop;

      // Update position state
      UpdatePositionState();

      // Early exit if we're not flat and not checking exits
      if (!isFlat && !enableExit)
      {
        // Log minimal state info when in position
        LogMessage($"OnBarUpdate(): Bar {CurrentBar} | In {(isLong ? "LONG" : "SHORT")} position - Maintaining current position state", "MARKET_STATE");

        // Handle stop/target management
        ManageAutoBreakeven();
        ManageStopLoss();
        SetProfitTargets();

        if (Bars.IsFirstBarOfSession)
        {
          cumPnL = totalPnL;
          dailyPnL = totalPnL - cumPnL;
          LogMessage($"OnBarUpdate(): New session started - Reset daily PnL tracking. CumPnL: {cumPnL:C}, DailyPnL: {dailyPnL:C}", "PNL");
        }
        dailyPnL = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - cumPnL;
        // Show PnL status if enabled
        if (showPnl) ShowPNLStatus();
        if (showDailyPnl) DrawStrategyPnL();

        return; // Exit early since we're in a position and not checking exits
      }

      // Only calculate full indicator signals if we're flat (looking for entries) or if exit checking is enabled
      bool needFullSignals = isFlat || (enableExit && (isLong || isShort));

      if (needFullSignals)
      {
        // Log all indicator states in a single line
        LogMessage($"OnBarUpdate(): Indicators: ADX[{currentAdx:F2}|{(adxUp ? "↑" : "↓")}] ATR[{currentAtr:F2}|{(atrUp ? "↑" : "↓")}] MOMO[{currentMomentum:F2}|{(momoUp ? "↑" : momoDown ? "↓" : "-")}] HMA[{(hmaUp ? "↑" : hmaDown ? "↓" : "-")}] VMA[{(volMaUp ? "↑" : volMaDown ? "↓" : "-")}] RegChan[{(regChanUp ? "↑" : regChanDown ? "↓" : "-")}] BuySell[{(buyPressureUp ? "Buy↑" : sellPressureUp ? "Sell↑" : "-")}]", "MARKET_STATE");

        atrUp = !enableVolatility || currentAtr > atrThreshold;
        adxUp = !enableADX || currentAdx > AdxThreshold;

        momoUp = !enableMomo || (currentMomentum > 0);
        momoDown = !enableMomo || (currentMomentum < 0);

        regChanUp = RegressionChannel1.Middle[0] > RegressionChannel1.Middle[1];
        regChanDown = RegressionChannel1.Middle[0] < RegressionChannel1.Middle[1];

        buyPressureUp = !enableBuySellPressure || (BuySellPressure1.BuyPressure[0] > BuySellPressure1.SellPressure[0]);
        sellPressureUp = !enableBuySellPressure || (BuySellPressure1.SellPressure[0] > BuySellPressure1.BuyPressure[0]);

        buyPressure[0] = BuySellPressure1.BuyPressure[0];
        sellPressure[0] = BuySellPressure1.SellPressure[0];

        hmaUp = (hullMAHooks[0] > hullMAHooks[1]);
        hmaDown = (hullMAHooks[0] < hullMAHooks[1]);

        // ***** START MODIFIED SECTION for VMA *****
        volMaUp = false;
        volMaDown = false;

        if (VMA1 != null && VMA1.IsValidDataPoint(1))
        {
          volMaUp = !enableVMA || VMA1[0] > VMA1[1];
          volMaDown = !enableVMA || VMA1[0] < VMA1[1];
          LogDebug($"OnBarUpdate(): VMA State - Up: {volMaUp}, Down: {volMaDown}");
        }
        else
        {
          LogDebug("OnBarUpdate(): VMA not ready for calculation");
        }

        aboveEMAHigh = !enableEMAFilter || Open[1] > EMA(High, emaLength)[1];
        belowEMALow = !enableEMAFilter || Open[1] < EMA(Low, emaLength)[1];

        if (EnableChoppinessDetection)
        {
          marketIsChoppy = false; // Default

          if (CurrentBar >= Math.Max(RegChanPeriod, Math.Max(adxPeriod, SlopeLookBack)) - 1)
          {
            double middleNow = RegressionChannel1.Middle[0];
            double middleBefore = RegressionChannel1.Middle[SlopeLookBack];
            double regChanSlope = (middleNow - middleBefore) / SlopeLookBack;
            double flatSlopeThreshold = FlatSlopeFactor * TickSize;
            bool isRegChanFlat = Math.Abs(regChanSlope) < flatSlopeThreshold;
            bool adxIsLow = currentAdx < ChopAdxThreshold;

            marketIsChoppy = isRegChanFlat && adxIsLow;
            LogMessage($"OnBarUpdate(): Choppiness Check - Slope: {regChanSlope:F5}, ADX: {currentAdx:F2}, IsChoppy: {marketIsChoppy}", "MARKET_STATE");
          }

          bool autoStatusChanged = false;

          if (marketIsChoppy)
          {
            TransparentColor(64, Colors.LightGray);
          }
          else
          {
            BackBrush = null;
          }

          if (marketIsChoppy)
          {
            if (isAutoEnabled)
            {
              isAutoEnabled = false;
              autoDisabledByChop = true;
              autoStatusChanged = true;
              LogMessage("OnBarUpdate(): Market choppy - Auto trading DISABLED", "TRADING_STATE");
            }
          }
          else
          {
            if (autoDisabledByChop)
            {
              isAutoEnabled = true;
              autoDisabledByChop = false;
              autoStatusChanged = true;
              LogMessage("OnBarUpdate(): Market no longer choppy - Auto trading RE-ENABLED", "TRADING_STATE");
            }
          }

          if (autoStatusChanged && autoBtn != null && ChartControl != null)
          {
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
              DecorateButton(autoBtn, isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
              DecorateButton(manualBtn, !isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
            });
          }
        }

        uptrend = adxUp && momoUp && buyPressureUp && hmaUp && volMaUp && regChanUp && atrUp && aboveEMAHigh;
        downtrend = adxUp && momoDown && sellPressureUp && hmaDown && volMaDown && regChanDown && atrUp && belowEMALow;

        LogMessage($"OnBarUpdate(): Trend State - Uptrend: {uptrend}, Downtrend: {downtrend}", "MARKET_STATE");
      }
      else
      {
        // Reset trend signals when not calculating full signals
        uptrend = false;
        downtrend = false;

        // Log minimal state info when in position
        LogMessage($"OnBarUpdate(): Bar {CurrentBar} | In {(isLong ? "LONG" : "SHORT")} position - Maintaining current position state", "MARKET_STATE");
      }

      if (enableBackgroundSignal)
      {
        if (uptrend)
        {
          TransparentColor(64, Colors.Lime);
        }
        else if (downtrend)
        {
          TransparentColor(64, Colors.Crimson);
        }
        else
        {
          BackBrush = null;
        }
      }



      priceUp = Close[0] > Close[1] && Close[0] > Open[0];
      priceDown = Close[0] < Close[1] && Close[0] < Open[0];

      if (isAutoEnabled)
      {
        ProcessLongEntry();
        ProcessShortEntry();
      }

      // --- Stop/Target Management ---
      ManageAutoBreakeven();
      ManageStopLoss();
      SetProfitTargets();

      if (enableAtrProfitTarget)
      {
        ProfitTarget = ATR1[0] * RiskRewardRatio / TickSize;
        LogDebug($"OnBarUpdate(): ATR Profit Target updated: {ProfitTarget:F2} ticks");
      }

      if (Bars.IsFirstBarOfSession)
      {
        cumPnL = totalPnL;
        dailyPnL = totalPnL - cumPnL;
        LogMessage($"OnBarUpdate(): New session started - Reset daily PnL tracking. CumPnL: {cumPnL:C}, DailyPnL: {dailyPnL:C}", "PNL");
      }

      if (showPnl) ShowPNLStatus();
      if (showDailyPnl) DrawStrategyPnL();

      #region Reset Trades Per Direction
      if (TradesPerDirection)
      {
        if (counterLong != 0 && Close[1] < Open[1])
        {
          counterLong = 0;
          LogDebug($"OnBarUpdate(): Reset long counter due to price action");
        }
        if (counterShort != 0 && Close[1] > Open[1])
        {
          counterShort = 0;
          LogDebug($"OnBarUpdate(): Reset short counter due to price action");
        }
      }
      #endregion

      #region Reset Stop Loss

      if (isFlat)
      {
        lock (orderLock)
        {
          List<Order> stopsToCancel = Orders.Where(o =>
              o.OrderState == OrderState.Working &&
              o.IsStopMarket &&
              (o.FromEntrySignal == LE || o.FromEntrySignal == SE ||
               o.FromEntrySignal == LE2 || o.FromEntrySignal == SE2 ||
               o.FromEntrySignal == LE3 || o.FromEntrySignal == SE3 ||
               o.FromEntrySignal == LE4 || o.FromEntrySignal == SE4
              )).ToList();

          if (stopsToCancel.Count > 0)
          {
            LogMessage($"OnBarUpdate(): Position flat - Cancelling {stopsToCancel.Count} working stop order(s)", "ORDER");
            foreach (Order stopOrder in stopsToCancel)
            {
              try
              {
                CancelOrder(stopOrder);
              }
              catch (Exception ex)
              {
                LogError($"OnBarUpdate(): Error cancelling stop order {stopOrder.OrderId} on flatten", ex);
              }
            }
          }
        }


        trailStop = InitialStop;
        lastStopLevel = InitialStop;
        _beRealized = false;

        lock (orderLock)
        {
          activeOrders.Clear();
        }
        LogMessage("OnBarUpdate(): Position flat - Reset all trading state variables", "TRADING_STATE");
      }


      #endregion

      if (ValidateExitLong())
      {
        string[] orderLabels = additionalContractExists ? new[] { LE2, LE3, LE4 } : new[] { LE };
        LogMessage($"OnBarUpdate(): Executing long exit for labels: {string.Join(", ", orderLabels)}", "EXIT");
        foreach (string label in orderLabels)
        {
          ExitLong(label);
        }
      }

      if (ValidateExitShort())
      {
        string[] orderLabels = additionalContractExists ? new[] { SE2, SE3, SE4 } : new[] { SE };
        LogMessage($"OnBarUpdate(): Executing short exit for labels: {string.Join(", ", orderLabels)}", "EXIT");
        foreach (string label in orderLabels)
        {
          ExitShort(label);
        }
      }

      KillSwitch();


    }
    #endregion

    #region Transparent Background Color
    private void TransparentColor(byte percentTransparency, Color baseColor)
    {
      // percentTransparency = transparency, 50% = 128
      // Create the new semi-transparent color
      Color semiTransparentColor = Color.FromArgb(percentTransparency, baseColor.R, baseColor.G, baseColor.B);
      // Create the new brush
      SolidColorBrush semiTransparentBrush = new SolidColorBrush(semiTransparentColor);
      // Freeze the brush for performance (important!)
      semiTransparentBrush.Freeze();
      // Assign the semi-transparent brush to BackBrush
      BackBrush = semiTransparentBrush;
    }
    #endregion

    #region Breakeven Management

    // Helper method to determine the active order labels based on position
    private List<string> GetRelevantOrderLabels()
    {
      List<string> labels = new List<string>();
      bool isLongPosition = Position.MarketPosition == MarketPosition.Long;

      // Add base labels depending on position type (Auto or Quick)
      if (isLongPosition)
      {
        labels.Add(LE); // LE
        if (additionalContractExists) // Add scaled-in entries only if they exist conceptually
        {
          if (EnableProfitTarget2) labels.Add(LE2);
          if (EnableProfitTarget3) labels.Add(LE3);
          if (EnableProfitTarget4) labels.Add(LE4);
        }
      }
      else // Short Position
      {
        labels.Add(SE); // SE
        if (additionalContractExists) // Add scaled entries only if they exist conceptually
        {
          if (EnableProfitTarget2) labels.Add(SE2);
          if (EnableProfitTarget3) labels.Add(SE3);
          if (EnableProfitTarget4) labels.Add(SE4);
        }
      }

      return labels;
    }

    // Helper to safely set TRAILING stop loss (incorporates error handling)
    private void SetTrailingStop(string fromEntrySignal, CalculationMode mode, double value, bool isSimulatedStop = true)
    {
      lock (orderLock) // Ensure thread safety
      {

        try
        {
          // Use isSimulatedStop = true to keep strategy in control of trailing logic
          SetTrailStop(fromEntrySignal, mode, value, isSimulatedStop);
          //LogMessage($"{Time[0]}: SetTrailStop called for label '{fromEntrySignal}'. Mode: {mode}, Value: {value}, IsSimulated: {isSimulatedStop}", "TRAILING_STOP");
        }
        catch (Exception ex)
        {
          LogError($"SetTrailingStop(): Error calling SetTrailStop for label '{fromEntrySignal}': {ex.Message}", ex);
          orderErrorOccurred = true; // Flag the error
        }
      }
    }

    // Main method to manage the automatic breakeven logic for EITHER Fixed or Trailing Stops
    private void ManageAutoBreakeven()
    {
      // --- Pre-checks ---
      if (isFlat || !beSetAuto || _beRealized)
      {
        LogDebug($"ManageAutoBreakeven(): Skipped: Flat={isFlat}, BESetAuto={beSetAuto}, BERealized={_beRealized}");
        return;
      }

      // Use standard BE_Trigger and BE_Offset values
      int effectiveBeTrigger = BE_Trigger;
      int effectiveBeOffset = BE_Offset;

      // --- Calculation & Logging ---
      double currentUnrealizedPnlTicks = Position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks, Close[0]);
      LogMessage($"ManageAutoBreakeven(): Checking Auto BE - Current PnL: {currentUnrealizedPnlTicks:F2} ticks, Trigger: {effectiveBeTrigger}, Offset: {effectiveBeOffset}", "BREAKEVEN");

      // --- Trigger Condition (using effective trigger) ---
      if (currentUnrealizedPnlTicks >= effectiveBeTrigger)
      {
        LogMessage($"ManageAutoBreakeven(): Auto-Breakeven triggered - PnL ({currentUnrealizedPnlTicks:F2} ticks) >= Trigger ({effectiveBeTrigger})", "BREAKEVEN");

        // --- Calculate Target Breakeven Stop Price (using effective offset) ---
        double entryPrice = Position.AveragePrice;
        if (entryPrice == 0)
        {
          LogError($"ManageAutoBreakeven(): Cannot calculate breakeven - Entry price is 0");
          return;
        }

        double offsetPriceAdjustment = effectiveBeOffset * TickSize;
        double breakevenStopPrice = entryPrice + (Position.MarketPosition == MarketPosition.Long ? offsetPriceAdjustment : -offsetPriceAdjustment);

        LogMessage($"ManageAutoBreakeven(): Calculated BE Stop Price: {breakevenStopPrice:F5} (Entry: {entryPrice:F5}, Offset: {effectiveBeOffset} ticks)", "BREAKEVEN");

        // --- Apply Stop Based on Strategy Setting ---
        List<string> relevantLabels = GetRelevantOrderLabels();
        if (relevantLabels.Count == 0)
        {
          LogWarning("ManageAutoBreakeven(): Breakeven triggered but no relevant order labels found");
          return;
        }

        bool stopAppliedSuccessfully = false;

        // --- Decide if Stop is Fixed or Trailing ---
        if (enableTrail)
        {
          double currentMarketPrice = Close[0];
          double valueInTicks;
          if (Position.MarketPosition == MarketPosition.Long)
            valueInTicks = (currentMarketPrice - breakevenStopPrice) / TickSize;
          else
            valueInTicks = (breakevenStopPrice - currentMarketPrice) / TickSize;

          LogMessage($"ManageAutoBreakeven(): Calculated trailing value for BE stop: {valueInTicks:F2} ticks from market", "BREAKEVEN");

          if (valueInTicks <= 0 || !IsValidStopPlacement(breakevenStopPrice, Position.MarketPosition))
          {
            LogWarning($"ManageAutoBreakeven(): Cannot apply trailing BE stop - Invalid price {breakevenStopPrice:F5} / ticks {valueInTicks:F2} relative to market {currentMarketPrice:F5}");
          }
          else
          {
            LogMessage($"ManageAutoBreakeven(): Applying TRAILING Breakeven Stop (Ticks from market: {valueInTicks:F2}) to labels: {string.Join(", ", relevantLabels)}", "BREAKEVEN");
            foreach (string tag in relevantLabels)
            {
              try
              {
                SetTrailingStop(tag, CalculationMode.Ticks, valueInTicks, true);
                stopAppliedSuccessfully = true;
              }
              catch (Exception ex)
              {
                LogError($"ManageAutoBreakeven(): Failed to set trailing stop for label {tag}", ex);
              }
            }
          }
        }
        else if (enableFixedStopLoss)
        {
          if (!IsValidStopPlacement(breakevenStopPrice, Position.MarketPosition))
          {
            LogWarning($"ManageAutoBreakeven(): Cannot apply fixed BE stop - Invalid price {breakevenStopPrice:F5} relative to market");
          }
          else
          {
            LogMessage($"ManageAutoBreakeven(): Applying FIXED Breakeven Stop (Price: {breakevenStopPrice:F5}) to labels: {string.Join(", ", relevantLabels)}", "BREAKEVEN");
            foreach (string tag in relevantLabels)
            {
              try
              {
                SetFixedStopLoss(tag, CalculationMode.Price, breakevenStopPrice, false);
                stopAppliedSuccessfully = true;
              }
              catch (Exception ex)
              {
                LogError($"ManageAutoBreakeven(): Failed to set fixed stop for label {tag}", ex);
              }
            }
          }
        }
        else
        {
          LogWarning("ManageAutoBreakeven(): Breakeven triggered but neither Fixed Stop nor Trailing Stop is enabled");
        }

        // --- Mark as Realized ---
        if (stopAppliedSuccessfully)
        {
          _beRealized = true;
          LogMessage("ManageAutoBreakeven(): Auto-Breakeven process complete - BE realized flag set to true", "BREAKEVEN");
        }
        else
        {
          LogWarning("ManageAutoBreakeven(): Auto-Breakeven process failed - No stops were successfully applied");
        }
      }
      else
      {
        LogDebug($"ManageAutoBreakeven(): Auto-Breakeven conditions not met - PnL ({currentUnrealizedPnlTicks:F2} ticks) < Trigger ({effectiveBeTrigger})");
      }
    }
    #endregion

    #region Stop Loss Management

    // ***** MODIFIED SECTION *****
    // Helper to safely set stop loss (incorporates error handling)
    // THIS VERSION NOW USES ExitLongStopMarket / ExitShortStopMarket
    private void SetFixedStopLoss(string fromEntrySignal, CalculationMode mode, double priceValue, bool isSimulatedStop = false) // isSimulatedStop is now ignored
    {
      lock (orderLock) // Ensure thread safety
      {
        LogMessage($"SetFixedStopLoss(): Starting for signal {fromEntrySignal} - Mode: {mode}, Value: {priceValue:F5}", "STOP_MANAGEMENT");

        if (Position.MarketPosition == MarketPosition.Flat)
        {
          LogWarning($"SetFixedStopLoss(): Cannot set fixed stop for {fromEntrySignal} - Position is flat");
          return;
        }

        if (Position.Quantity == 0)
        {
          LogWarning($"SetFixedStopLoss(): Cannot set fixed stop for {fromEntrySignal} - Position quantity is zero");
          return;
        }

        double stopPrice = 0;

        // Calculate the target stop price based on mode
        if (mode == CalculationMode.Price)
        {
          stopPrice = priceValue;
          LogMessage($"SetFixedStopLoss(): Using direct price value {stopPrice:F5} for {fromEntrySignal}", "STOP_MANAGEMENT");
        }
        else if (mode == CalculationMode.Ticks)
        {
          double entryPrice = Position.AveragePrice;
          if (entryPrice == 0)
          {
            LogWarning($"SetFixedStopLoss(): Cannot calculate stop price from Ticks for {fromEntrySignal} - Entry price is 0");
            return;
          }
          if (TickSize <= 0)
          {
            LogWarning($"SetFixedStopLoss(): Cannot calculate stop price from Ticks for {fromEntrySignal} - Invalid TickSize: {TickSize}");
            return;
          }

          stopPrice = (Position.MarketPosition == MarketPosition.Long)
                      ? entryPrice - (priceValue * TickSize)
                      : entryPrice + (priceValue * TickSize);

          LogMessage($"SetFixedStopLoss(): Calculated stop price {stopPrice:F5} from {priceValue} ticks for {fromEntrySignal} (Entry: {entryPrice:F5})", "STOP_MANAGEMENT");
        }
        else
        {
          LogError($"SetFixedStopLoss(): Invalid CalculationMode: {mode} for {fromEntrySignal}");
          return;
        }

        // --- Validation ---
        if (!IsValidStopPlacement(stopPrice, Position.MarketPosition))
        {
          LogWarning($"SetFixedStopLoss(): Stop placement {stopPrice:F5} is invalid for {fromEntrySignal} - Skipping submission");
          return;
        }

        // --- Submit Exit Order ---
        int quantityToExit = Position.Quantity;
        string signalTag = "Fixed_Stop_" + fromEntrySignal;

        try
        {
          LogMessage($"SetFixedStopLoss(): Submitting stop order for {fromEntrySignal} - Quantity: {quantityToExit}, Price: {stopPrice:F5}", "STOP_MANAGEMENT");

          if (Position.MarketPosition == MarketPosition.Long)
          {
            ExitLongStopMarket(quantityToExit, stopPrice, signalTag, fromEntrySignal);
            LogMessage($"SetFixedStopLoss(): Submitted ExitLongStopMarket for {fromEntrySignal}", "STOP_MANAGEMENT");
          }
          else if (Position.MarketPosition == MarketPosition.Short)
          {
            ExitShortStopMarket(quantityToExit, stopPrice, signalTag, fromEntrySignal);
            LogMessage($"SetFixedStopLoss(): Submitted ExitShortStopMarket for {fromEntrySignal}", "STOP_MANAGEMENT");
          }
        }
        catch (Exception ex)
        {
          LogError($"SetFixedStopLoss(): Error submitting stop order for {fromEntrySignal}", ex);
          orderErrorOccurred = true;
        }
      }
    }
    // ***** END OF MODIFIED SECTION *****

    #endregion

    #region Set Stop Losses

    // (SetMultipleStopLosses does not need changes here as it receives the calculated price)
    // ... SetMultipleStopLosses remains the same ...
    private void SetMultipleStopLosses(double initialStopPrice, bool isTrailingIntendedLater)
    {
      string modeDesc = isTrailingIntendedLater ? "Initial Placement (for later Trail)" : "Initial Placement (Fixed)";
      LogMessage($"SetMultipleStopLosses(): Starting stop loss setup - Mode: {modeDesc}, Initial Price: {initialStopPrice:F5}", "STOP_MANAGEMENT");

      if (!enableFixedProfitTarget)
      {
        LogMessage("SetMultipleStopLosses(): Fixed profit targets not enabled, skipping scale-in stops", "STOP_MANAGEMENT");
        return;
      }

      // Determine label prefix
      string labelPrefix = "";
      MarketPosition currentPositionState = Position.MarketPosition;
      if (currentPositionState == MarketPosition.Long)
      {
        labelPrefix = LE;
        LogMessage($"SetMultipleStopLosses(): Processing LONG position stops with prefix {labelPrefix}", "STOP_MANAGEMENT");
      }
      else if (currentPositionState == MarketPosition.Short)
      {
        labelPrefix = SE;
        LogMessage($"SetMultipleStopLosses(): Processing SHORT position stops with prefix {labelPrefix}", "STOP_MANAGEMENT");
      }
      else
      {
        LogWarning("SetMultipleStopLosses(): Cannot determine label prefix, position is flat");
        return;
      }

      var scaleInTargets = new[] {
        new { Enabled = EnableProfitTarget2, Suffix = "2"},
        new { Enabled = EnableProfitTarget3, Suffix = "3"},
        new { Enabled = EnableProfitTarget4, Suffix = "4"}
      };

      foreach (var target in scaleInTargets)
      {
        if (target.Enabled)
        {
          string lbl = labelPrefix + target.Suffix;
          LogMessage($"SetMultipleStopLosses(): Setting stop for scale-in position {lbl} at {initialStopPrice:F5}", "STOP_MANAGEMENT");

          try
          {
            SetFixedStopLoss(lbl, CalculationMode.Price, initialStopPrice, false);
            LogMessage($"SetMultipleStopLosses(): Successfully set stop for {lbl}", "STOP_MANAGEMENT");
          }
          catch (Exception ex)
          {
            LogError($"SetMultipleStopLosses(): Failed to set stop for {lbl}", ex);
            orderErrorOccurred = true;
          }
        }
        else
        {
          LogDebug($"SetMultipleStopLosses(): Scale-in target {labelPrefix}{target.Suffix} not enabled, skipping stop");
        }
      }

      LogMessage("SetMultipleStopLosses(): Completed stop loss setup for all enabled scale-in positions", "STOP_MANAGEMENT");
    }

    #endregion

    // ***** MODIFIED SECTION *****
    #region Stop Loss Management

    // The SetFixedStopLoss helper (using Exit...StopMarket) remains unchanged from the previous version

    // Helper method to calculate the trailing stop value in ticks based on the active mode
    // (This function remains unchanged)
    private double CalculateTrailingStopTicks()
    {
      // ... (no changes here) ...
      double calculatedTrailStopTicks = InitialStop; // Default to InitialStop (acts like Tick Trail default)

      if (tickTrail)
      {
        calculatedTrailStopTicks = trailStop;
        // Print($"[DEBUG Tick Trail] Using InitialStop: {calculatedTrailStopTicks}"); // Reduced logging
      }
      else if (atrTrailSetAuto)
      {
        // ... (ATR logic unchanged) ...
        if (ATR1 != null && ATR1.IsValidDataPoint(0) && TickSize > 0)
        {
          calculatedTrailStopTicks = Math.Max(1, ATR1[0] * atrMultiplier / TickSize); // Ensure at least 1 tick
                                                                                      // Print($"[DEBUG ATR Trail] ATR: {ATR1[0]:F5}, Calculated Stop Ticks: {calculatedTrailStopTicks:F2}"); // Reduced logging
        }
        else
        {
          // Print($"[WARN ATR Trail] ATR not ready or TickSize invalid. Using default: {calculatedTrailStopTicks}"); // Reduced logging
        }
      }

      return calculatedTrailStopTicks;
    }

    // Main method to manage stop loss based on active settings
    // NOW, this method ACTIVATES and MANAGES trailing using SetTrailStop if enableTrail is true
    private void ManageStopLoss()
    {
      if (isFlat)
      {
        return;
      }

      // Check if we've already updated stops for this bar
      if (lastStopUpdateBar == CurrentBar)
      {
        LogDebug($"ManageStopLoss(): Skipping - Already updated stops for bar {CurrentBar}");
        return;
      }

      // Ensure we have enough bars and valid BarsArray
      if (BarsArray == null || BarsArray.Length == 0 || CurrentBar <= 1)
      {
        LogDebug($"ManageStopLoss(): Skipping - Insufficient bars or invalid BarsArray (CurrentBar: {CurrentBar})");
        return;
      }

      // Only update on new bars - using safer bar comparison
      if (Calculate != Calculate.OnBarClose)
      {
        // Get the current bar's time
        DateTime currentBarTime = Time[0];

        // Try to get previous bar's time safely
        DateTime? previousBarTime = null;
        try
        {
          if (CurrentBar > 0)
          {
            previousBarTime = Time[1];
          }
        }
        catch (Exception ex)
        {
          LogError("ManageStopLoss(): Error accessing previous bar time", ex);
          return;
        }

        // If we have both times and they're the same, skip update
        if (previousBarTime.HasValue && currentBarTime == previousBarTime.Value)
        {
          LogDebug("ManageStopLoss(): Skipping - Same bar update");
          return;
        }
      }

      List<string> relevantLabels = GetRelevantOrderLabels();
      if (relevantLabels.Count == 0)
      {
        LogWarning("ManageStopLoss(): No relevant order labels found");
        return;
      }

      // --- If Trailing is Enabled: Activate and Manage with SetTrailStop ---
      if (enableTrail)
      {
        // Calculate the DESIRED trailing stop distance/price based on the ACTIVE trail type for THIS bar
        double trailValue = 0;
        CalculationMode trailMode = CalculationMode.Ticks; // Default, ATR use Ticks

        // Handle trail types (Tick, ATR) which use Ticks
        trailValue = CalculateTrailingStopTicks(); // Get Ticks for Tick, ATR
        trailMode = CalculationMode.Ticks;

        if (trailValue <= 0)
        {
          LogWarning($"ManageStopLoss(): {trailStopType} calculated non-positive ticks ({trailValue:F2}). Skipping.");
          return;
        }

        LogMessage($"ManageStopLoss(): Applying {trailStopType} trail with {trailValue:F2} ticks on bar {CurrentBar} @ {Time[0]}", "STOP_MANAGEMENT");

        // --- Apply/Update Trailing Stop using SetTrailStop ---
        foreach (string label in relevantLabels)
        {
          try
          {
            SetTrailingStop(label, trailMode, trailValue, true);
            LogMessage($"ManageStopLoss(): Successfully set trailing stop for label {label} - Mode: {trailMode}, Value: {trailValue:F2}", "STOP_MANAGEMENT");
          }
          catch (Exception ex)
          {
            LogError($"ManageStopLoss(): Failed to set trailing stop for label {label}", ex);
          }
        }
      }
      else
      {
        LogDebug("ManageStopLoss(): Trailing stop not enabled, skipping stop management");
      }

      // Update the last stop update bar
      lastStopUpdateBar = CurrentBar;
    }

    #endregion
    // ***** END OF MODIFIED SECTION *****

    #region Helper Methods

    /// <summary>
    /// Validates if a target stop price is valid relative to the current market Bid/Ask.
    /// Includes a small buffer to account for transmission delays and slippage.
    /// </summary>
    /// <param name="targetStopPrice">The intended new stop price.</param>
    /// <param name="position">The current market position (Long or Short).</param>
    /// <param name="bufferTicks">Number of ticks buffer to apply. Adjust based on instrument volatility.</param>
    /// <returns>True if the price is valid, False otherwise.</returns>
    private bool IsValidStopPlacement(double targetStopPrice, MarketPosition position, int bufferTicks = 4) // Default buffer of 4 ticks (e.g., 1 point on MNQ/NQ)
    {
      // --- Essential Pre-Checks ---
      if (TickSize <= 0)
      {
        Print($"{Time[0]}: Validation FAIL: Invalid TickSize {TickSize}. Cannot validate stop.");
        return false;
      }
      // Ensure we have valid market data access (might not be strictly necessary in OnBarUpdate but good practice)
      if (!IsMarketDataValid())
      {
        Print($"{Time[0]}: Validation FAIL: Market data (Bid/Ask) not available. Cannot validate stop.");
        return false;
      }

      // --- Validation Logic ---
      if (position == MarketPosition.Long) // Validating a Sell Stop Order
      {
        double currentAsk = GetCurrentAsk();
        if (currentAsk == 0) { Print($"{Time[0]}: Validation WARN: Current Ask is 0. Cannot reliably validate Sell Stop."); return false; } // Cannot validate against 0

        // Sell Stop must be placed BELOW the current Ask price, including the buffer.
        double minStopLevel = currentAsk - bufferTicks * TickSize;
        if (targetStopPrice >= minStopLevel)
        {
          Print($"{Time[0]}: Validation FAIL: Target Sell Stop {targetStopPrice:F5} is >= Ask {currentAsk:F5} (minus {bufferTicks} tick buffer {minStopLevel:F5}).");
          return false;
        }
      }
      else if (position == MarketPosition.Short) // Validating a Buy Stop Order
      {
        double currentBid = GetCurrentBid();
        if (currentBid == 0) { Print($"{Time[0]}: Validation WARN: Current Bid is 0. Cannot reliably validate Buy Stop."); return false; } // Cannot validate against 0

        // Buy Stop must be placed ABOVE the current Bid price, including the buffer.
        double maxStopLevel = currentBid + bufferTicks * TickSize;
        if (targetStopPrice <= maxStopLevel)
        {
          Print($"{Time[0]}: Validation FAIL: Target Buy Stop {targetStopPrice:F5} is <= Bid {currentBid:F5} (plus {bufferTicks} tick buffer {maxStopLevel:F5}).");
          return false;
        }
      }
      else // Position is Flat or Unknown
      {
        Print($"{Time[0]}: Validation FAIL: Position is Flat or Unknown ({position}). Cannot validate stop.");
        return false; // Cannot validate if not in a position
      }

      // If all relevant checks passed
      Print($"{Time[0]}: Validation PASS: Target Stop {targetStopPrice:F5} is valid for position {position}.");
      return true;
    }

    /// <summary>
    /// Helper to check if essential market data (Bid/Ask) is available.
    /// </summary>
    /// <returns>True if Bid/Ask are likely available, False otherwise.</returns>
    private bool IsMarketDataValid()
    {
      // A simple check. More robust checks might involve looking at connection status or last update time if available.
      return GetCurrentBid() > 0 && GetCurrentAsk() > 0;
    }

    // --- Keep other existing helper methods like CanSubmitOrder ---

    #endregion // End Helper Methods

    #region Update Position State
    private void UpdatePositionState()
    {

      isLong = Position.MarketPosition == MarketPosition.Long;
      isShort = Position.MarketPosition == MarketPosition.Short;
      isFlat = Position.MarketPosition == MarketPosition.Flat;

      entryPrice = Position.AveragePrice;

      // Logic to check if additional contracts exist (i.e., more than one contract is held)
      additionalContractExists = Position.Quantity > 1;

    }
    #endregion

    #region Long Entry
    private void ProcessLongEntry()
    {
      LogMessage($"ProcessLongEntry(): Checking conditions...", "ENTRY");
      if (IsLongEntryConditionMet())
      {
        LogMessage($"ProcessLongEntry(): Long entry conditions met - Proceeding with entry", "ENTRY");
        EnterLongPosition();
      }
      else
      {
        LogMessage($"ProcessLongEntry(): Long entry conditions not met", "ENTRY");
      }
    }
    #endregion

    #region Short Entry
    private void ProcessShortEntry()
    {
      LogMessage($"ProcessShortEntry(): Checking conditions...", "ENTRY");
      if (IsShortEntryConditionMet())
      {
        LogMessage($"ProcessShortEntry(): Short entry conditions met - Proceeding with entry", "ENTRY");
        EnterShortPosition();
      }
      else
      {
        LogMessage($"ProcessShortEntry(): Short entry conditions not met", "ENTRY");
      }
    }
    #endregion

    #region Entry Condition Checkers

    private bool IsLongEntryConditionMet()
    {
      // Create individual condition checks for detailed logging
      bool validateLong = ValidateEntryLong();
      bool longEnabled = isLongEnabled;
      bool timersOk = checkTimers();
      bool pnlOk = !dailyLossProfit || (dailyPnL > -DailyLossLimit && dailyPnL < DailyProfitLimit);
      bool flatOk = isFlat;
      bool trendOk = uptrend;
      bool drawdownOk = !trailingDrawdownReached;
      bool barsSinceExitOk = (iBarsSinceExit > 0 ? BarsSinceExitExecution(0, "", 0) > iBarsSinceExit : BarsSinceExitExecution(0, "", 0) > 1 || BarsSinceExitExecution(0, "", 0) == -1);
      bool tradesPerDirOk = !TradesPerDirection || (TradesPerDirection && counterLong < longPerDirection);

      // Log failed conditions
      if (!validateLong) LogMessage("IsLongEntryConditionMet(): ValidateEntryLong returned false", "ENTRY_DETAIL");
      if (!longEnabled) LogMessage("IsLongEntryConditionMet(): Long trading is disabled", "ENTRY_DETAIL");
      if (!timersOk) LogMessage("IsLongEntryConditionMet(): Outside trading hours", "ENTRY_DETAIL");
      if (!pnlOk) LogMessage($"IsLongEntryConditionMet(): PnL limits exceeded (Daily PnL: {dailyPnL:C})", "ENTRY_DETAIL");
      if (!flatOk) LogMessage("IsLongEntryConditionMet(): Not flat - position already exists", "ENTRY_DETAIL");
      if (!trendOk) LogMessage("IsLongEntryConditionMet(): Uptrend condition not met", "ENTRY_DETAIL");
      if (!drawdownOk) LogMessage("IsLongEntryConditionMet(): Trailing drawdown limit reached", "ENTRY_DETAIL");
      if (!barsSinceExitOk) LogMessage("IsLongEntryConditionMet(): Not enough bars since last exit", "ENTRY_DETAIL");
      if (!tradesPerDirOk) LogMessage($"IsLongEntryConditionMet(): Max trades per direction reached (Counter: {counterLong}, Max: {longPerDirection})", "ENTRY_DETAIL");

      return validateLong && longEnabled && timersOk && pnlOk && flatOk && trendOk && drawdownOk && barsSinceExitOk && tradesPerDirOk;
    }

    private bool IsShortEntryConditionMet()
    {
      // Create individual condition checks for detailed logging
      bool validateShort = ValidateEntryShort();
      bool shortEnabled = isShortEnabled;
      bool timersOk = checkTimers();
      bool pnlOk = !dailyLossProfit || (dailyPnL > -DailyLossLimit && dailyPnL < DailyProfitLimit);
      bool flatOk = isFlat;
      bool trendOk = downtrend;
      bool drawdownOk = !trailingDrawdownReached;
      bool barsSinceExitOk = (iBarsSinceExit > 0 ? BarsSinceExitExecution(0, "", 0) > iBarsSinceExit : BarsSinceExitExecution(0, "", 0) > 1 || BarsSinceExitExecution(0, "", 0) == -1);
      bool tradesPerDirOk = !TradesPerDirection || (TradesPerDirection && counterShort < shortPerDirection);

      // Log failed conditions
      if (!validateShort) LogMessage("IsShortEntryConditionMet(): ValidateEntryShort returned false", "ENTRY_DETAIL");
      if (!shortEnabled) LogMessage("IsShortEntryConditionMet(): Short trading is disabled", "ENTRY_DETAIL");
      if (!timersOk) LogMessage("IsShortEntryConditionMet(): Outside trading hours", "ENTRY_DETAIL");
      if (!pnlOk) LogMessage($"IsShortEntryConditionMet(): PnL limits exceeded (Daily PnL: {dailyPnL:C})", "ENTRY_DETAIL");
      if (!flatOk) LogMessage("IsShortEntryConditionMet(): Not flat - position already exists", "ENTRY_DETAIL");
      if (!trendOk) LogMessage("IsShortEntryConditionMet(): Downtrend condition not met", "ENTRY_DETAIL");
      if (!drawdownOk) LogMessage("IsShortEntryConditionMet(): Trailing drawdown limit reached", "ENTRY_DETAIL");
      if (!barsSinceExitOk) LogMessage("IsShortEntryConditionMet(): Not enough bars since last exit", "ENTRY_DETAIL");
      if (!tradesPerDirOk) LogMessage($"IsShortEntryConditionMet(): Max trades per direction reached (Counter: {counterShort}, Max: {shortPerDirection})", "ENTRY_DETAIL");

      return validateShort && shortEnabled && timersOk && pnlOk && flatOk && trendOk && drawdownOk && barsSinceExitOk && tradesPerDirOk;
    }

    #endregion

    #region Entry Execution

    private void EnterLongPosition()
    {
      // Double-check delay here as well for extra safety
      if (DateTime.Now - lastEntryTime < TradeDelay)
      {
        LogMessage("EnterLongPosition(): Trade delay still active", "ENTRY_DELAY");
        return;
      }

      LogMessage($"EnterLongPosition(): Submitting long entry orders...", "ENTRY");
      counterLong += 1;
      counterShort = 0;
      string primaryLabel = LE;

      // --- 1. Submit Base Entry Order ---
      Order baseOrder = SubmitEntryOrder(primaryLabel, OrderType, Contracts);
      if (baseOrder == null)
      {
        LogMessage($"EnterLongPosition(): Failed to submit base long entry order {primaryLabel}. Aborting entry sequence.", "ENTRY_ERROR");
        counterLong -= 1;
        return;
      }
      Draw.Dot(this, primaryLabel + Convert.ToString(CurrentBars[0]), false, 0, (Close[0]), Brushes.Cyan);
      lastEntryTime = DateTime.Now;  // Update timestamp AFTER successful order submission
      LogMessage($"EnterLongPosition(): Submitted base long entry: {primaryLabel}", "ENTRY");

      // --- 2. Submit Scale-In Entry Orders ---
      EnterMultipleLongContracts(false);
    }

    private void EnterShortPosition()
    {
      // Double-check delay here as well for extra safety
      if (DateTime.Now - lastEntryTime < TradeDelay)
      {
        LogMessage("EnterShortPosition(): Trade delay still active", "ENTRY_DELAY");
        return;
      }

      LogMessage($"EnterShortPosition(): Submitting short entry orders...", "ENTRY");
      counterLong = 0;
      counterShort += 1;
      string primaryLabel = SE;

      // --- 1. Submit Base Entry Order ---
      Order baseOrder = SubmitEntryOrder(primaryLabel, OrderType, Contracts);
      if (baseOrder == null)
      {
        LogMessage($"EnterShortPosition(): Failed to submit base short entry order {primaryLabel}. Aborting entry sequence.", "ENTRY_ERROR");
        counterShort -= 1;
        return;
      }
      Draw.Dot(this, primaryLabel + Convert.ToString(CurrentBars[0]), false, 0, (Close[0]), Brushes.Yellow);
      lastEntryTime = DateTime.Now;  // Update timestamp AFTER successful order submission
      LogMessage($"EnterShortPosition(): Submitted base short entry: {primaryLabel}", "ENTRY");

      // --- 2. Submit Scale-In Entry Orders ---
      EnterMultipleShortContracts(false);
    }

    #endregion

    #region Order Submission Helpers

    // This method encapsulates all order submissions and error handling.
    private Order SubmitEntryOrder(string orderLabel, OrderType orderType, int contracts)
    {
      Order submittedOrder = null;

      lock (orderLock)
      {
        LogMessage($"SubmitEntryOrder(): Starting submission for {orderLabel}, Type: {orderType}, Contracts: {contracts}", "ORDER");



        if (!CanSubmitOrder())
        {
          LogMessage($"SubmitEntryOrder(): Cannot submit {orderLabel} order - Minimum order interval not met", "ORDER_ERROR");
          return null;
        }

        try
        {
          double limitPrice = 0;
          if (orderType == OrderType.Limit || orderType == OrderType.MIT || orderType == OrderType.StopLimit)
          {
            if (orderLabel.StartsWith("LE"))
              limitPrice = GetCurrentBid() - LimitOffset * TickSize;
            else if (orderLabel.StartsWith("SE"))
              limitPrice = GetCurrentAsk() + LimitOffset * TickSize;
            LogMessage($"SubmitEntryOrder(): Calculated limit price: {limitPrice:F2} for {orderLabel}", "ORDER_DETAIL");
          }

          switch (orderType)
          {
            case OrderType.Market:
              if (orderLabel.StartsWith("LE"))
              {
                submittedOrder = EnterLong(contracts, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted Market Long entry for {contracts} contracts", "ORDER");
              }
              else if (orderLabel.StartsWith("SE"))
              {
                submittedOrder = EnterShort(contracts, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted Market Short entry for {contracts} contracts", "ORDER");
              }
              break;

            case OrderType.Limit:
              if (orderLabel.StartsWith("LE"))
              {
                submittedOrder = EnterLongLimit(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted Limit Long entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              else if (orderLabel.StartsWith("SE"))
              {
                submittedOrder = EnterShortLimit(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted Limit Short entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              break;

            case OrderType.MIT:
              if (orderLabel.StartsWith("LE"))
              {
                submittedOrder = EnterLongMIT(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted MIT Long entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              else if (orderLabel.StartsWith("SE"))
              {
                submittedOrder = EnterShortMIT(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted MIT Short entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              break;

            case OrderType.StopLimit:
              if (orderLabel.StartsWith("LE"))
              {
                submittedOrder = EnterLongLimit(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted StopLimit Long entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              else if (orderLabel.StartsWith("SE"))
              {
                submittedOrder = EnterShortLimit(contracts, limitPrice, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted StopLimit Short entry at {limitPrice:F2} for {contracts} contracts", "ORDER");
              }
              break;

            case OrderType.StopMarket:
              if (orderLabel.StartsWith("LE"))
              {
                submittedOrder = EnterLong(contracts, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted StopMarket Long entry for {contracts} contracts", "ORDER");
              }
              else if (orderLabel.StartsWith("SE"))
              {
                submittedOrder = EnterShort(contracts, orderLabel);
                LogMessage($"SubmitEntryOrder(): Submitted StopMarket Short entry for {contracts} contracts", "ORDER");
              }
              break;

            default:
              LogMessage($"SubmitEntryOrder(): Unsupported order type: {orderType}", "ORDER_ERROR");
              throw new ArgumentOutOfRangeException(nameof(orderType), orderType, "Unsupported order type");
          }

          if (submittedOrder != null)
          {
            activeOrders[orderLabel] = submittedOrder;  // Track the order
            lastOrderActionTime = DateTime.Now;
            LogMessage($"SubmitEntryOrder(): Successfully tracked order - Label: {orderLabel}, OrderId: {submittedOrder.OrderId}", "ORDER");
          }
          else
          {
            LogMessage($"SubmitEntryOrder(): Error - {orderLabel} Entry order was null after submission", "ORDER_ERROR");
            orderErrorOccurred = true;
          }
        }
        catch (Exception ex)
        {
          LogError($"SubmitEntryOrder(): Error submitting {orderLabel} entry order", ex);
          orderErrorOccurred = true;
        }
      }

      return submittedOrder;
    }

    private void SubmitExitOrder(string orderLabel)
    {
      lock (orderLock)
      {
        LogMessage($"SubmitExitOrder(): Starting exit process for {orderLabel}", "ORDER");

        try
        {
          if (string.IsNullOrEmpty(orderLabel))
          {
            LogMessage("SubmitExitOrder(): Invalid order label (null or empty)", "ORDER_ERROR");
            return;
          }

          // Determine position type and execute appropriate exit
          if (orderLabel.StartsWith("LE"))
          {
            LogMessage($"SubmitExitOrder(): Executing long exit for {orderLabel}", "ORDER");
            ExitLong(orderLabel);
          }
          else if (orderLabel.StartsWith("SE"))
          {
            LogMessage($"SubmitExitOrder(): Executing short exit for {orderLabel}", "ORDER");
            ExitShort(orderLabel);
          }
          else
          {
            LogMessage($"SubmitExitOrder(): Invalid order label format: {orderLabel}", "ORDER_ERROR");
            return;
          }

          // Handle active order cancellation
          if (!activeOrders.ContainsKey(orderLabel))
          {
            LogMessage($"SubmitExitOrder(): No active order found for label {orderLabel}", "ORDER_WARNING");
            return;
          }

          if (activeOrders.TryGetValue(orderLabel, out Order orderToCancel))
          {
            LogMessage($"SubmitExitOrder(): Cancelling active order - Label: {orderLabel}, OrderId: {orderToCancel.OrderId}", "ORDER");
            try
            {
              CancelOrder(orderToCancel);
              activeOrders.Remove(orderLabel);
              LogMessage($"SubmitExitOrder(): Successfully cancelled and removed order tracking for {orderLabel}", "ORDER");
            }
            catch (Exception ex)
            {
              LogError($"SubmitExitOrder(): Error cancelling order {orderLabel}", ex);
            }
          }
        }
        catch (Exception ex)
        {
          LogError($"SubmitExitOrder(): Critical error processing exit for {orderLabel}", ex);
          orderErrorOccurred = true;
        }
      }
    }

    #endregion


    #region Can Submit Order

    // Method to check the minimum interval between order submissions
    private bool CanSubmitOrder()
    {
      return (DateTime.Now - lastOrderActionTime) >= minOrderActionInterval;
    }

    #endregion

    #region OnExecutionUpdate

    // ***** MODIFIED SECTION *****
    protected override void OnExecutionUpdate(Execution execution, string executionId, double price,
                                       int quantity, MarketPosition marketPosition, string orderId,
                                       DateTime time)
    {
      // No need to manage speed here anymore - UpdatePositionState will handle it
      LogMessage($"OnExecutionUpdate(): Received execution update - ID: {executionId}, OrderId: {orderId}, Price: {price}, Qty: {quantity}, Position: {marketPosition}, Time: {time}", "EXECUTION");

      if (execution == null || execution.Order == null)
      {
        LogWarning($"OnExecutionUpdate(): Null execution or order object received for executionId {executionId}");
        return;
      }

      // Process base class OnExecutionUpdate FIRST if it exists (unlikely here but good practice)
      // base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);

      // Check if position is flat and reset profit targets flag
      if (Position.MarketPosition == MarketPosition.Flat)
      {
        profitTargetsSet = false;  // Reset flag when position is closed
        LogMessage("OnExecutionUpdate(): Position flat - Reset profit target flag", "EXECUTION");
      }

      // --- Handle Fills for Stop/Target Placement ---
      if (execution.Order.OrderState == OrderState.Filled)
      {
        string fromEntrySignal = execution.Order.FromEntrySignal; // Get the label of the filled entry
        LogMessage($"OnExecutionUpdate(): Order {orderId} FILLED - Label: {fromEntrySignal}, Price: {price}, Quantity: {quantity}", "EXECUTION");

        // Check if the filled order's label corresponds to one of our ENTRY signals
        bool isEntryFill = !string.IsNullOrEmpty(fromEntrySignal) &&
                           (fromEntrySignal.StartsWith(LE) || fromEntrySignal.StartsWith(SE));

        if (isEntryFill)
        {
          LogMessage($"OnExecutionUpdate(): Entry fill detected for {fromEntrySignal} - Proceeding with stop/target placement", "EXECUTION");

          // Use a lock to prevent race conditions if multiple fills come in rapidly (unlikely but safe)
          lock (orderLock)
          {
            try
            {
              // --- Place Initial Stop Loss based on this specific fill ---
              LogMessage($"OnExecutionUpdate(): Setting initial stop loss for {fromEntrySignal}", "EXECUTION");
              SetInitialStopLossOnFill(fromEntrySignal, price);

              // --- Place Profit Target(s) based on this specific fill ---
              LogMessage($"OnExecutionUpdate(): Setting profit target(s) for {fromEntrySignal}", "EXECUTION");
              SetInitialProfitTargetOnFill(fromEntrySignal, price);
            }
            catch (Exception ex)
            {
              LogError($"OnExecutionUpdate(): Critical error in stop/target placement for {fromEntrySignal}", ex);
              orderErrorOccurred = true; // Halt strategy if placement fails critically
            }
          }
        }
        else
        {
          LogMessage($"OnExecutionUpdate(): Non-entry fill detected - Label: {fromEntrySignal}", "EXECUTION");
        }
      }

      // --- Handle Existing Order Tracking Logic ---
      lock (orderLock)
      {
        string orderLabel = activeOrders.FirstOrDefault(x => x.Value.OrderId == orderId).Key;

        if (!string.IsNullOrEmpty(orderLabel))
        {
          LogMessage($"OnExecutionUpdate(): Processing tracked order - Label: {orderLabel}, State: {execution.Order.OrderState}", "EXECUTION");

          switch (execution.Order.OrderState)
          {
            case OrderState.Filled:
              LogMessage($"OnExecutionUpdate(): Order {orderId} ({orderLabel}) filled - Removing from active tracking", "EXECUTION");
              activeOrders.Remove(orderLabel);
              break;
            case OrderState.Cancelled:
              LogMessage($"OnExecutionUpdate(): Order {orderId} ({orderLabel}) cancelled - Removing from active tracking", "EXECUTION");
              activeOrders.Remove(orderLabel);
              break;
            case OrderState.Rejected:
              LogMessage($"OnExecutionUpdate(): Order {orderId} ({orderLabel}) rejected - Removing from active tracking", "EXECUTION");
              activeOrders.Remove(orderLabel);
              break;
            default:
              LogMessage($"OnExecutionUpdate(): Order {orderId} ({orderLabel}) state updated to {execution.Order.OrderState}", "EXECUTION");
              break;
          }
        }
        else if (execution.Order.OrderState != OrderState.Unknown && !execution.Order.IsLimit && !execution.Order.IsStopMarket)
        {
          // Log execution updates for orders NOT tracked by our entry dictionary
          LogMessage($"OnExecutionUpdate(): Untracked order update - ID: {orderId}, Name: {execution.Order.Name}, State: {execution.Order.OrderState}, Price: {price}, Qty: {quantity}", "EXECUTION");
        }
      }

      // Add after base call
      base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);
    }

    #endregion

    // ***** NEW HELPER METHODS for OnExecutionUpdate *****

    #region Initial Stop/Target Placement on Fill

    // Places the initial stop loss immediately after an entry fill
    private void SetInitialStopLossOnFill(string filledEntrySignal, double executionPrice)
    {
      LogMessage($"SetInitialStopLossOnFill(): Starting for signal {filledEntrySignal} at execution price {executionPrice:F5}", "STOP_PLACEMENT");

      if (Position.MarketPosition == MarketPosition.Flat)
      {
        LogWarning($"SetInitialStopLossOnFill(): Cannot set initial stop for {filledEntrySignal}, position reported flat immediately after fill.");
        return;
      }

      if (TickSize <= 0)
      {
        LogError($"SetInitialStopLossOnFill(): Cannot set initial stop for {filledEntrySignal}. Invalid TickSize: {TickSize}");
        return;
      }

      // --- Check for Override Condition ---
      int effectiveInitialStopTicks = InitialStop;
      LogMessage($"SetInitialStopLossOnFill(): Using initial stop of {effectiveInitialStopTicks} ticks", "STOP_PLACEMENT");

      // --- Calculate Initial Stop Price using executionPrice ---
      double initialStopPrice = (Position.MarketPosition == MarketPosition.Long)
                               ? executionPrice - (effectiveInitialStopTicks * TickSize)
                               : executionPrice + (effectiveInitialStopTicks * TickSize);

      LogMessage($"SetInitialStopLossOnFill(): Calculated initial stop price: {initialStopPrice:F5} for {filledEntrySignal} (Execution: {executionPrice:F5}, Ticks: {effectiveInitialStopTicks})", "STOP_PLACEMENT");

      // --- Validate Stop Price ---
      if (!IsValidStopPlacement(initialStopPrice, Position.MarketPosition))
      {
        LogWarning($"SetInitialStopLossOnFill(): Initial stop placement {initialStopPrice:F5} is invalid for '{filledEntrySignal}'. Skipping placement.");
        return;
      }

      // --- Apply Stop using Explicit Exit Order ---
      try
      {
        SetFixedStopLoss(filledEntrySignal, CalculationMode.Price, initialStopPrice, false);
        string modeInfo = enableTrail ? "(Will Trail)" : "(Fixed)";
        LogMessage($"SetInitialStopLossOnFill(): Successfully applied initial stop at {initialStopPrice:F5} {modeInfo} for {filledEntrySignal}", "STOP_PLACEMENT");
      }
      catch (Exception ex)
      {
        LogError($"SetInitialStopLossOnFill(): Failed to set initial stop for {filledEntrySignal}", ex);
        orderErrorOccurred = true;
      }
    }

    // Places the initial profit target(s) immediately after an entry fill
    private void SetInitialProfitTargetOnFill(string filledEntrySignal, double executionPrice)
    {
      LogMessage($"SetInitialProfitTargetOnFill(): Starting for signal {filledEntrySignal} at execution price {executionPrice:F5}", "TARGET_PLACEMENT");

      if (Position.MarketPosition == MarketPosition.Flat)
      {
        LogWarning($"SetInitialProfitTargetOnFill(): Cannot set profit targets, position is flat");
        return;
      }

      if (TickSize <= 0)
      {
        LogError($"SetInitialProfitTargetOnFill(): Cannot set profit targets. Invalid TickSize: {TickSize}");
        return;
      }

      // Determine the base label and scaled labels based on the filled signal
      string baseLabel = "";
      string labelPrefix = "";

      if (filledEntrySignal.StartsWith(LE))
      {
        baseLabel = LE;
        labelPrefix = LE;
        LogMessage($"SetInitialProfitTargetOnFill(): Processing LONG entry signal with base label {baseLabel}", "TARGET_PLACEMENT");
      }
      else if (filledEntrySignal.StartsWith(SE))
      {
        baseLabel = SE;
        labelPrefix = SE;
        LogMessage($"SetInitialProfitTargetOnFill(): Processing SHORT entry signal with base label {baseLabel}", "TARGET_PLACEMENT");
      }
      else
      {
        LogWarning($"SetInitialProfitTargetOnFill(): Unknown entry signal type: {filledEntrySignal}");
        return;
      }

      if (EnableFixedProfitTarget)
      {
        LogMessage($"SetInitialProfitTargetOnFill(): Setting fixed profit targets for {baseLabel}", "TARGET_PLACEMENT");
        SetFixedProfitTargetsOnFill(executionPrice, baseLabel, labelPrefix);
      }
      else
      {
        LogMessage($"SetInitialProfitTargetOnFill(): Fixed profit targets not enabled, skipping target placement", "TARGET_PLACEMENT");
      }
    }

    // Helper specifically for setting fixed targets based on execution price
    private void SetFixedProfitTargetsOnFill(double execPrice, string baseLbl, string prefix)
    {
      LogMessage($"SetFixedProfitTargetsOnFill(): Starting target placement for {baseLbl} at execution price {execPrice:F5}", "TARGET_PLACEMENT");

      try
      {
        // Apply base target
        if (ProfitTarget > 0)
        {
          LogMessage($"SetFixedProfitTargetsOnFill(): Setting base target for {baseLbl} at {ProfitTarget} ticks", "TARGET_PLACEMENT");
          SetProfitTarget(baseLbl, CalculationMode.Ticks, ProfitTarget);
        }

        // Apply scaled targets if enabled
        if (EnableProfitTarget2 && ProfitTarget2 > 0)
        {
          LogMessage($"SetFixedProfitTargetsOnFill(): Setting target 2 for {prefix}2 at {ProfitTarget2} ticks", "TARGET_PLACEMENT");
          SetProfitTarget(prefix + "2", CalculationMode.Ticks, ProfitTarget2);
        }

        if (EnableProfitTarget3 && ProfitTarget3 > 0)
        {
          LogMessage($"SetFixedProfitTargetsOnFill(): Setting target 3 for {prefix}3 at {ProfitTarget3} ticks", "TARGET_PLACEMENT");
          SetProfitTarget(prefix + "3", CalculationMode.Ticks, ProfitTarget3);
        }

        if (EnableProfitTarget4 && ProfitTarget4 > 0)
        {
          LogMessage($"SetFixedProfitTargetsOnFill(): Setting target 4 for {prefix}4 at {ProfitTarget4} ticks", "TARGET_PLACEMENT");
          SetProfitTarget(prefix + "4", CalculationMode.Ticks, ProfitTarget4);
        }

        LogMessage($"SetFixedProfitTargetsOnFill(): Successfully completed profit target placement for {baseLbl}", "TARGET_PLACEMENT");
      }
      catch (Exception ex)
      {
        LogError($"SetFixedProfitTargetsOnFill(): Error setting fixed profit targets for {baseLbl}", ex);
        orderErrorOccurred = true;
      }
    }


    #endregion

    // ***** END NEW HELPER METHODS *****

    private void EnterMultipleLongContracts(bool isManual)
    {
      if (enableFixedProfitTarget)
      {
        LogMessage($"EnterMultipleLongContracts(): Starting manual long entry process", "ENTRY");
        EnterMultipleOrders(true, EnableProfitTarget2, @LE2, Contracts2);
        EnterMultipleOrders(true, EnableProfitTarget3, @LE3, Contracts3);
        EnterMultipleOrders(true, EnableProfitTarget4, @LE4, Contracts4);

      }
    }

    private void EnterMultipleShortContracts(bool isManual)
    {
      if (enableFixedProfitTarget)
      {
        LogMessage($"EnterMultipleShortContracts(): Starting manual short entry process", "ENTRY");
        EnterMultipleOrders(false, EnableProfitTarget2, @SE2, Contracts2);
        EnterMultipleOrders(false, EnableProfitTarget3, @SE3, Contracts3);
        EnterMultipleOrders(false, EnableProfitTarget4, @SE4, Contracts4);

      }
    }

    private void EnterMultipleOrders(bool isLong, bool isEnableTarget, string signalName, int contracts)
    {
      LogMessage($"EnterMultipleOrders(): Starting entry process for {signalName} with {contracts} contracts, {(isEnableTarget ? "with profit targets" : "without profit targets")}, isLong: {isLong}", "ENTRY");
      if (isEnableTarget)
      {
        if (isLong)
        {
          if (OrderType == OrderType.Market)
            EnterLong(Convert.ToInt32(contracts), signalName);
          else if (OrderType == OrderType.Limit)
            EnterLongLimit(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.MIT)
            EnterLongMIT(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.StopLimit)
            EnterLongLimit(Convert.ToInt32(contracts), GetCurrentBid() - LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.StopMarket)
            EnterLong(Convert.ToInt32(contracts), signalName);
        }
        else
        {
          if (OrderType == OrderType.Market)
            EnterShort(Convert.ToInt32(contracts), signalName);
          else if (OrderType == OrderType.Limit)
            EnterShortLimit(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.MIT)
            EnterShortMIT(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.StopLimit)
            EnterShortLimit(Convert.ToInt32(contracts), GetCurrentAsk() + LimitOffset * TickSize, signalName);
          else if (OrderType == OrderType.StopMarket)
            EnterShort(Convert.ToInt32(contracts), signalName);
        }
      }
    }



    #region Set Profit Targets

    private void SetProfitTargets()
    {
      // --- Exit if flat ---
      if (isFlat)
      {
        profitTargetsSet = false;  // Reset flag when flat
        LogDebug("SetProfitTargets(): Position is flat, skipping profit target setup");
        return;
      }

      // If targets are already set, skip
      if (profitTargetsSet)
      {
        LogDebug("SetProfitTargets(): Profit targets already set, skipping");
        return;
      }

      // --- Get Entry Price and Validate ---
      double entryPrice = Position.AveragePrice;
      if (entryPrice == 0 || TickSize <= 0)
      {
        if (entryPrice == 0)
          LogWarning("SetProfitTargets(): Cannot set targets - Position.AveragePrice is 0");
        if (TickSize <= 0)
          LogWarning($"SetProfitTargets(): Cannot set targets - Invalid TickSize: {TickSize}");
        return;
      }

      LogMessage($"SetProfitTargets(): Starting profit target setup - Entry Price: {entryPrice:F5}, Direction: {Position.MarketPosition}", "TARGET_MANAGEMENT");

      // === Route based on active Profit Target Mode ===
      if (EnableFixedProfitTarget)
      {
        LogMessage("SetProfitTargets(): Using Fixed Profit Target mode", "TARGET_MANAGEMENT");
        SetFixedProfitTargets(); // Call helper for Fixed logic
        profitTargetsSet = true;  // Mark targets as set
      }
      else
      {
        LogMessage("SetProfitTargets(): No profit target mode enabled", "TARGET_MANAGEMENT");
      }
    }

    protected void ResetProfitTargets()
    {
      profitTargetsSet = false;
      LogMessage("ResetProfitTargets(): Profit target flag reset - Will recalculate targets on next update", "TARGET_MANAGEMENT");
    }

    private void SetFixedProfitTargets()
    {
      try
      {
        LogMessage($"SetFixedProfitTargets(): Starting fixed target setup for {(isLong ? "LONG" : "SHORT")} position", "TARGET_MANAGEMENT");

        // Apply base target first
        string baseLabel = isLong ? (LE) : (SE);
        SetProfitTargetForLabel(baseLabel, ProfitTarget, true);
        LogMessage($"SetFixedProfitTargets(): Base target set for {baseLabel} at {ProfitTarget} ticks", "TARGET_MANAGEMENT");

        // Apply scaled targets if enabled
        string prefix = isLong ? (LE.Substring(0, 2)) : (SE.Substring(0, 2)); // LE or SE

        if (EnableProfitTarget2)
        {
          LogMessage($"SetFixedProfitTargets(): Setting target 2 for {prefix}2", "TARGET_MANAGEMENT");
          SetProfitTargetForLabel(prefix + "2", ProfitTarget2, EnableProfitTarget2);
        }

        if (EnableProfitTarget3)
        {
          LogMessage($"SetFixedProfitTargets(): Setting target 3 for {prefix}3", "TARGET_MANAGEMENT");
          SetProfitTargetForLabel(prefix + "3", ProfitTarget3, EnableProfitTarget3);
        }

        if (EnableProfitTarget4)
        {
          LogMessage($"SetFixedProfitTargets(): Setting target 4 for {prefix}4", "TARGET_MANAGEMENT");
          SetProfitTargetForLabel(prefix + "4", ProfitTarget4, EnableProfitTarget4);
        }

        LogMessage("SetFixedProfitTargets(): Completed fixed profit target setup", "TARGET_MANAGEMENT");
      }
      catch (Exception ex)
      {
        LogError("SetFixedProfitTargets(): Error setting fixed profit targets", ex);
        orderErrorOccurred = true;
      }
    }

    private void SetProfitTargetForLabel(string label, double profitTargetTicks, bool isEnabled)
    {
      if (!isEnabled)
      {
        LogDebug($"SetProfitTargetForLabel(): Target disabled for {label}, skipping");
        return;
      }

      if (profitTargetTicks <= 0)
      {
        LogWarning($"SetProfitTargetForLabel(): Invalid profit target ticks ({profitTargetTicks}) for {label}, skipping");
        return;
      }

      try
      {
        LogMessage($"SetProfitTargetForLabel(): Setting {profitTargetTicks} tick target for {label}", "TARGET_MANAGEMENT");
        SetProfitTarget(label, CalculationMode.Ticks, profitTargetTicks);
        LogMessage($"SetProfitTargetForLabel(): Successfully set target for {label}", "TARGET_MANAGEMENT");
      }
      catch (Exception ex)
      {
        HandleSetTargetError(label, "Fixed/Dynamic", ex);
      }
    }

    private void HandleSetTargetError(string label, string type, Exception ex)
    {
      LogError($"HandleSetTargetError(): Failed to set {type} profit target for {label}", ex);

      // Additional error context logging
      if (Position.MarketPosition == MarketPosition.Flat)
      {
        LogWarning($"HandleSetTargetError(): Position is flat while attempting to set target for {label}");
      }
      else
      {
        LogMessage($"HandleSetTargetError(): Error context - Position: {Position.MarketPosition}, Quantity: {Position.Quantity}, Average Price: {Position.AveragePrice:F5}", "ERROR_CONTEXT");
      }

      orderErrorOccurred = true;
    }


    #endregion

    #region Stop Adjustment (Manual Buttons)

    // Adjusts the active trailing stop by a specified number of ticks
    protected void AdjustStopLoss(int tickAdjustment)
    {
      LogMessage($"AdjustStopLoss(): Starting stop adjustment process. Adjustment: {tickAdjustment} ticks", "STOP_ADJUST");

      // --- Pre-checks ---
      if (isFlat)
      {
        LogMessage("AdjustStopLoss(): No active position to adjust stop for", "STOP_ADJUST");
        return;
      }

      // Ensure trailing is conceptually enabled
      if (!enableTrail && !enableFixedStopLoss)
      {
        LogMessage("AdjustStopLoss(): Neither Trailing nor Fixed Stop enabled - cannot move stop", "STOP_ADJUST");
        return;
      }

      if (tickAdjustment <= 0)
      {
        LogMessage($"AdjustStopLoss(): Invalid tick adjustment value ({tickAdjustment}). Must be positive.", "STOP_ADJUST_ERROR");
        return;
      }

      double entryPrice = Position.AveragePrice;
      bool isLong = Position.MarketPosition == MarketPosition.Long;
      double currentMarketPrice = Close[0];

      LogMessage($"AdjustStopLoss(): Current Position State - Direction: {(isLong ? "Long" : "Short")}, Entry: {entryPrice:F2}, Market: {currentMarketPrice:F2}", "STOP_ADJUST");

      // Get Current Stop Price
      double currentStopPrice = lastAdjustedStopPrice;
      if (currentStopPrice == 0)
      {
        Order workingStop = Orders.FirstOrDefault(o => o.OrderState == OrderState.Working && o.IsStopMarket);
        if (workingStop != null)
        {
          currentStopPrice = workingStop.StopPrice;
          LogMessage($"AdjustStopLoss(): Found working stop order at price {currentStopPrice:F2}", "STOP_ADJUST");
        }
        else
        {
          currentStopPrice = isLong ? currentMarketPrice - (InitialStop * TickSize) : currentMarketPrice + (InitialStop * TickSize);
          LogMessage($"AdjustStopLoss(): Inferred stop price: {currentStopPrice:F2}", "STOP_ADJUST");
        }
      }

      // Calculate New Stop Price
      double newTargetStopPrice;
      if (isLong)
      {
        // For long positions, move stop UP by tickAdjustment
        newTargetStopPrice = currentStopPrice + (tickAdjustment * TickSize);
      }
      else // Short position
      {
        // For short positions, move stop DOWN by tickAdjustment
        newTargetStopPrice = currentStopPrice - (tickAdjustment * TickSize);
      }

      // Calculate ticks from market for the new stop
      double ticksFromMarket;
      if (isLong)
      {
        ticksFromMarket = Math.Round((currentMarketPrice - newTargetStopPrice) / TickSize);
      }
      else
      {
        ticksFromMarket = Math.Round((newTargetStopPrice - currentMarketPrice) / TickSize);
      }

      // Sanity Check
      if (ticksFromMarket <= 0)
      {
        LogMessage($"AdjustStopLoss(): Invalid ticks from market ({ticksFromMarket:F1}). Stop would be beyond market price.", "STOP_ADJUST_ERROR");
        return;
      }

      // Validate stop price relative to market
      if (!IsValidStopPlacement(newTargetStopPrice, Position.MarketPosition))
      {
        LogMessage($"AdjustStopLoss(): Invalid stop price {newTargetStopPrice:F2} relative to market {currentMarketPrice:F2}", "STOP_ADJUST_ERROR");
        return;
      }

      // Apply to Relevant Labels using Safe Helper
      List<string> orderLabels = GetRelevantOrderLabels();
      if (orderLabels.Count == 0)
      {
        LogMessage("AdjustStopLoss(): No relevant order labels found to apply adjustment to", "STOP_ADJUST_ERROR");
        return;
      }

      LogMessage($"AdjustStopLoss(): Applying trailing stop {ticksFromMarket:F1} ticks from market to labels: {string.Join(", ", orderLabels)}", "STOP_ADJUST");
      foreach (string label in orderLabels)
      {
        try
        {
          SetTrailingStop(label, CalculationMode.Ticks, ticksFromMarket, true);
          LogMessage($"AdjustStopLoss(): Successfully set trailing stop for label {label}", "STOP_ADJUST");
        }
        catch (Exception ex)
        {
          LogError($"AdjustStopLoss(): Failed to set trailing stop for label {label}", ex);
        }
      }

      // Store the last adjusted stop price for future reference
      lastAdjustedStopPrice = newTargetStopPrice;
      LogMessage($"AdjustStopLoss(): Updated lastAdjustedStopPrice to {lastAdjustedStopPrice:F2}", "STOP_ADJUST");

      ForceRefresh();
      LogMessage("AdjustStopLoss(): Process completed", "STOP_ADJUST");
    }

    #endregion

    #region Move To Breakeven (Manual Buttons) // Keep methods in this region or similar

    // Manually moves the active trailing stop to the Breakeven level (+/- offset)
    protected void MoveToBreakeven()
    {
      LogMessage("MoveToBreakeven(): Starting breakeven move process", "BREAKEVEN");

      // --- Pre-checks ---
      if (isFlat)
      {
        LogMessage("MoveToBreakeven(): No active position to move to breakeven", "BREAKEVEN");
        return;
      }
      // Ensure trailing is conceptually enabled, otherwise BE doesn't make sense
      if (!enableTrail && !enableFixedStopLoss)
      {
        LogMessage("MoveToBreakeven(): Neither Trailing nor Fixed Stop enabled - cannot move to breakeven", "BREAKEVEN");
        return;
      }

      // Get Current State
      double entryPrice = Position.AveragePrice;
      if (entryPrice == 0)
      {
        LogMessage("MoveToBreakeven(): Cannot adjust, entry price is 0", "BREAKEVEN");
        return;
      }

      bool isLong = Position.MarketPosition == MarketPosition.Long;
      double currentMarketPrice = Close[0];
      double currentUnrealizedPnlTicks = Position.GetUnrealizedProfitLoss(PerformanceUnit.Ticks, Close[0]);

      LogMessage($"MoveToBreakeven(): Current Position State - Direction: {(isLong ? "Long" : "Short")}, Entry: {entryPrice:F2}, Market: {currentMarketPrice:F2}, PnL Ticks: {currentUnrealizedPnlTicks:F1}", "BREAKEVEN");

      // Calculate Target Breakeven Stop Price
      double offsetPriceAdjustment = BE_Offset * TickSize;
      double targetBreakevenStopPrice = entryPrice + (isLong ? offsetPriceAdjustment : -offsetPriceAdjustment);

      LogMessage($"MoveToBreakeven(): Target BE Stop Price: {targetBreakevenStopPrice:F2} (Entry: {entryPrice:F2}, Offset: {BE_Offset} ticks)", "BREAKEVEN");

      // Validate New Stop Price
      if ((isLong && targetBreakevenStopPrice >= currentMarketPrice) ||
          (!isLong && targetBreakevenStopPrice <= currentMarketPrice))
      {
        LogMessage($"MoveToBreakeven(): Cannot move stop. Target BE price {targetBreakevenStopPrice:F2} invalid relative to current market price {currentMarketPrice:F2}. Position might not be profitable enough.", "BREAKEVEN_ERROR");
        return;
      }

      if (currentUnrealizedPnlTicks < BE_Offset)
      {
        LogMessage($"MoveToBreakeven(): Position not sufficiently profitable (PnL Ticks: {currentUnrealizedPnlTicks:F1} < Offset: {BE_Offset})", "BREAKEVEN_ERROR");
        return;
      }

      // Determine if breakeven conditions are met
      if (currentUnrealizedPnlTicks >= BE_Offset)
      {
        // Calculate the distance in ticks from current market price to our target stop
        double ticksFromMarket;
        if (isLong)
        {
          ticksFromMarket = Math.Round((currentMarketPrice - targetBreakevenStopPrice) / TickSize);
        }
        else
        {
          ticksFromMarket = Math.Round((targetBreakevenStopPrice - currentMarketPrice) / TickSize);
        }

        LogMessage($"MoveToBreakeven(): Calculated ticks from market to stop: {ticksFromMarket:F1}", "BREAKEVEN");

        // Sanity Check
        if (ticksFromMarket <= 0)
        {
          LogMessage($"MoveToBreakeven(): Invalid ticks from market ({ticksFromMarket:F1}). Stop would be beyond market price.", "BREAKEVEN_ERROR");
          return;
        }

        // Apply to Relevant Labels using Safe Helper
        List<string> orderLabels = GetRelevantOrderLabels();
        if (orderLabels.Count == 0)
        {
          LogMessage("MoveToBreakeven(): No relevant order labels found to apply adjustment to", "BREAKEVEN_ERROR");
          return;
        }

        LogMessage($"MoveToBreakeven(): Applying trailing stop {ticksFromMarket:F1} ticks from market to labels: {string.Join(", ", orderLabels)}", "BREAKEVEN");
        foreach (string label in orderLabels)
        {
          try
          {
            SetTrailingStop(label, CalculationMode.Ticks, ticksFromMarket, true);
            LogMessage($"MoveToBreakeven(): Successfully set trailing stop for label {label}", "BREAKEVEN");
          }
          catch (Exception ex)
          {
            LogError($"MoveToBreakeven(): Failed to set trailing stop for label {label}", ex);
          }
        }

        // Mark breakeven as realized if using the flag for logic elsewhere
        _beRealized = true;
        LogMessage("MoveToBreakeven(): Manual Breakeven applied successfully. _beRealized set to true", "BREAKEVEN");
      }

      ForceRefresh(); // Refresh chart UI
      LogMessage("MoveToBreakeven(): Process completed", "BREAKEVEN");
    }

    #endregion

    #region Move Trail Stop 50%
    // Manually moves the active trailing stop closer to the current price by a percentage
    protected void MoveTrailingStopByPercentage(double percentage)
    {
      LogMessage($"MoveTrailingStopByPercentage(): Starting stop adjustment process. Percentage: {percentage:P1}", "STOP_PERCENT");

      if (percentage <= 0 || percentage >= 1)
      {
        LogMessage($"MoveTrailingStopByPercentage(): Invalid percentage ({percentage:P1}). Must be between 0% and 100%", "STOP_PERCENT_ERROR");
        return;
      }

      if (isFlat)
      {
        LogMessage("MoveTrailingStopByPercentage(): No active position to adjust stop for", "STOP_PERCENT");
        return;
      }

      // Ensure trailing is conceptually enabled
      if (!enableTrail && !enableFixedStopLoss)
      {
        LogMessage("MoveTrailingStopByPercentage(): Neither Trailing nor Fixed Stop enabled - cannot move stop", "STOP_PERCENT");
        return;
      }

      double entryPrice = Position.AveragePrice;
      bool isLong = Position.MarketPosition == MarketPosition.Long;
      double currentMarketPrice = Close[0];

      LogMessage($"MoveTrailingStopByPercentage(): Current Position State - Direction: {(isLong ? "Long" : "Short")}, Entry: {entryPrice:F2}, Market: {currentMarketPrice:F2}", "STOP_PERCENT");

      // Get Current Stop Price
      double currentStopPrice = lastAdjustedStopPrice;
      if (currentStopPrice == 0)
      {
        Order workingStop = Orders.FirstOrDefault(o => o.OrderState == OrderState.Working && o.IsStopMarket);
        if (workingStop != null)
        {
          currentStopPrice = workingStop.StopPrice;
          LogMessage($"MoveTrailingStopByPercentage(): Found working stop order at price {currentStopPrice:F2}", "STOP_PERCENT");
        }
        else
        {
          currentStopPrice = isLong ? currentMarketPrice - (InitialStop * TickSize) : currentMarketPrice + (InitialStop * TickSize);
          LogMessage($"MoveTrailingStopByPercentage(): Inferred stop price: {currentStopPrice:F2}", "STOP_PERCENT");
        }
      }

      // Calculate New Stop Price
      double distance = Math.Abs(currentMarketPrice - currentStopPrice);
      double moveAmount = distance * percentage;
      double newTargetStopPrice = isLong
          ? currentStopPrice + moveAmount // Move towards market
          : currentStopPrice - moveAmount; // Move towards market

      LogMessage($"MoveTrailingStopByPercentage(): Distance to market: {distance:F2}, Move amount: {moveAmount:F2}, New target: {newTargetStopPrice:F2}", "STOP_PERCENT");

      // Calculate ticks from market for the new stop
      double ticksFromMarket;
      if (isLong)
      {
        ticksFromMarket = Math.Round((currentMarketPrice - newTargetStopPrice) / TickSize);
      }
      else
      {
        ticksFromMarket = Math.Round((newTargetStopPrice - currentMarketPrice) / TickSize);
      }

      // Sanity Check
      if (ticksFromMarket <= 0)
      {
        LogMessage($"MoveTrailingStopByPercentage(): Invalid ticks from market ({ticksFromMarket:F1}). Stop would be beyond market price.", "STOP_PERCENT_ERROR");
        return;
      }

      // Apply to Relevant Labels using Safe Helper
      List<string> orderLabels = GetRelevantOrderLabels();
      if (orderLabels.Count == 0)
      {
        LogMessage("MoveTrailingStopByPercentage(): No relevant order labels found to apply adjustment to", "STOP_PERCENT_ERROR");
        return;
      }

      LogMessage($"MoveTrailingStopByPercentage(): Applying trailing stop {ticksFromMarket:F1} ticks from market to labels: {string.Join(", ", orderLabels)}", "STOP_PERCENT");
      foreach (string label in orderLabels)
      {
        try
        {
          SetTrailingStop(label, CalculationMode.Ticks, ticksFromMarket, true);
          LogMessage($"MoveTrailingStopByPercentage(): Successfully set trailing stop for label {label}", "STOP_PERCENT");
        }
        catch (Exception ex)
        {
          LogError($"MoveTrailingStopByPercentage(): Failed to set trailing stop for label {label}", ex);
        }
      }

      ForceRefresh();
      LogMessage("MoveTrailingStopByPercentage(): Process completed", "STOP_PERCENT");
    }

    #endregion // End Stop Adjustment region

    #region Button Definitions

    private List<ButtonDefinition> buttonDefinitions;

    private class ButtonDefinition
    {
      public string Name { get; set; }
      public string Content { get; set; }
      public string ToolTip { get; set; }
      public Action<KCAlgoBase, System.Windows.Controls.Button> InitialDecoration { get; set; }
      public Action<KCAlgoBase> ClickAction { get; set; } // Action to perform when clicked
    }

    private void InitializeButtonDefinitions()
    {
      buttonDefinitions = new List<ButtonDefinition>
      {
        new ButtonDefinition
        {
            Name = AutoButton,
            Content = "\uD83D\uDD12 Auto On",
            ToolTip = "Enable (Green) / Disbled (Red) Auto Button",
            InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off"),
            ClickAction = (strategy) =>
            {
                strategy.LogMessage($"Auto Button Clicked - Current State: Auto={strategy.isAutoEnabled}, Manual={strategy.isManualEnabled}", "BUTTON_CLICK");
                strategy.isAutoEnabled = !strategy.isAutoEnabled;
                strategy.isManualEnabled = !strategy.isManualEnabled;
                strategy.autoDisabledByChop = false; // User took control, clear the system flag
				        strategy.DecorateButton(strategy.autoBtn, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
                strategy.DecorateButton(strategy.manualBtn, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
                strategy.LogMessage($"Auto Button State Changed - New State: Auto={strategy.isAutoEnabled}, Manual={strategy.isManualEnabled}", "BUTTON_CLICK");
            }
        },
        new ButtonDefinition
        {
            Name = ManualButton,
            Content = "\uD83D\uDD12 Manual On",
            ToolTip = "Enable (Green) / Disbled (Red) Manual Button",
            InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off"),
            ClickAction = (strategy) =>
            {
                strategy.LogMessage($"Manual Button Clicked - Current State: Manual={strategy.isManualEnabled}, Auto={strategy.isAutoEnabled}", "BUTTON_CLICK");
                strategy.isManualEnabled = !strategy.isManualEnabled;
                strategy.isAutoEnabled = !strategy.isAutoEnabled;
                strategy.autoDisabledByChop = false; // User took control, clear the system flag
				        strategy.DecorateButton(strategy.manualBtn, strategy.isManualEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Manual On", "\uD83D\uDD13 Manual Off");
                strategy.DecorateButton(strategy.autoBtn, strategy.isAutoEnabled ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 Auto On", "\uD83D\uDD13 Auto Off");
                strategy.LogMessage($"Manual Button State Changed - New State: Manual={strategy.isManualEnabled}, Auto={strategy.isAutoEnabled}", "BUTTON_CLICK");
            }
        },
        new ButtonDefinition
        {
          Name = LongButton,
          Content = "LONG",
          ToolTip = "Enable (Green) / Disbled (Red) Auto Long Entry",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isLongEnabled ? ButtonState.Enabled : ButtonState.Disabled, "LONG", "LONG Off"),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage($"Long Button Clicked - Current State: Long={strategy.isLongEnabled}", "BUTTON_CLICK");
            strategy.isLongEnabled = !strategy.isLongEnabled;
            strategy.DecorateButton(strategy.longBtn, strategy.isLongEnabled ? ButtonState.Enabled : ButtonState.Disabled, "LONG", "LONG Off");
            strategy.LogMessage($"Long Button State Changed - New State: Long={strategy.isLongEnabled}", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = ShortButton,
          Content = "SHORT",
          ToolTip = "Enable (Green) / Disbled (Red) Auto Short Entry",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.isShortEnabled ? ButtonState.Enabled : ButtonState.Disabled, "SHORT", "SHORT Off"),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage($"Short Button Clicked - Current State: Short={strategy.isShortEnabled}", "BUTTON_CLICK");
            strategy.isShortEnabled = !strategy.isShortEnabled;
            strategy.DecorateButton(strategy.shortBtn, strategy.isShortEnabled ? ButtonState.Enabled : ButtonState.Disabled, "SHORT", "SHORT Off");
            strategy.LogMessage($"Short Button State Changed - New State: Short={strategy.isShortEnabled}", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = BEButton,
          Content = "\uD83D\uDD12 BE On",
          ToolTip = "Enable (Green) / Disbled (Red) Auto BE",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.beSetAuto ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 BE On", "\uD83D\uDD13 BE Off"),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage($"BE Button Clicked - Current State: BE={strategy.beSetAuto}", "BUTTON_CLICK");
            strategy.beSetAuto = !strategy.beSetAuto;
            strategy.DecorateButton(strategy.BEBtn, strategy.beSetAuto ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 BE On", "\uD83D\uDD13 BE Off");
            strategy.LogMessage($"BE Button State Changed - New State: BE={strategy.beSetAuto}", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = TSButton,
          Content = "\uD83D\uDD12 TS On",
          ToolTip = "Enable (Green) / Disbled (Red) Auto TS",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, strategy.enableTrail ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 TS On", "\uD83D\uDD13 TS Off"),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage($"TS Button Clicked - Current State: Trail={strategy.enableTrail}", "BUTTON_CLICK");
            strategy.enableTrail = !strategy.enableTrail;
            strategy.DecorateButton(strategy.TSBtn, strategy.enableTrail ? ButtonState.Enabled : ButtonState.Disabled, "\uD83D\uDD12 TS On", "\uD83D\uDD13 TS Off");
            strategy.LogMessage($"TS Button State Changed - New State: Trail={strategy.enableTrail}", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = MoveTSButton,
          Content = "Move TS",
          ToolTip = "Increase trailing stop",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Move TS", background: Brushes.DarkBlue, foreground: Brushes.Yellow),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage($"Move TS Button Clicked - Adjusting stop by {strategy.TickMove} ticks", "BUTTON_CLICK");
            strategy.AdjustStopLoss(strategy.TickMove);
            strategy.ForceRefresh();
            strategy.LogMessage("Move TS Button - Stop adjustment completed", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = MoveTS50PctButton,
          Content = "Move TS 50%",
          ToolTip = "Move trailing stop 50% closer to the current price",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Move TS 50%", background: Brushes.DarkBlue, foreground: Brushes.Yellow),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage("Move TS 50% Button Clicked - Moving stop 50% closer to current price", "BUTTON_CLICK");
            strategy.MoveTrailingStopByPercentage(0.5);
            strategy.ForceRefresh();
            strategy.LogMessage("Move TS 50% Button - Stop adjustment completed", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = MoveToBeButton,
          Content = "Breakeven",
          ToolTip = "Move stop to breakeven if in profit",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Breakeven", background: Brushes.DarkBlue, foreground: Brushes.White),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage("Move to BE Button Clicked - Moving stop to breakeven", "BUTTON_CLICK");
            strategy.MoveToBreakeven();
            strategy.ForceRefresh();
            strategy.LogMessage("Move to BE Button - Stop adjustment completed", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = CloseButton,
          Content = "Close All Positions",
          ToolTip = "Manual Close: CloseAllPosiions manually. Alert!!! Only works with the entries made by the strategy. Manual entries will not be closed from this option.",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "Close All Positions", background: Brushes.DarkRed, foreground: Brushes.White),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage("Close All Positions Button Clicked - Closing all strategy positions", "BUTTON_CLICK");
            strategy.CloseAllPositions();
            strategy.ForceRefresh();
            strategy.LogMessage("Close All Positions Button - Position closing completed", "BUTTON_CLICK");
          }
        },
        new ButtonDefinition
        {
          Name = PanicButton,
          Content = "\u2620 Panic Shutdown",
          ToolTip = "PanicBtn: CloseAllPosiions",
          InitialDecoration = (strategy, button) => strategy.DecorateButton(button, ButtonState.Neutral, "\u2620 Panic Shutdown", background: Brushes.DarkRed, foreground: Brushes.Yellow),
          ClickAction = (strategy) =>
          {
            strategy.LogMessage("Panic Button Clicked - Executing emergency shutdown", "BUTTON_CLICK");
            strategy.FlattenAllPositions();
            strategy.ForceRefresh();
            strategy.LogMessage("Panic Button - Emergency shutdown completed", "BUTTON_CLICK");
          }
        }
      };
    }

    #endregion

    #region Button Decorations

    private enum ButtonState
    {
      Enabled,
      Disabled,
      Neutral
    }

    private void DecorateButton(System.Windows.Controls.Button button, ButtonState state, string contentOn, string contentOff = null, Brush foreground = null, Brush background = null)
    {
      switch (state)
      {
        case ButtonState.Enabled:
          button.Content = contentOn;
          button.Background = background ?? Brushes.DarkGreen;
          button.Foreground = foreground ?? Brushes.White;
          break;
        case ButtonState.Disabled:
          button.Content = contentOff ?? contentOn;
          button.Background = background ?? Brushes.DarkRed;
          button.Foreground = foreground ?? Brushes.White;
          break;
        case ButtonState.Neutral:
          button.Content = contentOn;
          button.Background = background ?? Brushes.DarkBlue;
          button.Foreground = foreground ?? Brushes.White;
          break;
      }

      button.BorderBrush = Brushes.Black;
      button.BorderThickness = new Thickness(1);
      button.Effect = new System.Windows.Media.Effects.DropShadowEffect
      {
        ShadowDepth = 1,
        Direction = 315,
        Color = Colors.Black,
        Opacity = 0.3,
        BlurRadius = 2
      };
    }

    private ControlTemplate CreateButtonTemplate()
    {
      ControlTemplate template = new ControlTemplate(typeof(Button));

      // Create the button's visual tree
      var border = new FrameworkElementFactory(typeof(Border));
      border.Name = "border";
      border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
      border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
      border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
      border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

      // Add inner border for 3D effect
      var innerBorder = new FrameworkElementFactory(typeof(Border));
      innerBorder.SetValue(Border.MarginProperty, new Thickness(1));
      innerBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

      // Add content presenter
      var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
      contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
      contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
      contentPresenter.SetValue(ContentPresenter.MarginProperty, new Thickness(2));

      innerBorder.AppendChild(contentPresenter);
      border.AppendChild(innerBorder);
      template.VisualTree = border;

      // Add triggers for mouse interactions
      var mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
      mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 60, 60, 60))));

      var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
      pressedTrigger.Setters.Add(new Setter(Border.MarginProperty, new Thickness(2, 2, 0, 0), "border"));
      pressedTrigger.Setters.Add(new Setter(Button.EffectProperty, null));

      template.Triggers.Add(mouseOverTrigger);
      template.Triggers.Add(pressedTrigger);

      return template;
    }

    #endregion

    #region Create WPF Controls
    protected void CreateWPFControls()
    {
      //	ChartWindow
      chartWindow = System.Windows.Window.GetWindow(ChartControl.Parent) as Gui.Chart.Chart;

      // if not added to a chart, do nothing
      if (chartWindow == null)
        return;

      // this is the entire chart trader area grid
      chartTraderGrid = (chartWindow.FindFirst("ChartWindowChartTraderControl") as Gui.Chart.ChartTrader).Content as System.Windows.Controls.Grid;

      // this grid contains the existing chart trader buttons
      chartTraderButtonsGrid = chartTraderGrid.Children[0] as System.Windows.Controls.Grid;

      InitializeButtonDefinitions(); // Initialize the button definitions

      CreateButtons();

      // this grid is to organize stuff below
      lowerButtonsGrid = new System.Windows.Controls.Grid();

      // Initialize
      InitializeButtonGrid();

      addedRow = new System.Windows.Controls.RowDefinition() { Height = new GridLength(250) };

      // SetButtons
      SetButtonLocations();

      // AddButtons
      AddButtonsToPanel();

      if (TabSelected())
        InsertWPFControls();

      chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;

    }
    #endregion

    #region Create Buttons
    protected void CreateButtons()
    {
      // this style (provided by NinjaTrader_MichaelM) gives the correct default minwidth (and colors) to make buttons appear like chart trader buttons
      Style basicButtonStyle = System.Windows.Application.Current.FindResource("BasicEntryButton") as Style;

      manualBtn = CreateButton(ManualButton, basicButtonStyle);
      autoBtn = CreateButton(AutoButton, basicButtonStyle);
      longBtn = CreateButton(LongButton, basicButtonStyle);
      shortBtn = CreateButton(ShortButton, basicButtonStyle);
      BEBtn = CreateButton(BEButton, basicButtonStyle);
      TSBtn = CreateButton(TSButton, basicButtonStyle);
      moveTSBtn = CreateButton(MoveTSButton, basicButtonStyle);
      moveTS50PctBtn = CreateButton(MoveTS50PctButton, basicButtonStyle);
      moveToBEBtn = CreateButton(MoveToBeButton, basicButtonStyle);
      closeBtn = CreateButton(CloseButton, basicButtonStyle);
      panicBtn = CreateButton(PanicButton, basicButtonStyle);

    }

    private System.Windows.Controls.Button CreateButton(string buttonName, Style basicButtonStyle)
    {
      var definition = buttonDefinitions.FirstOrDefault(b => b.Name == buttonName);
      if (definition == null)
      {
        Print($"Error: Button definition not found for {buttonName}");
        return null;
      }

      var button = new System.Windows.Controls.Button
      {
        Name = buttonName,
        Height = 30,
        Margin = new Thickness(1, 1, 1, 2), // Added bottom margin
        Style = basicButtonStyle,
        BorderThickness = new Thickness(1),
        IsEnabled = true,
        ToolTip = definition.ToolTip,
        FocusVisualStyle = null,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center
      };

      definition.InitialDecoration?.Invoke(this, button);
      button.Click += OnButtonClick;

      return button;
    }

    protected void InitializeButtonGrid()
    {
      lowerButtonsGrid = new System.Windows.Controls.Grid();

      // Make columns equal width
      for (int i = 0; i < 2; i++)
      {
        var colDef = new System.Windows.Controls.ColumnDefinition();
        colDef.Width = new GridLength(1, GridUnitType.Star); // Equal width columns
        lowerButtonsGrid.ColumnDefinitions.Add(colDef);
      }

      // Add rows with specific heights for better spacing
      for (int i = 0; i < 10; i++) // Adjusted number of rows
      {
        var rowDef = new System.Windows.Controls.RowDefinition();
        if (i == 2) // Gap after first group
        {
          rowDef.Height = new GridLength(8);
        }
        else if (i == 5) // Gap after second group
        {
          rowDef.Height = new GridLength(8);
        }
        else
        {
          rowDef.Height = new GridLength(32); // Standard button height
        }
        lowerButtonsGrid.RowDefinitions.Add(rowDef);
      }

      lowerButtonsGrid.Margin = new Thickness(2);
      lowerButtonsGrid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
      lowerButtonsGrid.VerticalAlignment = System.Windows.VerticalAlignment.Top;
    }

    protected void SetButtonLocations()
    {
      // First group
      SetButtonLocation(manualBtn, 0, 0);    // Column 0, Row 0
      SetButtonLocation(autoBtn, 1, 0);      // Column 1, Row 0
      SetButtonLocation(longBtn, 0, 1);      // Column 0, Row 1
      SetButtonLocation(shortBtn, 1, 1);     // Column 1, Row 1

      // Second group (after gap)
      SetButtonLocation(BEBtn, 0, 3);        // Column 0, Row 3
      SetButtonLocation(TSBtn, 1, 3);        // Column 1, Row 3
      SetButtonLocation(moveTSBtn, 0, 4);    // Column 0, Row 4
      SetButtonLocation(moveTS50PctBtn, 1, 4); // Column 1, Row 4

      // Third group (after gap)
      SetButtonLocation(moveToBEBtn, 0, 6, 2); // Column 0, Row 6, Span 2 columns
      SetButtonLocation(closeBtn, 0, 7, 2);    // Column 0, Row 7, Span 2 columns
      SetButtonLocation(panicBtn, 0, 8, 2);    // Column 0, Row 8, Span 2 columns
    }

    protected void SetButtonLocation(System.Windows.Controls.Button button, int column, int row, int columnSpan = 1)
    {
      System.Windows.Controls.Grid.SetColumn(button, column);
      System.Windows.Controls.Grid.SetRow(button, row);

      if (columnSpan > 1)
        System.Windows.Controls.Grid.SetColumnSpan(button, columnSpan);
    }

    protected void AddButtonsToPanel()
    {
      // Add Buttons to grid
      lowerButtonsGrid.Children.Add(manualBtn);
      lowerButtonsGrid.Children.Add(autoBtn);
      lowerButtonsGrid.Children.Add(longBtn);
      lowerButtonsGrid.Children.Add(shortBtn);
      lowerButtonsGrid.Children.Add(BEBtn);
      lowerButtonsGrid.Children.Add(TSBtn);
      lowerButtonsGrid.Children.Add(moveTSBtn);
      lowerButtonsGrid.Children.Add(moveTS50PctBtn);
      lowerButtonsGrid.Children.Add(moveToBEBtn);
      lowerButtonsGrid.Children.Add(closeBtn);
      lowerButtonsGrid.Children.Add(panicBtn);

    }
    #endregion

    #region Buttons Click Events

    protected void OnButtonClick(object sender, RoutedEventArgs rea)
    {
      Button button = sender as Button;

      var definition = buttonDefinitions.FirstOrDefault(b => b.Name == button.Name);
      if (definition != null)
      {
        definition.ClickAction?.Invoke(this);
      }
      else
      {
        Print($"Error: No click action defined for button {button.Name}");
      }
    }

    #endregion

    #region Dispose
    protected void DisposeWPFControls()
    {
      if (chartWindow != null)
        chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

      //Unsubscribe from all button click events
      UnsubscribeButtonClick(manualBtn);
      UnsubscribeButtonClick(autoBtn);
      UnsubscribeButtonClick(longBtn);
      UnsubscribeButtonClick(shortBtn);
      UnsubscribeButtonClick(BEBtn);
      UnsubscribeButtonClick(TSBtn);
      UnsubscribeButtonClick(moveTSBtn);
      UnsubscribeButtonClick(moveTS50PctBtn);
      UnsubscribeButtonClick(moveToBEBtn);
      UnsubscribeButtonClick(closeBtn);
      UnsubscribeButtonClick(panicBtn);


      RemoveWPFControls();
    }

    private void UnsubscribeButtonClick(Button button)
    {
      if (button != null)
      {
        button.Click -= OnButtonClick;
      }
    }
    #endregion

    #region Insert WPF
    public void InsertWPFControls()
    {
      if (panelActive)
        return;

      // Add a new row for our lowerButtonsGrid below the ask and bid prices and pnl display
      addedRow = new System.Windows.Controls.RowDefinition()
      {
        Height = new GridLength(340) // Increased height to ensure all buttons are visible
      };

      chartTraderGrid.RowDefinitions.Add(addedRow);
      System.Windows.Controls.Grid.SetRow(lowerButtonsGrid, (chartTraderGrid.RowDefinitions.Count - 1));
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

    #region TabSelcected
    protected bool TabSelected()
    {
      bool tabSelected = false;

      // loop through each tab and see if the tab this indicator is added to is the selected item
      foreach (System.Windows.Controls.TabItem tab in chartWindow.MainTabControl.Items)
        if ((tab.Content as Gui.Chart.ChartTab).ChartControl == ChartControl && tab == chartWindow.MainTabControl.SelectedItem)
          tabSelected = true;

      return tabSelected;
    }
    #endregion

    #region TabHandler
    protected void TabChangedHandler(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (e.AddedItems.Count <= 0)
        return;

      tabItem = e.AddedItems[0] as System.Windows.Controls.TabItem;
      if (tabItem == null)
        return;

      chartTab = tabItem.Content as Gui.Chart.ChartTab;
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
      //	Close actual position manually
      //	Check if there is an open position
      Print("Position Closing");

      if (isLong)
      {
        // Create the order labels array based on whether additional contracts exist
        string[] orderLabels = additionalContractExists ? new[] { LE, LE2, LE3, LE4 } : new[] { LE };

        // Apply the initial trailing stop for all relevant orders
        foreach (string label in orderLabels)
        {
          ExitLong("Manual Exit", label);
        }
      }
      else if (isShort)
      {
        // Create the order labels array based on whether additional contracts exist
        string[] orderLabels = additionalContractExists ? new[] { SE, SE2, SE3, SE4 } : new[] { SE };

        // Apply the initial trailing stop for all relevant orders
        foreach (string label in orderLabels)
        {
          ExitShort("Manual Exit", label);
        }
      }
    }

    protected void FlattenAllPositions()
    {
      System.Collections.ObjectModel.Collection<Cbi.Instrument> instrumentsToClose = new System.Collections.ObjectModel.Collection<Instrument>();
      instrumentsToClose.Add(Position.Instrument);
      Position.Account.Flatten(instrumentsToClose);
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



    protected bool checkTimers()
    {
      //	check we are in timer
      if ((Times[0][0].TimeOfDay >= Start.TimeOfDay) && (Times[0][0].TimeOfDay < End.TimeOfDay)
          || (Time2 && Times[0][0].TimeOfDay >= Start2.TimeOfDay && Times[0][0].TimeOfDay <= End2.TimeOfDay)
          || (Time3 && Times[0][0].TimeOfDay >= Start3.TimeOfDay && Times[0][0].TimeOfDay <= End3.TimeOfDay)
          || (Time4 && Times[0][0].TimeOfDay >= Start4.TimeOfDay && Times[0][0].TimeOfDay <= End4.TimeOfDay)
          || (Time5 && Times[0][0].TimeOfDay >= Start5.TimeOfDay && Times[0][0].TimeOfDay <= End5.TimeOfDay)
          || (Time6 && Times[0][0].TimeOfDay >= Start6.TimeOfDay && Times[0][0].TimeOfDay <= End6.TimeOfDay)
      )
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    protected string GetActiveTimer()
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

    #endregion

    #region Logging Helpers
    protected void InitializeLogger()
    {
      if (!EnableLogging) return;  // Skip initialization if logging is disabled

      if (!loggerInitialized)
      {
        try
        {
          // Create timestamp for the log file
          string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

          // Initialize the log file path with timestamp
          LogFilePath = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
              "KCStrategies",
              $"KCStrategies_{timestamp}.log"
          );

          string logDir = Path.GetDirectoryName(LogFilePath);
          if (!Directory.Exists(logDir))
          {
            Directory.CreateDirectory(logDir);
            LogMessage($"Created log directory: {logDir}", "INFO");
          }

          // Create or append header to log file
          if (!File.Exists(LogFilePath))
          {
            File.WriteAllText(LogFilePath, $"=== KCStrategies Log Started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            LogMessage($"Created new log file: {LogFilePath}", "INFO");
          }

          loggerInitialized = true;
          LogMessage("Logger initialized successfully", "INFO");
        }
        catch (Exception ex)
        {
          Print($"Error initializing logger: {ex.Message}");
          if (ex is UnauthorizedAccessException)
          {
            Print("Access denied when trying to create log file or directory. Please check permissions.");
          }
          else if (ex is IOException)
          {
            Print("IO error when creating log file or directory. The file might be in use.");
          }
        }
      }
    }

    protected void LogMessage(string message, string level = "INFO")
    {
      try
      {
        // Skip if logging is disabled
        if (!EnableLogging) return;

        // Skip logging during historical calculation
        if (State != State.Realtime && level != "ERROR" && level != "CRITICAL")
          return;

        if (!loggerInitialized) return;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string barContext = $"Bar {CurrentBar} @ {Time[0]}";
        string logMessage = $"{timestamp} [{level}] {barContext}: {message}";

        lock (LogLock)
        {
          File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
      }
      catch (Exception ex)
      {
        Print($"Error writing to log file: {ex.Message}");
      }
    }

    protected void LogError(string message, Exception ex = null)
    {
      if (!EnableLogging) return;  // Skip if logging is disabled

      string fullMessage = ex != null ?
          $"{message} Error: {ex.Message}\nStack Trace: {ex.StackTrace}" :
          message;
      LogMessage(fullMessage, "ERROR");
    }

    protected void LogWarning(string message)
    {
      if (!EnableLogging) return;  // Skip if logging is disabled

      // Skip warning logs during historical calculation
      if (State != State.Realtime) return;
      LogMessage(message, "WARN");
    }

    protected void LogDebug(string message)
    {
      if (!EnableLogging) return;  // Skip if logging is disabled

      // Skip debug logs during historical calculation
      if (State != State.Realtime) return;
      LogMessage(message, "DEBUG");
    }
    #endregion

    #region DrawPnl
    protected void ShowPNLStatus()
    {
      textLine0 = "Active Timer";
      textLine1 = GetActiveTimer();
      textLine2 = "Long Per Direction";
      textLine3 = $"{counterLong} / {longPerDirection} | " + (TradesPerDirection ? "On" : "Off");
      textLine4 = "Short Per Direction";
      textLine5 = $"{counterShort} / {shortPerDirection} | " + (TradesPerDirection ? "On" : "Off");
      textLine6 = "Bars Since Exit ";
      textLine7 = $"{iBarsSinceExit}    |    " + (iBarsSinceExit > 1 ? "On" : "Off");
      string statusPnlText = textLine0 + "\t" + textLine1 + "\n" + textLine2 + "  " + textLine3 + "\n" + textLine4 + "  " + textLine5 + "\n" + textLine6 + "\t";
      SimpleFont font = new SimpleFont("Arial", 16);

      Draw.TextFixed(this, "statusPnl", statusPnlText, PositionPnl, colorPnl, font, Brushes.Transparent, Brushes.Transparent, 0);

    }
    #endregion


    #region Entry Signals & Inits

    protected abstract bool ValidateEntryLong();
    protected abstract bool ValidateEntryShort();
    protected virtual bool ValidateExitLong()
    {
      return false;
    }

    protected virtual bool ValidateExitShort()
    {
      return false;
    }

    protected abstract void InitializeIndicators();
    protected virtual void addDataSeries() { }

    #endregion


    #region Order Updates
    protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
                                    int quantity, int filled, double averageFillPrice,
                                    OrderState orderState, DateTime time, ErrorCode error,
                                    string comment)
    {
      try
      {
        // Handle order state changes for playback speed
        if (Cbi.Connection.PlaybackConnection != null)
        {
          if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
          {
            // Only restore normal speed if we're flat
            if (Position.MarketPosition == MarketPosition.Flat)
            {
              NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = NormalPlaybackSpeed;
              LogMessage($"OnOrderUpdate(): Order {order.OrderId} {orderState} while flat - Restored normal speed: {NormalPlaybackSpeed}", "PLAYBACK");
            }
            else
            {
              LogMessage($"OnOrderUpdate(): Order {order.OrderId} {orderState} but position not flat - Maintaining slow speed: {SlowPlaybackSpeed}", "PLAYBACK");
            }
          }
          else if (orderState == OrderState.Working)
          {
            // Ensure we're still slow for working orders
            NinjaTrader.Adapter.PlaybackAdapter.PlaybackSpeed = SlowPlaybackSpeed;
            LogMessage($"OnOrderUpdate(): Order {order.OrderId} now working - Maintaining slow speed: {SlowPlaybackSpeed}", "PLAYBACK");
          }
        }
      }
      catch (Exception ex)
      {
        LogError("OnOrderUpdate(): Error managing playback speed", ex);
      }

      // Call base implementation if it exists
      base.OnOrderUpdate(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, comment);
    }
    #endregion

    #region Daily PNL

    protected override void OnPositionUpdate(Cbi.Position position, double averagePrice,
      int quantity, Cbi.MarketPosition marketPosition)
    {
      if (isFlat && SystemPerformance.AllTrades.Count > 0)
      {
        totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit; ///Double that sets the total PnL
        dailyPnL = (totalPnL) - (cumPnL); ///Your daily limit is the difference between these

        // Reset lastAdjustedStopPrice when position is closed
        lastAdjustedStopPrice = 0;

        // Re-enable the strategy if it was disabled by the DD and totalPnL increases
        if (enableTrailingDrawdown && trailingDrawdownReached && totalPnL > maxProfit - TrailingDrawdown)
        {
          trailingDrawdownReached = false;
          isAutoEnabled = true;
          LogMessage("OnPositionUpdate(): Trailing Drawdown Lifted. Strategy Re-Enabled!", "TRADING_STATE");
        }

        // Only show daily loss/profit messages if the feature is enabled
        if (dailyLossProfit)
        {
          if (dailyPnL <= -DailyLossLimit)
          {
            LogMessage($"OnPositionUpdate(): Daily Loss of {DailyLossLimit} has been hit. No More Entries! Daily PnL: {dailyPnL}", "PNL");

            Text myTextLoss = Draw.TextFixed(this, "loss_text",
              $"Daily Loss of {DailyLossLimit} has been hit. No More Entries! Daily PnL >> ${totalPnL} <<",
              PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont,
              Brushes.Transparent, Brushes.Transparent, 100);
            myTextLoss.Font = new SimpleFont("Arial", 18) { Bold = true };
          }

          if (dailyPnL >= DailyProfitLimit)
          {
            LogMessage($"OnPositionUpdate(): Daily Profit of {DailyProfitLimit} has been hit. No more Entries! Daily PnL: {dailyPnL}", "PNL");

            Text myTextProfit = Draw.TextFixed(this, "profit_text",
              $"Daily Profit of {DailyProfitLimit} has been hit. No more Entries! Daily PnL >> ${totalPnL} <<",
              PositionDailyPNL, colorDailyProfitLoss, ChartControl.Properties.LabelFont,
              Brushes.Transparent, Brushes.Transparent, 100);
            myTextProfit.Font = new SimpleFont("Arial", 18) { Bold = true };
          }
        }
      }

      if (isFlat) checkPositions(); // Detect unwanted Positions opened (possible rogue Order?)
    }

    #endregion

    //		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
    //		{
    //			base.OnRender(chartControl, chartScale);
    //			if (showDailyPnl) DrawStrategyPnL(chartControl);
    //		}

    protected void DrawStrategyPnL()
    {
      if (Account == null) return; // Added safety check for connection

      // ... (Get account PnL logic remains the same) ...
      double accountRealized = (State == State.Realtime) ? Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar) : SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
      double accountUnrealized = (State == State.Realtime) ? Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar) : (Position != null && Position.MarketPosition != MarketPosition.Flat ? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]) : 0); // Safety for Position
      double accountTotal = accountRealized + accountUnrealized;
      dailyPnL = accountTotal - cumPnL; // Assuming cumPnL is correctly managed elsewhere
      if (accountTotal > maxProfit) maxProfit = accountTotal;

      // --- Determine Trend and Signal Strings ---
      // Need to ensure these conditions don't themselves cause issues if called too early
      // But typically OnBarUpdate handles the main indicator calculations first.
      string trendStatus = uptrend ? "Up" : downtrend ? "Down" : "Neutral";
      string signalStatus = "No Signal"; // Default

      // These checks rely on flags set in OnBarUpdate, which should be safe if OnBarUpdate handled warm-up
      if (IsLongEntryConditionMet()) signalStatus = "Long Ready";
      else if (IsShortEntryConditionMet()) signalStatus = "Short Ready";

      // --- Apply Overrides based on State ---
      if (!isFlat) signalStatus = "In Position";

      if (EnableChoppinessDetection)
      {
        if (!isAutoEnabled && !autoDisabledByChop) signalStatus = "Auto OFF (Manual)"; // User turned it off

        if (marketIsChoppy) // Choppiness override (applies even if Auto is OFF manually)
        {
          trendStatus = "Choppy";
          signalStatus = "No Trade (Chop)";
        }
        if (!isAutoEnabled && autoDisabledByChop) signalStatus = "Auto OFF (Chop)"; // System turned it off due to chop
      }

      // Other overrides (higher priority?)
      if (!checkTimers()) signalStatus = "Outside Hours";
      if (orderErrorOccurred) signalStatus = "Order Error!";
      if (enableTrailingDrawdown && trailingDrawdownReached) signalStatus = "Drawdown Limit Hit";
      if (dailyLossProfit && dailyPnL <= -DailyLossLimit) signalStatus = "Loss Limit Hit";
      if (dailyLossProfit && dailyPnL >= DailyProfitLimit) signalStatus = "Profit Limit Hit";

      string pnlSource = (State == State.Realtime) ? "Account" : "System";
      // Added null check for Account.Connection
      string connectionStatus = (Account.Connection != null) ? Account.Connection.Status.ToString() : "N/A";

      // --- FIXED INDICATOR VALUE DISPLAY ---
      // Instead of IsValidDataPoint, check if CurrentBar is sufficient for the indicator's period.
      // This prevents displaying default values (like 0) during the initial strategy warm-up.

      // Get the period for Momentum1 (it was hardcoded 14 during initialization)
      // If Momentum1 instance exists and has a Period property, use that, otherwise default to known value
      int momentumPeriod = (Momentum1 != null ? Momentum1.Period : 14);
      // BuySellPressure readiness check - using BarsRequiredToTrade as proxy
      // Assuming BuySellPressure1 needs at least BarsRequiredToTrade bars.
      int buySellPressureRequiredBars = BarsRequiredToTrade; // Or specific period if known for BuySellPressure

      // Check CurrentBar against the required period (0-based index means CurrentBar >= Period - 1)

      string adxStatus = currentAdx > AdxThreshold ? $"Trending ({currentAdx:F1})" : $"Choppy ({currentAdx:F1})";
      string momoStatus = currentMomentum > 0 ? $"Up ({currentMomentum:F1})" : currentMomentum < 0 ? $"Down ({currentMomentum:F1})" : $"Neutral ({currentMomentum:F1})";
      // Assuming buyPressure/sellPressure series are populated when BuySellPressure1 is calculated in OnBarUpdate
      string buyPressText = CurrentBar >= buySellPressureRequiredBars - 1 ? buyPressure[0].ToString("F1") : "N/A";
      string sellPressText = CurrentBar >= buySellPressureRequiredBars - 1 ? sellPressure[0].ToString("F1") : "N/A";
      // --- END FIXED INDICATOR VALUE DISPLAY ---

      string realTimeTradeText =
          $"{Account.Name} | {(Account.Connection != null ? Account.Connection.Options.Name : "N/A")} ({connectionStatus})\n" +
          $"PnL Src: {pnlSource}\n" +
          $"Real PnL:\t{accountRealized:C}\n" +
          $"Unreal PnL:\t{accountUnrealized:C}\n" +
          $"Total PnL:\t{accountTotal:C}\n" +
          $"Daily PnL:\t{dailyPnL:C}\n" +
          $"Max Profit:\t{(maxProfit == double.MinValue ? "N/A" : maxProfit.ToString("C"))}\n" +
          $"-------------\n" +
          $"ADX:\t\t{adxStatus}\n" +             // Use safe text
          $"Momentum:\t{momoStatus}\n" +           // Use safe text
          $"Buy Pressure:\t{buyPressText}\n" +   // Use safe text
          $"Sell Pressure:\t{sellPressText}\n" +  // Use safe text
      $"ATR:\t\t{currentAtr:F2}\n" +
          $"-------------\n" +
          $"Trend:\t{trendStatus}\n" +      // Use overridden status
          $"Signal:\t{signalStatus}";       // Use overridden status

      SimpleFont font = new SimpleFont("Arial", 16);
      Brush pnlColor = accountTotal == 0 ? Brushes.Cyan : accountTotal > 0 ? Brushes.Lime : Brushes.Pink;

      try
      {
        // Ensure ChartControl and other UI elements are available before drawing
        //if (chartControl != null)
        //{
        Draw.TextFixed(this, "realTimeTradeText", realTimeTradeText, PositionDailyPNL, pnlColor, font, Brushes.Transparent, Brushes.Transparent, 0);
        //}
      }
      catch (Exception ex) { Print($"Error drawing PNL display: {ex.Message}"); }
    }

    #region KillSwitch
    protected void KillSwitch()
    {
      // Only calculate P/L if either control is enabled
      if (dailyLossProfit || enableTrailingDrawdown)
      {
        totalPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;

        // Only calculate daily P/L if daily P/L control is enabled
        if (dailyLossProfit)
        {
          dailyPnL = totalPnL + Account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
        }

        // Track highest profit only if trailing drawdown is enabled
        if (enableTrailingDrawdown && totalPnL > maxProfit)
        {
          maxProfit = totalPnL;
          LogMessage($"OnKillSwitch(): New max profit recorded: {maxProfit:C}", "KILLSWITCH");
        }

        // Determine all relevant order labels
        List<string> longOrderLabels = new List<string> { LE }; // Base Labels for Longs
        List<string> shortOrderLabels = new List<string> { SE }; // Base Labels for Shorts

        if (EnableProfitTarget2)
        {
          longOrderLabels.AddRange(new[] { LE2 });
          shortOrderLabels.AddRange(new[] { SE2 });
        }

        if (EnableProfitTarget3)
        {
          longOrderLabels.AddRange(new[] { LE3 });
          shortOrderLabels.AddRange(new[] { SE3 });
        }

        if (EnableProfitTarget4)
        {
          longOrderLabels.AddRange(new[] { LE4 });
          shortOrderLabels.AddRange(new[] { SE4 });
        }

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

          isAutoEnabled = false;
          LogMessage("OnKillSwitch(): Kill Switch Activated: Strategy Disabled!", "KILLSWITCH");
        };

        // Check trailing drawdown only if enabled
        if (enableTrailingDrawdown && totalPnL >= StartTrailingDD)
        {
          if ((maxProfit - totalPnL) >= TrailingDrawdown)
          {
            LogMessage($"OnKillSwitch(): Trailing drawdown limit reached. Max Profit: {maxProfit:C}, Current P/L: {totalPnL:C}, Drawdown: {(maxProfit - totalPnL):C}", "KILLSWITCH");
            closeAllPositionsAndDisableStrategy();
            trailingDrawdownReached = true;
          }
        }

        // Check daily P/L limits only if enabled
        if (dailyLossProfit)
        {
          if (dailyPnL <= -DailyLossLimit)
          {
            LogMessage($"OnKillSwitch(): Daily loss limit reached: {dailyPnL:C}", "KILLSWITCH");
            closeAllPositionsAndDisableStrategy();
          }

          if (dailyPnL >= DailyProfitLimit)
          {
            LogMessage($"OnKillSwitch(): Daily profit limit reached: {dailyPnL:C}", "KILLSWITCH");
            closeAllPositionsAndDisableStrategy();
          }
        }

        if (!isAutoEnabled)
        {
          LogMessage("OnKillSwitch(): Strategy auto trading has been disabled by Kill Switch", "KILLSWITCH");
        }
      }
    }
    #endregion

    #region Custom Property Manipulation

    public void ModifyProperties(PropertyDescriptorCollection col)
    {
      if (TradesPerDirection == false)
      {
        col.Remove(col.Find("longPerDirection", true));
        col.Remove(col.Find("shortPerDirection", true));
      }
      if (Time2 == false)
      {
        col.Remove(col.Find("Start2", true));
        col.Remove(col.Find("End2", true));
      }
      if (Time3 == false)
      {
        col.Remove(col.Find("Start3", true));
        col.Remove(col.Find("End3", true));
      }
      if (Time4 == false)
      {
        col.Remove(col.Find("Start4", true));
        col.Remove(col.Find("End4", true));
      }
      if (Time5 == false)
      {
        col.Remove(col.Find("Start5", true));
        col.Remove(col.Find("End5", true));
      }
      if (Time6 == false)
      {
        col.Remove(col.Find("Start6", true));
        col.Remove(col.Find("End6", true));
      }
    }

    public void ModifyBESetAutoProperties(PropertyDescriptorCollection col)
    {
      if (showctrlBESetAuto == false)
      {
        col.Remove(col.Find("BE_Trigger", true));
        col.Remove(col.Find("BE_Offset", true));
      }
    }

    // This method now controls visibility of mode-SPECIFIC parameters
    public void ModifyProfitTargetProperties(PropertyDescriptorCollection col)
    {
      // Default visibility: Assume Fixed mode is default if none are explicitly true (though setters prevent this)
      bool fixedModeActive = enableFixedProfitTarget;

      // If Fixed Profit is NOT active, remove its parameters
      if (!fixedModeActive)
      {
        col.Remove(col.Find("EnableProfitTarget2", true));
        col.Remove(col.Find("Contracts2", true));
        col.Remove(col.Find("ProfitTarget2", true));
        col.Remove(col.Find("EnableProfitTarget3", true));
        col.Remove(col.Find("Contracts3", true));
        col.Remove(col.Find("ProfitTarget3", true));
        col.Remove(col.Find("EnableProfitTarget4", true));
        col.Remove(col.Find("Contracts4", true));
        col.Remove(col.Find("ProfitTarget4", true));
      }

    }

    public void ModifyTrailProperties(PropertyDescriptorCollection col)
    {
      if (showTrailOptions == false)
      {
        col.Remove(col.Find("TrailSetAuto", true));
        col.Remove(col.Find("AtrPeriod", true));
        col.Remove(col.Find("atrMultiplier", true));
        col.Remove(col.Find("RiskRewardRatio", true));

      }
    }

    public void ModifyTrailStopTypeProperties(PropertyDescriptorCollection col)
    {
      // Hide/Show ATR Trail parameters
      if (trailStopType != TrailStopTypeKC.ATR_Trail)
      {
        col.Remove(col.Find("AtrPeriod", true));      // This is likely used elsewhere, be careful removing
        col.Remove(col.Find("atrMultiplier", true));
      }

    }

    public void ModifyTrailSetAutoProperties(PropertyDescriptorCollection col)
    {
      if (showAtrTrailSetAuto == false)
      {
        col.Remove(col.Find("AtrPeriod", true));
        col.Remove(col.Find("atrMultiplier", true));
        col.Remove(col.Find("RiskRewardRatio", true));
      }
    }

    #endregion

    #region ICustomTypeDescriptor Members
    public AttributeCollection GetAttributes()
    {
      return TypeDescriptor.GetAttributes(GetType());
    }

    public string GetClassName()
    {
      return TypeDescriptor.GetClassName(GetType());
    }

    public string GetComponentName()
    {
      return TypeDescriptor.GetComponentName(GetType());
    }

    public TypeConverter GetConverter()
    {
      return TypeDescriptor.GetConverter(GetType());
    }

    public EventDescriptor GetDefaultEvent()
    {
      return TypeDescriptor.GetDefaultEvent(GetType());
    }

    public PropertyDescriptor GetDefaultProperty()
    {
      return TypeDescriptor.GetDefaultProperty(GetType());
    }

    public object GetEditor(Type editorBaseType)
    {
      return TypeDescriptor.GetEditor(GetType(), editorBaseType);
    }

    public EventDescriptorCollection GetEvents(Attribute[] attributes)
    {
      return TypeDescriptor.GetEvents(GetType(), attributes);
    }

    public EventDescriptorCollection GetEvents()
    {
      return TypeDescriptor.GetEvents(GetType());
    }

    // Ensure GetProperties calls the right method
    public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
    {
      PropertyDescriptorCollection orig = TypeDescriptor.GetProperties(GetType(), attributes);
      PropertyDescriptor[] arr = new PropertyDescriptor[orig.Count];
      orig.CopyTo(arr, 0);
      PropertyDescriptorCollection col = new PropertyDescriptorCollection(arr);

      // Call modification methods IN ORDER
      ModifyProperties(col); // General modifications (like Timeframes)
      ModifyBESetAutoProperties(col); // BE modifications
      ModifyProfitTargetProperties(col); // Profit Target modifications <--- Uses the corrected logic
      ModifyTrailProperties(col); // Trail modifications
      ModifyTrailStopTypeProperties(col);
      ModifyTrailSetAutoProperties(col);

      return col;
    }

    public PropertyDescriptorCollection GetProperties()
    {
      return TypeDescriptor.GetProperties(GetType());
    }

    public object GetPropertyOwner(PropertyDescriptor pd)
    {
      return this;
    }
    #endregion

    #region Properties

    #region 01a. Release Notes

    [ReadOnly(true)]
    [NinjaScriptProperty]
    [Display(Name = "BaseAlgoVersion", Order = 1, GroupName = "01a. Release Notes")]
    public string BaseAlgoVersion
    { get; set; }

    [ReadOnly(true)]
    [NinjaScriptProperty]
    [Display(Name = "Author", Order = 2, GroupName = "01a. Release Notes")]
    public string Author
    { get; set; }

    [ReadOnly(true)]
    [NinjaScriptProperty]
    //		[ReadOnly(true)]
    [Display(Name = "StrategyName", Order = 3, GroupName = "01a. Release Notes")]
    public string StrategyName
    { get; set; }

    [ReadOnly(true)]
    [NinjaScriptProperty]
    //		[ReadOnly(true)]
    [Display(Name = "Version", Order = 4, GroupName = "01a. Release Notes")]
    public string Version
    { get; set; }

    [ReadOnly(true)]
    [NinjaScriptProperty]
    //		[ReadOnly(true)]
    [Display(Name = "Credits", Order = 5, GroupName = "01a. Release Notes")]
    public string Credits
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Chart Type", Order = 6, GroupName = "01a. Release Notes")]
    public string ChartType
    { get; set; }

    #endregion



    #region 02. Order Settings

    [NinjaScriptProperty]
    [RefreshProperties(RefreshProperties.All)]
    [Display(Name = "Enable Fixed Profit Target", Order = 1, GroupName = "02. Order Settings")]
    public bool EnableFixedProfitTarget
    {
      get { return enableFixedProfitTarget; }
      set
      {
        // Only process if the value is changing to true
        if (value && !enableFixedProfitTarget)
        {
          enableFixedProfitTarget = true; // Set this one true
                                          // Set others false directly using their backing fields

          // Trigger UI update (essential when properties change affecting others)
          if (Calculate == Calculate.OnEachTick || Calculate == Calculate.OnPriceChange) // Check if UI updates are relevant
            ForceRefresh(); // Force parameter UI refresh
        }
        // Allow setting to false without forcing another default
        else if (!value)
        {
          enableFixedProfitTarget = false;
        }
        // If value is true but already true, do nothing
      }
    }


    [NinjaScriptProperty]
    [Display(Name = "Order Type (Market/Limit)", Order = 2, GroupName = "02. Order Settings")]
    public OrderType OrderType { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Limit Order Offset", Order = 3, GroupName = "02. Order Settings")]
    public double LimitOffset
    { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Contracts", Order = 4, GroupName = "02. Order Settings")]
    public int Contracts
    { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Adjust Tick Trailing Stop (When Button Clicked)", Order = 5, GroupName = "02. Order Settings")]
    public int TickMove
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Initial Stop (Ticks)", Order = 6, GroupName = "02. Order Settings")]
    public int InitialStop
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Profit Target", Order = 7, GroupName = "02. Order Settings")]
    public double ProfitTarget
    { get; set; }

    [NinjaScriptProperty]
    [RefreshProperties(RefreshProperties.All)]
    [Display(Name = "Enable Profit Target 2", Order = 8, GroupName = "02. Order Settings")]
    public bool EnableProfitTarget2
    { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Contract 2", Order = 9, GroupName = "02. Order Settings")]
    public int Contracts2
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Profit Target 2", Order = 10, GroupName = "02. Order Settings")]
    public double ProfitTarget2
    { get; set; }

    [NinjaScriptProperty]
    [RefreshProperties(RefreshProperties.All)]
    [Display(Name = "Enable Profit Target 3", Order = 11, GroupName = "02. Order Settings")]
    public bool EnableProfitTarget3
    { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Contract 3", Order = 12, GroupName = "02. Order Settings")]
    public int Contracts3
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Profit Target3", Order = 13, GroupName = "02. Order Settings")]
    public double ProfitTarget3
    { get; set; }

    [NinjaScriptProperty]
    [RefreshProperties(RefreshProperties.All)]
    [Display(Name = "Enable Profit Target 4", Order = 14, GroupName = "02. Order Settings")]
    public bool EnableProfitTarget4
    { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Contract 4", Order = 15, GroupName = "02. Order Settings")]
    public int Contracts4
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Profit Target4", Order = 16, GroupName = "02. Order Settings")]
    public double ProfitTarget4
    { get; set; }

    #endregion

    #region 03. Order Management

    [NinjaScriptProperty]
    [Display(ResourceType = typeof(Custom.Resource), Name = "Stop Loss Type", Description = "Type of Trail Stop", GroupName = "03. Order Management", Order = 1)]
    [RefreshProperties(RefreshProperties.All)]
    public TrailStopTypeKC TrailStopType
    {
      get { return trailStopType; }
      set
      {
        trailStopType = value;
        if (trailStopType == TrailStopTypeKC.Tick_Trail)
        {
          tickTrail = true;
          enableFixedStopLoss = false;
          atrTrailSetAuto = false;
          showAtrTrailSetAuto = false;
          showAtrTrailOptions = false;
          enableTrail = true;
        }
        else if (trailStopType == TrailStopTypeKC.Fixed_Stop)
        {
          enableFixedStopLoss = true;
          atrTrailSetAuto = false;
          showAtrTrailSetAuto = false;
          showAtrTrailOptions = false;
          tickTrail = false;
          enableTrail = false;
        }
        else if (trailStopType == TrailStopTypeKC.ATR_Trail)
        {
          enableFixedStopLoss = false;
          atrTrailSetAuto = true;
          showAtrTrailSetAuto = true;
          showAtrTrailOptions = true;
          tickTrail = false;
        }
      }
    }

    [NinjaScriptProperty]
    [Display(Name = "ATR Period", Order = 2, GroupName = "03. Order Management")]
    public int AtrPeriod
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "ATR Trailing Multiplier", Order = 3, GroupName = "03. Order Management")]
    public double atrMultiplier
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Risk To Reward Ratio", Order = 4, GroupName = "03. Order Management")]
    public double RiskRewardRatio
    { get; set; }


    [NinjaScriptProperty]
    [Display(Name = "Enable ATR Profit Target", Description = "Enable  Profit Target based on TrendMagic", Order = 5, GroupName = "03. Order Management")]
    [RefreshProperties(RefreshProperties.All)]
    public bool enableAtrProfitTarget
    { get; set; }

    //Breakeven Actual
    [NinjaScriptProperty]
    [RefreshProperties(RefreshProperties.All)]
    [Display(Name = "Enable Breakeven", Order = 6, GroupName = "03. Order Management")]
    public bool BESetAuto
    {
      get
      {
        return beSetAuto;
      }
      set
      {
        beSetAuto = value;

        if (beSetAuto == true)
        {
          showctrlBESetAuto = true;
        }
        else
        {
          showctrlBESetAuto = false;
        }
      }
    }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Breakeven Trigger", Order = 7, Description = "In Ticks", GroupName = "03. Order Management")]
    public int BE_Trigger
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Breakeven Offset", Order = 8, Description = "In Ticks", GroupName = "03. Order Management")]
    public int BE_Offset
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Background Color Signal", Description = "Enable Exit", Order = 9, GroupName = "03. Order Management")]
    [RefreshProperties(RefreshProperties.All)]
    public bool enableBackgroundSignal
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Exit", Description = "Enable Exit", Order = 10, GroupName = "03. Order Management")]
    [RefreshProperties(RefreshProperties.All)]
    public bool enableExit
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Trade Entry Delay (seconds)", Description = "Minimum delay between trade entries in seconds", Order = 1, GroupName = "03. Order Management")]
    [Range(1, 300)]  // Allow 1 second to 5 minutes
    public int TradeDelaySeconds { get; set; }

    #endregion

    #region 05. Profit/Loss Limit

    [NinjaScriptProperty]
    [Display(Name = "Enable Daily Loss / Profit ", Description = "Enable / Disable Daily Loss & Profit control", Order = 1, GroupName = "05. Profit/Loss Limit	")]
    [RefreshProperties(RefreshProperties.All)]
    public bool dailyLossProfit
    { get; set; }

    [NinjaScriptProperty]
    [Range(0, double.MaxValue)]
    [Display(ResourceType = typeof(Custom.Resource), Name = "Daily Profit Limit ($)", Description = "No positive or negative sign, just integer", Order = 2, GroupName = "05. Profit/Loss Limit	")]
    public double DailyProfitLimit
    { get; set; }

    [NinjaScriptProperty]
    [Range(0, double.MaxValue)]
    [Display(ResourceType = typeof(Custom.Resource), Name = "Daily Loss Limit ($)", Description = "No positive or negative sign, just integer", Order = 3, GroupName = "05. Profit/Loss Limit	")]
    public double DailyLossLimit
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Trailing Drawdown", Description = "Enable / Disable trailing drawdown", Order = 4, GroupName = "05. Profit/Loss Limit	")]
    [RefreshProperties(RefreshProperties.All)]
    public bool enableTrailingDrawdown
    { get; set; }

    [NinjaScriptProperty]
    [Range(0, double.MaxValue)]
    [Display(ResourceType = typeof(Custom.Resource), Name = "Trailing Drawdown ($)", Description = "No positive or negative sign, just integer", Order = 5, GroupName = "05. Profit/Loss Limit	")]
    public double TrailingDrawdown
    { get; set; }

    [NinjaScriptProperty]
    [Range(0, double.MaxValue)]
    [Display(ResourceType = typeof(Custom.Resource), Name = "Start Trailing Drawdown ($)", Description = "No positive or negative sign, just integer", Order = 6, GroupName = "05. Profit/Loss Limit	")]
    public double StartTrailingDD
    { get; set; }

    #endregion

    #region	06. Trades Per Direction
    [NinjaScriptProperty]
    [Display(Name = "Enable Trades Per Direction", Description = "Switch off Historical Trades to use this option.", Order = 0, GroupName = "06. Trades Per Direction")]
    [RefreshProperties(RefreshProperties.All)]
    public bool TradesPerDirection
    {
      get { return tradesPerDirection; }
      set { tradesPerDirection = (value); }
    }

    [NinjaScriptProperty]
    [Display(Name = "Long Per Direction", Description = "Number of long in a row", Order = 1, GroupName = "06. Trades Per Direction")]
    public int longPerDirection
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Short Per Direction", Description = "Number of short in a row", Order = 2, GroupName = "06. Trades Per Direction")]
    public int shortPerDirection
    { get; set; }

    #endregion

    #region 07. Other Trade Controls


    [NinjaScriptProperty]
    [Display(Name = "Bars Since Exit", Description = "Number of bars that have elapsed since the last specified exit. 0 == Not used. >1 == Use number of bars specified ", Order = 1, GroupName = "07. Other Trade Controls")]
    public int iBarsSinceExit
    { get; set; }

    #endregion

    #region 08b. Default Settings

    [NinjaScriptProperty]
    [Display(Name = "Enable Buy Sell Pressure", Order = 1, GroupName = "08b. Default Settings")]
    public bool enableBuySellPressure { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Buy Sell Pressure", Order = 2, GroupName = "08b. Default Settings")]
    public bool showBuySellPressure { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable VMA", Order = 3, GroupName = "08b. Default Settings")]
    public bool enableVMA { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show VMA", Order = 4, GroupName = "08b. Default Settings")]
    public bool showVMA { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable HMA Hooks", Order = 5, GroupName = "08b. Default Settings")]
    public bool enableHmaHooks { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show HMA Hooks", Order = 6, GroupName = "08b. Default Settings")]
    public bool showHmaHooks { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "HMA Period", Order = 7, GroupName = "08b. Default Settings")]
    public int HmaPeriod { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Regression Channel", Order = 8, GroupName = "08b. Default Settings")]
    public bool enableRegChan1 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Inner Regression Channel", Order = 9, GroupName = "08b. Default Settings")]
    public bool enableRegChan2 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Outer Regression Channel", Order = 10, GroupName = "08b. Default Settings")]
    public bool showRegChan1 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Inner Regression Channel", Order = 11, GroupName = "08b. Default Settings")]
    public bool showRegChan2 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show High and Low Lines", Order = 12, GroupName = "08b. Default Settings")]
    public bool showRegChanHiLo { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Regression Channel Period", Order = 13, GroupName = "08b. Default Settings")]
    public int RegChanPeriod { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Outer Regression Channel Width", Order = 14, GroupName = "08b. Default Settings")]
    public double RegChanWidth { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Inner Regression Channel Width", Order = 15, GroupName = "08b. Default Settings")]
    public double RegChanWidth2 { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Momentum", Order = 16, GroupName = "08b. Default Settings")]
    public bool enableMomo { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Momentum", Order = 17, GroupName = "08b. Default Settings")]
    public bool showMomo { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Momentum Up Level", Order = 18, GroupName = "08b. Default Settings")]
    public int MomoUp { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Momentum Down Level", Order = 19, GroupName = "08b. Default Settings")]
    public int MomoDown { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable ADX", Order = 20, GroupName = "08b. Default Settings")]
    public bool enableADX { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show ADX", Order = 21, GroupName = "08b. Default Settings")]
    public bool showAdx { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "ADX Period", Order = 22, GroupName = "08b. Default Settings")]
    public int adxPeriod { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "ADX Threshold 1", Order = 23, GroupName = "08b. Default Settings")]
    public int AdxThreshold { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "ADX Threshold 2", Order = 24, GroupName = "08b. Default Settings")]
    public int adxThreshold2 { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "ADX Exit Threshold", Order = 25, GroupName = "08b. Default Settings")]
    public int adxExitThreshold { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Volatility", Order = 26, GroupName = "08b. Default Settings")]
    public bool enableVolatility { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Volatility Threshold", Order = 27, GroupName = "08b. Default Settings")]
    public double atrThreshold { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable EMA Filter", Order = 28, GroupName = "08b. Default Settings")]
    public bool enableEMAFilter { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show EMA", Order = 29, GroupName = "08b. Default Settings")]
    public bool showEMA { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "EMA Length", Order = 30, GroupName = "08b. Default Settings")]
    public int emaLength { get; set; }


    #endregion

    #region 09. Market Condition

    [NinjaScriptProperty]
    [Display(Name = "Enable Choppiness Detection", Order = 1, GroupName = "09. Market Condition")]
    public bool EnableChoppinessDetection { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Regression Channel Look Back Period", Description = "Period for Regression Channel used in chop detection.", Order = 2, GroupName = "09. Market Condition")]
    public int SlopeLookBack { get; set; }

    [NinjaScriptProperty]
    [Range(0.1, 1.0)] // Factor less than 1 to indicate narrower than average
    [Display(Name = "Flat Slope Factor", Description = "Factor of slope of Regression Channel indicates flatness.", Order = 3, GroupName = "09. Market Condition")]
    public double FlatSlopeFactor { get; set; }

    [NinjaScriptProperty]
    [Range(1, int.MaxValue)]
    [Display(Name = "Chop ADX Threshold", Description = "ADX value below which the market is considered choppy (if RegChan is also flat).", Order = 4, GroupName = "09. Market Condition")]
    public int ChopAdxThreshold { get; set; }

    #endregion

    #region 10. Timeframes

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "Start Trades", Order = 1, GroupName = "10. Timeframes")]
    public DateTime Start
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Trades", Order = 2, GroupName = "10. Timeframes")]
    public DateTime End
    { get; set; }

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
    public DateTime Start2
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Time 2", Order = 5, GroupName = "10. Timeframes")]
    public DateTime End2
    { get; set; }

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
    public DateTime Start3
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Time 3", Order = 8, GroupName = "10. Timeframes")]
    public DateTime End3
    { get; set; }

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
    public DateTime Start4
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Time 4", Order = 11, GroupName = "10. Timeframes")]
    public DateTime End4
    { get; set; }

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
    public DateTime Start5
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Time 5", Order = 14, GroupName = "10. Timeframes")]
    public DateTime End5
    { get; set; }

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
    public DateTime Start6
    { get; set; }

    [NinjaScriptProperty]
    [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
    [Display(Name = "End Time 6", Order = 17, GroupName = "10. Timeframes")]
    public DateTime End6
    { get; set; }

    #endregion

    #region 11. Status Panel

    [NinjaScriptProperty]
    [Display(Name = "Show Status Panels", Description = "Enable/Disable the signals and settings information panels", Order = 0, GroupName = "11. Status Panel")]
    public bool ShowStatusPanels { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Enable Logging", Description = "Enable/Disable strategy logging to file", Order = 1, GroupName = "11. Status Panel")]
    public bool EnableLogging { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Show Daily PnL", Order = 2, GroupName = "11. Status Panel")]
    public bool showDailyPnl { get; set; }

    [XmlIgnore()]
    [Display(Name = "Daily PnL Color", Order = 3, GroupName = "11. Status Panel")]
    public Brush colorDailyProfitLoss
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Daily PnL Position", Description = "Daily PNL Alert Position", Order = 4, GroupName = "11. Status Panel")]
    public TextPosition PositionDailyPNL
    { get; set; }

    // Serialize our Color object
    [Browsable(false)]
    public string colorDailyProfitLossSerialize
    {
      get { return Serialize.BrushToString(colorDailyProfitLoss); }
      set { colorDailyProfitLoss = Serialize.StringToBrush(value); }
    }

    [NinjaScriptProperty]
    [Display(Name = "Show STATUS PANEL", Order = 5, GroupName = "11. Status Panel")]
    public bool showPnl { get; set; }

    [XmlIgnore()]
    [Display(Name = "STATUS PANEL Color", Order = 6, GroupName = "11. Status Panel")]
    public Brush colorPnl
    { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "STATUS PANEL Position", Description = "Status PNL Position", Order = 7, GroupName = "11. Status Panel")]
    public TextPosition PositionPnl
    { get; set; }

    // Serialize our Color object
    [Browsable(false)]
    public string colorPnlSerialize
    {
      get { return Serialize.BrushToString(colorPnl); }
      set { colorPnl = Serialize.StringToBrush(value); }
    }

    [NinjaScriptProperty]
    [Display(Name = "Show Historical Trades", Description = "Show Historical Theoretical Trades", Order = 8, GroupName = "11. Status Panel")]
    public bool ShowHistorical
    { get; set; }

    #endregion


    #region Trailing Stop Type
    // Stop Loss Type
    public enum TrailStopTypeKC
    {
      Tick_Trail,
      ATR_Trail,
      Fixed_Stop
    }
    #endregion

    #endregion



    // Add helper property to convert seconds to TimeSpan
    private TimeSpan TradeDelay
    {
      get { return TimeSpan.FromSeconds(TradeDelaySeconds); }
    }

    #region 12. Playback Settings

    [NinjaScriptProperty]
    [Display(Name = "Normal Playback Speed", Description = "Speed to use during normal playback (1-100)", Order = 1, GroupName = "12. Playback Settings")]
    [Range(1, 100)]
    public int NormalPlaybackSpeed { get; set; }

    [NinjaScriptProperty]
    [Display(Name = "Slow Playback Speed", Description = "Speed to use during order execution (0-100)", Order = 2, GroupName = "12. Playback Settings")]
    [Range(1, 100)]
    public int SlowPlaybackSpeed { get; set; }

    #endregion

    #region Base Status Display
    protected virtual void DrawStrategyStatus()
    {
      if (!ShowStatusPanels) return;

      // Calculate current values
      double currentPnL = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;
      double unrealizedPnL = Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
      double netProfit = currentPnL + unrealizedPnL;

      // Update highest net profit if we have a new high
      if (netProfit > highestNetProfit)
      {
        highestNetProfit = netProfit;
        LogMessage($"New highest net profit: {highestNetProfit:C}", "PROFIT");
      }

      // Calculate current drawdown from highest point
      double currentDrawdown = Math.Max(0, highestNetProfit - netProfit);

      // Update max drawdown if this is the worst we've seen
      if (currentDrawdown > maxDrawdown)
      {
        maxDrawdown = currentDrawdown;
        if (State == State.Realtime)
        {
          LogMessage($"New maximum drawdown: {maxDrawdown:C} (From high of {highestNetProfit:C} to current {netProfit:C})", "DRAWDOWN");
        }
      }

      string baseStatusText =
          $"\n=== ACCOUNT STATUS ===\n" +
          $"{Account.Name} | {(Account.Connection != null ? Account.Connection.Options.Name : "N/A")} ({Account.Connection?.Status.ToString() ?? "N/A"})\n" +
          $"Real PnL:\t\t{currentPnL:C}\n" +
          $"Unreal PnL:\t\t{unrealizedPnL:C}\n" +
          $"Total PnL:\t\t{netProfit:C}\n" +
          $"Daily PnL:\t\t{dailyPnL:C}\n" +
          $"Daily High:\t\t{dailyHighestProfit:C}\n" +
          $"Daily Max DD:\t\t{dailyWorstDrawdown:C}\n" +
          $"Previous Day:\t\t{previousDayPnL:C}\n" +
          $"Highest Net Profit:\t{highestNetProfit:C}\n" +
          $"Current DD:\t\t{currentDrawdown:C}\n" +
          $"Worst DD:\t\t{maxDrawdown:C}\n" +
          $"\n=== ORDER SETTINGS DETAILS ===\n" +
          $"{"Order Type:",-16} {OrderType,-10}\n" +
          $"{"Limit Offset:",-16} {LimitOffset,2} ticks\n" +
          $"\n=== ORDER MANAGEMENT ===\n" +
          $"{"Trail Stop Type:",-16} {TrailStopType}\n" +
          $"{"ATR Period:",-16} {AtrPeriod,2}\n" +
          $"{"ATR Multiplier:",-16} {atrMultiplier:F1}\n" +
          $"{"Risk/Reward:",-16} {RiskRewardRatio:F1}\n" +
          $"{"ATR Profit Target:",-16} {(enableAtrProfitTarget ? "ON" : "OFF"),-3}\n" +
          $"{"Breakeven:",-16} {(BESetAuto ? "ON" : "OFF"),-3} [Trigger: {BE_Trigger,2}, Offset: {BE_Offset,2}]\n" +
          $"\n=== PROFIT/LOSS LIMITS ===\n" +
          $"{"Daily P/L Control:",-16} {(dailyLossProfit ? "ON" : "OFF"),-3}\n" +
          $"{"Daily Loss Limit:",-16} ${DailyLossLimit:N0}\n" +
          $"{"Daily Profit Limit:",-16} ${DailyProfitLimit:N0}\n" +
          $"{"Trail DD Control:",-16} {(enableTrailingDrawdown ? "ON" : "OFF"),-3}\n" +
          $"{"Start Trail At:",-16} ${StartTrailingDD:N0}\n" +
          $"{"Trail DD Amount:",-16} ${TrailingDrawdown:N0}\n" +
          $"\n=== BASE SETTINGS STATUS ===\n" +
          $"{"Buy/Sell Pressure:",-12} {(buyPressureUp ? "Buy↑" : sellPressureUp ? "Sell↑" : "-"),-8} [Enabled: {enableBuySellPressure}]\n" +
          $"{"VMA:",-12} {(volMaUp ? "↑" : volMaDown ? "↓" : "-"),-8} [Enabled: {enableVMA}]\n" +
          $"{"ADX:",-12} {currentAdx,4:F1} [Threshold: {AdxThreshold}, Enabled: {enableADX}]\n" +
          $"{"Momentum:",-12} {currentMomentum,4:F1} [Up: {MomoUp}, Down: {MomoDown}, Enabled: {enableMomo}]\n" +
          $"{"HMA Hooks:",-12} {(hmaUp ? "↑" : hmaDown ? "↓" : "-"),-8} [Period: {HmaPeriod}, Enabled: {enableHmaHooks}]\n" +
          $"{"RegChan:",-12} {(regChanUp ? "↑" : regChanDown ? "↓" : "-"),-8} [Period: {RegChanPeriod}, Width: {RegChanWidth:F0}/{RegChanWidth2:F0}, Enabled: {enableRegChan1}]\n" +
          $"{"ATR:",-12} {currentAtr,4:F1} [Threshold: {atrThreshold}, Enabled: {enableVolatility}]\n" +
          $"{"EMA Filter:",-12} {(aboveEMAHigh ? "Above" : belowEMALow ? "Below" : "-"),-8} [Length: {emaLength}, Enabled: {enableEMAFilter}]\n" +
          $"{"Background:",-12} {(enableBackgroundSignal ? "ON" : "OFF"),-3}\n" +
          $"{"Exit:",-12} {(enableExit ? "ON" : "OFF"),-3}";

      Draw.TextFixed(this, "base_signals", baseStatusText, TextPosition.BottomLeft,
          Brushes.White, new SimpleFont("Arial", 10), null, Brushes.Black, 100, DashStyleHelper.Solid, 0, false, "");
    }
    #endregion

  }
}





